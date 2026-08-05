using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Ui.Localization;
using CSharpForgeWatcher.Ui.Theming;

namespace CSharpForgeWatcher.Ui.Views;

/// <summary>
/// Fenêtre d'édition d'un compte de forge : quelle forge, quelle adresse, quel jeton
/// (SPEC-CFG-008).
/// </summary>
/// <remarks>
/// <para>
/// Travaille sur une <b>copie</b> du compte, comme la fenêtre de configuration travaille sur
/// une copie de la configuration : fermer par « Annuler » ne laisse aucune trace
/// (SPEC-CFG-004).
/// </para>
/// <para>
/// Le jeton saisi n'est jamais chiffré ici : il ressort en clair dans <see cref="TypedToken"/>
/// et c'est <see cref="ConfigurationService"/> qui le protège à l'enregistrement — le secret
/// reste ainsi traité en un seul endroit (ADR-0002).
/// </para>
/// </remarks>
public sealed class AccountForm : Form
{
    private readonly ISourceControlGatewayFactory _gatewayFactory;
    private readonly IBrowserLauncher _browserLauncher;
    private readonly TextService _text;
    private readonly bool _hasStoredToken;

    private readonly ComboBox _providerBox = new();
    private readonly TextBox _labelBox = new();
    private readonly Label _urlLabel = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
    private readonly TextBox _urlBox = new();
    private readonly TextBox _tokenBox = new();
    private readonly Label _tokenScopeHint = new();
    private readonly LinkLabel _tokenPageLink = new();
    private readonly CheckBox _enabledBox = new();
    private readonly Label _status = new();
    private readonly Button _testButton = new();

    /// <summary>Ouvre l'édition d'un compte.</summary>
    /// <param name="account">Compte à éditer — une copie, jamais l'original.</param>
    /// <param name="hasStoredToken">Vrai si un jeton est déjà enregistré pour ce compte.</param>
    /// <param name="gatewayFactory">Fabrique servant au test de connexion.</param>
    /// <param name="browserLauncher">Ouverture de la page de création de jeton.</param>
    /// <param name="themeService">Applique le thème courant à cette fenêtre.</param>
    /// <param name="textService">Formule les libellés dans la langue choisie.</param>
    public AccountForm(
        WatchedAccount account,
        bool hasStoredToken,
        ISourceControlGatewayFactory gatewayFactory,
        IBrowserLauncher browserLauncher,
        ThemeService themeService,
        TextService textService)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(themeService);

        Account = account;
        _hasStoredToken = hasStoredToken;
        _gatewayFactory = gatewayFactory;
        _browserLauncher = browserLauncher;
        _text = textService ?? throw new ArgumentNullException(nameof(textService));

        Text = string.IsNullOrEmpty(account.Id)
            ? _text[TextKeys.Screen.AccountFormNew]
            : _text.Format(TextKeys.Screen.AccountFormEdit, account.DisplayLabel);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(680, 380);
        Font = new Font("Segoe UI", 9F);
        Icon = Tray.TrayIconFactory.LoadApplicationIcon();

        Controls.Add(BuildLayout());
        Controls.Add(BuildFooter());

