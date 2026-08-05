using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Links;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Infrastructure.GitLab.Dtos;
using CSharpForgeWatcher.Infrastructure.SourceControl;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Infrastructure.GitLab;

/// <summary>
/// Implémentation REST du port <see cref="ISourceControlGateway"/> pour GitLab (API v4),
/// gitlab.com comme instance auto-hébergée.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lecture seule</b> : uniquement des <c>GET</c>. Un jeton personnel de portée
/// <c>read_api</c> suffit — GitLab est la seule des trois forges à proposer une portée
/// réellement limitée à la lecture, ce qui en fait le meilleur élève du principe de moindre
/// privilège annoncé en SPEC-FORGE-001.
/// </para>
/// <para>
/// Le modèle de GitLab est le plus proche du domaine : les discussions sont déjà regroupées
/// et portent leur état de résolution, un projet est à la fois un dépôt et son pipeline. Les
/// deux seules traductions notables sont le vocabulaire (<i>merge request</i>, <i>note</i>,
/// <i>approbation</i>) et l'identifiant textuel des discussions, remplacé par celui de leur
/// première note.
/// </para>
/// </remarks>
public sealed class GitLabRestGateway : RestGatewayBase, ISourceControlGateway
{
    /// <summary>Nombre maximal de merge requests ouvertes lues par projet et par cycle.</summary>
    private const int MaxPullRequestsPerRepository = 200;

    /// <summary>Entrée désignant les projets personnels, hors de tout groupe.</summary>
    private const string PersonalScope = "Projets personnels";

    /// <summary>
    /// Générateur d'adresses web, utilisé pour ancrer les notes.
    /// </summary>
    /// <remarks>
    /// L'API des discussions ne rappelle pas l'adresse de la merge request, alors que chaque
    /// note doit porter la sienne (SPEC-LINK-004) : on la reconstruit donc ici, avec le même
    /// générateur que celui du reste de l'application.
    /// </remarks>
    private readonly GitLabLinkBuilder _links;

    /// <summary>Construit la passerelle pour un serveur et un jeton donnés.</summary>
    /// <param name="serverUrl">Racine du serveur, ex. <c>https://gitlab.com</c>.</param>
    /// <param name="personalAccessToken">Jeton en clair.</param>
    /// <param name="httpClient">Client à réutiliser ; <c>null</c> pour en créer un dédié.</param>
    /// <param name="logger">Journal, facultatif.</param>
    public GitLabRestGateway(
        string serverUrl,
        string personalAccessToken,
        HttpClient? httpClient = null,
        ILogger? logger = null)
        : base(ToApiUrl(serverUrl), "GitLab", httpClient, logger)
    {
        var server = ServerUrl.Origin(serverUrl);
        _links = GitLabLinkBuilder.For(server);

        // GitLab accepte « Bearer » pour un jeton personnel comme pour un jeton OAuth ;
        // l'en-tête PRIVATE-TOKEN est l'autre forme, réservée aux jetons personnels.
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", personalAccessToken);

        HttpClient.DefaultRequestHeaders.Accept.Clear();
        HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        HttpClient.DefaultRequestHeaders.UserAgent.Clear();
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ForgeWatcher/1.0");
    }

    /// <summary>Déduit l'adresse de l'API de celle du serveur (SPEC-FORGE-002).</summary>
    /// <remarks>
    /// GitLab sert son API sous <c>/api/v4</c> du même hôte, y compris sur gitlab.com : il n'y
    /// a pas d'hôte dédié comme chez GitHub, donc pas de cas particulier.
    /// </remarks>
    public static string ToApiUrl(string? serverUrl) => $"{ServerUrl.Origin(serverUrl)}/api/v4";

    /// <inheritdoc />
    public async Task<ViewerIdentity> GetViewerAsync(CancellationToken cancellationToken)
    {
        var user = await GetAsync<GlUser>("/user", cancellationToken).ConfigureAwait(false);
        var viewer = GitLabMapper.ToViewer(user);

        if (string.IsNullOrEmpty(viewer.Id))
        {
            throw new SourceControlException(
                TextRef.Of(TextKeys.Forge.NoIdentity, ForgeName),
                (int)HttpStatusCode.Unauthorized);
        }

        return viewer;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Les « espaces » de GitLab sont ses <b>groupes</b> (SPEC-FORGE-004). Les projets
    /// personnels, qui n'appartiennent à aucun groupe, sont proposés séparément sous une entrée
    /// dédiée.
    /// </remarks>
    public async Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        var groups = await GetPagedAsync<GlGroup>(
            "/groups?min_access_level=10&all_available=false&order_by=path&sort=asc",
            cancellationToken).ConfigureAwait(false);

        var spaces = new List<ProjectSummary>
        {
            new(PersonalScope, PersonalScope, "Vos projets personnels"),
        };

        spaces.AddRange(groups
            .Select(GitLabMapper.ToGroup)
            .Where(group => !string.IsNullOrEmpty(group.Name))
            .OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase));

