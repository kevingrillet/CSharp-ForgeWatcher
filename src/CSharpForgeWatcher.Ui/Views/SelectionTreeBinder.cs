using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Ui.Localization;

namespace CSharpForgeWatcher.Ui.Views;

/// <summary>
/// Pilote une arborescence « comptes → espaces → éléments cochables » avec chargement à la
/// demande.
/// </summary>
/// <remarks>
/// <para>
/// Les onglets *Dépôts* (SPEC-CFG-002) et *Pipelines* (SPEC-PIPE-003) présentent exactement
/// la même mécanique : lister les comptes, déplier pour charger leurs espaces, déplier un
/// espace pour charger son contenu, cocher ce qu'on surveille, filtrer par nom. Cette classe
/// porte cette mécanique une seule fois ; chaque onglet ne fournit que ses différences
/// (comment charger, comment étiqueter, que faire d'une case cochée).
/// </para>
/// <para>
/// Le chargement est <b>différé à chaque niveau</b> : sur trois forges comportant chacune des
/// dizaines d'espaces, tout charger d'emblée serait long et inutile. Un compte n'est
/// interrogé que si l'utilisateur le déplie.
/// </para>
/// <para>
/// Le niveau « compte » n'est pas cochable : cocher une forge entière n'a pas de sens, et le
/// geste serait trop facile à faire par accident.
/// </para>
/// </remarks>
/// <typeparam name="TItem">Type des éléments cochables (dépôt, définition de pipeline…).</typeparam>
internal sealed class SelectionTreeBinder<TItem>
    where TItem : class
{
    /// <summary>Marqueur d'un nœud dont le contenu n'est pas encore chargé.</summary>
    private static readonly object LoadingMarker = new();

    private readonly TreeView _tree;
    private readonly TextService _text;
    private readonly Func<IReadOnlyList<WatchedAccount>> _accounts;
    private readonly Func<WatchedAccount, CancellationToken, Task<IReadOnlyList<ProjectSummary>>> _loadScopes;
    private readonly Func<WatchedAccount, string, CancellationToken, Task<IReadOnlyList<TItem>>> _loadItems;
    private readonly Func<TItem, string> _label;
    private readonly Func<WatchedAccount, TItem, bool> _isSelected;
    private readonly Action<WatchedAccount, TItem, bool> _onSelectionChanged;
    private readonly Action<string> _reportStatus;

    /// <summary>Espaces déjà chargés, par compte.</summary>
    private readonly Dictionary<string, List<ProjectSummary>> _scopesByAccount = new(StringComparer.Ordinal);

    /// <summary>Éléments déjà chargés, par « compte|espace ».</summary>
    private readonly Dictionary<string, List<TItem>> _itemsByScope = new(StringComparer.Ordinal);

    private bool _suppressEvents;
    private string _filter = string.Empty;

    /// <summary>
    /// Numéro de génération du contenu, incrémenté par <see cref="Reload"/>.
    /// </summary>
    /// <remarks>
    /// Les chargements ne sont pas attendus : au retour, celui qui a été lancé avant un
    /// <see cref="Reload"/> décrit une forge qu'on ne surveille plus dans les mêmes termes —
    /// URL ou jeton modifiés, compte retiré. Sans ce numéro, son résultat repeuplerait le
    /// cache que <see cref="Reload"/> vient justement de vider, et l'utilisateur verrait
    /// réapparaître des dépôts lus avec les identifiants précédents.
    /// </remarks>
    private int _generation;

    /// <summary>Branche le pilote sur une arborescence.</summary>
    /// <param name="tree">Arborescence à piloter (les cases à cocher sont activées).</param>
    /// <param name="accounts">Comptes à présenter, dans l'ordre d'affichage.</param>
    /// <param name="loadScopes">Charge les espaces d'un compte.</param>
    /// <param name="loadItems">Charge les éléments d'un espace d'un compte.</param>
    /// <param name="label">Libellé d'un élément.</param>
    /// <param name="isSelected">Indique si un élément est déjà surveillé sur ce compte.</param>
    /// <param name="onSelectionChanged">Appelé quand l'utilisateur coche ou décoche un élément.</param>
    /// <param name="reportStatus">Affiche un message d'état à l'utilisateur.</param>
    /// <param name="text">Formule les libellés dans la langue choisie.</param>
    public SelectionTreeBinder(
        TreeView tree,
        TextService text,
        Func<IReadOnlyList<WatchedAccount>> accounts,
        Func<WatchedAccount, CancellationToken, Task<IReadOnlyList<ProjectSummary>>> loadScopes,
        Func<WatchedAccount, string, CancellationToken, Task<IReadOnlyList<TItem>>> loadItems,
        Func<TItem, string> label,
        Func<WatchedAccount, TItem, bool> isSelected,
        Action<WatchedAccount, TItem, bool> onSelectionChanged,
        Action<string> reportStatus)
    {
        _tree = tree;
        _text = text;
        _accounts = accounts;
        _loadScopes = loadScopes;
        _loadItems = loadItems;
        _label = label;
        _isSelected = isSelected;
        _onSelectionChanged = onSelectionChanged;
        _reportStatus = reportStatus;

        _tree.CheckBoxes = true;
        _tree.HideSelection = false;
        _tree.BeforeExpand += OnBeforeExpand;
        _tree.AfterCheck += OnAfterCheck;
    }

    /// <summary>
    /// Reconstruit l'arborescence des comptes, en oubliant ce qui avait été chargé.
    /// </summary>
    /// <remarks>
    /// Appelée aussi après une modification des comptes : un compte dont l'URL ou le jeton a
    /// changé ne doit pas conserver le contenu lu avec les précédents.
    /// </remarks>
    public void Reload()
    {
        _generation++;
        _scopesByAccount.Clear();
        _itemsByScope.Clear();
        Rebuild();

        var accounts = _accounts();
        _reportStatus(accounts.Count == 0
            ? _text[TextKeys.Screen.TreeAddAccountFirst]
            : _text.Format(TextKeys.Screen.TreeAccountsLoaded, accounts.Count));
    }

    /// <summary>
    /// Filtre sur le nom des espaces et des éléments déjà chargés.
    /// </summary>
    /// <remarks>
    /// Un espace dont le nom ne correspond pas reste affiché s'il contient un élément qui
    /// correspond : on cherche le plus souvent un dépôt, pas l'espace qui le contient. À
    /// l'inverse, un espace dont le nom correspond montre tout son contenu. Les comptes ne
    /// sont jamais filtrés : ce sont les branches par lesquelles on charge le reste, les
    /// masquer rendrait leur contenu inatteignable.
    /// </remarks>
    public void ApplyFilter(string filter)
    {
        _filter = filter?.Trim() ?? string.Empty;
        Rebuild();
    }

    /// <summary>Décoche les nœuds affichés correspondant au prédicat, sans déclencher d'événement.</summary>
    public void UncheckWhere(Func<TItem, bool> predicate)
    {
        _suppressEvents = true;

        foreach (TreeNode accountNode in _tree.Nodes)
        {
            foreach (TreeNode scopeNode in accountNode.Nodes)
            {
                foreach (TreeNode itemNode in scopeNode.Nodes)
                {
                    if (itemNode.Tag is ItemNode item && predicate(item.Item))
                    {
                        itemNode.Checked = false;
                    }
                }
            }
        }

        _suppressEvents = false;
    }

    /// <summary>Reconstruit l'arborescence depuis le cache, filtre appliqué.</summary>
    private void Rebuild()
    {
        _suppressEvents = true;
        _tree.BeginUpdate();

        try
        {
            _tree.Nodes.Clear();

            foreach (var account in _accounts())
            {
                var node = new TreeNode(AccountCaption(account)) { Tag = account };

                if (_scopesByAccount.TryGetValue(account.Id, out var scopes))
                {
                    AddScopeNodes(node, account, scopes);
                }
                else
                {
                    node.Nodes.Add(new TreeNode(_text[TextKeys.Screen.TreeLoading]) { Tag = LoadingMarker });
                }

                _tree.Nodes.Add(node);
            }
        }
        finally
        {
            _tree.EndUpdate();
            _suppressEvents = false;
        }
    }

    /// <summary>Libellé d'un compte, l'état désactivé étant signalé.</summary>
    private string AccountCaption(WatchedAccount account)
        => account.IsEnabled
            ? account.DisplayLabel
            : _text.Format(TextKeys.Screen.TreeAccountDisabled, account.DisplayLabel);

    private void AddScopeNodes(TreeNode accountNode, WatchedAccount account, IEnumerable<ProjectSummary> scopes)
    {
        accountNode.Nodes.Clear();

        foreach (var scope in scopes)
        {
            var loaded = _itemsByScope.TryGetValue(ScopeKey(account, scope), out var items) ? items : null;
            var scopeMatches = Matches(scope.Name);

            if (!scopeMatches && (loaded is null || !loaded.Any(item => Matches(_label(item)))))
            {
                continue;
            }

            var node = new TreeNode(ScopeCaption(scope)) { Tag = new ScopeNode(account, scope) };

            if (loaded is not null)
            {
                AddItemNodes(node, account, loaded, showAll: scopeMatches);
            }
            else
            {
                node.Nodes.Add(new TreeNode(_text[TextKeys.Screen.TreeLoading]) { Tag = LoadingMarker });
            }

            accountNode.Nodes.Add(node);
        }

        if (accountNode.Nodes.Count == 0)
        {
            accountNode.Nodes.Add(new TreeNode(EmptyCaption()));
        }
    }

    /// <summary>Peuple un espace de ses éléments cochables, filtre appliqué.</summary>
    /// <remarks>
    /// <c>showAll</c> vaut vrai quand l'espace lui-même correspond au filtre : tout son
    /// contenu est alors montré, sans quoi filtrer sur un nom d'espace n'afficherait aucun
    /// élément.
    /// </remarks>
    private void AddItemNodes(
        TreeNode scopeNode,
        WatchedAccount account,
        IEnumerable<TItem> items,
        bool showAll)
    {
        scopeNode.Nodes.Clear();

        foreach (var item in items)
        {
            if (!showAll && !Matches(_label(item)))
            {
                continue;
            }

            scopeNode.Nodes.Add(new TreeNode(_label(item))
            {
                Tag = new ItemNode(account, item),
                Checked = _isSelected(account, item),
            });
        }

        if (scopeNode.Nodes.Count == 0)
        {
            scopeNode.Nodes.Add(new TreeNode(EmptyCaption()));
        }
    }

    /// <summary>
    /// Libellé d'un espace : son nom tel que la forge le donne, sauf pour l'espace synthétique
    /// des dépôts personnels GitHub, dont le nom est une clé de catalogue.
    /// </summary>
    /// <remarks>
    /// L'adaptateur ne peut pas formuler — il ignore la langue —, il renvoie donc la clé comme
    /// nom d'espace. Un nom d'espace réel n'est jamais une clé connue du catalogue, et ressort
    /// donc inchangé.
    /// </remarks>
    private string ScopeCaption(ProjectSummary scope) => _text[scope.Name];

    /// <summary>Vrai si le texte correspond au filtre courant, ou si aucun filtre n'est actif.</summary>
    private bool Matches(string text)
        => _filter.Length == 0 || text.Contains(_filter, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    /// Libellé d'un nœud sans contenu : « aucun résultat » sous un filtre, « aucun élément »
    /// sinon — la nuance dit à l'utilisateur si c'est son filtre qui masque tout.
    /// </summary>
    private string EmptyCaption()
        => _text[_filter.Length > 0 ? TextKeys.Screen.TreeNoMatch : TextKeys.Screen.TreeEmpty];

    private void OnBeforeExpand(object? sender, TreeViewCancelEventArgs args)
    {
        if (args.Node is null || !NeedsLoading(args.Node))
        {
            return;
        }

        // Volontairement non attendu : le nœud affiche « Chargement… » puis se remplit.
        switch (args.Node.Tag)
        {
            case WatchedAccount account:
                _ = LoadScopesAsync(args.Node, account);
                break;

            case ScopeNode scope:
                _ = LoadItemsAsync(args.Node, scope);
                break;
        }
    }

    private static bool NeedsLoading(TreeNode node)
        => node.Nodes.Count == 1 && ReferenceEquals(node.Nodes[0].Tag, LoadingMarker);

    private async Task LoadScopesAsync(TreeNode accountNode, WatchedAccount account)
    {
        var generation = _generation;

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var scopes = await _loadScopes(account, timeout.Token);

            if (generation != _generation)
            {
                return;
            }

            _scopesByAccount[account.Id] = scopes.ToList();

            // Nœud remplacé entre-temps par un changement de filtre : le cache vient d'être
            // renseigné, une reconstruction suffit à faire apparaître le résultat.
            if (accountNode.TreeView is null)
            {
                Rebuild();
                return;
            }

            _suppressEvents = true;
            AddScopeNodes(accountNode, account, scopes);
            _suppressEvents = false;

            accountNode.Expand();
            _reportStatus(_text.Format(
                TextKeys.Screen.TreeScopesLoaded,
                account.DisplayLabel,
                scopes.Count,
                _text[account.Provider.ScopeLabelKey()].ToLowerInvariant()));
        }
        catch (Exception exception)
        {
            ShowLoadFailure(accountNode, exception, account.DisplayLabel, generation);
        }
    }

    private async Task LoadItemsAsync(TreeNode scopeNode, ScopeNode scope)
    {
        var generation = _generation;

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var items = await _loadItems(scope.Account, scope.Scope.Name, timeout.Token);

            if (generation != _generation)
            {
                return;
            }

            _itemsByScope[ScopeKey(scope.Account, scope.Scope)] = items.ToList();

            if (scopeNode.TreeView is null)
            {
                Rebuild();
                return;
            }

            _suppressEvents = true;
            AddItemNodes(scopeNode, scope.Account, items, showAll: Matches(scope.Scope.Name));
            _suppressEvents = false;

            scopeNode.Expand();
            _reportStatus(_text.Format(TextKeys.Screen.TreeItemsLoaded, scope.Scope.Name, items.Count));
        }
        catch (Exception exception)
        {
            ShowLoadFailure(scopeNode, exception, scope.Scope.Name, generation);
        }
    }

    /// <summary>Remplace le contenu d'un nœud par un message d'échec, sans masquer la cause.</summary>
    /// <remarks>
    /// Attrape <b>tout</b>, volontairement : ces chargements ne sont pas attendus, et une
    /// exception imprévue n'aurait aucun observateur — la tâche serait abandonnée en silence
    /// et le nœud resterait sur « Chargement… » indéfiniment. Une panne inattendue doit se
    /// voir comme les autres.
    /// </remarks>
    private void ShowLoadFailure(TreeNode node, Exception exception, string subject, int generation)
    {
        if (generation != _generation || node.TreeView is null)
        {
            return;
        }

        _suppressEvents = true;
        node.Nodes.Clear();
        node.Nodes.Add(new TreeNode(_text[TextKeys.Screen.TreeLoadFailed]));
        _suppressEvents = false;

        _reportStatus(exception switch
        {
            SourceControlException forgeException
                => _text.Format(
                    TextKeys.Screen.TreeLoadFailedWith,
                    subject,
                    _text.Of(forgeException.ToUserText())),
            OperationCanceledException
                => _text.Format(TextKeys.Screen.TreeLoadTimedOut, subject),
            _ => _text.Format(TextKeys.Screen.TreeLoadFailedWith, subject, exception.Message),
        });
    }

    private void OnAfterCheck(object? sender, TreeViewEventArgs args)
    {
        if (_suppressEvents || args.Node is null)
        {
            return;
        }

        switch (args.Node.Tag)
        {
            case ItemNode item:
                _onSelectionChanged(item.Account, item.Item, args.Node.Checked);
                break;

            // Cocher un espace coche les éléments déjà chargés ; les autres suivront au
            // dépliage.
            case ScopeNode:
                foreach (TreeNode child in args.Node.Nodes)
                {
                    if (child.Tag is ItemNode)
                    {
                        child.Checked = args.Node.Checked;
                    }
                }

                break;

            // Un compte n'est pas cochable : on annule la coche pour que l'affichage ne
            // suggère pas une sélection qui n'existe pas.
            case WatchedAccount:
                _suppressEvents = true;
                args.Node.Checked = false;
                _suppressEvents = false;
                break;
        }
    }

    private static string ScopeKey(WatchedAccount account, ProjectSummary scope) => $"{account.Id}|{scope.Name}";

    /// <summary>Nœud d'espace : l'espace et le compte auquel il appartient.</summary>
    private sealed record ScopeNode(WatchedAccount Account, ProjectSummary Scope);

    /// <summary>Nœud d'élément cochable : l'élément et le compte auquel il appartient.</summary>
    private sealed record ItemNode(WatchedAccount Account, TItem Item);
}
