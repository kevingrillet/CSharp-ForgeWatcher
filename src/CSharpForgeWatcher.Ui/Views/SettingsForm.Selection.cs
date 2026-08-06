using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Ui.Views;

// Onglets « Dépôts » et « Pipelines » : deux arborescences à cocher, un seul agencement.
// La fenêtre est déclarée en plusieurs fichiers : voir SettingsForm.cs pour ses
// champs, son assemblage et son enregistrement.
public sealed partial class SettingsForm
{
    // ---------------------------------------------------------------- onglet Dépôts

    private TabPage BuildRepositoriesTab()
    {
        _repositoryBinder = new SelectionTreeBinder<RepositoryRef>(
            _repositoryTree,
            _text,
            accounts: () => _draft.Accounts,
            loadScopes: LoadScopesAsync,
            loadItems: LoadRepositoriesAsync,
            label: repository => repository.RepositoryName,
            isSelected: (account, repository) => account.Repositories.Any(watched =>
                string.Equals(watched.RepositoryId, repository.RepositoryId, StringComparison.OrdinalIgnoreCase)),
            onSelectionChanged: SetRepositoryWatched,
            reportStatus: message => _repositoryStatus.Text = message);

        return BuildSelectionTab(
            title: _text[TextKeys.Screen.TabRepositories],
            treeTitle: _text[TextKeys.Screen.RepositoriesTreeTitle],
            selectionTitle: _text[TextKeys.Screen.RepositoriesSelectionTitle],
            tree: _repositoryTree,
            selection: _selectedRepositories,
            status: _repositoryStatus,
            initialStatus: _text[TextKeys.Screen.RepositoriesHint],
            onReload: () => _repositoryBinder!.Reload(),
            onFilter: filter => _repositoryBinder!.ApplyFilter(filter),
            onRemove: RemoveSelectedRepository);
    }

