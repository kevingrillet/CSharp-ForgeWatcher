using System.Collections.Concurrent;
using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Resilience;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Infrastructure.GitHub;

/// <summary>
/// Fabrique les passerelles GitHub et les met en cache par connexion
/// (patron Factory, complété du Decorator de résilience).
/// </summary>
/// <remarks>
/// Même construction que la fabrique Azure DevOps, et pour les mêmes raisons : un
/// <see cref="HttpClient"/> par couple (serveur, jeton) pour éviter l'épuisement des
/// sockets, et une enveloppe <see cref="ResilientSourceControlGateway"/> qui apporte le
/// réessai des erreurs transitoires (SPEC-POLL-005). La passerelle GitHub y gagne aussi une
/// identité mémorisée, réutilisée d'un cycle à l'autre.
/// </remarks>
public sealed class GitHubGatewayFactory : ISourceControlGatewayFactory, IDisposable
{
    private readonly ConcurrentDictionary<string, ISourceControlGateway> _gateways = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<GitHubRestGateway> _disposables = [];
    private readonly IDelayScheduler _delayScheduler;
    private readonly ILoggerFactory? _loggerFactory;

    /// <summary>Construit la fabrique.</summary>
    /// <param name="delayScheduler">Mécanisme d'attente utilisé entre deux tentatives.</param>
    /// <param name="loggerFactory">Fabrique de journaux, facultative.</param>
    public GitHubGatewayFactory(IDelayScheduler delayScheduler, ILoggerFactory? loggerFactory = null)
    {
        _delayScheduler = delayScheduler ?? throw new ArgumentNullException(nameof(delayScheduler));
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public ISourceControlGateway Create(SourceControlConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return _gateways.GetOrAdd(connection.CacheKey, _ =>
        {
            var logger = _loggerFactory?.CreateLogger<GitHubRestGateway>();
            var rest = new GitHubRestGateway(
                connection.OrganizationUrl,
                connection.PersonalAccessToken,
                httpClient: null,
                logger: logger);

            _disposables.Add(rest);

            return new ResilientSourceControlGateway(
                rest,
                _delayScheduler,
                logger: _loggerFactory?.CreateLogger<ResilientSourceControlGateway>());
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var gateway in _disposables)
        {
            gateway.Dispose();
        }

        _gateways.Clear();
    }
}
