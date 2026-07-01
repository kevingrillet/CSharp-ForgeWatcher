using CSharp_AzureDevopsNotifier.Entities;
using CSharp_AzureDevopsNotifier.Helpers;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CSharp_AzureDevopsNotifier
{
    public class AzureDevOpsManager
    {
        private readonly StorageInfos _storageInfos;
        private AzureDevOpsClient _azureDevOpsClient;
        private CancellationTokenSource _cts;
        private string _lastErrorMessage;
        private AzureDevOpsSettings _settings;

        public AzureDevOpsManager(AzureDevOpsSettings azureDevOpsSettings = null)
        {
            Update(azureDevOpsSettings);
            _storageInfos = JsonHelpers<StorageInfos>.Load(@"Configurations/StorageInfos.json");
        }

        public async Task RunAsync()
        {
            // Cancel any loop already running before starting a fresh one,
            // so successive Refresh calls cannot stack up overlapping loops.
            _cts?.Cancel();
            var cts = new CancellationTokenSource();
            _cts = cts;

            // Poll at least once a minute even if Delay is misconfigured to 0,
            // otherwise a failing call would spin in a tight loop.
            int delayMs = Math.Max(_settings.Delay, 1) * 60 * 1000;

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    await Run();
                    _lastErrorMessage = null;
                }
                catch (Exception ex)
                {
                    // A transient failure (network, expired PAT, bad config, ...) must not
                    // kill the polling loop. Surface it once, then keep retrying.
                    if (ex.Message != _lastErrorMessage)
                    {
                        _lastErrorMessage = ex.Message;
                        ToastHelpers.ShowToastNotification("AzureDevopsNotifier - error", ex.Message, null);
                    }
                }

                try
                {
                    // Wait for X minutes before the next check
                    await Task.Delay(delayMs, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    // Loop superseded or stopped.
                    break;
                }
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        public void Update(AzureDevOpsSettings azureDevOpsSettings)
        {
            _cts?.Cancel();
            _settings = azureDevOpsSettings ?? JsonHelpers<AzureDevOpsSettings>.Load(@"Configurations/AzureDevOpsSettings.json");
            _azureDevOpsClient = new AzureDevOpsClient(_settings);
        }

        private async Task Run()
        {
            await QueryGit();
            await QueryWorkItems();
            SaveStorageInfos();
        }

        private async Task QueryGit()
        {
            foreach (var query in _settings.Queries.Where(q => q.Running && q.Type == AzureDevopsQueryType.Git))
            {
                var prs = await AzureDevOpsHelpers.GetNewPullRequestsAsync(_azureDevOpsClient.GetGitClient(), _settings.ProjectName, query.RepositoryName);
                AzureDevOpsHelpers.DisplayNewItems(prs, _storageInfos.DisplayedPrIds, _settings, query);
            }
        }

        private async Task QueryWorkItems()
        {
            foreach (var query in _settings.Queries.Where(q => q.Running && q.Type == AzureDevopsQueryType.WorkItem))
            {
                var workItems = await AzureDevOpsHelpers.GetWorkItemsAsync(_azureDevOpsClient.GetWorkItemClient(), query.Filters);
                AzureDevOpsHelpers.DisplayNewItems(workItems, _storageInfos.DisplayedWorkItemsIds, _settings);
            }
        }

        private void SaveStorageInfos()
        {
            if (_storageInfos != null)
            {
                JsonHelpers<StorageInfos>.Save("Configurations/StorageInfos.json", _storageInfos);
            }
        }
    }
}
