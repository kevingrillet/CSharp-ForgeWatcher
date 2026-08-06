using CSharpForgeWatcher.Application.Detection.Rules;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Tests.Doubles;

using static CSharpForgeWatcher.Tests.Doubles.Texte;

namespace CSharpForgeWatcher.Tests.Detection;

/// <summary>SPEC-EVT-002 — ajout comme relecteur.</summary>
[TestFixture]
[Category("SPEC-EVT-002")]
public sealed class ReviewerAssignedRuleTests
{
    private readonly ReviewerAssignedRule _rule = new();

    [Test]
    public void Etre_relecteur_dune_PR_inconnue_est_signale()
    {
        var context = Build.Context(Build.Pull(
            author: Build.Alice,
            reviewers: [Build.Vote(Build.Viewer)]));

        var events = _rule.Detect(context).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(events[0].Kind, Is.EqualTo(NotificationKind.ReviewerAssigned));
            Assert.That(Fr(events[0].Message), Does.Contain("Alice"));
        });
    }

    [Test]
    public void Etre_ajoute_relecteur_sur_une_PR_connue_est_signale()
    {
        var before = Build.Pull(author: Build.Alice);
        var after = Build.Pull(author: Build.Alice, reviewers: [Build.Vote(Build.Viewer)]);

        var events = _rule.Detect(Build.Context(after, previous: Build.Snapshot(before))).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
    }

    [Test]
    public void Etre_deja_relecteur_nest_pas_une_nouveaute()
    {
        var pullRequest = Build.Pull(author: Build.Alice, reviewers: [Build.Vote(Build.Viewer)]);

        var events = _rule.Detect(Build.Context(pullRequest, previous: Build.Snapshot(pullRequest)));

        Assert.That(events, Is.Empty);
    }

    [Test]
    public void Ne_pas_etre_relecteur_ne_produit_rien()
    {
        var context = Build.Context(Build.Pull(author: Build.Alice, reviewers: [Build.Vote(Build.Bob)]));

        Assert.That(_rule.Detect(context), Is.Empty);
    }

    [Test]
    public void Etre_relecteur_de_sa_propre_PR_ne_produit_rien()
    {
        var context = Build.Context(Build.Pull(author: Build.Viewer, reviewers: [Build.Vote(Build.Viewer)]));

        Assert.That(_rule.Detect(context), Is.Empty);
    }

    [Test]
    public void Le_caractere_obligatoire_est_precise()
    {
        var context = Build.Context(Build.Pull(
            author: Build.Alice,
            reviewers: [Build.Vote(Build.Viewer, ReviewerVote.NoVote, required: true)]));

        Assert.That(Fr(_rule.Detect(context).Single().Message), Does.Contain("obligatoire"));
    }
}
