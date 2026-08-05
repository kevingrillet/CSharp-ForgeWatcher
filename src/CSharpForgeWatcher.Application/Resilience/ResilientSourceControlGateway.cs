using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Application.Resilience;

/// <summary>
/// Ajoute un réessai avec attente exponentielle à n'importe quelle passerelle de forge
/// (patron Decorator, SPEC-POLL-005).
/// </summary>
/// <remarks>
/// Vit dans la couche application, et non dans l'infrastructure : la politique de
/// réessai est une règle de fonctionnement, elle ne dépend pas de HTTP — et elle sert donc
/// telle quelle à toutes les forges. La classification
/// des erreurs est portée par <see cref="SourceControlException"/>, et l'attente par le port
/// <see cref="IDelayScheduler"/> — d'où des tests instantanés.
/// </remarks>
public sealed class ResilientSourceControlGateway : ISourceControlGateway
{
    /// <summary>Nombre de tentatives par défaut (1 appel + 2 réessais).</summary>
    public const int DefaultMaxAttempts = 3;

    private readonly ISourceControlGateway _inner;
    private readonly IDelayScheduler _delayScheduler;
    private readonly ILogger? _logger;
    private readonly int _maxAttempts;
    private readonly TimeSpan _initialBackoff;

    /// <summary>Décore une passerelle existante.</summary>
    /// <param name="inner">Passerelle décorée.</param>
    /// <param name="delayScheduler">Mécanisme d'attente entre deux tentatives.</param>
    /// <param name="maxAttempts">Nombre total de tentatives (minimum 1).</param>
    /// <param name="initialBackoff">Attente avant le premier réessai (doublée ensuite).</param>
    /// <param name="logger">Journal, facultatif.</param>
    public ResilientSourceControlGateway(
        ISourceControlGateway inner,
        IDelayScheduler delayScheduler,
        int maxAttempts = DefaultMaxAttempts,
        TimeSpan? initialBackoff = null,
        ILogger? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _delayScheduler = delayScheduler ?? throw new ArgumentNullException(nameof(delayScheduler));
        _maxAttempts = Math.Max(1, maxAttempts);
        _initialBackoff = initialBackoff ?? TimeSpan.FromSeconds(2);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ViewerIdentity> GetViewerAsync(CancellationToken cancellationToken)
        => ExecuteAsync(nameof(GetViewerAsync), _inner.GetViewerAsync, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken)
        => ExecuteAsync(nameof(GetProjectsAsync), _inner.GetProjectsAsync, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<RepositoryRef>> GetRepositoriesAsync(string projectName, CancellationToken cancellationToken)
        => ExecuteAsync(
            nameof(GetRepositoriesAsync),
            token => _inner.GetRepositoriesAsync(projectName, token),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<PullRequest>> GetActivePullRequestsAsync(RepositoryRef repository, CancellationToken cancellationToken)
        => ExecuteAsync(
            nameof(GetActivePullRequestsAsync),
            token => _inner.GetActivePullRequestsAsync(repository, token),
            cancellationToken);

    /// <inheritdoc />
    public Task<PullRequest?> GetPullRequestAsync(RepositoryRef repository, int pullRequestId, CancellationToken cancellationToken)
        => ExecuteAsync(
            nameof(GetPullRequestAsync),
            token => _inner.GetPullRequestAsync(repository, pullRequestId, token),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<CommentThread>> GetThreadsAsync(RepositoryRef repository, int pullRequestId, CancellationToken cancellationToken)
        => ExecuteAsync(
            nameof(GetThreadsAsync),
            token => _inner.GetThreadsAsync(repository, pullRequestId, token),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<PipelineDefinitionRef>> GetPipelineDefinitionsAsync(
        string projectName,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            nameof(GetPipelineDefinitionsAsync),
            token => _inner.GetPipelineDefinitionsAsync(projectName, token),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<PipelineRun>> GetRecentPipelineRunsAsync(
        string projectName,
        IReadOnlyCollection<long> definitionIds,
        int maxRuns,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            nameof(GetRecentPipelineRunsAsync),
            token => _inner.GetRecentPipelineRunsAsync(projectName, definitionIds, maxRuns, token),
            cancellationToken);

    /// <summary>
    /// Exécute une opération, en réessayant tant que l'échec est classé comme transitoire
    /// et que des tentatives restent disponibles.
    /// </summary>
    private async Task<TResult> ExecuteAsync<TResult>(
        string operationName,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var backoff = _initialBackoff;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (SourceControlException exception) when (exception.IsTransient && attempt < _maxAttempts)
            {
                _logger?.LogWarning(
                    "{Operation} : échec transitoire (tentative {Attempt}/{Max}, code {StatusCode}). Nouvel essai dans {Delay}.",
                    operationName,
                    attempt,
                    _maxAttempts,
                    exception.StatusCode,
                    backoff);

                await _delayScheduler.DelayAsync(backoff, cancellationToken).ConfigureAwait(false);
                backoff = TimeSpan.FromTicks(backoff.Ticks * 2);
            }
        }
    }
}
