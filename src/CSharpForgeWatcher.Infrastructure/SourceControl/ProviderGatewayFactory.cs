using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Infrastructure.AzureDevOps;
using CSharpForgeWatcher.Infrastructure.GitHub;
using CSharpForgeWatcher.Infrastructure.GitLab;

namespace CSharpForgeWatcher.Infrastructure.SourceControl;

/// <summary>
/// Choisit l'adaptateur correspondant au fournisseur de la connexion (SPEC-FORGE-002).
/// </summary>
/// <remarks>
/// <para>
/// C'est l'un des deux seuls points de bascule de la solution — l'autre est le générateur de
/// liens. Tout le reste du code ne connaît que <see cref="ISourceControlGateway"/>, et c'est
/// ce qui permet d'ajouter une forge sans toucher au métier ni à l'interface.
/// </para>
/// <para>
/// Chaque fabrique déléguée garde son propre cache : passer d'une forge à l'autre, puis
/// revenir, ne reconstruit pas de client HTTP.
/// </para>
/// </remarks>
public sealed class ProviderGatewayFactory : ISourceControlGatewayFactory, IDisposable
{
    private readonly AzureDevOpsGatewayFactory _azureDevOps;
    private readonly GitHubGatewayFactory _gitHub;
    private readonly GitLabGatewayFactory _gitLab;

    /// <summary>Construit l'aiguilleur.</summary>
    public ProviderGatewayFactory(
        AzureDevOpsGatewayFactory azureDevOps,
        GitHubGatewayFactory gitHub,
        GitLabGatewayFactory gitLab)
    {
        _azureDevOps = azureDevOps ?? throw new ArgumentNullException(nameof(azureDevOps));
        _gitHub = gitHub ?? throw new ArgumentNullException(nameof(gitHub));
        _gitLab = gitLab ?? throw new ArgumentNullException(nameof(gitLab));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Aiguillage <b>exhaustif</b> : une forge non implémentée lève un message explicite
    /// plutôt que de retomber en silence sur Azure DevOps, ce qui produirait des appels
    /// incompréhensibles vers le mauvais serveur. La validation de configuration refuse déjà
    /// ces fournisseurs en amont (SPEC-FORGE-002).
    /// </remarks>
    public ISourceControlGateway Create(SourceControlConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return connection.Provider switch
        {
            SourceControlProvider.AzureDevOps => _azureDevOps.Create(connection),
            SourceControlProvider.GitHub => _gitHub.Create(connection),
            SourceControlProvider.GitLab => _gitLab.Create(connection),
            _ => throw new NotSupportedException(
                $"Aucun adaptateur pour le fournisseur {connection.Provider} : "
                + "cette forge n'est pas encore implémentée."),
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _azureDevOps.Dispose();
        _gitHub.Dispose();
        _gitLab.Dispose();
    }
}
