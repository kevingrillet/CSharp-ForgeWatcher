using CSharpForgeWatcher.Application.Detection;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Tests.Doubles;

namespace CSharpForgeWatcher.Tests.Detection;

/// <summary>Composition des règles : déduplication, isolation des pannes, tri.</summary>
[TestFixture]
public sealed class PullRequestEventDetectorTests
{
    [Test]
    public void Le_detecteur_standard_a_besoin_des_discussions()
    {
        Assert.That(PullRequestEventDetector.CreateDefault().AnyRuleRequiresThreads, Is.True);
    }

    [Test]
    public void Une_regle_defaillante_ninterrompt_pas_les_autres()
    {
        var detector = new PullRequestEventDetector([new ExplodingRule(), new AlwaysOneEventRule()]);

        var events = detector.Detect(Build.Context(Build.Pull()));

        Assert.That(events, Has.Count.EqualTo(1), "La règle saine doit s'exécuter malgré l'échec de l'autre.");
    }

    [Test]
    public void Les_evenements_de_meme_cle_sont_dedoublonnes()
    {
        var detector = new PullRequestEventDetector([new AlwaysOneEventRule(), new AlwaysOneEventRule()]);

        Assert.That(detector.Detect(Build.Context(Build.Pull())), Has.Count.EqualTo(1));
    }

    [Test]
    public void Les_evenements_sont_tries_du_plus_precis_au_plus_general()
    {
        var detector = new PullRequestEventDetector(
        [
            new AlwaysOneEventRule(NotificationKind.PullRequestCreated, "general"),
            new AlwaysOneEventRule(NotificationKind.MentionedInComment, "precis"),
        ]);

        var events = detector.Detect(Build.Context(Build.Pull()));

        Assert.That(events.Select(e => e.Kind), Is.EqualTo(new[]
        {
            NotificationKind.MentionedInComment,
            NotificationKind.PullRequestCreated,
        }));
    }

    [Test]
    public void Un_detecteur_sans_regle_est_refuse()
    {
        Assert.That(
            () => new PullRequestEventDetector(Array.Empty<IPullRequestEventRule>()),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    [Category("SPEC-EVT-001")]
    [Category("SPEC-EVT-002")]
    public void Une_PR_ou_je_suis_relecteur_ne_produit_quun_evenement()
    {
        // Les deux règles pourraient parler ; SPEC-EVT-001 règle 2 impose le silence
        // de « Nouvelle PR » au profit de « Vous êtes relecteur ».
        var detector = PullRequestEventDetector.CreateDefault();

        var events = detector.Detect(Build.Context(Build.Pull(
            author: Build.Alice,
            reviewers: [Build.Vote(Build.Viewer)])));

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Kind, Is.EqualTo(NotificationKind.ReviewerAssigned));
    }

    /// <summary>Règle qui lève systématiquement, pour vérifier l'isolation.</summary>
    private sealed class ExplodingRule : IPullRequestEventRule
    {
        public string Name => "Règle défaillante";

        public bool RequiresThreads => false;

        public IEnumerable<PullRequestEvent> Detect(DetectionContext context)
            => throw new InvalidOperationException("Panne simulée.");
    }

    /// <summary>Règle qui émet toujours un événement de clé fixe.</summary>
    private sealed class AlwaysOneEventRule(
        NotificationKind kind = NotificationKind.PullRequestCreated,
        string dedupKey = "fixe") : IPullRequestEventRule
    {
        public string Name => "Règle de test";

        public bool RequiresThreads => false;

        public IEnumerable<PullRequestEvent> Detect(DetectionContext context)
            => [context.CreateEvent(kind, TextRef.Of("Test.Message"), dedupKey: dedupKey)];
    }
}
