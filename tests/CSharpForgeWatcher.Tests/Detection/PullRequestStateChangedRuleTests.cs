using CSharpForgeWatcher.Application.Detection.Rules;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Tests.Doubles;

using static CSharpForgeWatcher.Tests.Doubles.Texte;

namespace CSharpForgeWatcher.Tests.Detection;

/// <summary>SPEC-EVT-009 — changement d'état d'une pull request.</summary>
[TestFixture]
[Category("SPEC-EVT-009")]
public sealed class PullRequestStateChangedRuleTests
{
    private readonly PullRequestStateChangedRule _rule = new();

    [Test]
    public void La_completion_de_ma_PR_est_signalee()
    {
        var before = Build.Pull(author: Build.Viewer);
        var after = Build.Pull(author: Build.Viewer, status: PullRequestStatus.Completed);

        var events = _rule.Detect(Build.Context(after, previous: Build.Snapshot(before))).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(events[0].Kind, Is.EqualTo(NotificationKind.PullRequestStateChanged));
            Assert.That(Fr(events[0].Message), Does.Contain("complétée"));
        });
    }

    [Test]
    public void Labandon_dune_PR_que_je_relis_est_signale()
    {
        var before = Build.Pull(author: Build.Alice, reviewers: [Build.Vote(Build.Viewer)]);
        var after = Build.Pull(
            author: Build.Alice,
            status: PullRequestStatus.Abandoned,
            reviewers: [Build.Vote(Build.Viewer)]);

        var message = Fr(_rule.Detect(Build.Context(after, previous: Build.Snapshot(before))).Single().Message);

        Assert.That(message, Does.Contain("abandonnée"));
    }

    [Test]
    public void La_publication_dun_brouillon_est_signalee()
    {
        var before = Build.Pull(author: Build.Alice, isDraft: true, reviewers: [Build.Vote(Build.Viewer)]);
        var after = Build.Pull(author: Build.Alice, isDraft: false, reviewers: [Build.Vote(Build.Viewer)]);

        var events = _rule.Detect(Build.Context(after, previous: Build.Snapshot(before))).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(Fr(events[0].Message), Does.Contain("Brouillon publié"));
    }

    [Test]
    public void Une_PR_qui_ne_me_concerne_pas_est_ignoree()
    {
        var before = Build.Pull(author: Build.Alice, reviewers: [Build.Vote(Build.Bob)]);
        var after = Build.Pull(
            author: Build.Alice,
            status: PullRequestStatus.Completed,
            reviewers: [Build.Vote(Build.Bob)]);

        Assert.That(_rule.Detect(Build.Context(after, previous: Build.Snapshot(before))), Is.Empty);
    }

    [Test]
    public void Une_PR_ou_jai_seulement_commente_me_concerne()
    {
        // Participation à une discussion : suffit à être « impliqué ».
        var thread = Build.Thread(1, comments: [Build.Comment(1, Build.Viewer, "Attention à la migration.")]);
        var before = Build.Pull(author: Build.Alice);
        var after = Build.Pull(author: Build.Alice, status: PullRequestStatus.Completed);

        var events = _rule.Detect(Build.Context(after, previous: Build.Snapshot(before, [thread])));

        Assert.That(events.Count(), Is.EqualTo(1));
    }

    [Test]
    public void Sans_etat_precedent_aucun_changement_nest_detectable()
    {
        Assert.That(
            _rule.Detect(Build.Context(Build.Pull(author: Build.Viewer, status: PullRequestStatus.Completed))),
            Is.Empty);
    }
}
