using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Monitoring;
using CSharpForgeWatcher.Application.Text;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Tests.Doubles;
using static CSharpForgeWatcher.Tests.Doubles.Texte;

namespace CSharpForgeWatcher.Tests.Monitoring;

/// <summary>
/// Scénarios de cycle complet : amorçage, détection, isolation des pannes, économie
/// d'appels, purge de l'état.
/// </summary>
[TestFixture]
public sealed class PullRequestMonitorTests
{
    [Test]
    [Category("SPEC-POLL-001")]
    public async Task Le_premier_cycle_memorise_sans_notifier()
    {
        var harness = new MonitorHarness();
        harness.Gateway.WithActive(Build.Repository, Build.Pull(id: 1, author: Build.Alice));

        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(PollStatus.Success));
            Assert.That(report.WasSeeding, Is.True);
            Assert.That(report.Events, Is.Empty);
            Assert.That(harness.Presenter.Shown, Is.Empty);
            Assert.That(harness.State.IsSeeded, Is.True);
            Assert.That(harness.State.PullRequests, Has.Count.EqualTo(1));
            Assert.That(report.PullRequests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    [Category("SPEC-EVT-001")]
    public async Task Le_cycle_suivant_signale_une_nouvelle_PR()
    {
        var harness = new MonitorHarness();
        var existing = Build.Pull(id: 1, author: Build.Alice);
        harness.Gateway.WithActive(Build.Repository, existing);
        await harness.PollAsync();

        harness.Gateway.WithActive(Build.Repository, existing, Build.Pull(id: 2, author: Build.Bob, title: "Ajoute le cache"));
        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.Events, Has.Count.EqualTo(1));
            Assert.That(report.Events[0].Kind, Is.EqualTo(NotificationKind.PullRequestCreated));
            // Le compte rendu expose des INotifiableEvent : on redescend au type concret
            // pour vérifier le numéro de PR.
            Assert.That(report.Events.OfType<PullRequestEvent>().Single().PullRequestId, Is.EqualTo(2));
            Assert.That(harness.Presenter.Shown, Has.Count.EqualTo(1));
        });
    }

    [Test]
    [Category("SPEC-EVT-004")]
    [Category("SPEC-NOTIF-001")]
    public async Task Un_commentaire_ajoute_produit_un_evenement_qui_pointe_la_discussion()
    {
        var harness = new MonitorHarness();
        var mine = Build.Pull(id: 10, author: Build.Viewer);
        harness.Gateway.WithActive(Build.Repository, mine);
        harness.Gateway.WithThreads(10, Build.Thread(4, comments: [Build.Comment(1, Build.Alice, "Premier passage")]));
        await harness.PollAsync();

        harness.Gateway.WithThreads(10, Build.Thread(4, comments:
        [
            Build.Comment(1, Build.Alice, "Premier passage"),
            Build.Comment(2, Build.Bob, "Il reste un cas non couvert"),
        ]));
        var report = await harness.PollAsync();

        Assert.That(report.Events, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(report.Events[0].Kind, Is.EqualTo(NotificationKind.CommentOnMyPullRequest));
            Assert.That(report.Events[0].Url, Does.EndWith("/pullrequest/10?discussionId=4"));
        });
    }

    [Test]
    [Category("SPEC-POLL-002")]
    public async Task Un_depot_illisible_nempeche_pas_les_autres_et_son_etat_est_conserve()
    {
        var harness = new MonitorHarness(Build.Repository, Build.OtherRepository);
        harness.Gateway.WithActive(Build.Repository, Build.Pull(id: 1, author: Build.Alice));
        harness.Gateway.WithActive(Build.OtherRepository, Build.Pull(
            id: 2,
            author: Build.Alice,
            repository: Build.OtherRepository));
        await harness.PollAsync();

        harness.Gateway.RepositoryFailures[Build.OtherRepository.RepositoryId] =
            new SourceControlException(TextRef.Of("Test.ForgeError"), 403);
        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(PollStatus.PartialFailure));
            Assert.That(report.Warnings, Has.Count.EqualTo(1));
            Assert.That(report.PullRequests, Has.Count.EqualTo(1), "Le dépôt lisible reste suivi.");
            Assert.That(
                harness.State.ForRepository(Build.OtherRepository.RepositoryId).Count(),
                Is.EqualTo(1),
                "L'état du dépôt en erreur doit être conservé, sinon ses PR sembleraient disparues.");
            Assert.That(report.Events, Is.Empty, "Aucun faux événement de disparition.");
        });
    }

    [Test]
    [Category("SPEC-POLL-004")]
    public async Task Un_PAT_refuse_produit_un_echec_explicite_sans_toucher_a_letat()
    {
        var harness = new MonitorHarness();
        harness.Gateway.ViewerFailure = new SourceControlException(TextRef.Of("Test.ForgeError"), 401);

        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(PollStatus.Failure));
            // Le message porte le conseil propre à un refus d'authentification.
            Assert.That(Fr(report.ErrorMessage), Does.Contain("PAT"));
            Assert.That(harness.Presenter.Errors, Has.Count.EqualTo(1));
            Assert.That(harness.StateStore.SaveCount, Is.Zero);
        });
    }

    [Test]
    [Category("SPEC-CFG-003")]
    public async Task Sans_depot_selectionne_aucun_appel_nest_tente()
    {
        var harness = new MonitorHarness();
        harness.ReconfigureAccount(account => account.Repositories.Clear());

        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(PollStatus.NotConfigured));
            Assert.That(harness.Gateway.Calls, Is.Empty);
        });
    }

    [Test]
    [Category("SPEC-EVT-009")]
    public async Task Une_PR_disparue_de_la_liste_active_est_relue_puis_retiree_de_letat()
    {
        var harness = new MonitorHarness();
        var mine = Build.Pull(id: 5, author: Build.Viewer);
        harness.Gateway.WithActive(Build.Repository, mine);
        await harness.PollAsync();

        harness.Gateway.WithActive(Build.Repository);
        harness.Gateway.PullRequestsById[5] = Build.Pull(id: 5, author: Build.Viewer, status: PullRequestStatus.Completed);
        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.Events.Select(e => e.Kind), Does.Contain(NotificationKind.PullRequestStateChanged));
            Assert.That(harness.State.PullRequests, Is.Empty, "Une PR terminée n'a plus à être surveillée.");
        });
    }

    [Test]
    [Category("SPEC-POLL-002")]
    public async Task Une_PR_dont_letat_final_est_inconnu_reste_surveillee()
    {
        var harness = new MonitorHarness();
        harness.Gateway.WithActive(Build.Repository, Build.Pull(id: 5, author: Build.Viewer));
        await harness.PollAsync();

        harness.Gateway.WithActive(Build.Repository);
        harness.Gateway.PullRequestFailures[5] = new SourceControlException(TextRef.Of("Test.ForgeError"), 503);
        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(PollStatus.PartialFailure));
            Assert.That(harness.State.PullRequests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    [Category("SPEC-POLL-003")]
    public async Task Les_discussions_ne_sont_relues_que_pour_les_PR_qui_me_concernent()
    {
        var harness = new MonitorHarness();
        var mine = Build.Pull(id: 1, author: Build.Viewer);
        var foreign = Build.Pull(id: 2, author: Build.Alice);
        harness.Gateway.WithActive(Build.Repository, mine, foreign);
        await harness.PollAsync();

        harness.ForgetCalls();
        await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(harness.Gateway.Calls, Does.Contain("threads:1"));
            Assert.That(harness.Gateway.Calls, Does.Not.Contain("threads:2"));
        });
    }

    [Test]
    [Category("SPEC-POLL-003")]
    public async Task Une_PR_qui_ne_me_concerne_pas_est_revisitee_apres_le_delai_de_rafraichissement()
    {
        var harness = new MonitorHarness();
        harness.Gateway.WithActive(Build.Repository, Build.Pull(id: 2, author: Build.Alice));
        await harness.PollAsync();

        harness.Clock.Advance(TimeSpan.FromMinutes(31));
        harness.ForgetCalls();
        await harness.PollAsync();

        Assert.That(harness.Gateway.Calls, Does.Contain("threads:2"));
    }

    [Test]
    [Category("SPEC-POLL-003")]
    public async Task La_portee_complete_relit_toutes_les_discussions()
    {
        var harness = new MonitorHarness();
        harness.Reconfigure(configuration =>
            configuration.ThreadScope = ThreadPollingScope.AllWatchedPullRequests);
        harness.Gateway.WithActive(
            Build.Repository,
            Build.Pull(id: 1, author: Build.Viewer),
            Build.Pull(id: 2, author: Build.Alice));
        await harness.PollAsync();

        harness.ForgetCalls();
        await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(harness.Gateway.Calls, Does.Contain("threads:1"));
            Assert.That(harness.Gateway.Calls, Does.Contain("threads:2"));
        });
    }

    [Test]
    [Category("SPEC-POLL-001")]
    public async Task Un_changement_didentite_declenche_un_nouvel_amorcage()
    {
        var harness = new MonitorHarness();
        harness.Gateway.WithActive(Build.Repository, Build.Pull(id: 1, author: Build.Alice));
        await harness.PollAsync();

        harness.Gateway.Viewer = new ViewerIdentity("99999999-9999-9999-9999-999999999999", "Autre compte");
        harness.Gateway.WithActive(
            Build.Repository,
            Build.Pull(id: 1, author: Build.Alice),
            Build.Pull(id: 2, author: Build.Bob));
        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.WasSeeding, Is.True);
            Assert.That(report.Events, Is.Empty);
        });
    }

    [Test]
    [Category("SPEC-CFG-002")]
    public async Task Retirer_un_depot_de_la_configuration_purge_ses_PR_memorisees()
    {
        var harness = new MonitorHarness(Build.Repository, Build.OtherRepository);
        harness.Gateway.WithActive(Build.Repository, Build.Pull(id: 1, author: Build.Alice));
        harness.Gateway.WithActive(Build.OtherRepository, Build.Pull(
            id: 2,
            author: Build.Alice,
            repository: Build.OtherRepository));
        await harness.PollAsync();

        harness.ReconfigureAccount(account => account.Repositories.RemoveAll(
            repository => repository.RepositoryId == Build.OtherRepository.RepositoryId));
        await harness.PollAsync();

        Assert.That(
            harness.State.ForRepository(Build.OtherRepository.RepositoryId),
            Is.Empty);
    }

    [Test]
    public async Task Reinitialiser_letat_provoque_un_amorcage_silencieux()
    {
        var harness = new MonitorHarness();
        harness.Gateway.WithActive(Build.Repository, Build.Pull(id: 1, author: Build.Alice));
        await harness.PollAsync();

        harness.Monitor.ResetState();
        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.WasSeeding, Is.True);
            Assert.That(harness.Presenter.Shown, Is.Empty);
        });
    }

    [Test]
    public async Task Le_compte_rendu_decrit_les_PR_suivies()
    {
        var harness = new MonitorHarness();
        harness.Gateway.WithActive(
            Build.Repository,
            Build.Pull(id: 1, author: Build.Viewer, title: "Ma PR"),
            Build.Pull(id: 2, author: Build.Alice, reviewers: [Build.Vote(Build.Viewer, ReviewerVote.WaitingForAuthor)]));

        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.ViewerName, Is.EqualTo("Camille"));
            Assert.That(report.PullRequests[0].IsMine, Is.True, "Mes PR sont listées en premier.");
            Assert.That(report.PullRequests[1].ViewerIsReviewer, Is.True);
            Assert.That(report.PullRequests[1].ViewerVote, Is.EqualTo(ReviewerVote.WaitingForAuthor));
            Assert.That(
                report.PullRequests[1].DisplayDetail(TextCatalogue.For(EffectiveLanguage.French)),
                Does.Contain("Alice"));
        });
    }
}
