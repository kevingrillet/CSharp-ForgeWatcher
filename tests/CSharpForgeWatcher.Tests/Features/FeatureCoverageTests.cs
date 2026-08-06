using System.Reflection;
using System.Text.RegularExpressions;

namespace CSharpForgeWatcher.Tests.Features;

/// <summary>
/// <para>
/// Garde-fou entre les scénarios Gherkin de <c>docs/features/</c> et les tests de cette
/// solution.
/// </para>
/// <para>
/// <b>Pourquoi ce test existe.</b> Les fichiers <c>.feature</c> sont de la documentation
/// vivante, pas des tests exécutables : aucun runner BDD (Reqnroll ou autre) ne les joue,
/// pour les raisons expliquées dans <c>docs/features/README.md</c>. Rien n'empêcherait
/// donc, mécaniquement, que la documentation et les tests divergent : un scénario qui
/// décrit un comportement que plus aucun test ne vérifie, ou un comportement testé que
/// plus aucun scénario ne raconte. Dans les deux cas la documentation devient trompeuse,
/// et c'est exactement ce qu'on lui reproche d'habitude.
/// </para>
/// <para>
/// Ce test rétablit le lien qu'un runner BDD assurerait : il compare les étiquettes
/// <c>@SPEC-...</c> des scénarios aux catégories NUnit <c>[Category("SPEC-...")]</c>
/// portées par les tests, dans les deux sens. C'est bon marché — de la lecture de fichiers
/// et de la réflexion — et ça échoue au bon moment : quand on renomme une spec, quand on
/// supprime un test, ou quand on ajoute un comportement sans le documenter.
/// </para>
/// </summary>
[TestFixture]
public sealed class FeatureCoverageTests
{
    /// <summary>
    /// <para>
    /// Specs documentées en Gherkin mais dont la vérification est <b>manuelle ou à venir</b> :
    /// aucun test automatisé ne porte (encore) leur catégorie, et c'est assumé.
    /// </para>
    /// <para>
    /// Sans cette liste, le premier test de ce fichier serait rouge en permanence, ce qui
    /// est la meilleure façon de faire ignorer un garde-fou. Chaque entrée doit avoir une
    /// raison ; les raisons détaillées vivent dans la section « Zones non couvertes par des
    /// tests automatisés » de <c>docs/TRACEABILITE.md</c>.
    /// </para>
    /// <para>
    /// <b>Comment la vider.</b> Dès qu'un test porte la catégorie d'une spec listée ici,
    /// l'entrée correspondante devient inutile : la retirer ne casse rien (la liste tolère
    /// une absence de test, elle n'exige pas qu'elle persiste). Le réflexe est donc :
    /// j'écris un test pour une spec, je supprime sa ligne ici. La liste doit rétrécir avec
    /// le temps ; si elle grossit, c'est le signe d'une documentation qui avance plus vite
    /// que les tests.
    /// </para>
    /// </summary>
    private static readonly string[] VerificationManuelleOuAVenir =
    [
        // Emplacements sur disque et tolérance aux fichiers abîmés : dépend du système de
        // fichiers réel et du dossier de données de l'utilisateur.
        "SPEC-CFG-005",

        // Démarrage avec Windows : écrit dans le registre de l'utilisateur courant.
        "SPEC-CFG-006",

        // Pastille de non-lus sur l'icône de la zone de notification : rendu WinForms.
        "SPEC-NOTIF-005",

        // Abstraction de la forge : contraintes d'architecture — port unique et en lecture
        // seule, contrat que doit remplir une nouvelle forge, neutralité du vocabulaire du
        // domaine. Ce sont des propriétés du code, pas des comportements exécutables ; elles
        // sont vérifiées par le subagent « relecteur-architecture » et les balayages du skill
        // « respecter-architecture ».
        "SPEC-FORGE-001",
        "SPEC-FORGE-004",
        "SPEC-FORGE-005",

        // Thème et icône : rendu WinForms et lecture de l'apparence de Windows.
        "SPEC-UI-THEME-001",
        "SPEC-UI-THEME-002",
        "SPEC-UI-THEME-003",
        "SPEC-UI-THEME-004",
        "SPEC-UI-ICON-001",
    ];

    /// <summary>
    /// Étiquette de spec dans un fichier Gherkin, par exemple <c>@SPEC-EVT-005</c> ou
    /// <c>@SPEC-UI-THEME-002</c>.
    /// </summary>
    private static readonly Regex EtiquetteDeSpec = new(
        @"@(SPEC-[A-Z]+(?:-[A-Z]+)*-\d+)",
        RegexOptions.CultureInvariant);

    /// <summary>Chemin du dossier <c>docs/features</c>, résolu une fois pour toutes.</summary>
    private static string DossierDesScenarios => _dossierDesScenarios ??= LocaliserLeDossierDesScenarios();

    private static string? _dossierDesScenarios;

    [Test]
    public void Les_fichiers_de_scenarios_sont_trouves_et_etiquetes()
    {
        var fichiers = FichiersDeScenarios();
        var etiquettes = EtiquettesDesScenarios();

        Assert.Multiple(() =>
        {
            Assert.That(
                fichiers,
                Is.Not.Empty,
                $"Aucun fichier .feature dans « {DossierDesScenarios} » : les deux tests de "
                + "cohérence qui suivent passeraient alors sans rien vérifier.");
            Assert.That(
                etiquettes,
                Is.Not.Empty,
                "Aucune étiquette @SPEC-... dans les fichiers .feature : la convention "
                + "d'étiquetage décrite dans docs/features/README.md n'est pas respectée.");
        });
    }

