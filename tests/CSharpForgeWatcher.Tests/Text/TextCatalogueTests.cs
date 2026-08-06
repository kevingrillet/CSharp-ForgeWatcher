using System.Globalization;
using System.Reflection;
using CSharpForgeWatcher.Application.Text;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Tests.Text;

/// <summary>
/// Garde-fous du catalogue de textes (SPEC-UI-LANG-002).
/// </summary>
/// <remarks>
/// Deux langues qui divergent ne se voient pas à la compilation : une clé traduite d'un seul
/// côté ne se découvre qu'à l'exécution, chez l'utilisateur, et sous la forme d'une clé brute
/// affichée à l'écran. Ces tests rétablissent la vérification que le compilateur ne fait pas.
/// </remarks>
[TestFixture]
public sealed class TextCatalogueTests
{
    private static TextCatalogue French => TextCatalogue.For(EffectiveLanguage.French);

    private static TextCatalogue English => TextCatalogue.For(EffectiveLanguage.English);

    [Test]
    [Category("SPEC-UI-LANG-002")]
    public void Les_deux_langues_portent_exactement_les_memes_cles()
    {
        var francais = French.Keys.ToHashSet(StringComparer.Ordinal);
        var anglais = English.Keys.ToHashSet(StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(
                anglais.Except(francais, StringComparer.Ordinal).Order(StringComparer.Ordinal),
                Is.Empty,
                "Clés présentes en anglais et absentes du français.");

            Assert.That(
                francais.Except(anglais, StringComparer.Ordinal).Order(StringComparer.Ordinal),
                Is.Empty,
                "Clés présentes en français et absentes de l'anglais : elles s'afficheraient "
                + "en français dans une interface anglaise.");
        });
    }

    [Test]
    public void Le_catalogue_n_est_pas_vide()
    {
        // Sans cette garde, un fichier de ressources non embarqué rendrait les deux tests de
        // parité verts en ne comparant rien.
        Assert.That(French.Keys, Has.Count.GreaterThan(100));
    }

    [Test]
    [Category("SPEC-UI-LANG-002")]
    public void Chaque_cle_declaree_dans_TextKeys_est_formulee_dans_les_deux_langues()
    {
        var manquantes = ClesDeclarees()
            .Where(cle => !French.Knows(cle) || !English.Knows(cle))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.That(
            manquantes,
            Is.Empty,
            $"Clés déclarées dans TextKeys mais absentes d'au moins une langue : {string.Join(", ", manquantes)}.");
    }

    [Test]
    public void Chaque_valeur_d_enumeration_affichee_a_sa_formulation()
    {
        var manquantes = new List<string>();

        foreach (var kind in NotificationKindExtensions.All)
        {
            Verifier(TextKeys.KindLabel(kind));
            Verifier(TextKeys.KindDescription(kind));
        }

        foreach (var vote in Enum.GetValues<ReviewerVote>())
        {
            Verifier(TextKeys.VoteAction(vote));
            Verifier(TextKeys.VoteLabel(vote));
        }

        foreach (var status in Enum.GetValues<PullRequestStatus>())
        {
            Verifier(TextKeys.PullRequestStatusLabel(status));
        }

        foreach (var result in Enum.GetValues<PipelineRunResult>())
        {
            Verifier(TextKeys.PipelineResultLabel(result));
        }

        foreach (var status in Enum.GetValues<CommentThreadStatus>())
        {
            Verifier(TextKeys.ThreadStatusLabel(status));
        }

        Assert.That(
            manquantes,
            Is.Empty,
            $"Valeurs d'énumération sans formulation : {string.Join(", ", manquantes)}. "
            + "Ajouter une valeur à une énumération affichée oblige à la formuler dans les deux langues.");

        void Verifier(string cle)
        {
            if (!French.Knows(cle) || !English.Knows(cle))
            {
                manquantes.Add(cle);
            }
        }
    }

    [Test]
    public void Une_formulation_differe_bien_d_une_langue_a_l_autre()
    {
        // Vérifie que l'assembly satellite anglais est bien chargé : s'il manquait, l'anglais
        // retomberait silencieusement sur le français et tous les autres tests passeraient.
        Assert.That(
            English.Get(TextKeys.Screen.ButtonSave),
            Is.EqualTo("Save").And.Not.EqualTo(French.Get(TextKeys.Screen.ButtonSave)));
    }

    [Test]
    public void Les_sauts_de_ligne_survivent_au_format_de_ressources()
    {
        // Le XML normalise les sauts de ligne littéraux : ils sont écrits en références de
        // caractères pour traverser intacts, ce que ce test protège.
        Assert.That(French.Get(TextKeys.Screen.AccountsExplanation), Does.Contain("\r\n"));
    }

    [Test]
    public void Un_message_met_en_forme_ses_arguments()
    {
        var texte = TextRef.Of(TextKeys.Screen.AccountsSummary, 3, 2, 17);

        Assert.That(French.Resolve(texte), Is.EqualTo("3 compte(s), dont 2 surveillé(s) · 17 élément(s) suivi(s)."));
    }

    [Test]
    public void Un_fragment_imbrique_est_formule_avant_d_etre_inséré()
    {
        // Le fragment « fichier commenté » n'existe que dans certains cas : il est passé comme
        // message, et non comme chaîne déjà formulée, pour que chaque langue le place où elle
        // veut dans la phrase.
        var texte = TextRef.Of(
            TextKeys.Event.Comment,
            "Camille",
            TextRef.Of(TextKeys.Event.CommentFile, "Program.cs"),
            "il manque un test",
            TextRef.Empty);

        Assert.That(French.Resolve(texte), Is.EqualTo("Camille [Program.cs] : il manque un test"));
    }

    [Test]
    public void Un_fragment_absent_ne_laisse_aucune_trace()
    {
        var texte = TextRef.Of(
            TextKeys.Event.Comment,
            "Camille",
            TextRef.Empty,
            "il manque un test",
            TextRef.Empty);

        Assert.That(English.Resolve(texte), Is.EqualTo("Camille: il manque un test"));
    }

    [Test]
    public void Une_cle_inconnue_est_rendue_telle_quelle()
    {
        // Choix assumé : un texte technique à l'écran vaut mieux qu'une fenêtre qui disparaît.
        Assert.That(French.Get("Cle.Qui.N.Existe.Pas"), Is.EqualTo("Cle.Qui.N.Existe.Pas"));
    }

    [Test]
    public void La_mise_en_forme_des_nombres_suit_la_culture_courante()
    {
        var precedente = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var texte = TextRef.Of(TextKeys.Screen.RepositoriesCount, 1234);

            // Séparateur de milliers anglais : la langue de l'interface et le format des
            // nombres suivent le même réglage.
            Assert.That(English.Resolve(texte), Does.Contain("1234").Or.Contain("1,234"));
        }
        finally
        {
            CultureInfo.CurrentCulture = precedente;
        }
    }

    /// <summary>Toutes les constantes de clé déclarées dans <see cref="TextKeys"/>.</summary>
    private static IEnumerable<string> ClesDeclarees()
    {
        foreach (var cle in ConstantesDe(typeof(TextKeys)))
        {
            yield return cle;
        }

        foreach (var imbrique in typeof(TextKeys).GetNestedTypes(BindingFlags.Public))
        {
            foreach (var cle in ConstantesDe(imbrique))
            {
                yield return cle;
            }
        }

        static IEnumerable<string> ConstantesDe(Type type)
            => type
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()!);
    }
}
