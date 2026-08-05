using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Detection;
using CSharpForgeWatcher.Application.Detection.Pipelines;
using CSharpForgeWatcher.Application.Links;
using CSharpForgeWatcher.Application.Monitoring;
using CSharpForgeWatcher.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Application.DependencyInjection;

/// <summary>
/// Enregistrement des services de la couche application.
/// </summary>
/// <remarks>
/// La couche déclare elle-même comment elle s'assemble ; la racine de composition (l'UI)
/// n'a plus qu'à appeler cette méthode puis à fournir les adaptateurs d'infrastructure.
/// </remarks>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Ajoute les cas d'usage, la détection et la diffusion de notifications.
    /// </summary>
    /// <remarks>
    /// Prérequis (à enregistrer par l'infrastructure et l'UI) :
    /// <c>IConfigurationStore</c>, <c>IMonitorStateStore</c>, <c>ISecretProtector</c>,
    /// <c>ISourceControlGatewayFactory</c>, <c>INotificationPresenter</c>, <c>IClock</c>.
    /// </remarks>
    public static IServiceCollection AddForgeWatcherApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Source unique de vérité pour la configuration : une seule instance partagée.
        services.AddSingleton<ConfigurationService>();

        // Le générateur de liens n'est pas enregistré ici : depuis que plusieurs comptes sont
        // surveillés ensemble, il n'existe plus « un » générateur — chaque compte a sa forge
        // et son serveur, donc son propre générateur, obtenu par
        // WatchedAccount.CreateLinkBuilder() (SPEC-CFG-008, SPEC-FORGE-003).

        // Les règles sont enregistrées individuellement : en ajouter une revient à
        // ajouter une ligne ici (ou à remplacer le jeu de règles par défaut).
        foreach (var rule in PullRequestEventDetector.CreateDefaultRules())
        {
            services.AddSingleton(rule);
        }

        services.AddSingleton(provider => new PullRequestEventDetector(
            provider.GetServices<IPullRequestEventRule>(),
            provider.GetService<ILogger<PullRequestEventDetector>>()));

        // Mêmes principes pour les pipelines : une règle par comportement, composées par
        // un détecteur dédié (SPEC-PIPE-001, SPEC-PIPE-002).
        foreach (var rule in PipelineEventDetector.CreateDefaultRules())
        {
            services.AddSingleton(rule);
        }

        services.AddSingleton(provider => new PipelineEventDetector(
            provider.GetServices<IPipelineEventRule>(),
            provider.GetService<ILogger<PipelineEventDetector>>()));

        services.AddSingleton<NotificationDispatcher>();
        services.AddSingleton<PullRequestMonitor>();

        return services;
    }
}
