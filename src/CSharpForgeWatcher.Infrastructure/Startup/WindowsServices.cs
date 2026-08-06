using System.Diagnostics;
using CSharpForgeWatcher.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace CSharpForgeWatcher.Infrastructure.Startup;

/// <summary>
/// Démarrage automatique via la clé <c>Run</c> de l'utilisateur courant (SPEC-CFG-006).
/// </summary>
/// <remarks>
/// <c>HKCU</c> et non <c>HKLM</c> : aucun droit administrateur nécessaire, et le réglage
/// suit l'utilisateur. L'état affiché est lu dans le registre, jamais déduit de la
/// configuration — les deux pouvant diverger si l'entrée est supprimée par ailleurs.
/// </remarks>
public sealed class RegistryAutoStartService : IAutoStartService
{
    /// <summary>Clé de démarrage automatique de l'utilisateur courant.</summary>
    /// <remarks>Partagé avec <see cref="LegacyIdentityMigration"/>, qui y reprend l'ancien nom.</remarks>
    internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Nom de la valeur inscrite dans la clé <c>Run</c>.</summary>
    internal const string ValueName = "ForgeWatcher";

    private readonly ILogger<RegistryAutoStartService>? _logger;

    /// <summary>Construit le service.</summary>
    public RegistryAutoStartService(ILogger<RegistryAutoStartService>? logger = null) => _logger = logger;

    /// <inheritdoc />
    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Lecture du démarrage automatique impossible.");
            return false;
        }
    }

    /// <inheritdoc />
    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return false;
            }

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(executablePath))
            {
                _logger?.LogWarning("Chemin de l'exécutable introuvable : démarrage automatique non configuré.");
                return false;
            }

            // Les guillemets sont indispensables : le chemin contient souvent des espaces.
            key.SetValue(ValueName, $"\"{executablePath}\"");
            return true;
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Écriture du démarrage automatique impossible.");
            return false;
        }
    }
}

/// <summary>Ouvre une URL dans le navigateur par défaut (SPEC-NOTIF-001).</summary>
public sealed class DefaultBrowserLauncher : IBrowserLauncher
{
    private readonly ILogger<DefaultBrowserLauncher>? _logger;

    /// <summary>Construit le lanceur.</summary>
    public DefaultBrowserLauncher(ILogger<DefaultBrowserLauncher>? logger = null) => _logger = logger;

    /// <inheritdoc />
    public void Open(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        // Garde-fou : on n'ouvre que du http(s), jamais un chemin local ou un autre schéma.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _logger?.LogWarning("URL refusée car non http(s) : {Url}", url);
            return;
        }

        try
        {
            // UseShellExecute : c'est le shell Windows qui choisit le navigateur par défaut.
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Ouverture du navigateur impossible pour {Url}.", uri.AbsoluteUri);
        }
    }
}
