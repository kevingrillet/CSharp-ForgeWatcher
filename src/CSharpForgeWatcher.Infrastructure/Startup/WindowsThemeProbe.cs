using CSharpForgeWatcher.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace CSharpForgeWatcher.Infrastructure.Startup;

/// <summary>
/// Lit l'apparence choisie dans Windows (SPEC-UI-THEME-002).
/// </summary>
/// <remarks>
/// Windows expose ce réglage dans
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize</c> :
/// <c>AppsUseLightTheme</c> vaut 0 en sombre, 1 en clair. La valeur est absente sur les
/// versions anciennes — auquel cas on répond « clair », le défaut historique.
/// </remarks>
public sealed class WindowsThemeProbe : ISystemThemeProbe
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    private readonly ILogger<WindowsThemeProbe>? _logger;

    /// <summary>Construit la sonde.</summary>
    public WindowsThemeProbe(ILogger<WindowsThemeProbe>? logger = null) => _logger = logger;

    /// <inheritdoc />
    public bool PrefersDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath, writable: false);
            return key?.GetValue(AppsUseLightThemeValue) is int useLight && useLight == 0;
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Apparence de Windows illisible : thème clair supposé.");
            return false;
        }
    }
}
