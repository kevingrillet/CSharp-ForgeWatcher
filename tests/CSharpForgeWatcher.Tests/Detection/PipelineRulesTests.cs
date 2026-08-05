using CSharpForgeWatcher.Application.Detection.Pipelines;
using CSharpForgeWatcher.Application.Detection.Pipelines.Rules;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Tests.Doubles;

using static CSharpForgeWatcher.Tests.Doubles.Texte;

namespace CSharpForgeWatcher.Tests.Detection;

/// <summary>SPEC-PIPE-001 — un pipeline surveillé passe en échec.</summary>
[TestFixture]
[Category("SPEC-PIPE-001")]
public sealed class PipelineFailedRuleTests
{
    private readonly PipelineFailedRule _rule = new();

    [Test]
    public void Un_echec_apres_un_succes_est_signale()
    {
        var context = Build.PipelineContext(
            Build.Run(101, PipelineRunResult.Failed),
            Build.PipelineState(100, PipelineRunResult.Succeeded));

        var events = _rule.Detect(context).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(events[0].Kind, Is.EqualTo(NotificationKind.PipelineFailed));
            Assert.That(Fr(events[0].Message), Does.Contain("échec"));
            Assert.That(Fr(events[0].Subject), Does.Contain("CI backoffice-api"));
            Assert.That(events[0].Url, Does.Contain("buildId=101"));
        });
    }

    [Test]
    public void Un_succes_partiel_compte_comme_un_echec()
    {
        var context = Build.PipelineContext(
            Build.Run(101, PipelineRunResult.PartiallySucceeded),
            Build.PipelineState(100, PipelineRunResult.Succeeded));

        Assert.That(Fr(_rule.Detect(context).Single().Message), Does.Contain("succès partiel"));
    }

    [Test]
    public void Une_execution_annulee_nest_pas_un_echec()
    {
        var context = Build.PipelineContext(
            Build.Run(101, PipelineRunResult.Canceled),
            Build.PipelineState(100, PipelineRunResult.Succeeded));

        Assert.That(_rule.Detect(context), Is.Empty);
    }

    [Test]
    public void Une_execution_en_cours_ne_notifie_rien()
    {
        var context = Build.PipelineContext(
            Build.Run(101, PipelineRunResult.Unknown, state: PipelineRunState.InProgress),
            Build.PipelineState(100, PipelineRunResult.Succeeded));

        Assert.That(_rule.Detect(context), Is.Empty);
    }

    [Test]
    public void La_meme_execution_nest_pas_notifiee_deux_fois()
    {
        // L'exécution 101 est déjà celle mémorisée : rien de nouveau.
        var context = Build.PipelineContext(
            Build.Run(101, PipelineRunResult.Failed),
            Build.PipelineState(101, PipelineRunResult.Failed));

        Assert.That(_rule.Detect(context), Is.Empty);
    }

    [Test]
    public void Deux_echecs_consecutifs_produisent_deux_evenements_distincts()
    {
        var premier = _rule.Detect(Build.PipelineContext(
            Build.Run(101, PipelineRunResult.Failed),
            Build.PipelineState(100, PipelineRunResult.Succeeded))).Single();

        var second = _rule.Detect(Build.PipelineContext(
            Build.Run(102, PipelineRunResult.Failed),
            Build.PipelineState(101, PipelineRunResult.Failed))).Single();

        Assert.That(premier.EffectiveDedupKey, Is.Not.EqualTo(second.EffectiveDedupKey));
    }

    [Test]
    public void Un_pipeline_decouvert_est_memorise_sans_alerter()
    {
        var context = Build.PipelineContext(Build.Run(101, PipelineRunResult.Failed), previous: null);

        Assert.That(_rule.Detect(context), Is.Empty);
    }
}

/// <summary>SPEC-PIPE-002 — retour au vert.</summary>
[TestFixture]
[Category("SPEC-PIPE-002")]
public sealed class PipelineRecoveredRuleTests
{
    private readonly PipelineRecoveredRule _rule = new();

    [Test]
    public void Un_succes_apres_un_echec_est_signale()
    {
        var context = Build.PipelineContext(
            Build.Run(102, PipelineRunResult.Succeeded),
            Build.PipelineState(101, PipelineRunResult.Failed));

        var events = _rule.Detect(context).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(events[0].Kind, Is.EqualTo(NotificationKind.PipelineRecovered));
            Assert.That(Fr(events[0].Message), Does.Contain("succès"));
        });
    }

    [Test]
    public void Deux_succes_de_suite_ne_notifient_rien()
    {
        var context = Build.PipelineContext(
            Build.Run(102, PipelineRunResult.Succeeded),
            Build.PipelineState(101, PipelineRunResult.Succeeded));

        Assert.That(_rule.Detect(context), Is.Empty);
    }

    [Test]
    public void Un_echec_qui_persiste_ne_notifie_pas_de_retour_au_vert()
    {
        var context = Build.PipelineContext(
            Build.Run(102, PipelineRunResult.Failed),
            Build.PipelineState(101, PipelineRunResult.Failed));

        Assert.That(_rule.Detect(context), Is.Empty);
    }
}

/// <summary>Composition des règles de pipeline.</summary>
[TestFixture]
public sealed class PipelineEventDetectorTests
{
    [Test]
    [Category("SPEC-PIPE-001")]
    [Category("SPEC-PIPE-002")]
    public void Le_detecteur_standard_couvre_lechec_et_le_retour_au_vert()
    {
        var detector = PipelineEventDetector.CreateDefault();

        var echec = detector.Detect(Build.PipelineContext(
            Build.Run(101, PipelineRunResult.Failed),
            Build.PipelineState(100, PipelineRunResult.Succeeded)));

        var retour = detector.Detect(Build.PipelineContext(
            Build.Run(102, PipelineRunResult.Succeeded),
            Build.PipelineState(101, PipelineRunResult.Failed)));

        Assert.Multiple(() =>
        {
            Assert.That(echec.Single().Kind, Is.EqualTo(NotificationKind.PipelineFailed));
            Assert.That(retour.Single().Kind, Is.EqualTo(NotificationKind.PipelineRecovered));
        });
    }

    [Test]
    public void Un_detecteur_sans_regle_est_refuse()
    {
        Assert.That(
            () => new PipelineEventDetector(Array.Empty<IPipelineEventRule>()),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    [Category("SPEC-FORGE-006")]
    public void Les_identifiants_dexecution_dune_forge_tiennent_sur_64_bits()
    {
        // Les identifiants d'exécution de GitHub Actions dépassent les dix chiffres. Tronqués,
        // deux exécutions successives pourraient se confondre — donc un échec passer inaperçu.
        var definition = new PipelineDefinitionRef("mon-organisation/backoffice-api", 3_000_000_000L, "CI");
        var previous = Build.PipelineState(12_345_678_900L, PipelineRunResult.Succeeded, definition);
        var run = Build.Run(12_345_678_901L, PipelineRunResult.Failed, definition);

        var events = new PipelineEventDetector(PipelineEventDetector.CreateDefaultRules())
            .Detect(Build.PipelineContext(run, previous))
            .ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(definition.DefinitionId, Is.GreaterThan(int.MaxValue));
            Assert.That(events[0].Kind, Is.EqualTo(NotificationKind.PipelineFailed));
            Assert.That(
                previous.Key,
                Is.EqualTo("mon-organisation/backoffice-api:3000000000"),
                "La clé persistée doit restituer l'identifiant sans perte.");
            Assert.That(events[0].EffectiveDedupKey, Does.Contain("12345678901"));
        });
    }
}
