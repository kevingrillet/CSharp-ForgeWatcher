using System.Globalization;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Text;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Ui.Localization;

/// <summary>
/// Donne à l'interface la formulation des messages dans la langue choisie
/// (SPEC-UI-LANG-001).
/// </summary>
/// <remarks>
/// <para>
/// Pendant de <c>ThemeService</c> pour la langue : le réglage vit dans la configuration, la
/// résolution est pure (<see cref="LanguageResolver"/>), et ce service fait le lien avec les
/// fenêtres. C'est le <b>seul</b> point de la couche Ui qui transforme une clé en phrase.
/// </para>
/// <para>
/// <b>Ce qui change tout de suite, et ce qui attend.</b> Le menu de la zone de notification
/// est reconstruit à chaque ouverture : il suit la langue immédiatement. Les fenêtres, elles,
/// composent leurs libellés à la construction — WinForms ne relit pas ses textes — et
/// prennent donc la nouvelle langue à leur prochaine ouverture. La fenêtre de configuration
/// le dit à l'utilisateur.
/// </para>
/// <para>
/// Le format des nombres et des dates n'est <b>pas</b> touché : il reste celui des paramètres
/// régionaux du poste. Quelqu'un qui lit l'interface en anglais depuis un poste français
/// attend toujours ses dates en jour/mois.
/// </para>
/// </remarks>
public sealed class TextService
{
    private readonly ConfigurationService _configuration;

    /// <summary>Construit le service et aligne la culture d'interface du processus.</summary>
    public TextService(ConfigurationService configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _configuration.Changed += (_, _) => ApplyUiCulture();

        ApplyUiCulture();
    }

    /// <summary>Langue effectivement employée.</summary>
    public EffectiveLanguage Current
        => LanguageResolver.Resolve(_configuration.Current.Language, CultureInfo.InstalledUICulture);

    /// <summary>Catalogue de la langue courante.</summary>
    public TextCatalogue Catalogue => TextCatalogue.For(Current);

    /// <summary>Formulation d'une clé.</summary>
    public string this[string key] => Catalogue.Get(key);

    /// <summary>Formulation d'une clé et de ses arguments.</summary>
    public string Format(string key, params object?[] arguments)
        => Catalogue.Resolve(TextRef.Of(key, arguments));

    /// <summary>Formulation d'un message produit par les couches basses.</summary>
    public string Of(TextRef? text) => Catalogue.Resolve(text);

    /// <summary>
    /// Formulation d'une liste de messages, une par ligne.
    /// </summary>
    /// <remarks>
    /// Employé pour les erreurs de validation : la couche application en produit une liste, la
    /// fenêtre les présente ensemble.
    /// </remarks>
    public string Join(IEnumerable<TextRef> texts)
        => string.Join(Environment.NewLine, texts.Select(Of));

    /// <summary>Formulation d'une préférence de langue, pour la liste déroulante.</summary>
    public string LabelOf(LanguagePreference preference) => this[preference.ToLabelKey()];

    /// <summary>
    /// Aligne la culture d'interface des threads sur la langue choisie.
    /// </summary>
    /// <remarks>
    /// Concerne ce que .NET et Windows formulent eux-mêmes — messages d'exception, boutons des
    /// boîtes de dialogue système. Sans cela, une interface anglaise afficherait « Oui / Non »
    /// sur un poste français.
    /// </remarks>
    private void ApplyUiCulture()
    {
        var culture = LanguageResolver.ToCulture(Current);

        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}
