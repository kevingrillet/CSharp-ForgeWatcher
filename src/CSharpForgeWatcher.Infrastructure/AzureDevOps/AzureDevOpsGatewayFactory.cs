using System.Collections.Concurrent;
using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Resilience;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Infrastructure.AzureDevOps;

/// <summary>
/// Fabrique les passerelles Azure DevOps et les met en cache par connexion
/// (patron Factory, complété du Decorator de résilience).
/// </summary>
/// <remarks>
/// <para>
/// Une passerelle — donc un <see cref="HttpClient"/> — est conservée par couple
/// (organisation, jeton). Cela évite l'épuisement des sockets d'un <c>HttpClient</c>
/// recréé à chaque cycle, tout en permettant de changer d'organisation ou de jeton à
/// chaud : la nouvelle connexion produit simplement une nouvelle entrée.
/// </para>
/// <para>
/// Chaque passerelle REST est enveloppée dans
/// <see cref="ResilientSourceControlGateway"/> : l'appelant obtient donc gratuitement le
/// réessai des erreurs transitoires (SPEC-POLL-005).
/// </para>
/// </remarks>
public sealed class AzureDevOpsGatewayFactory : ISourceControlGatewayFactory, IDisposable
{
    private readonly ConcurrentDictionary<string, ISourceControlGateway> _gateways = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<AzureDevOpsRestGateway> _disposables = [];
    private readonly IDelayScheduler _delayScheduler;
    private readonly ILoggerFactory? _loggerFactory;

    /// <summary>Construit la fabrique.</summary>
    /// <param name="delayScheduler">Mécanisme d'attente utilisé entre deux tentatives.</param>
    /// <param name="loggerFactory">Fabrique de journaux, facultative.</param>
    public AzureDevOpsGatewayFactory(IDelayScheduler delayScheduler, ILoggerFactory? loggerFactory = null)
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
            var logger = _loggerFactory?.CreateLogger<AzureDevOpsRestGateway>();
            var rest = new AzureDevOpsRestGateway(
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
