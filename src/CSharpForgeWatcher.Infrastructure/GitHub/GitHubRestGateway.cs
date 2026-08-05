using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Links;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Infrastructure.GitHub.Dtos;
using CSharpForgeWatcher.Infrastructure.SourceControl;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Infrastructure.GitHub;

/// <summary>
/// Implémentation REST du port <see cref="ISourceControlGateway"/> pour GitHub
/// (API 2022-11-28), github.com comme GitHub Enterprise Server.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lecture seule</b> : uniquement des <c>GET</c>. Un jeton « fine-grained » limité à
/// <i>Metadata</i>, <i>Pull requests</i> et <i>Actions</i> en lecture suffit
/// (cf. SPEC-FORGES, § Jeton et portées).
/// </para>
/// <para>
/// Les écarts de modèle entre GitHub et le domaine sont tous absorbés ici et dans
/// <see cref="GitHubMapper"/> : trois surfaces de commentaires réunies en discussions,
/// relectures traduites en votes, workflows d'un dépôt présentés comme les pipelines d'un
/// espace <c>propriétaire/dépôt</c>. Les choix correspondants sont justifiés dans ADR-0004.
/// </para>
/// </remarks>
public sealed class GitHubRestGateway : RestGatewayBase, ISourceControlGateway
{
    /// <summary>Version d'API épinglée : GitHub demande de la déclarer explicitement.</summary>
    private const string ApiVersion = "2022-11-28";

    private readonly SemaphoreSlim _viewerGate = new(1, 1);

    /// <summary>Identité résolue une fois, puis réutilisée (le <c>login</c> ne change pas).</summary>
    private ViewerIdentity? _viewer;

    /// <summary>Construit la passerelle pour un serveur et un jeton donnés.</summary>
    /// <param name="serverUrl">Racine du serveur, ex. <c>https://github.com</c>.</param>
    /// <param name="personalAccessToken">Jeton en clair.</param>
    /// <param name="httpClient">Client à réutiliser ; <c>null</c> pour en créer un dédié.</param>
    /// <param name="logger">Journal, facultatif.</param>
    public GitHubRestGateway(
        string serverUrl,
        string personalAccessToken,
        HttpClient? httpClient = null,
        ILogger? logger = null)
        : base(ToApiUrl(serverUrl), "GitHub", httpClient, logger)
    {
        // « Bearer » convient aux deux familles de jetons personnels, classique comme
        // « fine-grained ».
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", personalAccessToken);

        HttpClient.DefaultRequestHeaders.Accept.Clear();
        HttpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        HttpClient.DefaultRequestHeaders.Remove("X-GitHub-Api-Version");
        HttpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", ApiVersion);

        // GitHub refuse les requêtes sans agent utilisateur.
        HttpClient.DefaultRequestHeaders.UserAgent.Clear();
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ForgeWatcher/1.0");
    }

    /// <summary>
    /// Déduit l'adresse de l'API de celle du serveur (SPEC-FORGE-002).
    /// </summary>
    /// <remarks>
    /// github.com sert son API sur un hôte dédié ; une instance GitHub Enterprise Server la
    /// sert sous <c>/api/v3</c> du même hôte. C'est la seule différence, et elle évite un
    /// second champ à saisir.
    /// </remarks>
    public static string ToApiUrl(string? serverUrl)
    {
        var server = ServerUrl.Origin(serverUrl);

        if (!Uri.TryCreate(server, UriKind.Absolute, out var uri))
        {
            return server;
        }

        var isPublicGitHub = uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                             || uri.Host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase);

