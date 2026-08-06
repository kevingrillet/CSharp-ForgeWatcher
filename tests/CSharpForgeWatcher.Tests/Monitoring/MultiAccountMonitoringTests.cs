using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Monitoring;
using CSharpForgeWatcher.Domain.Identity;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Tests.Doubles;
using static CSharpForgeWatcher.Tests.Doubles.Texte;

namespace CSharpForgeWatcher.Tests.Monitoring;

/// <summary>
/// SPEC-CFG-008 — plusieurs forges surveillées dans le même cycle.
/// </summary>
/// <remarks>
/// Ce qui est vérifié ici n'est pas « GitHub fonctionne » — cela demanderait un serveur — mais
/// le <b>cloisonnement</b> : chaque compte a son identité, son amorçage, sa mémoire et ses
/// pannes, et aucun ne peut priver les autres de leur cycle.
/// </remarks>
[TestFixture]
[Category("SPEC-CFG-008")]
public sealed class MultiAccountMonitoringTests
{
    private const string GitHubAccountId = "compte-github";

    /// <summary>Dépôt GitHub : propriétaire + identifiant numérique, comme le produit le mappeur.</summary>
    private static readonly RepositoryRef GitHubRepository = new("mon-organisation", "4711", "outils-internes");

    private static readonly UserRef GitHubViewer = new("camille", "Camille");

    /// <summary>
    /// Monte un harnais à deux comptes : celui d'Azure DevOps par défaut, plus un compte
    /// GitHub avec sa propre passerelle.
    /// </summary>
    private static (MonitorHarness Harness, FakeSourceControlGateway GitHub) TwoAccounts()
    {
        var harness = new MonitorHarness();

        var gitHub = new FakeSourceControlGateway
        {
            Viewer = new ViewerIdentity(GitHubViewer.Id, GitHubViewer.SafeDisplayName, GitHubViewer.Id),
        };

        harness.GatewayFactory.With(SourceControlProvider.GitHub, gitHub);

        harness.Reconfigure(configuration => configuration.Accounts.Add(new WatchedAccount
        {
            Id = GitHubAccountId,
            Provider = SourceControlProvider.GitHub,
            Url = "https://github.com",
            Repositories = [WatchedRepository.From(GitHubRepository)],
        }));

        return (harness, gitHub);
    }