        LoadAccountIntoControls();
        themeService.Register(this);
    }

    /// <summary>Compte édité. Modifié en place quand l'utilisateur valide.</summary>
    public WatchedAccount Account { get; }

    /// <summary>
    /// Jeton saisi en clair, ou <c>null</c> si l'utilisateur a laissé le champ vide — auquel
    /// cas le jeton déjà enregistré est conservé.
    /// </summary>
    public string? TypedToken { get; private set; }

    private Control BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(16),
            AutoSize = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _providerBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _providerBox.Width = 240;
        foreach (var provider in SourceControlProviderExtensions.Implemented)
        {
            _providerBox.Items.Add(provider.ToLabel());
        }

        _providerBox.SelectedIndexChanged += (_, _) => RefreshProviderLabels();

        _urlBox.Dock = DockStyle.Fill;
        _labelBox.Dock = DockStyle.Fill;
        _labelBox.PlaceholderText = _text[TextKeys.Screen.AccountLabelPlaceholder];

        _tokenBox.Dock = DockStyle.Fill;
        _tokenBox.UseSystemPasswordChar = true;

        _tokenScopeHint.AutoSize = true;
        _tokenScopeHint.MaximumSize = new Size(480, 0);
        _tokenScopeHint.ForeColor = SystemColors.GrayText;

        _tokenPageLink.Text = _text[TextKeys.Screen.AccountTokenPage];
        _tokenPageLink.AutoSize = true;
        _tokenPageLink.LinkClicked += (_, _) => _browserLauncher.Open(CurrentProvider().TokenPageUrl(_urlBox.Text));

        _enabledBox.Text = _text[TextKeys.Screen.AccountEnabled];
        _enabledBox.AutoSize = true;

        _testButton.Text = _text[TextKeys.Screen.AccountTest];
        _testButton.AutoSize = true;
        _testButton.Click += async (_, _) => await TestConnectionAsync();

        _status.AutoSize = true;
        _status.MaximumSize = new Size(480, 0);
        _status.ForeColor = SystemColors.GrayText;
        _status.Text = _text[TextKeys.Screen.AccountTestIdle];

        AddRow(layout, _text[TextKeys.Screen.AccountProvider], _providerBox);
        AddRow(layout, string.Empty, _urlLabel, _urlBox);
        AddRow(layout, _text[TextKeys.Screen.AccountLabel], _labelBox);
        AddRow(layout, _text[TextKeys.Screen.AccountToken], _tokenBox);
        AddRow(layout, string.Empty, _tokenScopeHint);
        AddRow(layout, string.Empty, _tokenPageLink);
        AddRow(layout, string.Empty, _enabledBox);
        AddRow(layout, string.Empty, _testButton);
        AddRow(layout, string.Empty, _status);

        return layout;
    }

    private Control BuildFooter()
    {
        var okButton = new Button { Text = _text[TextKeys.Screen.ButtonConfirm], AutoSize = true };
        okButton.Click += (_, _) => Confirm();

        var cancelButton = new Button
        {
            Text = _text[TextKeys.Screen.ButtonCancel],
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
        };

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(10),
        };
        footer.Controls.Add(cancelButton);
        footer.Controls.Add(okButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
        return footer;
    }

    private SourceControlProvider CurrentProvider()
        => SourceControlProviderExtensions.Implemented[Math.Max(0, _providerBox.SelectedIndex)];

    private void LoadAccountIntoControls()
    {
        _providerBox.SelectedIndex = Math.Max(
            0,
            SourceControlProviderExtensions.Implemented.ToList().IndexOf(Account.Provider));

        _urlBox.Text = Account.Url;
        _labelBox.Text = Account.Label;
        _enabledBox.Checked = Account.IsEnabled;

        _tokenBox.PlaceholderText = _hasStoredToken
            ? _text[TextKeys.Screen.AccountTokenStored]
            : _text[TextKeys.Screen.AccountTokenEmpty];

        RefreshProviderLabels();
    }

    /// <summary>Aligne les libellés dépendant de la forge (SPEC-FORGE-002).</summary>
    private void RefreshProviderLabels()
    {
        var provider = CurrentProvider();

        _urlLabel.Text = _text[provider.UrlLabelKey()];
        _urlBox.PlaceholderText = provider.UrlPlaceholder();
        _tokenScopeHint.Text = _text[provider.TokenScopeHintKey()];
    }

    /// <summary>Reporte la saisie sur le compte, après un contrôle de cohérence.</summary>
    private void Confirm()
    {
        var candidate = Account.Clone();
        candidate.Provider = CurrentProvider();
        candidate.Url = _urlBox.Text.Trim();
        candidate.Label = _labelBox.Text.Trim();
        candidate.IsEnabled = _enabledBox.Checked;

        var typed = _tokenBox.Text.Trim();
        var effectiveToken = typed.Length > 0 ? typed : (_hasStoredToken ? "déjà-enregistré" : string.Empty);

        var validation = candidate.Validate(effectiveToken);
        if (!validation.IsValid)
        {
            MessageBox.Show(
                this,
                _text.Format(TextKeys.Screen.AccountInvalid, _text.Join(validation.Errors)),
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        // Changer de forge ou de serveur rend la sélection existante inutilisable : ses
        // identifiants appartiennent à l'ancienne (SPEC-FORGE-002).
        if (!ConfirmSelectionResetIfNeeded(candidate))
        {
            return;
        }

        Account.Provider = candidate.Provider;
        Account.Url = candidate.Url;
        Account.Label = candidate.Label;
        Account.IsEnabled = candidate.IsEnabled;
        TypedToken = typed.Length > 0 ? typed : null;

        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>
    /// Propose de vider la sélection quand la forge ou le serveur change.
    /// </summary>
    /// <returns>Faux si l'utilisateur préfère revenir à sa saisie.</returns>
    private bool ConfirmSelectionResetIfNeeded(WatchedAccount candidate)
    {
        var sameForge = candidate.Provider == Account.Provider
                        && string.Equals(candidate.Url, Account.Url, StringComparison.OrdinalIgnoreCase);

        var selectionCount = Account.Repositories.Count + Account.Pipelines.Count;

        if (sameForge || selectionCount == 0)
        {
            return true;
        }

        var answer = MessageBox.Show(
            this,
            _text.Format(TextKeys.Screen.AccountForgeChanged, selectionCount),
            Text,
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning);

        switch (answer)
        {
            case DialogResult.Yes:
                Account.Repositories.Clear();
                Account.Pipelines.Clear();
                return true;

            case DialogResult.No:
                // L'utilisateur assume : la sélection est conservée telle quelle.
                return true;

            default:
                return false;
        }
    }

    private async Task TestConnectionAsync()
    {
        var url = _urlBox.Text.Trim();
        var typed = _tokenBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(url))
        {
            ShowStatus(
                _text.Format(
                    TextKeys.Screen.AccountTestUrlMissing,
                    _text[CurrentProvider().UrlLabelKey()].ToLowerInvariant()),
                isError: true);
            return;
        }

        if (typed.Length == 0 && !_hasStoredToken)
        {
            ShowStatus(_text[TextKeys.Screen.AccountTestTokenMissing], isError: true);
            return;
        }

        // Sans nouvelle saisie, on teste avec le jeton déjà enregistré : c'est le cas le plus
        // fréquent quand on vient corriger une URL.
        var token = typed.Length > 0 ? typed : Account.ProtectedPersonalAccessToken;
        if (typed.Length == 0)
        {
            ShowStatus(_text[TextKeys.Screen.AccountTestTokenNeeded], isError: true);
            return;
        }

        _testButton.Enabled = false;
        ShowStatus(_text[TextKeys.Screen.AccountTestRunning], isError: false);

        try
        {
            var gateway = _gatewayFactory.Create(new SourceControlConnection(url, token, CurrentProvider()));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var viewer = await gateway.GetViewerAsync(timeout.Token);
            ShowStatus(_text.Format(TextKeys.Screen.AccountTestOk, viewer.DisplayName), isError: false);
        }
        catch (SourceControlException exception)
        {
            ShowStatus(_text.Of(exception.ToUserText()), isError: true);
        }
        catch (OperationCanceledException)
        {
            ShowStatus(_text[TextKeys.Screen.AccountTestTimeout], isError: true);
        }
        finally
        {
            _testButton.Enabled = true;
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        _status.Text = message;
        _status.ForeColor = isError ? Color.Firebrick : Color.SeaGreen;
    }

    private static void AddRow(TableLayoutPanel layout, string label, Control control)
    {
        var row = layout.RowCount++;
        layout.Controls.Add(
            new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0) },
            0,
            row);
        layout.Controls.Add(control, 1, row);
    }

    private static void AddRow(TableLayoutPanel layout, string label, Control labelControl, Control control)
    {
        var row = layout.RowCount++;
        layout.Controls.Add(string.IsNullOrEmpty(label) ? labelControl : new Label { Text = label }, 0, row);
        layout.Controls.Add(control, 1, row);
    }
}
