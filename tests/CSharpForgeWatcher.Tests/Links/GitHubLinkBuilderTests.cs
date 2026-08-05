using CSharpForgeWatcher.Application.Links;
using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Tests.Links;

/// <summary>
/// SPEC-FORGE-003 — formats d'URL de GitHub, et SPEC-FORGE-006 — identifiants 64 bits.
/// </summary>
/// <remarks>
/// C'est le seul endroit du dépôt où comparer une URL littérale complète est la bonne façon
/// de tester : ces chaînes sont un contrat avec un service externe, pas un détail
/// d'implémentation.
/// </remarks>
[TestFixture]
public sealed class GitHubLinkBuilderTests
{
    private const string Server = "https://github.com";

    private static readonly RepositoryRef Repository = new("mon-organisation", "42", "backoffice-api");

    [Test]
    [Category("SPEC-LINK-001")]
    [Category("SPEC-FORGE-003")]
    public void Le_lien_dune_PR_suit_le_format_de_GitHub()
    {
        var builder = GitHubLinkBuilder.For(Server);

        Assert.That(
            builder.ForPullRequest(Repository, 1234),
            Is.EqualTo("https://github.com/mon-organisation/backoffice-api/pull/1234"));
    }

    [Test]
    [Category("SPEC-LINK-002")]
    [Category("SPEC-FORGE-003")]
    public void Le_lien_dune_discussion_ancre_le_commentaire_de_ligne()
    {
        var builder = GitHubLinkBuilder.For(Server);

        Assert.That(
            builder.ForThread(Repository, 1234, 987654321),
            Is.EqualTo("https://github.com/mon-organisation/backoffice-api/pull/1234#discussion_r987654321"));
    }

    [Test]
    [Category("SPEC-LINK-002")]
    public void Une_discussion_synthetique_ne_porte_pas_dancre()
    {
        // L'onglet « Conversation » n'est pas structuré en fils : sa discussion porte un
        // identifiant négatif, qui n'a aucune traduction dans une URL.
        var builder = GitHubLinkBuilder.For(Server);

        Assert.That(
            builder.ForThread(Repository, 1234, -1),
            Is.EqualTo("https://github.com/mon-organisation/backoffice-api/pull/1234"));
    }

    [Test]
    [Category("SPEC-LINK-003")]
    public void Le_lien_dun_depot_pointe_la_liste_des_PR()
    {
        var builder = GitHubLinkBuilder.For(Server);

        Assert.That(
            builder.ForRepositoryPullRequests(Repository),
            Is.EqualTo("https://github.com/mon-organisation/backoffice-api/pulls"));
    }

    [Test]
    [Category("SPEC-FORGE-003")]
    [Category("SPEC-FORGE-006")]
    public void Le_lien_dune_execution_porte_son_identifiant_64_bits()
    {
        // Un identifiant d'exécution GitHub Actions dépasse la capacité d'un entier 32 bits :
        // il doit être restitué caractère pour caractère.
        const long RunId = 12_345_678_901L;
        var builder = GitHubLinkBuilder.For(Server);

        Assert.Multiple(() =>
        {
            Assert.That(RunId, Is.GreaterThan(int.MaxValue));
            Assert.That(
                builder.ForPipelineRun("mon-organisation/backoffice-api", RunId),
                Is.EqualTo("https://github.com/mon-organisation/backoffice-api/actions/runs/12345678901"));
        });
    }

    [Test]
    [Category("SPEC-FORGE-003")]
    public void Un_espace_de_pipeline_sans_depot_renvoie_la_racine_du_serveur()
    {
        // Cas d'une configuration constituée sur une autre forge : mieux vaut la page
        // d'accueil qu'une adresse fabriquée menant à une erreur 404.
        var builder = GitHubLinkBuilder.For(Server);

        Assert.That(builder.ForPipelineRun("Backoffice", 12), Is.EqualTo(Server));
    }

    [Test]
    [Category("SPEC-FORGE-002")]
    public void Seule_lorigine_de_lURL_saisie_est_retenue()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ServerUrl.Origin("https://github.com/"), Is.EqualTo(Server));
            Assert.That(
                ServerUrl.Origin("https://github.com/mon-organisation"),
                Is.EqualTo(Server),
                "Le propriétaire se choisit dans l'arborescence, pas dans l'URL.");
            Assert.That(
                ServerUrl.Origin("  https://github.example.com:8443/gh/  "),
                Is.EqualTo("https://github.example.com:8443"),
                "Le port d'une instance auto-hébergée est conservé.");
            Assert.That(
                ServerUrl.Origin("pas-une-url/"),
                Is.EqualTo("pas-une-url"),
                "Une valeur inexploitable est rendue telle quelle : c'est la validation qui la refuse.");
        });
    }

    [Test]
    [Category("SPEC-FORGE-003")]
    public void Le_serveur_dentreprise_est_utilise_tel_quel()
    {
        var builder = GitHubLinkBuilder.For("https://github.mon-entreprise.fr");

        Assert.That(
            builder.ForPullRequest(Repository, 7),
            Is.EqualTo("https://github.mon-entreprise.fr/mon-organisation/backoffice-api/pull/7"));
    }

    [Test]
    [Category("SPEC-CFG-004")]
    public void Le_serveur_est_relu_a_chaque_appel()
    {
        var server = Server;
        var builder = new GitHubLinkBuilder(() => server);

        var before = builder.ForPullRequest(Repository, 1);
        server = "https://github.mon-entreprise.fr";
        var after = builder.ForPullRequest(Repository, 1);

        Assert.Multiple(() =>
        {
            Assert.That(before, Does.StartWith(Server));
            Assert.That(after, Does.StartWith("https://github.mon-entreprise.fr"));
        });
    }
}
