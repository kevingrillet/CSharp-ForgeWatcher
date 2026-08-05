using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Monitoring;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Tests.Doubles;

/// <summary>Horloge figée : les tests ne dépendent jamais de l'heure réelle.</summary>
internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;

    /// <summary>Fait avancer l'horloge, pour tester les fenêtres de rafraîchissement.</summary>
    public void Advance(TimeSpan delta) => UtcNow += delta;
}

/// <summary>Attente instantanée : les tests de réessai ne perdent pas de temps réel.</summary>
internal sealed class ImmediateDelayScheduler : IDelayScheduler
{
    public List<TimeSpan> RequestedDelays { get; } = [];

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        RequestedDelays.Add(delay);
        return Task.CompletedTask;
    }
}

/// <summary>
/// « Chiffrement » réversible et lisible, suffisant pour vérifier que la configuration
/// ne stocke jamais le jeton brut (le vrai chiffrement DPAPI est testé sur machine).
/// </summary>
internal sealed class ReversibleSecretProtector : ISecretProtector
{
    /// <summary>Marque une valeur produite par ce protecteur (comme le fait DPAPI à sa façon).</summary>
    public const string Prefix = "enc:";

    /// <summary>« Chiffre » : le texte encodé ne doit pas contenir le jeton en clair.</summary>
    public string Protect(string plainText)
        => Prefix + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainText));

    public bool TryUnprotect(string protectedText, out string plainText)
    {
        plainText = string.Empty;

        if (!protectedText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            plainText = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(protectedText[Prefix.Length..]));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>Store de configuration en mémoire.</summary>
internal sealed class InMemoryConfigurationStore(WatcherConfiguration? initial = null) : IConfigurationStore
{
    public string Location => "(mémoire)";

    public WatcherConfiguration Configuration { get; private set; } = initial ?? new WatcherConfiguration();

    public int SaveCount { get; private set; }

    public WatcherConfiguration Load() => Configuration;

    public void Save(WatcherConfiguration configuration)
    {
        Configuration = configuration;
        SaveCount++;
    }
}

/// <summary>Store d'état en mémoire.</summary>
internal sealed class InMemoryMonitorStateStore(MonitorSnapshot? initial = null) : IMonitorStateStore
{
    public string Location => "(mémoire)";

    public MonitorSnapshot Snapshot { get; private set; } = initial ?? new MonitorSnapshot();

    public int SaveCount { get; private set; }

    public MonitorSnapshot Load() => Snapshot;

    public void Save(MonitorSnapshot snapshot)
    {
        Snapshot = snapshot;
        SaveCount++;
    }

    public void Clear()
    {
        Snapshot = new MonitorSnapshot();
        SaveCount++;
    }
}

/// <summary>Presenter qui enregistre ce qu'on lui demande d'afficher.</summary>
internal sealed class RecordingNotificationPresenter : INotificationPresenter
{
    public List<INotifiableEvent> Shown { get; } = [];

    public List<IReadOnlyList<INotifiableEvent>> Summaries { get; } = [];

    public List<(TextRef Title, TextRef Message)> Errors { get; } = [];

    public bool LastWasSilent { get; private set; }

    /// <summary>Simule un canal d'affichage défaillant (SPEC-NOTIF-004).</summary>
    public bool ThrowOnShow { get; set; }

    public void ShowEvent(INotifiableEvent notification, bool silent)
    {
        if (ThrowOnShow)
        {
            throw new InvalidOperationException("Canal d'affichage indisponible.");
        }

        LastWasSilent = silent;
        Shown.Add(notification);
    }

    public void ShowSummary(IReadOnlyList<INotifiableEvent> notifications, bool silent)
    {
        if (ThrowOnShow)
        {
            throw new InvalidOperationException("Canal d'affichage indisponible.");
        }

        LastWasSilent = silent;
        Summaries.Add(notifications);
    }

    public void ShowError(TextRef title, TextRef message) => Errors.Add((title, message));
}

/// <summary>
/// Fabrique retournant une passerelle par forge surveillée.
/// </summary>
/// <remarks>
/// Par défaut, une seule passerelle sert toutes les connexions — ce qui suffit à la grande
/// majorité des scénarios. Les tests multi-comptes (SPEC-CFG-008) en déclarent une par
/// fournisseur, afin de vérifier que chaque compte est bien interrogé sur sa propre forge.
/// </remarks>
internal sealed class StubGatewayFactory(ISourceControlGateway gateway) : ISourceControlGatewayFactory
{
    private readonly Dictionary<SourceControlProvider, ISourceControlGateway> _byProvider = [];

    /// <summary>Connexions demandées, dans l'ordre : sert à vérifier qui a été interrogé.</summary>
    public List<SourceControlConnection> Connections { get; } = [];

    /// <summary>Associe une passerelle dédiée à un fournisseur.</summary>
    public StubGatewayFactory With(SourceControlProvider provider, ISourceControlGateway dedicated)
    {
        _byProvider[provider] = dedicated;
        return this;
    }

    public ISourceControlGateway Create(SourceControlConnection connection)
    {
        Connections.Add(connection);
        return _byProvider.TryGetValue(connection.Provider, out var dedicated) ? dedicated : gateway;
    }
}
