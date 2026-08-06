using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Application.Abstractions;

/// <summary>Identité de l'utilisateur authentifié par le jeton courant.</summary>
/// <param name="Id">
/// Identifiant d'identité tel que la forge l'emploie <b>dans le texte des commentaires</b> :
/// GUID pour Azure DevOps, <c>login</c> pour GitHub. Cette contrainte vient de la détection
/// de mention (SPEC-EVT-006, ADR-0004).
/// </param>
/// <param name="DisplayName">Nom affiché.</param>
/// <param name="UniqueName">Adresse de connexion (peut être absente).</param>
public sealed record ViewerIdentity(string Id, string DisplayName, string? UniqueName = null);

/// <summary>
/// Espace regroupant des dépôts, tel que listé dans la fenêtre de configuration : projet
/// d'équipe sur Azure DevOps, propriétaire (compte ou organisation) sur GitHub.
/// </summary>
/// <param name="Id">Identifiant de l'espace.</param>
/// <param name="Name">Nom de l'espace, celui passé aux autres méthodes du port.</param>
/// <param name="Description">Description, si renseignée.</param>
public sealed record ProjectSummary(string Id, string Name, string? Description = null);

/// <summary>
/// Port de lecture de la forge : la seule porte de sortie vers le serveur (SPEC-FORGE-001).
/// </summary>
/// <remarks>
/// Volontairement **en lecture seule** : l'application n'écrit jamais dans la forge,
/// ce qui autorise un jeton restreint à la lecture du code (cf. SDD §6).
/// <para>
/// Toute implémentation lève <see cref="SourceControlException"/> en cas d'échec, afin que
/// les couches supérieures distinguent une panne transitoire d'un problème
/// d'authentification sans connaître HTTP.
/// </para>
/// </remarks>
public interface ISourceControlGateway
{
    /// <summary>Résout l'utilisateur correspondant au jeton.</summary>
    Task<ViewerIdentity> GetViewerAsync(CancellationToken cancellationToken);

    /// <summary>Liste les espaces visibles (projets, ou propriétaires selon la forge).</summary>
    Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken);

    /// <summary>Liste les dépôts Git d'un espace.</summary>
    /// <param name="projectName">Nom (ou identifiant) de l'espace.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    Task<IReadOnlyList<RepositoryRef>> GetRepositoriesAsync(string projectName, CancellationToken cancellationToken);

    /// <summary>Liste les pull requests actives d'un dépôt.</summary>
    Task<IReadOnlyList<PullRequest>> GetActivePullRequestsAsync(RepositoryRef repository, CancellationToken cancellationToken);

    /// <summary>
    /// Lit une pull request par son numéro, quel que soit son état.
    /// Sert à connaître l'état final d'une PR disparue de la liste active (SPEC-EVT-009).
    /// </summary>
    /// <returns>La PR, ou <c>null</c> si elle n'existe plus / n'est pas accessible.</returns>
    Task<PullRequest?> GetPullRequestAsync(RepositoryRef repository, int pullRequestId, CancellationToken cancellationToken);

    /// <summary>
    /// Lit les discussions d'une pull request.
    /// </summary>
    /// <remarks>
    /// Une forge peut répartir ses messages sur plusieurs points d'entrée (GitHub en
    /// utilise trois) : c'est à l'implémentation de les réunir en discussions cohérentes,
    /// pas aux règles de détection.
    /// </remarks>
    Task<IReadOnlyList<CommentThread>> GetThreadsAsync(RepositoryRef repository, int pullRequestId, CancellationToken cancellationToken);

    /// <summary>
    /// Liste les définitions de pipeline d'un espace (SPEC-PIPE-003).
    /// </summary>
    /// <remarks>
    /// Une forge sans notion de pipeline retourne une liste vide : la fonctionnalité
    /// disparaît alors d'elle-même, sans code conditionnel ailleurs (SPEC-FORGE-004).
    /// </remarks>
    Task<IReadOnlyList<PipelineDefinitionRef>> GetPipelineDefinitionsAsync(
        string projectName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lit les exécutions récentes de plusieurs définitions d'un même espace.
    /// </summary>
    /// <remarks>
    /// Les définitions sont demandées <b>ensemble</b> : l'appelant n'émet qu'un appel par
    /// espace et par cycle, quel que soit le nombre de pipelines surveillés
    /// (SPEC-PIPE-004). Ce que l'implémentation en fait dépend de l'API : Azure DevOps
    /// accepte plusieurs définitions dans une requête, GitHub en exige une par workflow
    /// (SPEC-FORGE-007, ADR-0004).
    /// </remarks>
    /// <param name="projectName">Espace propriétaire.</param>
    /// <param name="definitionIds">Définitions dont on veut les exécutions.</param>
    /// <param name="maxRuns">Nombre maximal d'exécutions retournées, toutes définitions confondues.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    Task<IReadOnlyList<PipelineRun>> GetRecentPipelineRunsAsync(
        string projectName,
        IReadOnlyCollection<long> definitionIds,
        int maxRuns,
        CancellationToken cancellationToken);
}
