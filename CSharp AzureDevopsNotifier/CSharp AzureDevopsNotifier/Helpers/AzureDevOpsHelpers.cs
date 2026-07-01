using CSharp_AzureDevopsNotifier.Entities;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CSharp_AzureDevopsNotifier.Helpers
{
    /// <summary>
    /// The <c>AzureDevOpsHelpers</c> class provides helper methods for working with Azure DevOps,
    /// specifically for displaying new items (pull requests and work items) and retrieving them from Azure DevOps.
    /// </summary>
    public static class AzureDevOpsHelpers
    {
        /// <summary>
        /// Displays new items as toast notifications.
        /// </summary>
        /// <typeparam name="TType">Type of item (GitPullRequest or WorkItem).</typeparam>
        /// <param name="items">List of items.</param>
        /// <param name="displayedItemIds">List of displayed item IDs.</param>
        /// <param name="settings">Azure DevOps settings.</param>
        /// <param name="query">Azure DevOps query.</param>
        public static void DisplayNewItems<TType>(IList<TType> items, IList<int> displayedItemIds, AzureDevOpsSettings settings, AzureDevOpsQuery query = null)
        {
            foreach (var item in items)
            {
                int itemId = GetItemId(item);

                if (displayedItemIds.Contains(itemId))
                {
                    continue;
                }

                string itemName = GetItemName(item);
                string itemUrl = GetItemUrl(item, settings, query);

                ToastHelpers.ShowToastNotification($"New {typeof(TType).Name}", itemName, itemUrl);

                displayedItemIds.Add(itemId);
            }
        }

        /// <summary>
        /// Retrieves the list of new pull requests from Azure DevOps.
        /// </summary>
        /// <param name="gitClient">GitHttpClient instance.</param>
        /// <param name="projectName">Name of the project.</param>
        /// <param name="repositoryName">Name of the repository.</param>
        /// <returns>List of new pull requests.</returns>
        public static async Task<List<GitPullRequest>> GetNewPullRequestsAsync(GitHttpClient gitClient, string projectName, string repositoryName)
        {
            GitPullRequestSearchCriteria prSearchCriteria = new()
            {
                Status = PullRequestStatus.Active
            };

            return await gitClient.GetPullRequestsAsync(projectName, repositoryName, prSearchCriteria);
        }

        /// <summary>
        /// Retrieves the list of new work items from Azure DevOps.
        /// </summary>
        /// <param name="workItemClient">WorkItemTrackingHttpClient instance.</param>
        /// <param name="filters">List of filters.</param>
        /// <returns>List of new work items.</returns>
        public static async Task<List<WorkItem>> GetWorkItemsAsync(WorkItemTrackingHttpClient workItemClient, IList<string> filters)
        {
            string wiql = $"SELECT [System.Id], [System.Title], [System.State], [System.CreatedDate] FROM workitems WHERE {string.Join(" And ", filters)} ORDER BY [System.CreatedDate] DESC";
            WorkItemQueryResult queryResult = await workItemClient.QueryByWiqlAsync(new Wiql { Query = wiql });
            List<WorkItemReference> workItemReferences = queryResult.WorkItems.ToList();

            return await workItemClient.GetWorkItemsAsync(workItemReferences.Select(w => w.Id));
        }

        private static int GetItemId<TType>(TType item)
        {
            return item switch
            {
                GitPullRequest pr => pr.PullRequestId,
                WorkItem wi => wi.Id ?? 0,
                _ => throw new InvalidOperationException("Unsupported item type")
            };
        }

        private static string GetItemName<TType>(TType item)
        {
            return item switch
            {
                GitPullRequest pr => pr.Title,
                WorkItem wi => wi.Fields["System.Title"].ToString(),
                _ => throw new InvalidOperationException("Unsupported item type")
            };
        }

        private static string GetItemUrl<TType>(TType item, AzureDevOpsSettings settings, AzureDevOpsQuery query)
        {
            return item switch
            {
                GitPullRequest => $"{settings.OrganizationUrl}/{settings.ProjectName}/_git/{query?.RepositoryName}/pullrequest/{GetItemId(item)}",
                WorkItem => $"{settings.OrganizationUrl}/{settings.ProjectName}/_workitems/edit/{GetItemId(item)}",
                _ => throw new InvalidOperationException("Unsupported item type")
            };
        }
    }
}
