using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Monitoring;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Tests.Doubles;

namespace CSharpForgeWatcher.Tests.Monitoring;

/// <summary>
/// Cycle complet, côté pipelines : amorçage, détection, coût des appels, isolation des
/// pannes, purge, et configuration « pipelines seuls ».
/// </summary>
[TestFixture]
public sealed class PipelineMonitoringTests
{
    /// <summary>Monte un harnais surveillant le pipeline de référence.</summary>
    private static MonitorHarness HarnessWatchingPipeline(params PipelineDefinitionRef[] definitions)
    {
        var harness = new MonitorHarness();
        var watched = definitions.Length == 0 ? [Build.Pipeline] : definitions;

        harness.ReconfigureAccount(account =>
            account.Pipelines = watched.Select(WatchedPipeline.From).ToList());

        return harness;
    }

    [Test]
    [Category("SPEC-POLL-001")]
    public async Task Le_premier_cycle_memorise_les_pipelines_sans_notifier()
    {
        var harness = HarnessWatchingPipeline();
        harness.Gateway.WithPipelineRuns(Build.Pipeline.ProjectName, Build.Run(100, PipelineRunResult.Failed));

        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.WasSeeding, Is.True);
            Assert.That(report.Events, Is.Empty);
            Assert.That(harness.State.Pipelines, Has.Count.EqualTo(1));
            Assert.That(report.Pipelines, Has.Count.EqualTo(1));
            Assert.That(report.Pipelines[0].IsFailing, Is.True);
        });
    }

    [Test]
    [Category("SPEC-PIPE-001")]
    public async Task Un_echec_au_cycle_suivant_est_notifie()
    {
        var harness = HarnessWatchingPipeline();
        harness.Gateway.WithPipelineRuns(Build.Pipeline.ProjectName, Build.Run(100, PipelineRunResult.Succeeded));
        await harness.PollAsync();

        harness.Gateway.WithPipelineRuns(
            Build.Pipeline.ProjectName,
            Build.Run(101, PipelineRunResult.Failed),
            Build.Run(100, PipelineRunResult.Succeeded));
        var report = await harness.PollAsync();

        Assert.That(report.Events, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(report.Events[0].Kind, Is.EqualTo(NotificationKind.PipelineFailed));
            Assert.That(harness.Presenter.Shown, Has.Count.EqualTo(1));
            Assert.That(report.Pipelines[0].IsFailing, Is.True);
        });
    }

    [Test]
    [Category("SPEC-PIPE-002")]
    public async Task Le_retour_au_vert_est_notifie()
    {
        var harness = HarnessWatchingPipeline();
        harness.Gateway.WithPipelineRuns(Build.Pipeline.ProjectName, Build.Run(101, PipelineRunResult.Failed));
        await harness.PollAsync();

        harness.Gateway.WithPipelineRuns(
            Build.Pipeline.ProjectName,
            Build.Run(102, PipelineRunResult.Succeeded),
            Build.Run(101, PipelineRunResult.Failed));
        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.Events.Select(e => e.Kind), Does.Contain(NotificationKind.PipelineRecovered));
            Assert.That(report.Pipelines[0].IsFailing, Is.False);
        });
    }

    [Test]
    [Category("SPEC-PIPE-001")]
    public async Task Seule_la_derniere_execution_terminee_est_prise_en_compte()
    {
        var harness = HarnessWatchingPipeline();
        harness.Gateway.WithPipelineRuns(Build.Pipeline.ProjectName, Build.Run(100, PipelineRunResult.Succeeded));
        await harness.PollAsync();

        // Une exécution en cours (103) plus récente que l'échec terminé (102) : c'est
        // l'échec qui doit être signalé, l'exécution en cours n'a pas de résultat.
        harness.Gateway.WithPipelineRuns(
            Build.Pipeline.ProjectName,
            Build.Run(103, PipelineRunResult.Unknown, state: PipelineRunState.InProgress),
            Build.Run(102, PipelineRunResult.Failed));
        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.Events, Has.Count.EqualTo(1));
            Assert.That(report.Events[0].Kind, Is.EqualTo(NotificationKind.PipelineFailed));
            Assert.That(harness.State.Pipelines.Values.Single().LastCompletedRunId, Is.EqualTo(102));
        });
    }

    [Test]
    [Category("SPEC-PIPE-004")]
    public async Task Un_seul_appel_par_projet_quel_que_soit_le_nombre_de_pipelines()
    {
        var deuxieme = new PipelineDefinitionRef(Build.Pipeline.ProjectName, 13, "CI backoffice-web");
        var harness = HarnessWatchingPipeline(Build.Pipeline, deuxieme);
        harness.Gateway.WithPipelineRuns(
            Build.Pipeline.ProjectName,
            Build.Run(100, PipelineRunResult.Succeeded),
            Build.Run(200, PipelineRunResult.Succeeded, deuxieme));

        await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(harness.Gateway.PipelineRunCallCount, Is.EqualTo(1));
            Assert.That(harness.Gateway.Calls, Does.Contain("runs:Backoffice:12,13"));
            Assert.That(harness.State.Pipelines, Has.Count.EqualTo(2));
        });
    }

    [Test]
    [Category("SPEC-PIPE-005")]
    public async Task Un_projet_illisible_conserve_letat_et_reste_affiche()
    {
        var harness = HarnessWatchingPipeline();
        harness.Gateway.WithPipelineRuns(Build.Pipeline.ProjectName, Build.Run(100, PipelineRunResult.Failed));
        await harness.PollAsync();

        harness.Gateway.PipelineFailures[Build.Pipeline.ProjectName] =
            new SourceControlException(TextRef.Of("Test.ForgeError"), 403);
        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(PollStatus.PartialFailure));
            Assert.That(report.Warnings, Has.Count.EqualTo(1));
            Assert.That(harness.State.Pipelines, Has.Count.EqualTo(1), "L'état doit être conservé.");
            Assert.That(report.Pipelines, Has.Count.EqualTo(1), "Le pipeline reste visible avec son dernier état connu.");
            Assert.That(report.Events, Is.Empty);
        });
    }

    [Test]
    [Category("SPEC-PIPE-003")]
    public async Task Retirer_un_pipeline_de_la_configuration_purge_son_etat()
    {
        var harness = HarnessWatchingPipeline();
        harness.Gateway.WithPipelineRuns(Build.Pipeline.ProjectName, Build.Run(100));
        await harness.PollAsync();

        harness.ReconfigureAccount(account => account.Pipelines.Clear());
        await harness.PollAsync();

        Assert.That(harness.State.Pipelines, Is.Empty);
    }

    [Test]
    [Category("SPEC-PIPE-006")]
    public async Task Surveiller_uniquement_des_pipelines_est_une_configuration_valide()
    {
        var harness = new MonitorHarness();
        harness.ReconfigureAccount(account =>
        {
            account.Repositories.Clear();
            account.Pipelines = [WatchedPipeline.From(Build.Pipeline)];
        });
        harness.Gateway.WithPipelineRuns(Build.Pipeline.ProjectName, Build.Run(100, PipelineRunResult.Succeeded));

        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(PollStatus.Success));
            Assert.That(report.PullRequests, Is.Empty);
            Assert.That(report.Pipelines, Has.Count.EqualTo(1));
        });
    }

    [Test]
    [Category("SPEC-NOTIF-003")]
    public async Task Desactiver_la_preference_supprime_la_notification_mais_pas_la_memorisation()
    {
        var harness = HarnessWatchingPipeline();
        harness.Gateway.WithPipelineRuns(Build.Pipeline.ProjectName, Build.Run(100, PipelineRunResult.Succeeded));
        await harness.PollAsync();

        harness.Reconfigure(configuration => configuration.Notifications.PipelineFailed = false);
        harness.Gateway.WithPipelineRuns(Build.Pipeline.ProjectName, Build.Run(101, PipelineRunResult.Failed));
        var report = await harness.PollAsync();

        Assert.Multiple(() =>
        {
            Assert.That(report.Events, Is.Empty);
            Assert.That(harness.Presenter.Shown, Is.Empty);
            Assert.That(
                harness.State.Pipelines.Values.Single().LastCompletedRunId,
                Is.EqualTo(101),
                "L'échec doit être mémorisé pour ne pas être re-détecté plus tard.");
        });
    }
}
