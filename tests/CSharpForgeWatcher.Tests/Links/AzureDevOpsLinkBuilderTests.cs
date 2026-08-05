using CSharpForgeWatcher.Application.Links;
using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Tests.Links;

/// <summary>SPEC-LINK-001 à SPEC-LINK-003 — construction des liens profonds.</summary>
[TestFixture]
public sealed class AzureDevOpsLinkBuilderTests
{
    private static readonly RepositoryRef Repository = new("Mon Projet", "guid-du-repo", "mon-repo");

    [Test]
    [Category("SPEC-LINK-001")]
    public void Le_lien_dune_PR_encode_les_noms()
    {
        var builder = AzureDevOpsLinkBuilder.For("https://dev.azure.com/contoso");

        Assert.That(
            builder.ForPullRequest(Repository, 1234),
            Is.EqualTo("https://dev.azure.com/contoso/Mon%20Projet/_git/mon-repo/pullrequest/1234"));
    }

    [Test]
    [Category("SPEC-LINK-001")]
    public void Le_slash_final_de_lorganisation_est_ignore()
    {
        var builder = AzureDevOpsLinkBuilder.For("https://dev.azure.com/contoso/");

        Assert.That(builder.ForPullRequest(Repository, 7), Does.Not.Contain("contoso//"));
    }

    [Test]
    [Category("SPEC-LINK-002")]
    public void Le_lien_dune_discussion_porte_le_discussionId()
    {
        var builder = AzureDevOpsLinkBuilder.For("https://dev.azure.com/contoso");

        Assert.That(
            builder.ForThread(Repository, 1234, 99),
            Is.EqualTo("https://dev.azure.com/contoso/Mon%20Projet/_git/mon-repo/pullrequest/1234?discussionId=99"));
    }

    [Test]
    [Category("SPEC-LINK-003")]
    public void Le_lien_dun_depot_pointe_la_liste_des_PR()
    {
        var builder = AzureDevOpsLinkBuilder.For("https://dev.azure.com/contoso");

        Assert.That(builder.ForRepositoryPullRequests(Repository), Does.EndWith("/_git/mon-repo/pullrequests"));
    }

    [Test]
    [Category("SPEC-CFG-004")]
    public void Lorganisation_est_relue_a_chaque_appel()
    {
        // L'utilisateur peut changer d'organisation sans redémarrer l'application.
        var organization = "https://dev.azure.com/premier";
        var builder = new AzureDevOpsLinkBuilder(() => organization);

        var before = builder.ForPullRequest(Repository, 1);
        organization = "https://contoso.visualstudio.com";
        var after = builder.ForPullRequest(Repository, 1);

        Assert.Multiple(() =>
        {
            Assert.That(before, Does.StartWith("https://dev.azure.com/premier"));
            Assert.That(after, Does.StartWith("https://contoso.visualstudio.com"));
        });
    }
}