    /// <summary>
    /// Sens 1 : la documentation ne doit pas promettre plus que ce qui est vérifié.
    /// Une étiquette orpheline signale une spec renommée, un test supprimé, ou un scénario
    /// écrit avant son test — ce dernier cas est légitime, mais doit alors être assumé
    /// explicitement dans <see cref="VerificationManuelleOuAVenir"/>.
    /// </summary>
    [Test]
    public void Chaque_scenario_Gherkin_renvoie_a_une_spec_reellement_testee()
    {
        var tolerees = VerificationManuelleOuAVenir.ToHashSet(StringComparer.Ordinal);

        var orphelines = EtiquettesDesScenarios()
            .Except(CategoriesDesTests(), StringComparer.Ordinal)
            .Where(etiquette => !tolerees.Contains(etiquette))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.That(
            orphelines,
            Is.Empty,
            $"""
             {orphelines.Count} étiquette(s) de spec des fichiers .feature ne correspondent à
             aucune catégorie de test : {string.Join(", ", orphelines)}.
             Trois issues possibles : écrire le test manquant avec [Category("...")], corriger
             l'étiquette du scénario si la spec a été renommée, ou — si le comportement n'est
             pas automatisable — ajouter l'identifiant à VerificationManuelleOuAVenir avec sa
             raison.
             """);
    }

    /// <summary>
    /// Sens 2 : ce qui est vérifié doit être racontable. Une spec testée mais absente des
    /// scénarios est un comportement que l'équipe garantit sans l'avoir expliqué.
    /// </summary>
    [Test]
    public void Chaque_spec_couverte_par_un_test_est_illustree_par_un_scenario()
    {
        var nonIllustrees = CategoriesDesTests()
            .Except(EtiquettesDesScenarios(), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.That(
            nonIllustrees,
            Is.Empty,
            $"""
             {nonIllustrees.Count} spec(s) sont couvertes par des tests mais n'apparaissent dans
             aucun scénario Gherkin : {string.Join(", ", nonIllustrees)}.
             Ajouter le scénario correspondant dans docs/features/ (fichier par thème), étiqueté
             avec l'identifiant de la spec.
             """);
    }

    /// <summary>Fichiers <c>.feature</c> du dossier de documentation.</summary>
    private static IReadOnlyList<string> FichiersDeScenarios()
        => [.. Directory.EnumerateFiles(DossierDesScenarios, "*.feature", SearchOption.AllDirectories)];

    /// <summary>Identifiants de spec étiquetés dans les scénarios Gherkin.</summary>
    private static ISet<string> EtiquettesDesScenarios()
    {
        var etiquettes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fichier in FichiersDeScenarios())
        {
            foreach (Match correspondance in EtiquetteDeSpec.Matches(File.ReadAllText(fichier)))
            {
                etiquettes.Add(correspondance.Groups[1].Value);
            }
        }

        return etiquettes;
    }

    /// <summary>
    /// Catégories de la forme <c>SPEC-...</c> portées par les tests de cet assembly,
    /// qu'elles soient déclarées sur la classe de test ou sur la méthode.
    /// </summary>
    private static ISet<string> CategoriesDesTests()
    {
        const BindingFlags Membres =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        var categories = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in typeof(FeatureCoverageTests).Assembly.GetTypes())
        {
            Collecter(type.GetCustomAttributes<CategoryAttribute>(inherit: true));

            foreach (var methode in type.GetMethods(Membres))
            {
                Collecter(methode.GetCustomAttributes<CategoryAttribute>(inherit: true));
            }
        }

        return categories;

        void Collecter(IEnumerable<CategoryAttribute> attributs)
        {
            foreach (var attribut in attributs.Where(a => a.Name.StartsWith("SPEC-", StringComparison.Ordinal)))
            {
                categories.Add(attribut.Name);
            }
        }
    }

    /// <summary>
    /// Remonte depuis le dossier d'exécution des tests (<c>bin/Debug/net9.0</c>) jusqu'à la
    /// racine du dépôt pour y trouver <c>docs/features</c>. On ne s'appuie pas sur le
    /// répertoire courant, qui dépend de la façon dont les tests sont lancés.
    /// </summary>
    private static string LocaliserLeDossierDesScenarios()
    {
        var depart = TestContext.CurrentContext.TestDirectory;

        for (var dossier = new DirectoryInfo(depart); dossier is not null; dossier = dossier.Parent)
        {
            var candidat = Path.Combine(dossier.FullName, "docs", "features");
            if (Directory.Exists(candidat))
            {
                return candidat;
            }
        }

        Assert.Fail(
            $"Dossier « docs/features » introuvable en remontant depuis « {depart} ». "
            + "Ce test compare les scénarios Gherkin aux catégories des tests : sans les "
            + "fichiers .feature, il ne peut rien vérifier. Vérifier que le dépôt est complet "
            + "(les scénarios ne sont pas copiés dans la sortie de compilation, ils sont lus "
            + "depuis les sources).");

        return string.Empty; // Inatteignable : Assert.Fail lève toujours.
    }
}