    private async Task<IReadOnlyList<ProjectSummary>> LoadScopesAsync(
        WatchedAccount account,
        CancellationToken cancellationToken)
    {
        var gateway = CreateGateway(account);
        return gateway is null
            ? Array.Empty<ProjectSummary>()
            : await gateway.GetProjectsAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<RepositoryRef>> LoadRepositoriesAsync(
        WatchedAccount account,
        string scopeName,
        CancellationToken cancellationToken)
    {
        var gateway = CreateGateway(account);
        return gateway is null
            ? Array.Empty<RepositoryRef>()
            : await gateway.GetRepositoriesAsync(scopeName, cancellationToken);
    }

    private void SetRepositoryWatched(WatchedAccount account, RepositoryRef repository, bool watched)
    {
        var existing = account.Repositories.FirstOrDefault(candidate =>
            string.Equals(candidate.RepositoryId, repository.RepositoryId, StringComparison.OrdinalIgnoreCase));

        if (watched && existing is null)
        {
            account.Repositories.Add(WatchedRepository.From(repository));
        }
        else if (!watched && existing is not null)
        {
            account.Repositories.Remove(existing);
        }

        RefreshSelectedRepositories();
    }

    private void RefreshSelectedRepositories()
    {
        Fill(
            _selectedRepositories,
            _draft.Accounts.SelectMany(account => account.Repositories
                .OrderBy(repository => repository.ProjectName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(repository => repository.RepositoryName, StringComparer.CurrentCultureIgnoreCase)
                .Select(repository => new SelectedItem<WatchedRepository>(account, repository))));

        var total = _draft.Accounts.Sum(account => account.Repositories.Count);
        _repositoryStatus.Text = _text.Format(TextKeys.Screen.RepositoriesCount, total);
        RefreshAccountList();
    }

    private void RemoveSelectedRepository()
    {
        if (_selectedRepositories.SelectedItem is not SelectedItem<WatchedRepository> selected)
        {
            return;
        }

        selected.Account.Repositories.Remove(selected.Item);
        _repositoryBinder?.UncheckWhere(candidate =>
            string.Equals(candidate.RepositoryId, selected.Item.RepositoryId, StringComparison.OrdinalIgnoreCase));
        RefreshSelectedRepositories();
    }

    // ---------------------------------------------------------------- onglet Pipelines

    private TabPage BuildPipelinesTab()
    {
        _pipelineBinder = new SelectionTreeBinder<PipelineDefinitionRef>(
            _pipelineTree,
            _text,
            accounts: () => _draft.Accounts,
            loadScopes: LoadScopesAsync,
            loadItems: LoadPipelinesAsync,
            label: definition => definition.Name,
            isSelected: (account, definition) => account.Pipelines.Any(watched => watched.Key == definition.Key),
            onSelectionChanged: SetPipelineWatched,
            reportStatus: message => _pipelineStatus.Text = message);

        return BuildSelectionTab(
            title: _text[TextKeys.Screen.TabPipelines],
            treeTitle: _text[TextKeys.Screen.PipelinesTreeTitle],
            selectionTitle: _text[TextKeys.Screen.PipelinesSelectionTitle],
            tree: _pipelineTree,
            selection: _selectedPipelines,
            status: _pipelineStatus,
            initialStatus: _text[TextKeys.Screen.PipelinesHint],
            onReload: () => _pipelineBinder!.Reload(),
            onFilter: filter => _pipelineBinder!.ApplyFilter(filter),
            onRemove: RemoveSelectedPipeline);
    }

    private async Task<IReadOnlyList<PipelineDefinitionRef>> LoadPipelinesAsync(
        WatchedAccount account,
        string scopeName,
        CancellationToken cancellationToken)
    {
        var gateway = CreateGateway(account);
        return gateway is null
            ? Array.Empty<PipelineDefinitionRef>()
            : await gateway.GetPipelineDefinitionsAsync(scopeName, cancellationToken);
    }

    private void SetPipelineWatched(WatchedAccount account, PipelineDefinitionRef definition, bool watched)
    {
        var existing = account.Pipelines.FirstOrDefault(candidate => candidate.Key == definition.Key);

        if (watched && existing is null)
        {
            account.Pipelines.Add(WatchedPipeline.From(definition));
        }
        else if (!watched && existing is not null)
        {
            account.Pipelines.Remove(existing);
        }

        RefreshSelectedPipelines();
    }

    private void RefreshSelectedPipelines()
    {
        Fill(
            _selectedPipelines,
            _draft.Accounts.SelectMany(account => account.Pipelines
                .OrderBy(pipeline => pipeline.ProjectName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(pipeline => pipeline.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(pipeline => new SelectedItem<WatchedPipeline>(account, pipeline))));

        var total = _draft.Accounts.Sum(account => account.Pipelines.Count);
        _pipelineStatus.Text = _text.Format(TextKeys.Screen.PipelinesCount, total);
        RefreshAccountList();
    }

    private void RemoveSelectedPipeline()
    {
        if (_selectedPipelines.SelectedItem is not SelectedItem<WatchedPipeline> selected)
        {
            return;
        }

        selected.Account.Pipelines.Remove(selected.Item);
        _pipelineBinder?.UncheckWhere(candidate => candidate.Key == selected.Item.Key);
        RefreshSelectedPipelines();
    }

    // ------------------------------------------------- agencement commun aux deux onglets

    /// <summary>
    /// Construit un onglet « arborescence à cocher + liste de la sélection ».
    /// </summary>
    /// <remarks>
    /// Les onglets *Dépôts* et *Pipelines* ne diffèrent que par leurs libellés et leurs
    /// délégués : leur agencement n'est décrit qu'une fois, ici.
    /// </remarks>
    private TabPage BuildSelectionTab(
        string title,
        string treeTitle,
        string selectionTitle,
        TreeView tree,
        ListBox selection,
        Label status,
        string initialStatus,
        Action onReload,
        Action<string> onFilter,
        Action onRemove)
    {
        var page = new TabPage(title) { Padding = new Padding(12) };

        var reloadButton = new Button { Text = _text[TextKeys.Screen.ButtonReload], AutoSize = true };
        reloadButton.Click += (_, _) => onReload();

        var filterBox = new TextBox { PlaceholderText = _text[TextKeys.Screen.FilterPlaceholder], Width = 240 };
        filterBox.TextChanged += (_, _) => onFilter(filterBox.Text);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
        toolbar.Controls.Add(reloadButton);
        toolbar.Controls.Add(filterBox);

        status.Dock = DockStyle.Bottom;
        status.AutoSize = true;
        status.ForeColor = SystemColors.GrayText;
        status.Text = initialStatus;

        tree.Dock = DockStyle.Fill;
        selection.Dock = DockStyle.Fill;
        selection.SelectionMode = SelectionMode.One;

        var removeButton = new Button
        {
            Text = _text[TextKeys.Screen.ButtonRemove],
            Dock = DockStyle.Bottom,
            AutoSize = true,
        };
        removeButton.Click += (_, _) => onRemove();

        var right = new Panel { Dock = DockStyle.Fill };
        right.Controls.Add(selection);
        right.Controls.Add(removeButton);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
        split.Panel1.Controls.Add(tree);
        split.Panel1.Controls.Add(NewLabel(treeTitle, dock: DockStyle.Top));
        split.Panel2.Controls.Add(right);
        split.Panel2.Controls.Add(NewLabel(selectionTitle, dock: DockStyle.Top));

        page.Controls.Add(split);
        page.Controls.Add(status);
        page.Controls.Add(toolbar);
        return page;
    }
}
