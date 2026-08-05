using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Infrastructure.AzureDevOps;
using CSharpForgeWatcher.Infrastructure.GitHub;
using CSharpForgeWatcher.Infrastructure.GitLab;
using CSharpForgeWatcher.Infrastructure.Persistence;
using CSharpForgeWatcher.Infrastructure.Security;
using CSharpForgeWatcher.Infrastructure.SourceControl;
using CSharpForgeWatcher.Infrastructure.Startup;
using CSharpForgeWatcher.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Infrastructure.DependencyInjection;

/// <summary>
/// Enregistrement des adaptateurs concrets : c'est ici que les ports rencontrent Windows,
/// le disque et le réseau.
/// </summary>
/// <remarks>
/// Chaque ligne associe **un port de la couche application** à **une implémentation**.
/// Pour changer de technologie (autre stockage, autre forge, autre chiffrement), il suffit
/// de remplacer la ligne correspondante — rien d'autre dans la solution ne bouge.
/// </remarks>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Ajoute les adaptateurs Windows / forges / système de fichiers.</summary>
    public static IServiceCollection AddForgeWatcherInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        AppPaths.EnsureDataDirectory();

        // Système
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IDelayScheduler, SystemDelayScheduler>();
        services.AddSingleton<IBrowserLauncher, DefaultBrowserLauncher>();
        services.AddSingleton<IAutoStartService, RegistryAutoStartService>();
        services.AddSingleton<ISystemThemeProbe, WindowsThemeProbe>();

        // Secret (ADR-0002)
        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();

        // Persistance (SPEC-CFG-005)
        services.AddSingleton<IConfigurationStore>(provider => new JsonConfigurationStore(
            path: null,
            logger: provider.GetService<ILogger<JsonConfigurationStore>>()));

        services.AddSingleton<IMonitorStateStore>(provider => new JsonMonitorStateStore(
            path: null,
            logger: provider.GetService<ILogger<JsonMonitorStateStore>>()));

        // Forges : une fabrique par adaptateur — chacune mutualise ses clients HTTP et
        // ajoute la résilience —, et un aiguilleur qui choisit selon le réglage Provider
        // (SPEC-FORGE-002).
        services.AddSingleton<AzureDevOpsGatewayFactory>();
        services.AddSingleton<GitHubGatewayFactory>();
        services.AddSingleton<GitLabGatewayFactory>();
        services.AddSingleton<ProviderGatewayFactory>();
        services.AddSingleton<ISourceControlGatewayFactory>(provider =>
            provider.GetRequiredService<ProviderGatewayFactory>());

        return services;
    }
}