    [Test]
    public async Task Les_deux_comptes_sont_interroges_dans_le_meme_cycle()
    {
        var (harness, gitHub) = TwoAccounts();
        harness.Gateway.WithActive(Build.Repository, Build.Pull(id: 1, author: Build.Alice));
        gitHub.WithActive(GitHubRepository, Build.Pull(id: 2, author: Build.Bob, repository: GitHubRepository));

        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(PollStatus.Success));
            Assert.That(harness.Gateway.Calls, Has.Some.StartsWith("viewer:"));
            Assert.That(gitHub.Calls, Has.Some.StartsWith("viewer:"));
            Assert.That(report.PullRequests, Has.Count.EqualTo(2), "Les deux forges alimentent la même liste.");
            Assert.That(
                report.ViewerName,
                Does.Contain("Camille"),
                "Le compte rendu nomme les identités rencontrées.");
        });
    }

    [Test]
    public async Task Chaque_compte_a_sa_propre_memoire()
    {
        var (harness, gitHub) = TwoAccounts();
        harness.Gateway.WithActive(Build.Repository, Build.Pull(id: 1, author: Build.Alice));
        gitHub.WithActive(GitHubRepository, Build.Pull(id: 2, author: Build.Bob, repository: GitHubRepository));

        await harness.PollAsync();

        var state = harness.StateStore.Snapshot;

        Assert.Multiple(() =>
        {
            Assert.That(state.Accounts, Has.Count.EqualTo(2));
            Assert.That(state.ForAccount(MonitorHarness.AccountId).ViewerId, Is.EqualTo(Build.ViewerId));
            Assert.That(
                state.ForAccount(GitHubAccountId).ViewerId,
                Is.EqualTo(GitHubViewer.Id),
                "L'identité GitHub est un login, pas le GUID d'Azure DevOps.");
            Assert.That(state.ForAccount(MonitorHarness.AccountId).PullRequests, Has.Count.EqualTo(1));
            Assert.That(state.ForAccount(GitHubAccountId).PullRequests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Un_compte_en_panne_ne_prive_pas_les_autres()
    {
        var (harness, gitHub) = TwoAccounts();
        harness.Gateway.WithActive(Build.Repository, Build.Pull(id: 1, author: Build.Viewer));
        gitHub.WithActive(GitHubRepository, Build.Pull(id: 2, author: Build.Bob, repository: GitHubRepository));

        // Premier cycle : amorçage silencieux des deux comptes.
        await harness.PollAsync();

        // Le jeton GitHub expire, et un commentaire arrive sur la PR d'Azure DevOps.
        gitHub.ViewerFailure = new SourceControlException(TextRef.Of("Test.ForgeError"), 401);
        harness.Gateway.WithThreads(1, Build.Thread(5, comments: [Build.Comment(1, Build.Alice, "Un souci ici.")]));

        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(PollStatus.PartialFailure));
            Assert.That(
                report.Warnings.Select(Fr),
                Has.Some.Contains("GitHub"),
                "Le compte fautif doit être nommé.");
            Assert.That(
                harness.Presenter.Shown,
                Has.Count.EqualTo(1),
                "Le commentaire d'Azure DevOps doit être notifié malgré la panne de GitHub.");
            Assert.That(
                harness.StateStore.Snapshot.ForAccount(GitHubAccountId).PullRequests,
                Has.Count.EqualTo(1),
                "L'état du compte en panne est conservé intact.");
        });
    }

    [Test]
    public async Task Toutes_les_forges_en_panne_ne_touchent_pas_a_letat()
    {
        var (harness, gitHub) = TwoAccounts();
        harness.Gateway.ViewerFailure = new SourceControlException(TextRef.Of("Test.ForgeError"), 401);
        gitHub.ViewerFailure = new SourceControlException(TextRef.Of("Test.ForgeError"), 401);

        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(PollStatus.Failure));
            Assert.That(harness.StateStore.SaveCount, Is.Zero, "Rien n'a été appris : rien n'est écrit.");
            Assert.That(harness.Presenter.Errors, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Un_compte_ajoute_samorce_seul()
    {
        // Le compte Azure DevOps est établi depuis un cycle ; le compte GitHub arrive après.
        var harness = new MonitorHarness();
        harness.Gateway.WithActive(Build.Repository, Build.Pull(id: 1, author: Build.Viewer));
        await harness.PollAsync();

        var gitHub = new FakeSourceControlGateway
        {
            Viewer = new ViewerIdentity(GitHubViewer.Id, GitHubViewer.SafeDisplayName, GitHubViewer.Id),
        };
        gitHub.WithActive(GitHubRepository, Build.Pull(id: 2, author: Build.Bob, repository: GitHubRepository));
        harness.GatewayFactory.With(SourceControlProvider.GitHub, gitHub);

        harness.Reconfigure(configuration => configuration.Accounts.Add(new WatchedAccount
        {
            Id = GitHubAccountId,
            Provider = SourceControlProvider.GitHub,
            Url = "https://github.com",
            Repositories = [WatchedRepository.From(GitHubRepository)],
        }));

        // Un commentaire arrive sur la PR d'Azure DevOps pendant l'amorçage de GitHub.
        harness.Gateway.WithThreads(1, Build.Thread(5, comments: [Build.Comment(1, Build.Alice, "Un souci ici.")]));

        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.WasSeeding, Is.True, "Un compte s'est amorcé pendant ce cycle.");
            Assert.That(
                harness.Presenter.Shown,
                Has.Count.EqualTo(1),
                "L'amorçage du nouveau compte ne doit pas faire taire les comptes établis.");
            Assert.That(harness.StateStore.Snapshot.ForAccount(GitHubAccountId).IsSeeded, Is.True);
            Assert.That(harness.StateStore.Snapshot.ForAccount(GitHubAccountId).PullRequests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Retirer_un_compte_oublie_sa_memoire_seule()
    {
        var (harness, gitHub) = TwoAccounts();
        harness.Gateway.WithActive(Build.Repository, Build.Pull(id: 1, author: Build.Alice));
        gitHub.WithActive(GitHubRepository, Build.Pull(id: 2, author: Build.Bob, repository: GitHubRepository));
        await harness.PollAsync();

        harness.Reconfigure(configuration => configuration.Accounts.RemoveAll(account =>
            string.Equals(account.Id, GitHubAccountId, StringComparison.Ordinal)));
        await harness.PollAsync();

        var state = harness.StateStore.Snapshot;

        Assert.Multiple(() =>
        {
            Assert.That(state.Accounts.Keys, Is.EqualTo(new[] { MonitorHarness.AccountId }));
            Assert.That(state.ForAccount(MonitorHarness.AccountId).PullRequests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Les_notifications_indiquent_leur_compte_dorigine()
    {
        var (harness, gitHub) = TwoAccounts();
        harness.Gateway.WithActive(Build.Repository, Build.Pull(id: 1, author: Build.Viewer));
        gitHub.WithActive(GitHubRepository, Build.Pull(id: 2, author: Build.Bob, repository: GitHubRepository));
        await harness.PollAsync();

        harness.Gateway.WithThreads(1, Build.Thread(5, comments: [Build.Comment(1, Build.Alice, "Un souci ici.")]));

        await harness.PollAsync();

        var notification = harness.Presenter.Shown.Single();

        Assert.That(
            notification.Context,
            Does.Contain("Azure DevOps"),
            "Avec plusieurs forges, deux dépôts homonymes seraient sinon indistinguables.");
    }

    [Test]
    public async Task Un_compte_desactive_nest_pas_interroge()
    {
        var (harness, gitHub) = TwoAccounts();
        harness.Gateway.WithActive(Build.Repository, Build.Pull(id: 1, author: Build.Alice));

        harness.Reconfigure(configuration =>
            configuration.Accounts.Single(account => account.Id == GitHubAccountId).IsEnabled = false);

        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(gitHub.Calls, Is.Empty, "Un compte désactivé ne doit provoquer aucun appel.");
            Assert.That(report.Status, Is.EqualTo(PollStatus.Success));
        });
    }
}
