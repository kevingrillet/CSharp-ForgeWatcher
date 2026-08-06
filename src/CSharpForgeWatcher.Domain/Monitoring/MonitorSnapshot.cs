using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Domain.Monitoring;

/// <summary>
/// Mémoire d'un compte surveillé entre deux cycles : ses pull requests et ses pipelines.
/// </summary>
/// <remarks>
/// <para>
/// C'est l'unité de cloisonnement de l'état. Chaque compte a sa propre identité et son propre
/// amorçage : ajouter un compte, ou renouveler son jeton, ne réamorce que celui-là et laisse
/// les autres continuer à notifier normalement (SPEC-CFG-008).
/// </para>
/// <para>
/// POCO muable, sérialisé tel quel en JSON. Les clés des dictionnaires sont la forme texte de
/// <see cref="PullRequestKey"/> (« repoId:prId ») et la clé d'un pipeline
/// (« espace:définition »).
/// </para>
/// </remarks>
public sealed class AccountSnapshot
{
    /// <summary>
    /// Vrai dès que le cycle d'amorçage de ce compte a eu lieu. Tant qu'il est faux, on
    /// mémorise sans notifier (SPEC-POLL-001).
    /// </summary>
    public bool IsSeeded { get; set; }

    /// <summary>
    /// Identité à laquelle cet état correspond. Un changement de compte ou d'organisation
    /// l'invalide : les « nouveautés » ne sont pas les mêmes.
    /// </summary>
    public string? ViewerId { get; set; }

    /// <summary>Instantanés des PR connues, indexés par <c>repoId:prId</c>.</summary>
    public Dictionary<string, PullRequestSnapshot> PullRequests { get; set; } = [];

    /// <summary>
    /// Instantanés des pipelines surveillés, indexés par <c>espace:définition</c>
    /// (SPEC-PIPE-001).
    /// </summary>
    public Dictionary<string, PipelineSnapshot> Pipelines { get; set; } = [];

    /// <summary>Instantané mémorisé d'une PR, ou <c>null</c> si elle est inconnue.</summary>
    public PullRequestSnapshot? Find(PullRequestKey key)
        => PullRequests.TryGetValue(key.ToString(), out var snapshot) ? snapshot : null;

    /// <summary>Ajoute ou remplace l'instantané d'une PR.</summary>
    public void Put(PullRequestKey key, PullRequestSnapshot snapshot)
        => PullRequests[key.ToString()] = snapshot;

    /// <summary>Retire une PR de l'état (PR terminée ou dépôt retiré de la configuration).</summary>
    public bool Remove(PullRequestKey key) => PullRequests.Remove(key.ToString());

    /// <summary>Instantanés appartenant au dépôt indiqué.</summary>
    public IEnumerable<PullRequestSnapshot> ForRepository(string repositoryId)
        => PullRequests.Values.Where(p =>
            string.Equals(p.RepositoryId, repositoryId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Retire les PR dont le dépôt n'est plus surveillé (SPEC-CFG-002, règle 3).
    /// </summary>
    /// <param name="watchedRepositoryIds">Identifiants des dépôts encore surveillés.</param>
    /// <returns>Nombre d'instantanés purgés.</returns>
    public int PruneRepositoriesOutside(IReadOnlySet<string> watchedRepositoryIds)
    {
        var obsolete = PullRequests
            .Where(pair => !watchedRepositoryIds.Contains(pair.Value.RepositoryId))
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in obsolete)
        {
            PullRequests.Remove(key);
        }

        return obsolete.Count;
    }

    /// <summary>Instantané mémorisé d'un pipeline, ou <c>null</c> s'il est inconnu.</summary>
    public PipelineSnapshot? FindPipeline(string key)
        => Pipelines.TryGetValue(key, out var snapshot) ? snapshot : null;

    /// <summary>Ajoute ou remplace l'instantané d'un pipeline.</summary>
    public void PutPipeline(PipelineSnapshot snapshot) => Pipelines[snapshot.Key] = snapshot;

    /// <summary>
    /// Retire les pipelines qui ne sont plus surveillés (SPEC-PIPE-003, règle 2).
    /// </summary>
    /// <param name="watchedKeys">Clés <c>espace:définition</c> encore surveillées.</param>
    /// <returns>Nombre d'instantanés purgés.</returns>
    public int PrunePipelinesOutside(IReadOnlySet<string> watchedKeys)
    {
        var obsolete = Pipelines.Keys.Where(key => !watchedKeys.Contains(key)).ToList();

        foreach (var key in obsolete)
        {
            Pipelines.Remove(key);
        }

        return obsolete.Count;
    }

    /// <summary>Vide l'état de ce compte : le cycle suivant réamorcera en silence.</summary>
    public void Reset(string? viewerId)
    {
        ViewerId = viewerId;
        IsSeeded = false;
        PullRequests.Clear();
        Pipelines.Clear();
    }
}

/// <summary>
/// État complet mémorisé entre deux cycles de sondage — le contenu de <c>state.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// C'est la pièce maîtresse de la détection par diff (ADR-0003). Depuis la version 2 du
/// format, l'état est <b>cloisonné par compte</b> (ADR-0005) : un même poste peut surveiller
/// Azure DevOps, GitHub et GitLab en même temps, et chaque forge a sa propre identité, son
/// propre amorçage et sa propre mémoire.
/// </para>
/// <para>
/// Un <c>state.json</c> écrit par la version 1 ne porte pas de comptes : il est relu comme un
/// état vide, et le premier cycle réamorce simplement en silence (SPEC-POLL-001). C'est un
/// cache, pas une donnée utilisateur — le migrer ne vaudrait pas le code que cela coûterait.
/// </para>
/// </remarks>
public sealed class MonitorSnapshot
{
    /// <summary>Version du format. 1 : état unique. 2 : état par compte.</summary>
    public int Version { get; set; } = 2;

    /// <summary>Date du dernier cycle réussi.</summary>
    public DateTimeOffset? LastPollOn { get; set; }

    /// <summary>États mémorisés, indexés par identifiant de compte.</summary>
    public Dictionary<string, AccountSnapshot> Accounts { get; set; } = [];

    /// <summary>
    /// État du compte indiqué, créé à la volée s'il est inconnu.
    /// </summary>
    /// <remarks>
    /// Un compte inconnu naît donc <b>non amorcé</b>, ce qui est exactement le comportement
    /// attendu : le cycle qui le découvre mémorise sans notifier.
    /// </remarks>
    public AccountSnapshot ForAccount(string accountId)
    {
        if (Accounts.TryGetValue(accountId, out var snapshot))
        {
            return snapshot;
        }

        snapshot = new AccountSnapshot();
        Accounts[accountId] = snapshot;
        return snapshot;
    }

    /// <summary>
    /// Retire l'état des comptes qui ne sont plus configurés.
    /// </summary>
    /// <param name="accountIds">Identifiants des comptes encore présents.</param>
    /// <returns>Nombre de comptes purgés.</returns>
    public int PruneAccountsOutside(IReadOnlySet<string> accountIds)
    {
        var obsolete = Accounts.Keys.Where(id => !accountIds.Contains(id)).ToList();

        foreach (var id in obsolete)
        {
            Accounts.Remove(id);
        }

        return obsolete.Count;
    }

    /// <summary>Nombre total de PR mémorisées, tous comptes confondus.</summary>
    public int PullRequestCount => Accounts.Values.Sum(account => account.PullRequests.Count);

    /// <summary>Crée un état vierge.</summary>
    public static MonitorSnapshot Empty() => new();
}