        return spaces;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RepositoryRef>> GetRepositoriesAsync(
        string projectName,
        CancellationToken cancellationToken)
    {
        var projects = await ReadProjectsAsync(projectName, cancellationToken).ConfigureAwait(false);

        return projects
            .Select(project => GitLabMapper.ToRepository(project, projectName))
            .OrderBy(repository => repository.RepositoryName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Les approbations sont lues uniquement sur les merge requests qui concernent
    /// l'utilisateur : elles coûtent une requête chacune, et la règle de vote ne notifie de
    /// toute façon que celles dont il est l'auteur (SPEC-EVT-003, SPEC-FORGE-007).
    /// </remarks>
    public async Task<IReadOnlyList<PullRequest>> GetActivePullRequestsAsync(
        RepositoryRef repository,
        CancellationToken cancellationToken)
    {
        var project = RequireProjectId(repository);

        var mergeRequests = await GetPagedAsync<GlMergeRequest>(
            $"/projects/{project}/merge_requests?state=opened&order_by=created_at&sort=desc",
            cancellationToken).ConfigureAwait(false);

        var viewer = await GetViewerAsync(cancellationToken).ConfigureAwait(false);

        return await RunBoundedAsync(
            mergeRequests.Take(MaxPullRequestsPerRepository).ToList(),
            async (mergeRequest, token) =>
            {
                GlApprovals? approvals = null;
                IReadOnlyList<GlReviewer>? reviewers = null;

                if (Concerns(mergeRequest, viewer.Id))
                {
                    approvals = await GetOrNullAsync<GlApprovals>(
                        $"/projects/{project}/merge_requests/{mergeRequest.Iid}/approvals",
                        token).ConfigureAwait(false);

                    // Point d'entrée absent des versions anciennes : son absence n'est pas une
                    // erreur, elle prive seulement du détail « changements demandés ».
                    reviewers = await GetPagedOrNullAsync<GlReviewer>(
                        $"/projects/{project}/merge_requests/{mergeRequest.Iid}/reviewers",
                        token).ConfigureAwait(false);
                }

                return GitLabMapper.ToPullRequest(mergeRequest, repository, approvals, reviewers);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PullRequest?> GetPullRequestAsync(
        RepositoryRef repository,
        int pullRequestId,
        CancellationToken cancellationToken)
    {
        var project = RequireProjectId(repository);

        var dto = await GetOrNullAsync<GlMergeRequest>(
            $"/projects/{project}/merge_requests/{pullRequestId}",
            cancellationToken).ConfigureAwait(false);

        return dto is null
            ? null
            : GitLabMapper.ToPullRequest(dto, repository, approvals: null, reviewers: null);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Un seul point d'entrée : GitLab livre les discussions déjà regroupées, avec leurs notes
    /// et leur état de résolution. C'est la forge dont le modèle colle le mieux au domaine.
    /// </remarks>
    public async Task<IReadOnlyList<CommentThread>> GetThreadsAsync(
        RepositoryRef repository,
        int pullRequestId,
        CancellationToken cancellationToken)
    {
        var project = RequireProjectId(repository);

        var discussions = await GetPagedAsync<GlDiscussion>(
            $"/projects/{project}/merge_requests/{pullRequestId}/discussions",
            cancellationToken).ConfigureAwait(false);

        return GitLabMapper.ToThreads(discussions, _links.ForPullRequest(repository, pullRequestId));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Un projet GitLab porte un unique <c>.gitlab-ci.yml</c> : le projet <b>est</b> le
    /// pipeline. Lister les « définitions » d'un groupe revient donc à lister ses projets —
    /// une seule requête, là où GitHub doit interroger chaque dépôt (SPEC-FORGE-007).
    /// </remarks>
    public async Task<IReadOnlyList<PipelineDefinitionRef>> GetPipelineDefinitionsAsync(
        string projectName,
        CancellationToken cancellationToken)
    {
        var projects = await ReadProjectsAsync(projectName, cancellationToken).ConfigureAwait(false);

        return projects
            .Where(GitLabMapper.HasPipelines)
            .Select(project => GitLabMapper.ToPipelineDefinition(project, projectName))
            .OrderBy(definition => definition.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    /// <remarks>
    /// L'espace d'un pipeline GitLab est le chemin complet du projet, et sa « définition »
    /// l'identifiant de ce même projet : une seule requête suffit donc, quel que soit le
    /// nombre de définitions demandées — elles désignent toutes le même projet.
    /// </remarks>
    public async Task<IReadOnlyList<PipelineRun>> GetRecentPipelineRunsAsync(
        string projectName,
        IReadOnlyCollection<long> definitionIds,
        int maxRuns,
        CancellationToken cancellationToken)
    {
        if (definitionIds.Count == 0)
        {
            return Array.Empty<PipelineRun>();
        }

        var top = Math.Clamp(maxRuns, 1, PageSize);

        var perDefinition = await RunBoundedAsync(
            definitionIds.Distinct().ToList(),
            async (definitionId, token) =>
            {
                var definition = new PipelineDefinitionRef(projectName, definitionId, projectName);

                var pipelines = await GetPagedOrNullAsync<GlPipeline>(
                    $"/projects/{definitionId}/pipelines?per_page={top}&order_by=id&sort=desc",
                    token).ConfigureAwait(false);

                return (pipelines ?? [])
                    .Take(top)
                    .Select(pipeline => GitLabMapper.ToPipelineRun(pipeline, definition))
                    .ToArray();
            },
            cancellationToken).ConfigureAwait(false);

        return perDefinition.SelectMany(runs => runs).ToArray();
    }

    /// <inheritdoc />
    /// <remarks>
    /// GitLab respecte les codes : <c>429</c> pour un quota dépassé, <c>403</c> pour une
    /// autorisation manquante. Aucun reclassement n'est nécessaire, contrairement à GitHub.
    /// </remarks>
    protected override SourceControlException Describe(HttpResponseMessage response, string body, string url)
        => DescribeByStatus(response.StatusCode, body);

    /// <summary>
    /// Vrai si la merge request concerne l'utilisateur : il en est l'auteur, ou sa relecture
    /// est attendue.
    /// </summary>
    private static bool Concerns(GlMergeRequest mergeRequest, string viewerId)
        => string.Equals(mergeRequest.Author?.Username, viewerId, StringComparison.OrdinalIgnoreCase)
           || (mergeRequest.Reviewers ?? []).Any(reviewer =>
               string.Equals(reviewer.Username, viewerId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Projets d'un groupe, ou projets personnels de l'utilisateur.</summary>
    private async Task<IReadOnlyList<GlProject>> ReadProjectsAsync(
        string scope,
        CancellationToken cancellationToken)
    {
        var projects = string.Equals(scope, PersonalScope, StringComparison.Ordinal)
            ? await GetPagedAsync<GlProject>(
                "/projects?membership=true&owned=true&order_by=path&sort=asc",
                cancellationToken).ConfigureAwait(false)
            : await GetPagedAsync<GlProject>(
                $"/groups/{Escape(scope)}/projects?include_subgroups=true&order_by=path&sort=asc",
                cancellationToken).ConfigureAwait(false);

        return projects.Where(project => !project.Archived).ToArray();
    }

    /// <summary>
    /// Identifiant numérique du projet, seule forme acceptée sans ambiguïté par l'API.
    /// </summary>
    /// <remarks>
    /// Une valeur non numérique vient d'une configuration constituée sur une autre forge —
    /// Azure DevOps mémorise des GUID. Mieux vaut un message explicite qu'une suite d'appels
    /// voués à l'échec.
    /// </remarks>
    private static string RequireProjectId(RepositoryRef repository)
    {
        if (!long.TryParse(repository.RepositoryId, out var id) || id <= 0)
        {
            throw new SourceControlException(
                TextRef.Of(TextKeys.Forge.InvalidGitLabProject, repository.DisplayPath),
                (int)HttpStatusCode.BadRequest);
        }

        return id.ToString(CultureInfo.InvariantCulture);
    }
}
