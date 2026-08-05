using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Notifications;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Tests.Doubles;

namespace CSharpForgeWatcher.Tests.Notifications;

/// <summary>SPEC-NOTIF-002 (pas de rafale), SPEC-NOTIF-003 (filtres), SPEC-NOTIF-004 (robustesse).</summary>
[TestFixture]
public sealed class NotificationDispatcherTests
{
    private static PullRequestEvent Event(NotificationKind kind, string dedupKey)
        => Build.Context(Build.Pull()).CreateEvent(
            kind,
            TextRef.Of("Test.Message", dedupKey),
            dedupKey: dedupKey);

    private static WatcherConfiguration Configuration(int maximum = 5, bool sound = true) => new()
    {
        OrganizationUrl = Build.OrganizationUrl,
        MaxNotificationsPerPoll = maximum,
        PlayNotificationSound = sound,
        Repositories = [WatchedRepository.From(Build.Repository)],
    };

    [Test]
    [Category("SPEC-NOTIF-003")]
    public void Un_type_desactive_nest_ni_affiche_ni_retenu()
    {
        var presenter = new RecordingNotificationPresenter();
        var dispatcher = new NotificationDispatcher(presenter);
        var configuration = Configuration();
        configuration.Notifications.VoteChanged = false;

        var retained = dispatcher.Dispatch(
        [
            Event(NotificationKind.VoteChanged, "a"),
            Event(NotificationKind.CommentOnMyPullRequest, "b"),
        ],
            configuration);

        Assert.Multiple(() =>
        {
            Assert.That(retained, Has.Count.EqualTo(1));
            Assert.That(retained[0].Kind, Is.EqualTo(NotificationKind.CommentOnMyPullRequest));
            Assert.That(presenter.Shown, Has.Count.EqualTo(1));
        });
    }

    [Test]
    [Category("SPEC-NOTIF-002")]
    public void En_deca_du_seuil_chaque_evenement_est_affiche()
    {
        var presenter = new RecordingNotificationPresenter();
        var dispatcher = new NotificationDispatcher(presenter);

        dispatcher.Dispatch(
        [
            Event(NotificationKind.CommentOnMyPullRequest, "a"),
            Event(NotificationKind.VoteChanged, "b"),
        ],
            Configuration(maximum: 5));

        Assert.Multiple(() =>
        {
            Assert.That(presenter.Shown, Has.Count.EqualTo(2));
            Assert.That(presenter.Summaries, Is.Empty);
        });
    }

    [Test]
    [Category("SPEC-NOTIF-002")]
    public void Au_dela_du_seuil_une_seule_synthese_est_affichee()
    {
        var presenter = new RecordingNotificationPresenter();
        var dispatcher = new NotificationDispatcher(presenter);

        var events = Enumerable.Range(0, 7)
            .Select(index => Event(NotificationKind.CommentOnMyPullRequest, $"e{index}"))
            .ToList();

        var retained = dispatcher.Dispatch(events, Configuration(maximum: 5));

        Assert.Multiple(() =>
        {
            Assert.That(presenter.Shown, Is.Empty);
            Assert.That(presenter.Summaries, Has.Count.EqualTo(1));
            Assert.That(presenter.Summaries[0], Has.Count.EqualTo(7));
            Assert.That(retained, Has.Count.EqualTo(7), "Tous les événements restent consultables.");
        });
    }

    [Test]
    public void Le_son_suit_la_preference()
    {
        var presenter = new RecordingNotificationPresenter();
        var dispatcher = new NotificationDispatcher(presenter);

        dispatcher.Dispatch([Event(NotificationKind.VoteChanged, "a")], Configuration(sound: false));

        Assert.That(presenter.LastWasSilent, Is.True);
    }

    [Test]
    [Category("SPEC-NOTIF-004")]
    public void Un_canal_daffichage_defaillant_ne_fait_pas_echouer_le_cycle()
    {
        var presenter = new RecordingNotificationPresenter { ThrowOnShow = true };
        var dispatcher = new NotificationDispatcher(presenter);

        var retained = dispatcher.Dispatch([Event(NotificationKind.VoteChanged, "a")], Configuration());

        Assert.That(retained, Has.Count.EqualTo(1));
    }

    [Test]
    public void Les_alertes_de_fonctionnement_respectent_la_preference()
    {
        var presenter = new RecordingNotificationPresenter();
        var dispatcher = new NotificationDispatcher(presenter);
        var configuration = Configuration();

        dispatcher.NotifyProblem(TextRef.Of("Test.Title"), TextRef.Of("Test.Message"), configuration);
        configuration.Notifications.OperationalErrors = false;
        dispatcher.NotifyProblem(TextRef.Of("Test.Title"), TextRef.Of("Test.Ignored"), configuration);

        Assert.That(presenter.Errors, Has.Count.EqualTo(1));
    }

    [Test]
    public void Aucun_evenement_ne_declenche_aucun_affichage()
    {
        var presenter = new RecordingNotificationPresenter();
        var dispatcher = new NotificationDispatcher(presenter);

        var retained = dispatcher.Dispatch([], Configuration());

        Assert.Multiple(() =>
        {
            Assert.That(retained, Is.Empty);
            Assert.That(presenter.Shown, Is.Empty);
            Assert.That(presenter.Summaries, Is.Empty);
        });
    }
}
