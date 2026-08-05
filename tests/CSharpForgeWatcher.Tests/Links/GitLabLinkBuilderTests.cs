using CSharpForgeWatcher.Application.Links;
using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Tests.Links;

/// <summary>
/// SPEC-FORGE-003 — formats d'URL de GitLab.
/// </summary>
/// <remarks>
/// La particularité de GitLab est le chemin de groupe, qui peut être imbriqué : c'est le
/// piège que ces tests surveillent, avec le préfixe <c>/-/</c> qui rend ce chemin non ambigu.
/// </remarks>
[TestFixture]
public sealed class GitLabLinkBuilderTests
{
    private const string Server = "https://gitlab.com";

    private static readonly RepositoryRef Repository = new("equipe/backoffice", "4711", "backoffice-api");

    [Test]
    [Category("SPEC-LINK-001")]
    [Category("SPEC-FORGE-003")]
    public void Le_lien_dune_MR_conserve_le_chemin_du_groupe_imbrique()
    {
        var builder = GitLabLinkBuilder.For(Server);

        Assert.That(
            builder.ForPullRequest(Repository, 1234),
            Is.EqualTo("https://gitlab.com/equipe/backoffice/backoffice-api/-/merge_requests/1234"),
            "Les barres obliques du groupe doivent rester des séparateurs, pas devenir %2F.");
    }

    [Test]
    [Category("SPEC-LINK-002")]
    [Category("SPEC-FORGE-003")]
    public void Le_lien_dune_discussion_ancre_la_note()
    {
        // L'identifiant de discussion de GitLab étant une empreinte textuelle, c'est celui de
        // la première note du fil qui tient ce rôle — et il sert aussi d'ancre.
        var builder = GitLabLinkBuilder.For(Server);

        Assert.That(
            builder.ForThread(Repository, 1234, 987654321),
            Is.EqualTo("https://gitlab.com/equipe/backoffice/backoffice-api/-/merge_requests/1234#note_987654321"));
    }

    [Test]
    [Category("SPEC-LINK-003")]
    public void Le_lien_dun_depot_pointe_la_liste_des_MR()
    {
        var builder = GitLabLinkBuilder.For(Server);

        Assert.That(
            builder.ForRepositoryPullRequests(Repository),
            Is.EqualTo("https://gitlab.com/equipe/backoffice/backoffice-api/-/merge_requests"));
    }

    [Test]
    [Category("SPEC-FORGE-003")]
    [Category("SPEC-FORGE-006")]
    public void Le_lien_dune_execution_porte_son_identifiant_64_bits()
    {
        const long RunId = 9_876_543_210L;
        var builder = GitLabLinkBuilder.For(Server);

        Assert.Multiple(() =>
        {
            Assert.That(RunId, Is.GreaterThan(int.MaxValue));
            Assert.That(
                builder.ForPipelineRun("equipe/backoffice/backoffice-api", RunId),
                Is.EqualTo("https://gitlab.com/equipe/backoffice/backoffice-api/-/pipelines/9876543210"));
        });
    }

    [Test]
    [Category("SPEC-LINK-001")]
    public void Les_segments_sont_encodes_un_a_un()
    {
        var builder = GitLabLinkBuilder.For(Server);
        var repository = new RepositoryRef("mon groupe/sous groupe", "1", "mon projet");

        Assert.That(
            builder.ForPullRequest(repository, 7),
            Is.EqualTo("https://gitlab.com/mon%20groupe/sous%20groupe/mon%20projet/-/merge_requests/7"));
    }

    [Test]
    [Category("SPEC-FORGE-002")]
    public void Le_serveur_auto_heberge_est_utilise_tel_quel()
    {
        var builder = GitLabLinkBuilder.For("https://gitlab.mon-entreprise.fr/un/chemin/ignore");

        Assert.That(
            builder.ForPullRequest(Repository, 7),
            Does.StartWith("https://gitlab.mon-entreprise.fr/equipe/backoffice/backoffice-api"));
    }
}
