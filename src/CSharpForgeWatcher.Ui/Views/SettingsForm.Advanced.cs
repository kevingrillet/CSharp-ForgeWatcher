using System.Diagnostics;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Infrastructure.Persistence;

namespace CSharpForgeWatcher.Ui.Views;

// Onglet « Avancé » : emplacements des fichiers, test de notification, réinitialisation.
// La fenêtre est déclarée en plusieurs fichiers : voir SettingsForm.cs pour ses
// champs, son assemblage et son enregistrement.
public sealed partial class SettingsForm
{
    // ---------------------------------------------------------------- onglet Avancé

    private TabPage BuildAdvancedTab()
    {
        var page = new TabPage(_text[TextKeys.Screen.TabAdvanced]) { Padding = new Padding(16) };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
        };

        layout.Controls.Add(NewSectionLabel(_text[TextKeys.Screen.AdvancedFiles]));
        layout.Controls.Add(NewLabel(
            _text.Format(TextKeys.Screen.AdvancedConfigurationFile, _configurationService.Location)));
        layout.Controls.Add(NewLabel(_text.Format(TextKeys.Screen.AdvancedStateFile, AppPaths.StateFile)));
        layout.Controls.Add(NewLabel(_text.Format(TextKeys.Screen.AdvancedLogFile, AppPaths.LogFile)));

        var openFolder = new Button { Text = _text[TextKeys.Screen.AdvancedOpenFolder], AutoSize = true };
        openFolder.Click += (_, _) => OpenDataFolder();
        layout.Controls.Add(openFolder);

        layout.Controls.Add(NewLabel(" "));
        layout.Controls.Add(NewSectionLabel(_text[TextKeys.Screen.AdvancedNotificationCheck]));
        layout.Controls.Add(NewLabel(_text[TextKeys.Screen.AdvancedNotificationHint], muted: true));

        var testNotificationButton = new Button
        {
            Text = _text[TextKeys.Screen.AdvancedTestNotification],
            AutoSize = true,
        };
        testNotificationButton.Click += (_, _) => ShowSampleNotification();
        layout.Controls.Add(testNotificationButton);

        layout.Controls.Add(NewLabel(" "));
        layout.Controls.Add(NewSectionLabel(_text[TextKeys.Screen.AdvancedReset]));
        layout.Controls.Add(NewLabel(_text[TextKeys.Screen.AdvancedResetHint], muted: true));

        var resetButton = new Button { Text = _text[TextKeys.Screen.AdvancedResetButton], AutoSize = true };
        resetButton.Click += (_, _) => ResetState();
        layout.Controls.Add(resetButton);

        page.Controls.Add(layout);
        return page;
    }

    private void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppPaths.EnsureDataDirectory()) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                _text.Format(TextKeys.Screen.AdvancedOpenFolderFailed, exception.Message),
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// Affiche une notification d'exemple, par le même canal que les vraies : c'est le moyen
    /// le plus simple de vérifier que les toasts fonctionnent sur ce poste.
    /// </summary>
    private void ShowSampleNotification()
    {
        var account = _draft.Accounts.FirstOrDefault();
        var repository = account?.Repositories.FirstOrDefault();

        var sample = new PullRequestEvent
        {
            Kind = NotificationKind.CommentOnMyPullRequest,
            Key = new PullRequestKey("exemple", 1234),
            Repository = new RepositoryRef(
                repository?.ProjectName ?? "Projet",
                "exemple",
                repository?.RepositoryName ?? "mon-depot"),
            PullRequestTitle = _text[TextKeys.Screen.SamplePullRequestTitle],
            Message = TextRef.Of(TextKeys.Screen.SampleNotification),
            Url = string.IsNullOrWhiteSpace(account?.Url)
                ? SourceControlProvider.AzureDevOps.UrlPlaceholder()
                : account!.Url,
            OccurredOn = DateTimeOffset.Now,
            AccountLabel = _draft.Accounts.Count > 1 ? account?.DisplayLabel ?? string.Empty : string.Empty,
        };

        _notificationPresenter.ShowEvent(sample, silent: !_soundBox.Checked);
    }

    private void ResetState()
    {
        var confirmation = MessageBox.Show(
            this,
            _text[TextKeys.Screen.AdvancedResetConfirm],
            Text,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        _monitor.ResetState();
        MessageBox.Show(
            this,
            _text[TextKeys.Screen.AdvancedResetDone],
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