        return isPublicGitHub ? "https://api.github.com" : $"{server}/api/v3";
    }

    /// <inheritdoc />
    public async Task<ViewerIdentity> GetViewerAsync(CancellationToken cancellationToken)
    {
        var account = await GetAsync<GhAccount>("/user", cancellationToken).ConfigureAwait(false);
        var viewer = GitHubMapper.ToViewer(account);

        if (string.IsNullOrEmpty(viewer.Id))
        {
            throw new SourceControlException(
                TextRef.Of(TextKeys.Forge.NoIdentity, ForgeName),
                (int)HttpStatusCode.Unauthorized);
        }

        _viewer = viewer;
        return viewer;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Les « espaces » de GitHub sont le compte de l'utilisateur, puis ses organisations
    /// (SPEC-FORGE-004). Lister les organisations demande une portée supplémentaire : si
    /// elle manque, on se contente du compte personnel plutôt que d'échouer.
    /// </remarks>
    public async Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        var viewer = await EnsureViewerAsync(cancellationToken).ConfigureAwait(false);
        var owners = new List<ProjectSummary>
        {
            new(viewer.Id, viewer.Id, TextKeys.Forge.GitHubPersonalRepositories),
        };

        try
        {
            var organizations = await GetPagedAsync<GhAccount>("/user/orgs", cancellationToken)
                .ConfigureAwait(false);

            owners.AddRange(organizations
                .Where(organization => !string.IsNullOrEmpty(organization.Login))
                .Select(GitHubMapper.ToOwner)
                .OrderBy(owner => owner.Name, StringComparer.CurrentCultureIgnoreCase));
        }
        catch (SourceControlException exception) when (exception.StatusCode == (int)HttpStatusCode.Forbidden)
        {
            Logger?.LogWarning(
                "Organisations non listées : le jeton n'a pas la portée nécessaire. "
                + "Seuls les dépôts personnels sont proposés.");
        }

        return owners;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RepositoryRef>> GetRepositoriesAsync(
        string projectName,
        CancellationToken cancellationToken)
    {
        var repositories = await ReadRepositoriesAsync(projectName, cancellationToken).ConfigureAwait(false);

        return repositories
            .Select(repository => GitHubMapper.ToRepository(repository, projectName))
            .OrderBy(repository => repository.RepositoryName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Les relectures — donc les votes — ne sont lues que sur les pull requests qui
    /// concernent l'utilisateur : elles coûtent une requête chacune, et la règle de vote ne
    /// notifie de toute façon que les PR dont il est l'auteur (SPEC-EVT-003,
    /// SPEC-FORGE-007).
    /// </remarks>
    public async Task<IReadOnlyList<PullRequest>> GetActivePullRequestsAsync(
        RepositoryRef repository,
        CancellationToken cancellationToken)
    {
        var (owner, name) = RequirePath(repository);

        var pullRequests = await GetPagedAsync<GhPullRequest>(
            $"/repos/{owner}/{name}/pulls?state=open&sort=created&direction=desc",
            cancellationToken).ConfigureAwait(false);

        var viewer = await EnsureViewerAsync(cancellationToken).ConfigureAwait(false);

        return await RunBoundedAsync(
            pullRequests,
            async (pullRequest, token) =>
            {
                var reviews = Concerns(pullRequest, viewer.Id)
                    ? await GetPagedAsync<GhReview>(
                        $"/repos/{owner}/{name}/pulls/{pullRequest.Number}/reviews",
                        token).ConfigureAwait(false)
                    : null;

                return GitHubMapper.ToPullRequest(pullRequest, repository, reviews);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Sert à connaître le sort d'une pull request disparue de la liste active : seul son
    /// état compte, les relectures ne sont donc pas lues.
    /// </remarks>
    public async Task<PullRequest?> GetPullRequestAsync(
        RepositoryRef repository,
        int pullRequestId,
        CancellationToken cancellationToken)
    {
        var (owner, name) = RequirePath(repository);

        var dto = await GetOrNullAsync<GhPullRequest>(
            $"/repos/{owner}/{name}/pulls/{pullRequestId}",
            cancellationToken).ConfigureAwait(false);

        return dto is null ? null : GitHubMapper.ToPullRequest(dto, repository, reviews: null);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Trois points d'entrée, réunis par le mappeur : messages de conversation, corps de
    /// relecture et commentaires de ligne (SPEC-FORGES, § Discussions).
    /// </remarks>
    public async Task<IReadOnlyList<CommentThread>> GetThreadsAsync(
        RepositoryRef repository,
        int pullRequestId,
        CancellationToken cancellationToken)
    {
        var (owner, name) = RequirePath(repository);

        var issueComments = await GetPagedAsync<GhIssueComment>(
            $"/repos/{owner}/{name}/issues/{pullRequestId}/comments",
            cancellationToken).ConfigureAwait(false);

        var reviews = await GetPagedAsync<GhReview>(
            $"/repos/{owner}/{name}/pulls/{pullRequestId}/reviews",
            cancellationToken).ConfigureAwait(false);

        var reviewComments = await GetPagedAsync<GhReviewComment>(
            $"/repos/{owner}/{name}/pulls/{pullRequestId}/comments",
            cancellationToken).ConfigureAwait(false);

        return GitHubMapper.ToThreads(issueComments, reviews, reviewComments);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Les workflows appartiennent à un dépôt : les lister pour un propriétaire demande de
    /// parcourir ses dépôts. Le coût n'est payé qu'à l'ouverture de l'onglet <i>Pipelines</i>,
    /// et le nombre de dépôts parcourus est journalisé (SPEC-FORGE-007). Un dépôt illisible
    /// est ignoré, pour ne pas priver l'utilisateur des autres.
    /// </remarks>
    public async Task<IReadOnlyList<PipelineDefinitionRef>> GetPipelineDefinitionsAsync(
        string projectName,
        CancellationToken cancellationToken)
    {
        var repositories = await ReadRepositoriesAsync(projectName, cancellationToken).ConfigureAwait(false);

        var perRepository = await RunBoundedAsync(
            repositories.Where(repository => !string.IsNullOrEmpty(repository.Name)).ToList(),
            async (repository, token) =>
            {
                var owner = repository.Owner?.Login ?? projectName;

                try
                {
                    var list = await GetOrNullAsync<GhWorkflowList>(
                        $"/repos/{Escape(owner)}/{Escape(repository.Name!)}/actions/workflows?per_page={PageSize}",
                        token).ConfigureAwait(false);

                    return (list?.Workflows ?? [])
                        .Where(GitHubMapper.IsWorkflowEnabled)
                        .Select(workflow => GitHubMapper.ToPipelineDefinition(workflow, owner, repository.Name!))
                        .ToArray();
                }
                catch (SourceControlException exception)
                {
                    Logger?.LogWarning(
                        "Workflows du dépôt {Repository} illisibles : {Message}",
                        repository.FullName ?? repository.Name,
                        exception.Message);

                    return Array.Empty<PipelineDefinitionRef>();
                }
            },
            cancellationToken).ConfigureAwait(false);

        var definitions = perRepository
            .SelectMany(definition => definition)
            .OrderBy(definition => definition.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        Logger?.LogInformation(
            "{Workflows} workflow(s) trouvé(s) dans {Repositories} dépôt(s) de {Owner}.",
            definitions.Length,
            repositories.Count,
            projectName);

        return definitions;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Une requête par workflow surveillé : l'API Actions ne sait pas filtrer les exécutions
    /// sur plusieurs workflows à la fois, et trier une page d'exécutions communes ferait
    /// disparaître les plus anciennes — donc risquerait de manquer un échec (ADR-0004,
    /// décision 4).
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

        var (owner, name) = SplitPipelineScope(projectName);
        var perWorkflow = Math.Clamp(maxRuns / definitionIds.Count, 2, 20);

        var perDefinition = await RunBoundedAsync(
            definitionIds.Distinct().ToList(),
            async (definitionId, token) =>
            {
                var list = await GetOrNullAsync<GhWorkflowRunList>(
                    $"/repos/{Escape(owner)}/{Escape(name)}/actions/workflows/{definitionId}/runs"
                    + $"?per_page={perWorkflow}&exclude_pull_requests=true",
                    token).ConfigureAwait(false);

                return (list?.WorkflowRuns ?? [])
                    .Select(run => GitHubMapper.ToPipelineRun(run, owner, name))
                    .ToArray();
            },
            cancellationToken).ConfigureAwait(false);

        return perDefinition.SelectMany(runs => runs).ToArray();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewerGate.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Un point mérite l'attention : GitHub signale un quota épuisé par un <b>403</b>, code
    /// que le reste de l'application interprète comme un refus d'autorisation — donc sans
    /// réessai. L'en-tête <c>x-ratelimit-remaining</c> permet de distinguer les deux cas ; le
    /// quota est alors reclassé en 429, ce qui le rend transitoire et réessayable
    /// (SPEC-POLL-005).
    /// </remarks>
    protected override SourceControlException Describe(HttpResponseMessage response, string body, string url)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden && IsRateLimited(response, body))
        {
            var wait = ResetDelay(response) is { } delay
                ? TextRef.Of(TextKeys.Forge.QuotaReset, Math.Ceiling(delay.TotalMinutes))
                : TextRef.Empty;

            return new SourceControlException(
                TextRef.Of(TextKeys.Forge.QuotaExhausted, ForgeName, wait),
                (int)HttpStatusCode.TooManyRequests);
        }

        return DescribeByStatus(response.StatusCode, body);
    }

    /// <summary>
    /// Vrai si la pull request concerne l'utilisateur : il en est l'auteur, ou sa relecture
    /// est attendue.
    /// </summary>
    private static bool Concerns(GhPullRequest pullRequest, string viewerId)
        => string.Equals(pullRequest.User?.Login, viewerId, StringComparison.OrdinalIgnoreCase)
           || (pullRequest.RequestedReviewers ?? []).Any(reviewer =>
               string.Equals(reviewer.Login, viewerId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Vrai si le refus est dû au quota et non aux autorisations.</summary>
    private static bool IsRateLimited(HttpResponseMessage response, string body)
        => IsHeaderExhausted(response, "x-ratelimit-remaining")

           // Les limites secondaires ne renseignent pas toujours l'en-tête, mais le corps de
           // la réponse les nomme explicitement.
           || body.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
           || body.Contains("secondary rate", StringComparison.OrdinalIgnoreCase);

    /// <summary>Délai annoncé avant réinitialisation du quota, s'il est exploitable.</summary>
    private static TimeSpan? ResetDelay(HttpResponseMessage response)
        => response.Headers.TryGetValues("retry-after", out var retryAfter)
           && int.TryParse(retryAfter.FirstOrDefault(), CultureInfo.InvariantCulture, out var seconds)
           && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : null;

    /// <summary>
    /// Sépare un espace de pipeline <c>propriétaire/dépôt</c>.
    /// </summary>
    /// <remarks>
    /// Une valeur sans barre oblique vient d'une configuration constituée sur une autre
    /// forge. Mieux vaut alors un message explicite — que le moniteur transformera en
    /// avertissement sur ce seul espace — qu'une URL fabriquée menant à une erreur 404
    /// répétée à chaque cycle.
    /// </remarks>
    private static (string Owner, string Repository) SplitPipelineScope(string projectName)
    {
        var value = (projectName ?? string.Empty).Trim().Trim('/');
        var separator = value.IndexOf('/', StringComparison.Ordinal);

        if (separator <= 0 || separator == value.Length - 1)
        {
            throw new SourceControlException(
                TextRef.Of(TextKeys.Forge.InvalidGitHubPath, projectName),
                (int)HttpStatusCode.BadRequest);
        }

        return (value[..separator], value[(separator + 1)..]);
    }

    /// <summary>Identité de l'utilisateur, résolue au besoin puis mémorisée.</summary>
    private async Task<ViewerIdentity> EnsureViewerAsync(CancellationToken cancellationToken)
    {
        if (_viewer is { } known)
        {
            return known;
        }

        await _viewerGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _viewer ?? await GetViewerAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _viewerGate.Release();
        }
    }

    /// <summary>
    /// Dépôts d'un propriétaire, qu'il s'agisse d'une organisation, d'un autre compte, ou du
    /// compte de l'utilisateur — seul cas où l'API expose aussi les dépôts privés.
    /// </summary>
    private async Task<IReadOnlyList<GhRepository>> ReadRepositoriesAsync(
        string owner,
        CancellationToken cancellationToken)
    {
        var viewer = await EnsureViewerAsync(cancellationToken).ConfigureAwait(false);
        var escaped = Escape(owner);

        var repositories = string.Equals(owner, viewer.Id, StringComparison.OrdinalIgnoreCase)
            ? await GetPagedAsync<GhRepository>(
                "/user/repos?affiliation=owner&sort=full_name",
                cancellationToken).ConfigureAwait(false)
            : await GetPagedOrNullAsync<GhRepository>(
                  $"/orgs/{escaped}/repos?sort=full_name",
                  cancellationToken).ConfigureAwait(false)
              ?? await GetPagedAsync<GhRepository>(
                  $"/users/{escaped}/repos?sort=full_name",
                  cancellationToken).ConfigureAwait(false);

        return repositories
            .Where(repository => !repository.Archived && !repository.Disabled)
            .ToArray();
    }

    /// <summary>Propriétaire et nom du dépôt, nécessaires à toute URL de l'API.</summary>
    private static (string Owner, string Name) RequirePath(RepositoryRef repository)
    {
        if (string.IsNullOrWhiteSpace(repository.ProjectName) || string.IsNullOrWhiteSpace(repository.RepositoryName))
        {
            throw new SourceControlException(
                TextRef.Of(TextKeys.Forge.IncompleteGitHubRepository, repository.DisplayPath),
                (int)HttpStatusCode.BadRequest);
        }

        return (Escape(repository.ProjectName), Escape(repository.RepositoryName));
    }
}
