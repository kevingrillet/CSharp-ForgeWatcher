using CSharpForgeWatcher.Application.Detection.Rules;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Tests.Doubles;

using static CSharpForgeWatcher.Tests.Doubles.Texte;

namespace CSharpForgeWatcher.Tests.Detection;

/// <summary>SPEC-EVT-003 — vote d'un relecteur sur une PR de l'utilisateur.</summary>
[TestFixture]
[Category("SPEC-EVT-003")]
public sealed class VoteChangedRuleTests
{
    private readonly VoteChangedRule _rule = new();

    [Test]
    public void Une_approbation_sur_ma_PR_est_signalee()
    {
        var before = Build.Pull(author: Build.Viewer, reviewers: [Build.Vote(Build.Alice)]);
        var after = Build.Pull(author: Build.Viewer, reviewers: [Build.Vote(Build.Alice, ReviewerVote.Approved)]);

        var events = _rule.Detect(Build.Context(after, previous: Build.Snapshot(before))).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(events[0].Kind, Is.EqualTo(NotificationKind.VoteChanged));
            Assert.That(Fr(events[0].Message), Is.EqualTo("Alice a approuvé"));
        });
    }

    [Test]
    public void Une_attente_de_correction_est_signalee()
    {
        var before = Build.Pull(author: Build.Viewer, reviewers: [Build.Vote(Build.Alice)]);
        var after = Build.Pull(author: Build.Viewer, reviewers: [Build.Vote(Build.Alice, ReviewerVote.WaitingForAuthor)]);

        var message = Fr(_rule.Detect(Build.Context(after, previous: Build.Snapshot(before))).Single().Message);

        Assert.That(message, Does.Contain("attend une correction"));
    }

    [Test]
    public void Sans_etat_precedent_aucun_vote_nest_nouveau()
    {
        var pullRequest = Build.Pull(author: Build.Viewer, reviewers: [Build.Vote(Build.Alice, ReviewerVote.Approved)]);

        Assert.That(_rule.Detect(Build.Context(pullRequest)), Is.Empty);
    }

    [Test]
    public void Les_votes_sur_la_PR_dun_collegue_sont_ignores()
    {
        var before = Build.Pull(author: Build.Alice, reviewers: [Build.Vote(Build.Bob)]);
        var after = Build.Pull(author: Build.Alice, reviewers: [Build.Vote(Build.Bob, ReviewerVote.Approved)]);

        Assert.That(_rule.Detect(Build.Context(after, previous: Build.Snapshot(before))), Is.Empty);
    }

    [Test]
    public void Un_relecteur_ajoute_sans_vote_nest_pas_un_vote()
    {
        var before = Build.Pull(author: Build.Viewer);
        var after = Build.Pull(author: Build.Viewer, reviewers: [Build.Vote(Build.Alice)]);

        Assert.That(_rule.Detect(Build.Context(after, previous: Build.Snapshot(before))), Is.Empty);
    }

    [Test]
    public void Mon_propre_vote_ne_me_notifie_pas()
    {
        var before = Build.Pull(author: Build.Viewer, reviewers: [Build.Vote(Build.Viewer)]);
        var after = Build.Pull(author: Build.Viewer, reviewers: [Build.Vote(Build.Viewer, ReviewerVote.Approved)]);

        Assert.That(_rule.Detect(Build.Context(after, previous: Build.Snapshot(before))), Is.Empty);
    }

    [Test]
    public void Deux_votes_differents_produisent_deux_evenements_distincts()
    {
        var before = Build.Pull(author: Build.Viewer, reviewers: [Build.Vote(Build.Alice), Build.Vote(Build.Bob)]);
        var after = Build.Pull(
            author: Build.Viewer,
            reviewers: [Build.Vote(Build.Alice, ReviewerVote.Approved), Build.Vote(Build.Bob, ReviewerVote.Rejected)]);

        var events = _rule.Detect(Build.Context(after, previous: Build.Snapshot(before))).ToList();

        Assert.That(events, Has.Count.EqualTo(2));
        Assert.That(
            events.Select(e => e.EffectiveDedupKey).Distinct().Count(),
            Is.EqualTo(2),
            "Deux votes distincts doivent avoir deux clés de déduplication distinctes.");
    }
}
