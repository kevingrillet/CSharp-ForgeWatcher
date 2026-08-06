using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Links;
using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Tests.Links;

/// <summary>
/// SPEC-FORGE-002 et SPEC-FORGE-003 — le réglage de forge détermine la forme des liens.
/// </summary>
[TestFixture]
public sealed class ProviderAwareLinkBuilderTests
{
    private static readonly RepositoryRef Repository = new("mon-organisation", "42", "backoffice-api");

    [Test]
    [Category("SPEC-FORGE-003")]
    public void Chaque_fournisseur_produit_ses_propres_formats()
    {
        static IPullRequestLinkBuilder For(SourceControlProvider provider, string url)
            => new ProviderAwareLinkBuilder(() => provider, () => url);

        var azure = For(SourceControlProvider.AzureDevOps, "https://dev.azure.com/contoso");
        var gitHub = For(SourceControlProvider.GitHub, "https://github.com");
        var gitLab = For(SourceControlProvider.GitLab, "https://gitlab.com");

        Assert.Multiple(() =>
        {
            Assert.That(
                azure.ForPullRequest(Repository, 12),
                Is.EqualTo("https://dev.azure.com/contoso/mon-organisation/_git/backoffice-api/pullrequest/12"));
            Assert.That(
                gitHub.ForPullRequest(Repository, 12),
                Is.EqualTo("https://github.com/mon-organisation/backoffice-api/pull/12"));
            Assert.That(
                gitLab.ForPullRequest(Repository, 12),
                Is.EqualTo("https://gitlab.com/mon-organisation/backoffice-api/-/merge_requests/12"));
        });
    }

    [Test]
    [Category("SPEC-FORGE-002")]
    [Category("SPEC-CFG-004")]
    public void Changer_de_forge_prend_effet_immediatement()
    {
        // Le fournisseur est relu à chaque appel : la fenêtre de configuration s'applique à
        // chaud, sans redémarrage, et sans réenregistrer le conteneur d'injection.
        var provider = SourceControlProvider.AzureDevOps;
        var url = "https://dev.azure.com/contoso";
        var builder = new ProviderAwareLinkBuilder(() => provider, () => url);

        var before = builder.ForRepositoryPullRequests(Repository);

        provider = SourceControlProvider.GitHub;
        url = "https://github.com";
        var after = builder.ForRepositoryPullRequests(Repository);

        Assert.Multiple(() =>
        {
            Assert.That(before, Does.EndWith("/_git/backoffice-api/pullrequests"));
            Assert.That(after, Is.EqualTo("https://github.com/mon-organisation/backoffice-api/pulls"));
        });
    }

    [Test]
    [Category("SPEC-FORGE-002")]
    public void Une_forge_non_implementee_leve_plutot_que_de_produire_un_lien_faux()
    {
        // Un repli silencieux sur Azure DevOps fabriquerait des liens plausibles menant
        // nulle part : le pire des comportements. La validation refuse de toute façon un tel
        // fournisseur en amont (SPEC-FORGE-002) ; ce test est le filet qui protégera le jour
        // où une quatrième forge entrera dans l'énumération sans son générateur de liens.
        var builder = new ProviderAwareLinkBuilder(
            () => (SourceControlProvider)99,
            () => "https://forge.exemple.fr");

        Assert.That(
            () => builder.ForPullRequest(Repository, 1),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("pas encore implémentée"));
    }
}
