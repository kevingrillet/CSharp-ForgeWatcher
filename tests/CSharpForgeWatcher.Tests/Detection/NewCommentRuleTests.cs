using CSharpForgeWatcher.Application.Detection;
using CSharpForgeWatcher.Application.Detection.Rules;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Identity;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Tests.Doubles;

using static CSharpForgeWatcher.Tests.Doubles.Texte;

namespace CSharpForgeWatcher.Tests.Detection;

/// <summary>
/// SPEC-EVT-004 à SPEC-EVT-007 — commentaires : sur ma PR, réponse, mention,
/// PR que je relis.
/// </summary>
[TestFixture]
public sealed class NewCommentRuleTests
{
    private readonly NewCommentRule _rule = new();

    [Test]
    [Category("SPEC-EVT-004")]
    public void Un_nouveau_commentaire_sur_ma_PR_est_signale_et_pointe_la_discussion()
    {
        var pullRequest = Build.Pull(author: Build.Viewer);
        var before = Build.Thread(7, comments: [Build.Comment(1, Build.Alice)]);
        var after = Build.Thread(7, comments: [Build.Comment(1, Build.Alice), Build.Comment(2, Build.Bob, "Et ici ?")]);

        var events = _rule.Detect(
            Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after])).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(events[0].Kind, Is.EqualTo(NotificationKind.CommentOnMyPullRequest));
            Assert.That(Fr(events[0].Message), Does.Contain("Bob").And.Contain("Et ici ?"));
            Assert.That(events[0].ThreadId, Is.EqualTo(7));
            Assert.That(events[0].Url, Does.EndWith("/pullrequest/42?discussionId=7"));
        });
    }

    [Test]
    [Category("SPEC-EVT-004")]
    public void Une_discussion_entierement_nouvelle_sur_ma_PR_est_signalee()
    {
        var pullRequest = Build.Pull(author: Build.Viewer);
        var thread = Build.Thread(9, comments: [Build.Comment(1, Build.Alice, "Attention au fuseau.")]);

        var events = _rule.Detect(
            Build.Context(pullRequest, previous: Build.Snapshot(pullRequest), threads: [thread])).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Kind, Is.EqualTo(NotificationKind.CommentOnMyPullRequest));
    }

    [Test]
    [Category("SPEC-EVT-005")]
    public void Une_reponse_dans_une_discussion_ou_jai_ecrit_est_signalee_comme_reponse()
    {
        // PR d'Alice : sans ma participation, cet événement n'existerait pas.
        var pullRequest = Build.Pull(author: Build.Alice);
        var before = Build.Thread(3, comments: [Build.Comment(1, Build.Viewer, "Peux-tu extraire cette méthode ?")]);
        var after = Build.Thread(3, comments:
        [
            Build.Comment(1, Build.Viewer, "Peux-tu extraire cette méthode ?"),
            Build.Comment(2, Build.Alice, "C'est fait."),
        ]);

        var events = _rule.Detect(
            Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after])).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Kind, Is.EqualTo(NotificationKind.ReplyToMyComment));
    }

    [Test]
    [Category("SPEC-EVT-006")]
    public void Une_mention_est_prioritaire_sur_les_autres_intitules()
    {
        var pullRequest = Build.Pull(author: Build.Viewer);
        var before = Build.Thread(4, comments: [Build.Comment(1, Build.Alice)]);
        var after = Build.Thread(4, comments:
        [
            Build.Comment(1, Build.Alice),
            Build.Comment(2, Build.Bob, $"@<{Build.ViewerId}> ton avis ?"),
        ]);

        var events = _rule.Detect(
            Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after])).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Kind, Is.EqualTo(NotificationKind.MentionedInComment));
    }

    [Test]
    [Category("SPEC-EVT-006")]
    public void Un_identifiant_qui_nest_pas_une_mention_ne_declenche_rien()
    {
        // Une identité GitHub est un mot ordinaire (« dev ») : sans exiger le « @» et une
        // fin de mot, la moindre prose la contenant passerait pour une mention.
        var reviewer = new UserRef("dev", "Dev");
        var pullRequest = Build.Pull(author: Build.Alice, reviewers: [Build.Vote(reviewer)]);
        var before = Build.Thread(4);
        var after = Build.Thread(4, comments:
        [
            Build.Comment(1, Build.Bob, "Le dossier dev/ contient un devlog et un développeur fantôme."),
        ]);

        var events = _rule.Detect(new DetectionContext
        {
            ViewerId = "dev",
            Observation = new PullRequestObservation(pullRequest, [after]),
            Previous = Build.Snapshot(pullRequest, [before]),
            ObservedOn = Build.Now,
            Links = Build.Links,
        }).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(
            events[0].Kind,
            Is.EqualTo(NotificationKind.CommentOnReviewedPullRequest),
            "L'événement existe parce que je relis la PR, mais ce n'est pas une mention.");
    }

    [Test]
    [Category("SPEC-EVT-006")]
    public void Une_mention_par_identifiant_lisible_est_detectee()
    {
        // Forme employée par GitHub : « @login », sans chevrons.
        var pullRequest = Build.Pull(author: Build.Alice);
        var before = Build.Thread(4);
        var after = Build.Thread(4, comments: [Build.Comment(1, Build.Bob, "@camille peux-tu regarder ?")]);

        var events = _rule.Detect(new DetectionContext
        {
            ViewerId = "camille",
            Observation = new PullRequestObservation(pullRequest, [after]),
            Previous = Build.Snapshot(pullRequest, [before]),
            ObservedOn = Build.Now,
            Links = Build.Links,
        }).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Kind, Is.EqualTo(NotificationKind.MentionedInComment));
    }

    [Test]
    [Category("SPEC-LINK-004")]
    public void Ladresse_fournie_par_la_forge_est_preferee_a_ladresse_reconstruite()
    {
        // GitHub livre l'ancre exacte de chaque message, et sous trois formes différentes :
        // la deviner serait inutile et fragile.
        const string Anchor = "https://github.com/mon-organisation/backoffice-api/pull/42#issuecomment-2100000000";

        var pullRequest = Build.Pull(author: Build.Viewer);
        var before = Build.Thread(-1);
        var after = Build.Thread(-1, comments: [Build.Comment(2_100_000_000L, Build.Bob, "Et ici ?", url: Anchor)]);

        var events = _rule.Detect(
            Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after])).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Url, Is.EqualTo(Anchor));
    }

    [Test]
    [Category("SPEC-FORGE-006")]
    public void Un_commentaire_dont_lidentifiant_depasse_32_bits_nest_notifie_quune_fois()
    {
        // Le risque n'est pas une exception mais un silence : un identifiant tronqué
        // ressemblerait à un identifiant déjà connu, et le message ne serait jamais notifié.
        const long BigId = 4_294_967_296L;
        var pullRequest = Build.Pull(author: Build.Viewer);
        var thread = Build.Thread(1, comments: [Build.Comment(BigId, Build.Bob, "Message à identifiant large.")]);

        var firstCycle = _rule.Detect(
            Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [Build.Thread(1)]), threads: [thread]));

        // Deuxième cycle : le message figure désormais dans l'instantané.
        var secondCycle = _rule.Detect(
            Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [thread]), threads: [thread]));

        Assert.Multiple(() =>
        {
            Assert.That(BigId, Is.GreaterThan(int.MaxValue));
            Assert.That(firstCycle.Count(), Is.EqualTo(1));
            Assert.That(secondCycle, Is.Empty);
        });
    }

    [Test]
    [Category("SPEC-EVT-007")]
    public void Un_commentaire_sur_une_PR_que_je_relis_est_signale()
    {
        var pullRequest = Build.Pull(author: Build.Alice, reviewers: [Build.Vote(Build.Viewer)]);
        var before = Build.Thread(5);
        var after = Build.Thread(5, comments: [Build.Comment(1, Build.Bob, "Le nommage est ambigu.")]);

        var events = _rule.Detect(
            Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after])).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Kind, Is.EqualTo(NotificationKind.CommentOnReviewedPullRequest));
    }

    [Test]
    [Category("SPEC-EVT-007")]
    public void Un_commentaire_sur_une_PR_qui_ne_me_concerne_pas_est_ignore()
    {
        var pullRequest = Build.Pull(author: Build.Alice, reviewers: [Build.Vote(Build.Bob)]);
        var before = Build.Thread(6);
        var after = Build.Thread(6, comments: [Build.Comment(1, Build.Bob)]);

        var events = _rule.Detect(
            Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after]));

        Assert.That(events, Is.Empty);
    }

    [Test]
    [Category("SPEC-EVT-004")]
    public void Les_commentaires_systeme_sont_ignores()
    {
        var pullRequest = Build.Pull(author: Build.Viewer);
        var before = Build.Thread(2);
        var after = Build.Thread(2, comments: [Build.Comment(1, Build.Alice, "Alice a voté 10", isSystem: true)]);

        var events = _rule.Detect(
            Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after]));

        Assert.That(events, Is.Empty);
    }

    [Test]
    [Category("SPEC-EVT-004")]
    public void Les_commentaires_supprimes_sont_ignores()
    {
        var pullRequest = Build.Pull(author: Build.Viewer);
        var before = Build.Thread(2);
        var after = Build.Thread(2, comments: [Build.Comment(1, Build.Alice, isDeleted: true)]);

        Assert.That(
            _rule.Detect(Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after])),
            Is.Empty);
    }

    [Test]
    [Category("SPEC-EVT-004")]
    public void Mes_propres_commentaires_ne_me_notifient_pas()
    {
        var pullRequest = Build.Pull(author: Build.Viewer);
        var before = Build.Thread(2);
        var after = Build.Thread(2, comments: [Build.Comment(1, Build.Viewer, "Note pour moi.")]);

        Assert.That(
            _rule.Detect(Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after])),
            Is.Empty);
    }

    [Test]
    [Category("SPEC-EVT-004")]
    public void Plusieurs_messages_dans_la_meme_discussion_donnent_un_seul_evenement()
    {
        var pullRequest = Build.Pull(author: Build.Viewer);
        var before = Build.Thread(8);
        var after = Build.Thread(8, comments:
        [
            Build.Comment(1, Build.Alice, "Premier"),
            Build.Comment(2, Build.Alice, "Deuxième"),
            Build.Comment(3, Build.Bob, "Troisième"),
        ]);

        var events = _rule.Detect(
            Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after])).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(Fr(events[0].Message), Does.Contain("Troisième"), "Le dernier message doit être mis en avant.");
            Assert.That(Fr(events[0].Message), Does.Contain("+2"), "Les autres messages doivent être comptés.");
        });
    }

    [Test]
    [Category("SPEC-POLL-003")]
    public void Sans_lecture_des_discussions_la_regle_se_tait()
    {
        var pullRequest = Build.Pull(author: Build.Viewer);

        // threads = null : les discussions n'ont pas été lues à ce cycle.
        Assert.That(
            _rule.Detect(Build.Context(pullRequest, previous: Build.Snapshot(pullRequest))),
            Is.Empty);
    }

    [Test]
    [Category("SPEC-POLL-001")]
    public void Sans_etat_precedent_la_regle_se_tait()
    {
        var pullRequest = Build.Pull(author: Build.Viewer);
        var thread = Build.Thread(1, comments: [Build.Comment(1, Build.Alice)]);

        Assert.That(_rule.Detect(Build.Context(pullRequest, threads: [thread])), Is.Empty);
    }

    [Test]
    [Category("SPEC-EVT-004")]
    public void Le_fichier_commente_est_indique()
    {
        var pullRequest = Build.Pull(author: Build.Viewer);
        var before = Build.Thread(11, filePath: "/src/Facturation/Calculateur.cs");
        var after = Build.Thread(
            11,
            comments: [Build.Comment(1, Build.Alice)],
            filePath: "/src/Facturation/Calculateur.cs");

        var message = Fr(_rule.Detect(
                Build.Context(pullRequest, previous: Build.Snapshot(pullRequest, [before]), threads: [after]))
            .Single().Message);

        Assert.That(message, Does.Contain("Calculateur.cs"));
    }
}
