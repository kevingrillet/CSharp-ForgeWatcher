using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Resilience;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Tests.Doubles;

namespace CSharpForgeWatcher.Tests.Monitoring;

/// <summary>SPEC-POLL-005 — réessai des erreurs transitoires uniquement.</summary>
[TestFixture]
[Category("SPEC-POLL-005")]
public sealed class ResilientSourceControlGatewayTests
{
    [Test]
    public async Task Une_erreur_transitoire_est_reessayee_jusquau_succes()
    {
        var inner = new FlakyGateway(new SourceControlException(TextRef.Of("Test.ForgeError"), 503), failures: 2);
        var delays = new ImmediateDelayScheduler();
        var gateway = new ResilientSourceControlGateway(inner, delays, maxAttempts: 3);

        var viewer = await gateway.GetViewerAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(viewer.Id, Is.EqualTo(Build.ViewerId));
            Assert.That(inner.Attempts, Is.EqualTo(3));
            Assert.That(delays.RequestedDelays, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Lattente_croit_de_maniere_exponentielle()
    {
        var inner = new FlakyGateway(new SourceControlException(TextRef.Of("Test.ForgeError"), 429), failures: 99);
        var delays = new ImmediateDelayScheduler();
        var gateway = new ResilientSourceControlGateway(
            inner,
            delays,
            maxAttempts: 4,
            initialBackoff: TimeSpan.FromSeconds(1));

        Assert.That(async () => await gateway.GetViewerAsync(CancellationToken.None),
            Throws.TypeOf<SourceControlException>());

        Assert.That(delays.RequestedDelays, Is.EqualTo(new[]
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4),
        }));
    }

    [Test]
    public void Une_erreur_dauthentification_nest_jamais_reessayee()
    {
        var inner = new FlakyGateway(new SourceControlException(TextRef.Of("Test.ForgeError"), 401), failures: 99);
        var delays = new ImmediateDelayScheduler();
        var gateway = new ResilientSourceControlGateway(inner, delays);

        Assert.That(async () => await gateway.GetViewerAsync(CancellationToken.None),
            Throws.TypeOf<SourceControlException>());

        Assert.Multiple(() =>
        {
            Assert.That(inner.Attempts, Is.EqualTo(1));
            Assert.That(delays.RequestedDelays, Is.Empty);
        });
    }

    [Test]
    public void Une_panne_reseau_sans_code_HTTP_est_consideree_transitoire()
    {
        var exception = new SourceControlException(TextRef.Of("Test.ForgeError"));

        Assert.Multiple(() =>
        {
            Assert.That(exception.IsTransient, Is.True);
            Assert.That(exception.IsAuthenticationFailure, Is.False);
        });
    }

    [Test]
    public void Un_404_nest_pas_transitoire()
    {
        Assert.That(new SourceControlException(TextRef.Of("Test.ForgeError"), 404).IsTransient, Is.False);
    }
}
