using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Monitoring;
using CSharpForgeWatcher.Application.Text;
using CSharpForgeWatcher.Application.Theming;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Ui.Localization;
using CSharpForgeWatcher.Ui.Theming;
using CSharpForgeWatcher.Ui.Tray;

namespace CSharpForgeWatcher.Ui.Views;

/// <summary>
/// Fenêtre de configuration : comptes, dépôts, pipelines, préférences, avancé.
/// </summary>
/// <remarks>
/// La fenêtre travaille sur une <b>copie</b> de la configuration
/// (<see cref="ConfigurationService.Edit"/>) : fermer par « Annuler » ne laisse aucune
/// trace (SPEC-CFG-004). La validation n'a lieu qu'à l'enregistrement, après quoi un cycle
/// est déclenché immédiatement.
/// </remarks>
public sealed partial class SettingsForm : Form
{
    private readonly ConfigurationService _configurationService;
    private readonly ISourceControlGatewayFactory _gatewayFactory;
    private readonly IAutoStartService _autoStartService;
    private readonly PullRequestMonitor _monitor;
    private readonly IBrowserLauncher _browserLauncher;
    private readonly INotificationPresenter _notificationPresenter;
    private readonly ThemeService _themeService;
    private readonly TextService _text;
    private readonly WatcherConfiguration _draft;

    /// <summary>
    /// Jetons saisis pendant cette session d'édition, par compte.
    /// </summary>
    /// <remarks>
    /// Ils restent ici, en clair et en mémoire, jusqu'à l'enregistrement : c'est
    /// <see cref="ConfigurationService"/> qui les chiffre, de sorte que le traitement du
    /// secret reste concentré en un seul endroit (ADR-0002).
    /// </remarks>
    private readonly Dictionary<string, string?> _typedTokens = new(StringComparer.Ordinal);

    private readonly ListBox _accountList = new();
    private readonly Label _accountStatus = new();

    private readonly TreeView _repositoryTree = new();
    private readonly ListBox _selectedRepositories = new();
    private readonly Label _repositoryStatus = new();
    private SelectionTreeBinder<RepositoryRef>? _repositoryBinder;

    private readonly TreeView _pipelineTree = new();
    private readonly ListBox _selectedPipelines = new();
    private readonly Label _pipelineStatus = new();
    private SelectionTreeBinder<PipelineDefinitionRef>? _pipelineBinder;

    private readonly Dictionary<NotificationKind, CheckBox> _kindCheckBoxes = [];
    private readonly NumericUpDown _pollIntervalBox = new();
    private readonly NumericUpDown _maxNotificationsBox = new();
    private readonly NumericUpDown _refreshMinutesBox = new();
    private readonly ComboBox _threadScopeBox = new();
    private readonly ComboBox _themeBox = new();
    private readonly ComboBox _languageBox = new();
    private readonly CheckBox _ownActionsBox = new();
    private readonly CheckBox _soundBox = new();
    private readonly CheckBox _startupBox = new();
    private readonly CheckBox _operationalErrorsBox = new();

    /// <summary>Construit la fenêtre de configuration.</summary>
    public SettingsForm(
        ConfigurationService configurationService,
        ISourceControlGatewayFactory gatewayFactory,
        IAutoStartService autoStartService,
        PullRequestMonitor monitor,
        IBrowserLauncher browserLauncher,
        INotificationPresenter notificationPresenter,
        ThemeService themeService,
        TextService textService)
    {
        _configurationService = configurationService;
        _gatewayFactory = gatewayFactory;
        _autoStartService = autoStartService;
        _monitor = monitor;
        _browserLauncher = browserLauncher;
        _notificationPresenter = notificationPresenter;
        _themeService = themeService;
        _text = textService;
        _draft = configurationService.Edit();

        Text = _text[TextKeys.Screen.SettingsTitle];
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 580);
        Size = new Size(920, 680);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        ShowInTaskbar = true;
        Font = new Font("Segoe UI", 9F);
        Icon = TrayIconFactory.LoadApplicationIcon();

