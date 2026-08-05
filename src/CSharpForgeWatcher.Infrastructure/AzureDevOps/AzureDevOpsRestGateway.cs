using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Infrastructure.AzureDevOps.Dtos;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Infrastructure.AzureDevOps;

/// <summary>
/// Implémentation REST du port <see cref="ISourceControlGateway"/> (API Azure DevOps 7.1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lecture seule</b> : uniquement des <c>GET</c>. Un PAT limité à « Code (Lecture) »
/// suffit, ce qui réduit fortement l'impact d'une fuite de jeton.
/// </para>
/// <para>
/// Toutes les erreurs sont converties en <see cref="SourceControlException"/> classée
/// (transitoire, authentification, introuvable) : les couches supérieures décident quoi
/// faire sans jamais manipuler de code HTTP.
/// </para>
/// </remarks>
public sealed class AzureDevOpsRestGateway : ISourceControlGateway, IDisposable
{
    /// <summary>Nom de la forge, employé dans les messages d'erreur.</summary>
    /// <remarks>
    /// Cet adaptateur ne dérive pas de <c>RestGatewayBase</c> — l'API Azure DevOps n'est ni
    /// paginée ni nommée comme celles de GitHub et GitLab — et porte donc son nom lui-même.
    /// </remarks>
    private const string ForgeName = "Azure DevOps";

    /// <summary>Version d'API utilisée pour les points d'entrée stables.</summary>
    private const string ApiVersion = "7.1";

    /// <summary><c>connectionData</c> n'existe qu'en préversion.</summary>
    private const string PreviewApiVersion = "7.1-preview.1";

    /// <summary>Nombre maximal de PR actives lues par dépôt et par cycle.</summary>
    private const int MaxPullRequestsPerRepository = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string _organizationUrl;
    private readonly ILogger? _logger;

