using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.DependencyInjection;
using CSharpForgeWatcher.Infrastructure.DependencyInjection;
using CSharpForgeWatcher.Infrastructure.Logging;
using CSharpForgeWatcher.Ui.Localization;
using CSharpForgeWatcher.Ui.Notifications;
using CSharpForgeWatcher.Ui.Theming;
using CSharpForgeWatcher.Ui.Tray;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Ui.Composition;

/// <summary>
/// Racine de composition : le seul endroit de la solution qui connaisse à la fois les
/// ports et leurs implémentations.
/// </summary>
/// <remarks>
/// Trois lignes suffisent à décrire l'assemblage : la couche application déclare ses cas
/// d'usage, l'infrastructure ses adaptateurs, l'UI ses éléments de présentation. Pour
/// substituer une brique (autre forge, autre canal de notification, autre stockage), on
/// modifie une seule ligne ici ou dans l'extension correspondante.
/// </remarks>
public static class ServiceRegistration
{
    /// <summary>Construit le conteneur de l'application.</summary>
    /// <param name="minimumLogLevel">Niveau minimal écrit dans le journal fichier.</param>
    public static ServiceProvider Build(LogLevel minimumLogLevel = LogLevel.Information)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(minimumLogLevel);
            builder.AddProvider(new FileLoggerProvider(minimumLevel: minimumLogLevel));
        });

        services.AddForgeWatcherInfrastructure();
        services.AddForgeWatcherApplication();

        // Présentation : la coquille à liaison tardive est enregistrée pour le port, puis
        // reliée au presenter réel par TrayApplicationContext (l'icône doit exister avant).
        services.AddSingleton<DeferredNotificationPresenter>();
        services.AddSingleton<INotificationPresenter>(provider =>
            provider.GetRequiredService<DeferredNotificationPresenter>());

        services.AddSingleton<TextService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<TrayApplicationContext>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
