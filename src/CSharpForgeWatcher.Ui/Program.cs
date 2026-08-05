using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Text;
using CSharpForgeWatcher.Application.Theming;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Infrastructure.Persistence;
using CSharpForgeWatcher.Infrastructure.Startup;
using CSharpForgeWatcher.Ui.Composition;
using CSharpForgeWatcher.Ui.Localization;
using CSharpForgeWatcher.Ui.Tray;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Alias indispensable : dans l'espace de noms CSharpForgeWatcher.Ui, le nom « Application »
// désigne l'espace de noms CSharpForgeWatcher.Application (couche applicative), qui masque
// System.Windows.Forms.Application.
using WinFormsApplication = System.Windows.Forms.Application;

namespace CSharpForgeWatcher.Ui;

/// <summary>Point d'entrée de l'application.</summary>
internal static class Program
{
    /// <summary>Nom du mutex garantissant une instance unique par session Windows.</summary>
    private const string SingleInstanceMutexName = @"Local\ForgeWatcher.SingleInstance";

    [STAThread]
    private static void Main()
    {
        // Instance unique : deux icônes de notification surveillant les mêmes dépôts
        // dupliqueraient les notifications et se battraient sur le fichier d'état.
        // Remarque : si l'application est relancée par le clic sur un toast alors qu'elle
        // tourne déjà, c'est l'instance en cours qui reçoit l'activation (serveur COM).
        using var singleInstance = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);

        if (!isFirstInstance)
        {
            return;
        }

        WinFormsApplication.EnableVisualStyles();
        WinFormsApplication.SetCompatibleTextRenderingDefault(false);
        WinFormsApplication.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        WinFormsApplication.SetDefaultFont(new Font("Segoe UI", 9F));

        // Avant le conteneur : le journal et la configuration créent le dossier de données,
        // ce qui ferait renoncer la reprise en lui laissant croire à une installation neuve.
        var migration = LegacyIdentityMigration.Run();

        using var services = ServiceRegistration.Build();
        var logger = services.GetService<ILogger<TrayApplicationContext>>();

        // Avant toute fenêtre : sa construction fixe la culture d'interface du processus.
        var text = services.GetService<TextService>();

        ReportMigration(migration, logger);

        // Filet de sécurité : une exception d'interface non gérée est journalisée et
        // signalée, mais ne fait pas disparaître l'icône sans explication.
        WinFormsApplication.ThreadException += (_, args) =>
        {
            logger?.LogError(args.Exception, "Exception non gérée sur le thread d'interface.");
            ShowFatalError(args.Exception, text);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            logger?.LogError(args.ExceptionObject as Exception, "Exception non gérée hors interface.");

        WinFormsApplication.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        // Avant toute fenêtre : le mode couleur ne s'applique qu'aux fenêtres créées ensuite.
        ApplyColorMode(services, logger);

        try
        {
            logger?.LogInformation("Démarrage de Forge Watcher.");
            WinFormsApplication.Run(services.GetRequiredService<TrayApplicationContext>());
            logger?.LogInformation("Arrêt de Forge Watcher.");
        }
        catch (Exception exception)
        {
            logger?.LogCritical(exception, "Démarrage impossible.");
            ShowFatalError(exception, text);
        }
    }

    /// <summary>
    /// Aligne le mode couleur de l'application sur le thème choisi, avant toute fenêtre.
    /// </summary>
    /// <remarks>
    /// Ce réglage concerne ce que WinForms peint lui-même et que l'application ne contrôle
    /// pas : barre de titre, barres de défilement, boîtes de dialogue système. Le reste est
    /// peint par <c>ThemeService</c>, qui peut changer à chaud ; celui-ci est appliqué une
    /// fois au démarrage — d'où la mention « redémarrage » dans la documentation.
    /// <para>
    /// L'API est marquée expérimentale dans .NET 9 (WFO5001) : l'appel est isolé ici et
    /// protégé, pour que son éventuelle évolution n'ait aucun impact ailleurs.
    /// </para>
    /// </remarks>
    private static void ApplyColorMode(IServiceProvider services, ILogger? logger)
    {
        try
        {
            var configuration = services.GetRequiredService<ConfigurationService>();
            var systemThemeProbe = services.GetRequiredService<ISystemThemeProbe>();
            var theme = ThemeResolver.Resolve(configuration.Current.Theme, systemThemeProbe.PrefersDarkTheme());

#pragma warning disable WFO5001 // Mode couleur de l'application : API expérimentale .NET 9.
            WinFormsApplication.SetColorMode(theme == EffectiveTheme.Dark
                ? SystemColorMode.Dark
                : SystemColorMode.Classic);
#pragma warning restore WFO5001
        }
        catch (Exception exception)
        {
            // Aucune conséquence fonctionnelle : seules les zones peintes par Windows
            // resteront claires.
            logger?.LogWarning(exception, "Mode couleur de l'application non appliqué.");
        }
    }

    /// <summary>
    /// Journalise la reprise de l'ancienne identité, une fois le journal disponible.
    /// </summary>
    /// <remarks>
    /// Un échec de reprise n'est qu'un avertissement : l'application démarre, au pire sur une
    /// configuration vierge que l'utilisateur ressaisira.
    /// </remarks>
    private static void ReportMigration(LegacyMigrationReport report, ILogger? logger)
    {
        foreach (var applied in report.Applied)
        {
            logger?.LogInformation("Reprise de l'ancien nom : {Etape}", applied);
        }

        foreach (var failure in report.Failures)
        {
            logger?.LogWarning("Reprise de l'ancien nom : {Etape}", failure);
        }
    }

    /// <summary>
    /// Signale une erreur qui empêche l'application de fonctionner.
    /// </summary>
    /// <remarks>
    /// Le service de textes peut manquer — c'est justement le cas où le conteneur n'a pas pu
    /// être construit : on retombe alors sur le catalogue français, qui est celui embarqué
    /// dans l'assembly principal et donc toujours disponible.
    /// </remarks>
    private static void ShowFatalError(Exception exception, TextService? text)
    {
        var catalogue = text?.Catalogue ?? TextCatalogue.For(EffectiveLanguage.French);

        MessageBox.Show(
            catalogue.Resolve(TextRef.Of(TextKeys.Screen.FatalError, exception.Message, AppPaths.LogFile)),
            catalogue.Get(TextKeys.AppName),
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
