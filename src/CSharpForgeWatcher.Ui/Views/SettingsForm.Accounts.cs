using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Ui.Views;

// Onglet « Comptes » : liste des comptes de forge et leur édition.
// La fenêtre est déclarée en plusieurs fichiers : voir SettingsForm.cs pour ses
// champs, son assemblage et son enregistrement.
public sealed partial class SettingsForm
{
    // ---------------------------------------------------------------- onglet Comptes

    private TabPage BuildAccountsTab()
    {
        var page = new TabPage(_text[TextKeys.Screen.TabAccounts]) { Padding = new Padding(12) };

        _accountList.Dock = DockStyle.Fill;
        _accountList.SelectionMode = SelectionMode.One;
        _accountList.DoubleClick += (_, _) => EditSelectedAccount();

        var addButton = new Button { Text = _text[TextKeys.Screen.ButtonAdd], AutoSize = true };
        addButton.Click += (_, _) => AddAccount();

        var editButton = new Button { Text = _text[TextKeys.Screen.ButtonEdit], AutoSize = true };
        editButton.Click += (_, _) => EditSelectedAccount();

        var removeButton = new Button { Text = _text[TextKeys.Screen.ButtonRemove], AutoSize = true };
        removeButton.Click += (_, _) => RemoveSelectedAccount();

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
        toolbar.Controls.Add(addButton);
        toolbar.Controls.Add(editButton);
        toolbar.Controls.Add(removeButton);

        _accountStatus.Dock = DockStyle.Bottom;
        _accountStatus.AutoSize = true;
        _accountStatus.ForeColor = SystemColors.GrayText;

        var explanation = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 108,
            ForeColor = SystemColors.GrayText,
            Text = _text[TextKeys.Screen.AccountsExplanation],
        };

        page.Controls.Add(_accountList);
        page.Controls.Add(_accountStatus);
        page.Controls.Add(explanation);
        page.Controls.Add(toolbar);
        return page;
    }

    private void AddAccount()
    {
        var account = new WatchedAccount { Id = WatchedAccount.NewId() };

        using var dialog = new AccountForm(
            account,
            hasStoredToken: false,
            _gatewayFactory,
            _browserLauncher,
            _themeService,
            _text);

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _draft.Accounts.Add(account);
        _typedTokens[account.Id] = dialog.TypedToken;
        AfterAccountsChanged();
    }

    private void EditSelectedAccount()
    {
        if (_accountList.SelectedItem is not WatchedAccount account)
        {
            return;
        }

        var draft = account.Clone();

        using var dialog = new AccountForm(
            draft,
            HasToken(account),
            _gatewayFactory,
            _browserLauncher,
            _themeService,
            _text);

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        // La copie a pu vider la sélection (changement de forge) : on la reprend telle quelle.
        account.Provider = draft.Provider;
        account.Url = draft.Url;
        account.Label = draft.Label;
        account.IsEnabled = draft.IsEnabled;
        account.Repositories = draft.Repositories;
        account.Pipelines = draft.Pipelines;

        if (dialog.TypedToken is { } typed)
        {
            _typedTokens[account.Id] = typed;
        }

        AfterAccountsChanged();
    }

    private void RemoveSelectedAccount()
    {
        if (_accountList.SelectedItem is not WatchedAccount account)
        {
            return;
        }

        var selectionCount = account.Repositories.Count + account.Pipelines.Count;
        var detail = selectionCount > 0
            ? _text.Format(TextKeys.Screen.AccountRemoveDetail, selectionCount)
            : string.Empty;

        var confirmation = MessageBox.Show(
            this,
            _text.Format(TextKeys.Screen.AccountRemoveConfirm, account.DisplayLabel, detail),
            Text,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        _draft.Accounts.Remove(account);
        _typedTokens.Remove(account.Id);
        AfterAccountsChanged();
    }

    /// <summary>Rafraîchit tout ce qui dépend de la liste des comptes.</summary>
    private void AfterAccountsChanged()
    {
        RefreshAccountList();

        // Les arborescences montrent les comptes : elles doivent être reconstruites, et leur
        // contenu oublié — l'adresse ou le jeton d'un compte a peut-être changé.
        _repositoryBinder?.Reload();
        _pipelineBinder?.Reload();

        RefreshSelectedRepositories();
        RefreshSelectedPipelines();
    }

    private void RefreshAccountList()
    {
        var selectedId = (_accountList.SelectedItem as WatchedAccount)?.Id;

        Fill(_accountList, _draft.Accounts);

        if (selectedId is not null)
        {
            var index = _draft.Accounts.FindIndex(account =>
                string.Equals(account.Id, selectedId, StringComparison.Ordinal));

            if (index >= 0)
            {
                _accountList.SelectedIndex = index;
            }
        }

        var enabled = _draft.Accounts.Count(account => account.IsEnabled);
        var watched = _draft.Accounts.Sum(account => account.Repositories.Count + account.Pipelines.Count);

        _accountStatus.Text = _draft.Accounts.Count == 0
            ? _text[TextKeys.Screen.AccountsEmpty]
            : _text.Format(TextKeys.Screen.AccountsSummary, _draft.Accounts.Count, enabled, watched);
    }

    /// <summary>Vrai si un jeton est disponible pour ce compte (enregistré ou saisi).</summary>
    private bool HasToken(WatchedAccount account)
        => !string.IsNullOrEmpty(account.ProtectedPersonalAccessToken)
           || !string.IsNullOrEmpty(_typedTokens.GetValueOrDefault(account.Id));
}
