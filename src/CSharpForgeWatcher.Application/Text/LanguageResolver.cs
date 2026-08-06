using System.Globalization;
using CSharpForgeWatcher.Application.Configuration;

namespace CSharpForgeWatcher.Application.Text;

/// <summary>
/// Résout la langue effective à partir du réglage et de la langue de Windows
/// (SPEC-UI-LANG-001).
/// </summary>
/// <remarks>
/// Pur, comme <see cref="Theming.ThemeResolver"/> qu'il imite volontairement : les deux
/// réglages ont la même forme à trois positions, dont une « comme Windows ». Aucune lecture
/// du système ici — la culture est fournie par l'appelant, ce qui rend la règle testable sans
/// changer les paramètres régionaux de la machine de test.
/// </remarks>
public static class LanguageResolver
{
    /// <summary>Les trois positions du réglage, dans l'ordre d'affichage.</summary>
    public static IReadOnlyList<LanguagePreference> All { get; } =
    [
        LanguagePreference.System,
        LanguagePreference.French,
        LanguagePreference.English,
    ];

    /// <summary>
    /// Langue à employer.
    /// </summary>
    /// <param name="preference">Réglage de l'utilisateur.</param>
    /// <param name="systemCulture">
    /// Culture d'interface de Windows. Toute culture dont la langue n'est pas le français
    /// donne l'anglais : c'est le repli le plus utile pour un poste étranger, et le seul
    /// choix honnête tant que l'application ne connaît que deux langues.
    /// </param>
    public static EffectiveLanguage Resolve(LanguagePreference preference, CultureInfo? systemCulture)
        => preference switch
        {
            LanguagePreference.French => EffectiveLanguage.French,
            LanguagePreference.English => EffectiveLanguage.English,
            _ => IsFrench(systemCulture) ? EffectiveLanguage.French : EffectiveLanguage.English,
        };

    /// <summary>Culture .NET correspondant à une langue effective.</summary>
    /// <remarks>
    /// Employée pour la mise en forme des dates et des nombres quand l'utilisateur impose une
    /// langue : afficher un texte anglais avec des séparateurs français serait bancal.
    /// </remarks>
    public static CultureInfo ToCulture(EffectiveLanguage language)
        => CultureInfo.GetCultureInfo(language == EffectiveLanguage.English ? "en" : "fr");

    /// <summary>Clé de libellé d'une position du réglage.</summary>
    public static string ToLabelKey(this LanguagePreference preference) => preference switch
    {
        LanguagePreference.French => Domain.Text.TextKeys.Preference.LanguageFrench,
        LanguagePreference.English => Domain.Text.TextKeys.Preference.LanguageEnglish,
        _ => Domain.Text.TextKeys.Preference.LanguageSystem,
    };

    /// <summary>Vrai si la culture indiquée relève du français.</summary>
    private static bool IsFrench(CultureInfo? culture)
        => culture is not null
           && culture.TwoLetterISOLanguageName.Equals("fr", StringComparison.OrdinalIgnoreCase);
}
