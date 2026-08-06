using CSharpForgeWatcher.Application.Detection.Rules;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Tests.Doubles;

using static CSharpForgeWatcher.Tests.Doubles.Texte;

namespace CSharpForgeWatcher.Tests.Detection;

/// <summary>SPEC-EVT-008 — discussion résolue ou réactivée.</summary>
[TestFixture]
[Category("SPEC-EVT-008")]
public sealed class ThreadStatusChangedRuleTests
{
    private readonly ThreadStatusChangedRule _rule = new();

    [Test]
    public void La_resolution_dune_discussion_ou_jai_ecrit_est_signalee()
    {
        var pullRequest = Build.Pull(author: Build.Alice);
        var before = Build.Thread(3, CommentThreadStatus.Active, [Build.Comment(1, Build.Viewer, "À revoir")]);
        var after = Build.Thread(3, CommentThreadStatus.Fixed, [Build.Comment(1, Build.Viewer, "À revoir")]);

        var events = _rule.Detect(
            Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after])).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(events[0].Kind, Is.EqualTo(NotificationKind.ThreadStatusChanged));
            Assert.That(Fr(events[0].Message), Does.Contain("Corrigé"));
            Assert.That(events[0].ThreadId, Is.EqualTo(3));
        });
    }

    [Test]
    public void La_reactivation_dune_discussion_est_signalee()
    {
        var pullRequest = Build.Pull(author: Build.Viewer);
        var before = Build.Thread(3, CommentThreadStatus.Fixed, [Build.Comment(1, Build.Alice)]);
        var after = Build.Thread(3, CommentThreadStatus.Active, [Build.Comment(1, Build.Alice)]);

        var message = Fr(_rule.Detect(
                Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after]))
            .Single().Message);

        Assert.That(message, Does.Contain("réactivée"));
    }

    [Test]
    public void Un_etat_inchange_ne_produit_rien()
    {
        var pullRequest = Build.Pull(author: Build.Viewer);
        var thread = Build.Thread(3, CommentThreadStatus.Active, [Build.Comment(1, Build.Alice)]);

        Assert.That(
            _rule.Detect(Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [thread]), threads: [thread])),
            Is.Empty);
    }

    [Test]
    public void Une_discussion_inconnue_est_traitee_par_la_regle_des_commentaires()
    {
        var pullRequest = Build.Pull(author: Build.Viewer);
        var thread = Build.Thread(3, CommentThreadStatus.Fixed, [Build.Comment(1, Build.Alice)]);

        Assert.That(
            _rule.Detect(Build.Context(pullRequest, previous: Build.Snapshot(pullRequest), threads: [thread])),
            Is.Empty);
    }

    [Test]
    public void Une_discussion_qui_ne_me_concerne_pas_est_ignoree()
    {
        var pullRequest = Build.Pull(author: Build.Alice, reviewers: [Build.Vote(Build.Viewer)]);
        var before = Build.Thread(3, CommentThreadStatus.Active, [Build.Comment(1, Build.Bob)]);
        var after = Build.Thread(3, CommentThreadStatus.Fixed, [Build.Comment(1, Build.Bob)]);

        Assert.That(
            _rule.Detect(Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after])),
            Is.Empty);
    }

    [Test]
    [Category("SPEC-FORGE-007")]
    public void Une_forge_qui_nexpose_pas_letat_des_discussions_rend_la_regle_muette()
    {
        // GitHub ne publie la résolution d'un fil que dans son API GraphQL : l'adaptateur
        // laisse donc l'état inconnu, et cette règle se tait sans qu'aucun code conditionnel
        // n'ait à connaître la forge. C'est le comportement attendu d'une capacité absente.
        var pullRequest = Build.Pull(author: Build.Viewer);
        var before = Build.Thread(3, CommentThreadStatus.Unknown, [Build.Comment(1, Build.Alice)]);
        var after = Build.Thread(3, CommentThreadStatus.Unknown, [Build.Comment(1, Build.Alice), Build.Comment(2, Build.Bob)]);

        Assert.That(
            _rule.Detect(Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after])),
            Is.Empty);
    }

    [Test]
    public void Les_discussions_purement_systeme_sont_ignorees()
    {
        var pullRequest = Build.Pull(author: Build.Viewer);
        var before = Build.Thread(3, CommentThreadStatus.Active, [Build.Comment(1, Build.Alice, isSystem: true)]);
        var after = Build.Thread(3, CommentThreadStatus.Closed, [Build.Comment(1, Build.Alice, isSystem: true)]);

        Assert.That(
            _rule.Detect(Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after])),
            Is.Empty);
    }
}
