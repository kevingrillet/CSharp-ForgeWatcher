using CSharpForgeWatcher.Application.Detection.Rules;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Tests.Doubles;

using static CSharpForgeWatcher.Tests.Doubles.Texte;

namespace CSharpForgeWatcher.Tests.Detection;

/// <summary>SPEC-EVT-001 — nouvelle pull request créée.</summary>
[TestFixture]
[Category("SPEC-EVT-001")]
public sealed class NewPullRequestRuleTests
{
    private readonly NewPullRequestRule _rule = new();

    [Test]
    public void Une_PR_inconnue_ouverte_par_un_autre_est_signalee()
    {
        var context = Build.Context(Build.Pull(id: 77, author: Build.Alice));

        var events = _rule.Detect(context).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(events[0].Kind, Is.EqualTo(NotificationKind.PullRequestCreated));
            Assert.That(Fr(events[0].Message), Does.Contain("Alice"));
            Assert.That(events[0].Url, Is.EqualTo("https://dev.azure.com/contoso/Backoffice/_git/backoffice-api/pullrequest/77"));
        });
    }

    [Test]
    public void Une_PR_deja_connue_nest_plus_une_nouveaute()
    {
        var pullRequest = Build.Pull(author: Build.Alice);
        var context = Build.Context(pullRequest, previous: Build.Snapshot(pullRequest));

        Assert.That(_rule.Detect(context), Is.Empty);
    }

    [Test]
    public void Ma_propre_PR_ne_me_notifie_pas()
    {
        var context = Build.Context(Build.Pull(author: Build.Viewer));

        Assert.That(_rule.Detect(context), Is.Empty);
    }

    [Test]
    public void Ma_propre_PR_me_notifie_si_je_lai_demande()
    {
        var context = Build.Context(Build.Pull(author: Build.Viewer), notifyOwnActions: true);

        Assert.That(_rule.Detect(context).Count(), Is.EqualTo(1));
    }

    [Test]
    public void Si_je_suis_deja_relecteur_la_regle_laisse_la_main()
    {
        // SPEC-EVT-001 règle 2 : « Vous êtes relecteur » est plus actionnable.
        var context = Build.Context(Build.Pull(
            author: Build.Alice,
            reviewers: [Build.Vote(Build.Viewer)]));

        Assert.That(_rule.Detect(context), Is.Empty);
    }

    [Test]
    public void Une_PR_decouverte_deja_close_nest_pas_annoncee()
    {
        var context = Build.Context(Build.Pull(author: Build.Alice, status: PullRequestStatus.Completed));

        Assert.That(_rule.Detect(context), Is.Empty);
    }
}