        var tabs = new ThemedTabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };
        tabs.TabPages.Add(BuildAccountsTab());
        tabs.TabPages.Add(BuildRepositoriesTab());
        tabs.TabPages.Add(BuildPipelinesTab());
        tabs.TabPages.Add(BuildPreferencesTab());
        tabs.TabPages.Add(BuildAdvancedTab());

        Controls.Add(tabs);
        Controls.Add(BuildFooter());

        LoadDraftIntoControls();

        // Peint la fenêtre au thème courant, et la repeint si le thème change.
        _themeService.Register(this);
    }

    // ---------------------------------------------------------------- pied de page

    private Control BuildFooter()
    {
        var saveButton = new Button
        {
            Text = _text[TextKeys.Screen.ButtonSave],
            AutoSize = true,
            DialogResult = DialogResult.None,
        };
        saveButton.Click += (_, _) => Save();

        var cancelButton = new Button
        {
            Text = _text[TextKeys.Screen.ButtonCancel],
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
        };
        cancelButton.Click += (_, _) =>
        {
            // La fenêtre a peut-être prévisualisé un autre thème : on rétablit celui de la
            // configuration enregistrée.
            _themeService.Reapply();
        };

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(10),
        };
        footer.Controls.Add(cancelButton);
        footer.Controls.Add(saveButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        return footer;
    }

    private void LoadDraftIntoControls()
    {
        _pollIntervalBox.Value = Math.Clamp(
            _draft.PollIntervalSeconds,
            (int)_pollIntervalBox.Minimum,
            (int)_pollIntervalBox.Maximum);

        _maxNotificationsBox.Value = Math.Clamp(_draft.MaxNotificationsPerPoll, 1, 30);
        _refreshMinutesBox.Value = Math.Clamp(_draft.UninvolvedThreadRefreshMinutes, 1, 240);
        _threadScopeBox.SelectedIndex = _draft.ThreadScope == ThreadPollingScope.AllWatchedPullRequests ? 1 : 0;
        _themeBox.SelectedIndex = Math.Max(0, ThemeResolver.All.ToList().IndexOf(_draft.Theme));
        _languageBox.SelectedIndex = Math.Max(0, LanguageResolver.All.ToList().IndexOf(_draft.Language));
        _ownActionsBox.Checked = _draft.NotifyOwnActions;
        _soundBox.Checked = _draft.PlayNotificationSound;
        _operationalErrorsBox.Checked = _draft.Notifications.OperationalErrors;

        // L'état réel du démarrage automatique est celui du registre, pas celui du fichier.
        _startupBox.Checked = _autoStartService.IsEnabled();

        AfterAccountsChanged();
    }

    /// <summary>
    /// Passerelle du compte indiqué, ou <c>null</c> si ses identifiants manquent.
    /// </summary>
    /// <remarks>
    /// Le jeton employé est celui saisi pendant cette session s'il y en a un, sinon celui
    /// déjà enregistré : parcourir une forge ne demande donc pas de ressaisir son jeton.
    /// </remarks>
    private ISourceControlGateway? CreateGateway(WatchedAccount account)
    {
        var url = account.Url.Trim();
        var token = _typedTokens.GetValueOrDefault(account.Id);

        if (string.IsNullOrEmpty(token))
        {
            var saved = _configurationService.Current.FindAccount(account.Id);
            token = saved is null ? null : _configurationService.TokenOf(saved);
        }

        return string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token)
            ? null
            : _gatewayFactory.Create(new SourceControlConnection(url, token, account.Provider));
    }

    private void Save()
    {
        _draft.PollIntervalSeconds = (int)_pollIntervalBox.Value;
        _draft.MaxNotificationsPerPoll = (int)_maxNotificationsBox.Value;
        _draft.UninvolvedThreadRefreshMinutes = (int)_refreshMinutesBox.Value;
        _draft.ThreadScope = _threadScopeBox.SelectedIndex == 1
            ? ThreadPollingScope.AllWatchedPullRequests
            : ThreadPollingScope.InvolvedOnly;
        _draft.Theme = ThemeResolver.All[Math.Max(0, _themeBox.SelectedIndex)];
        _draft.Language = LanguageResolver.All[Math.Max(0, _languageBox.SelectedIndex)];
        _draft.NotifyOwnActions = _ownActionsBox.Checked;
        _draft.PlayNotificationSound = _soundBox.Checked;
        _draft.LaunchOnWindowsStartup = _startupBox.Checked;
        _draft.Notifications.OperationalErrors = _operationalErrorsBox.Checked;

        foreach (var (kind, checkBox) in _kindCheckBoxes)
        {
            _draft.Notifications.SetEnabled(kind, checkBox.Checked);
        }

        var validation = _draft.Validate(EffectiveToken);
        if (!validation.IsValid)
        {
            MessageBox.Show(
                this,
                _text.Format(TextKeys.Screen.SaveInvalid, _text.Join(validation.Errors)),
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!_autoStartService.SetEnabled(_startupBox.Checked))
        {
            MessageBox.Show(
                this,
                _text[TextKeys.Screen.SaveStartupFailed],
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        // Les comptes non ressaisis conservent leur jeton enregistré (SPEC-CFG-004).
        _configurationService.Apply(_draft, _typedTokens);

        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>Jeton effectif d'un compte pour la validation : saisi, ou déjà enregistré.</summary>
    private string? EffectiveToken(WatchedAccount account)
    {
        var typed = _typedTokens.GetValueOrDefault(account.Id);
        if (!string.IsNullOrEmpty(typed))
        {
            return typed;
        }

        var saved = _configurationService.Current.FindAccount(account.Id);
        return saved is null ? null : _configurationService.TokenOf(saved);
    }

    // ---------------------------------------------------------------- fabriques de contrôles

    private static Label NewLabel(
        string text,
        bool muted = false,
        int topPadding = 0,
        DockStyle dock = DockStyle.None) => new()
        {
            Text = text,
            AutoSize = true,
            Dock = dock,
            ForeColor = muted ? SystemColors.GrayText : SystemColors.ControlText,
            Padding = new Padding(0, topPadding, 0, dock == DockStyle.Top ? 4 : 0),
        };

    private Label NewSectionLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font(Font, FontStyle.Bold),
    };

    private static void AddRow(TableLayoutPanel layout, string label, Control control)
    {
        var row = layout.RowCount++;
        layout.Controls.Add(NewLabel(label, topPadding: 5), 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private static void Fill<TItem>(ListBox listBox, IEnumerable<TItem> items)
        where TItem : notnull
    {
        listBox.BeginUpdate();
        try
        {
            listBox.Items.Clear();

            foreach (var item in items)
            {
                listBox.Items.Add(item);
            }
        }
        finally
        {
            listBox.EndUpdate();
        }
    }

    /// <summary>
    /// Élément sélectionné, avec le compte auquel il appartient.
    /// </summary>
    /// <remarks>
    /// Les listes de droite mélangent les comptes : sans cette association, retirer un dépôt
    /// ne saurait pas dans quel compte le chercher — et deux comptes peuvent parfaitement
    /// surveiller des dépôts homonymes.
    /// </remarks>
    private sealed record SelectedItem<TItem>(WatchedAccount Account, TItem Item)
        where TItem : notnull
    {
        public override string ToString() => $"{Account.DisplayLabel} — {Item}";
    }
}