    /// <summary>Construit la passerelle pour une organisation et un jeton donnés.</summary>
    /// <param name="organizationUrl">Ex. <c>https://dev.azure.com/contoso</c>.</param>
    /// <param name="personalAccessToken">PAT en clair.</param>
    /// <param name="httpClient">Client à réutiliser ; <c>null</c> pour en créer un dédié.</param>
    /// <param name="logger">Journal, facultatif.</param>
    public AzureDevOpsRestGateway(
        string organizationUrl,
        string personalAccessToken,
        HttpClient? httpClient = null,
        ILogger? logger = null)
    {
        _organizationUrl = (organizationUrl ?? string.Empty).Trim().TrimEnd('/');
        _logger = logger;
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        // Authentification « Basic » avec un identifiant vide et le PAT comme mot de passe :
        // c'est le mode documenté pour les PAT Azure DevOps.
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + personalAccessToken));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Sans cet en-tête, un jeton invalide provoque une redirection vers la page de
        // connexion et une réponse HTML en 200 — beaucoup plus difficile à diagnostiquer.
        _httpClient.DefaultRequestHeaders.Remove("X-TFS-FedAuthRedirect");
        _httpClient.DefaultRequestHeaders.Add("X-TFS-FedAuthRedirect", "Suppress");
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ForgeWatcher/1.0");
    }

    /// <inheritdoc />
    public async Task<ViewerIdentity> GetViewerAsync(CancellationToken cancellationToken)
    {
        var data = await GetAsync<AdoConnectionData>(
            $"/_apis/connectionData?api-version={PreviewApiVersion}",
            cancellationToken).ConfigureAwait(false);

        var viewer = AzureDevOpsMapper.ToViewer(data);

        if (string.IsNullOrEmpty(viewer.Id))
        {
            throw new SourceControlException(
                TextRef.Of(TextKeys.Forge.NoIdentity, ForgeName),
                (int)HttpStatusCode.Unauthorized);
        }

        return viewer;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        var response = await GetAsync<AdoCollection<AdoProject>>(
            $"/_apis/projects?$top=500&stateFilter=wellFormed&api-version={ApiVersion}",
            cancellationToken).ConfigureAwait(false);

        return response.Value
            .Select(AzureDevOpsMapper.ToProject)
            .OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RepositoryRef>> GetRepositoriesAsync(
        string projectName,
        CancellationToken cancellationToken)
    {
        var response = await GetAsync<AdoCollection<AdoRepository>>(
            $"/{Escape(projectName)}/_apis/git/repositories?api-version={ApiVersion}",
            cancellationToken).ConfigureAwait(false);

        return response.Value
            .Where(repository => repository.IsDisabled != true)
            .Select(repository => AzureDevOpsMapper.ToRepository(repository, projectName))
            .OrderBy(repository => repository.RepositoryName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PullRequest>> GetActivePullRequestsAsync(
        RepositoryRef repository,
        CancellationToken cancellationToken)
    {
        var response = await GetAsync<AdoCollection<AdoPullRequest>>(
            $"/{Escape(repository.ProjectName)}/_apis/git/repositories/{Escape(repository.RepositoryId)}" +
            $"/pullrequests?searchCriteria.status=active&$top={MaxPullRequestsPerRepository}&api-version={ApiVersion}",
            cancellationToken).ConfigureAwait(false);

        return response.Value
            .Select(pullRequest => AzureDevOpsMapper.ToPullRequest(pullRequest, repository))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<PullRequest?> GetPullRequestAsync(
        RepositoryRef repository,
        int pullRequestId,
        CancellationToken cancellationToken)
    {
        // Point d'entrée au niveau organisation : fonctionne quel que soit l'état de la PR.
        var dto = await GetOrNullAsync<AdoPullRequest>(
            $"/_apis/git/pullrequests/{pullRequestId}?api-version={ApiVersion}",
            cancellationToken).ConfigureAwait(false);

        return dto is null ? null : AzureDevOpsMapper.ToPullRequest(dto, repository);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommentThread>> GetThreadsAsync(
        RepositoryRef repository,
        int pullRequestId,
        CancellationToken cancellationToken)
    {
        var response = await GetAsync<AdoCollection<AdoThread>>(
            $"/{Escape(repository.ProjectName)}/_apis/git/repositories/{Escape(repository.RepositoryId)}" +
            $"/pullRequests/{pullRequestId}/threads?api-version={ApiVersion}",
            cancellationToken).ConfigureAwait(false);

        return response.Value
            .Select(AzureDevOpsMapper.ToThread)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PipelineDefinitionRef>> GetPipelineDefinitionsAsync(
        string projectName,
        CancellationToken cancellationToken)
    {
        var response = await GetAsync<AdoCollection<AdoBuildDefinition>>(
            $"/{Escape(projectName)}/_apis/build/definitions?$top=1000&api-version={ApiVersion}",
            cancellationToken).ConfigureAwait(false);

        return response.Value
            .Where(AzureDevOpsMapper.IsPipelineEnabled)
            .Select(definition => AzureDevOpsMapper.ToPipelineDefinition(definition, projectName))
            .OrderBy(definition => definition.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
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

        // Toutes les définitions dans la même requête, les plus récentes d'abord :
        // une requête par projet et par cycle (SPEC-PIPE-004).
        var definitions = string.Join(',', definitionIds.Distinct());
        var top = Math.Clamp(maxRuns, 1, 500);

        var response = await GetAsync<AdoCollection<AdoBuild>>(
            $"/{Escape(projectName)}/_apis/build/builds?definitions={definitions}" +
            $"&$top={top}&queryOrder=queueTimeDescending&api-version={ApiVersion}",
            cancellationToken).ConfigureAwait(false);

        return response.Value
            .Select(build => AzureDevOpsMapper.ToPipelineRun(build, projectName))
            .ToArray();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static string Escape(string value) => Uri.EscapeDataString(value ?? string.Empty);

    /// <summary>Exécute un GET et désérialise la réponse.</summary>
    private async Task<TResponse> GetAsync<TResponse>(string relativeUrl, CancellationToken cancellationToken)
        where TResponse : class
        => await GetOrNullAsync<TResponse>(relativeUrl, cancellationToken).ConfigureAwait(false)
           ?? throw new SourceControlException(
               TextRef.Of(TextKeys.Forge.EmptyResponse, ForgeName),
               (int)HttpStatusCode.NotFound);

    /// <summary>
    /// Exécute un GET ; retourne <c>null</c> sur un 404 (ressource absente), lève sinon.
    /// </summary>
    private async Task<TResponse?> GetOrNullAsync<TResponse>(string relativeUrl, CancellationToken cancellationToken)
        where TResponse : class
    {
        var url = _organizationUrl + relativeUrl;
        HttpResponseMessage response;

        try
        {
            response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Délai dépassé : transitoire, donc réessayable par le décorateur de résilience.
            throw new SourceControlException(TextRef.Of(TextKeys.Forge.Timeout, ForgeName), null, exception);
        }
        catch (HttpRequestException exception)
        {
            throw new SourceControlException(
                TextRef.Of(TextKeys.Forge.Unreachable, ForgeName, exception.Message),
                exception.StatusCode is { } status ? (int)status : null,
                exception);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw Describe(response.StatusCode, body, url);
            }

            // Un jeton refusé peut produire un 200 contenant la page de connexion.
            if (body.TrimStart().StartsWith('<'))
            {
                throw new SourceControlException(
                    TextRef.Of(TextKeys.Forge.HtmlResponse),
                    (int)HttpStatusCode.Unauthorized);
            }

            try
            {
                return JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
            }
            catch (JsonException exception)
            {
                _logger?.LogError(exception, "Réponse illisible pour {Url}.", url);
                throw new SourceControlException(
                    TextRef.Of(TextKeys.Forge.UnreadableResponse, ForgeName),
                    (int)response.StatusCode,
                    exception);
            }
        }
    }

    /// <summary>Transforme une réponse en échec en message compréhensible.</summary>
    private SourceControlException Describe(HttpStatusCode statusCode, string body, string url)
    {
        var code = (int)statusCode;
        _logger?.LogWarning("Appel {Url} en échec ({Code}).", url, code);

        var text = statusCode switch
        {
            HttpStatusCode.Unauthorized => TextRef.Of(TextKeys.Forge.Unauthorized, ForgeName),
            HttpStatusCode.Forbidden => TextRef.Of(TextKeys.Forge.ForbiddenAzureDevOps),
            HttpStatusCode.TooManyRequests => TextRef.Of(TextKeys.Forge.RateLimited, ForgeName),
            _ when code >= 500 => TextRef.Of(TextKeys.Forge.ServerError, ForgeName, code),
            _ => TextRef.Of(TextKeys.Forge.CallFailed, ForgeName, code, Summarize(body)),
        };

        return new SourceControlException(text, code);
    }

    /// <summary>Résumé court du corps de réponse, pour les diagnostics.</summary>
    private static string Summarize(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var flattened = body.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return flattened.Length <= 200 ? flattened : flattened[..200] + "…";
    }
}
