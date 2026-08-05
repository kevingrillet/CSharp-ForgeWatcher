using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Theming;

/// <summary>Thème réellement appliqué à l'interface.</summary>
public enum EffectiveTheme
{
    /// <summary>Fond clair.</summary>
    Light = 0,

    /// <summary>Fond sombre.</summary>
    Dark = 1,
}

/// <summary>
/// Traduit une préférence d'apparence en thème effectif (SPEC-UI-THEME-002).
/// </summary>
/// <remarks>
/// Fonction pure, isolée dans la couche application : elle est testable sans interface
/// graphique et sans lire le registre. La couche UI se contente de lui fournir l'apparence
/// courante de Windows, obtenue via <see cref="Abstractions.ISystemThemeProbe"/>.
/// </remarks>
public static class ThemeResolver
{
    /// <summary>
    /// Résout le thème à appliquer.
    /// </summary>
    /// <param name="preference">Choix de l'utilisateur.</param>
    /// <param name="systemPrefersDark">
    /// Vrai si Windows est réglé en sombre pour les applications. En cas de doute
    /// (information indisponible), passer <c>false</c> : le mode « système » retombe alors
    /// sur clair, défaut historique de Windows.
    /// </param>
    public static EffectiveTheme Resolve(ThemePreference preference, bool systemPrefersDark) => preference switch
    {
        ThemePreference.Light => EffectiveTheme.Light,
        ThemePreference.Dark => EffectiveTheme.Dark,
        _ => systemPrefersDark ? EffectiveTheme.Dark : EffectiveTheme.Light,
    };

    /// <summary>Clé de libellé de la préférence, pour la fenêtre de configuration.</summary>
    /// <remarks>
    /// Une clé, et non une formulation : la couche application ne choisit pas la langue
    /// (SPEC-UI-LANG-002).
    /// </remarks>
    public static string ToLabelKey(this ThemePreference preference) => preference switch
    {
        ThemePreference.Light => TextKeys.Preference.ThemeLight,
        ThemePreference.Dark => TextKeys.Preference.ThemeDark,
        _ => TextKeys.Preference.ThemeSystem,
    };

    /// <summary>Les trois positions du réglage, dans l'ordre d'affichage.</summary>
    public static IReadOnlyList<ThemePreference> All { get; } =
        [ThemePreference.System, ThemePreference.Light, ThemePreference.Dark];
}
