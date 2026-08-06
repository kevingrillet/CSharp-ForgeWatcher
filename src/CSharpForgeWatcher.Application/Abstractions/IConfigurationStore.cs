using CSharpForgeWatcher.Application.Configuration;

namespace CSharpForgeWatcher.Application.Abstractions;

/// <summary>
/// Persistance de la configuration utilisateur (patron Repository).
/// </summary>
/// <remarks>
/// L'implémentation ne doit **jamais** lever : une configuration absente ou corrompue
/// retourne les valeurs par défaut, afin que l'application démarre toujours
/// (SPEC-CFG-005).
/// </remarks>
public interface IConfigurationStore
{
    /// <summary>Chemin du fichier, affiché dans l'onglet « Avancé ».</summary>
    string Location { get; }

    /// <summary>Charge la configuration, ou les valeurs par défaut si elle est absente.</summary>
    WatcherConfiguration Load();

    /// <summary>Enregistre la configuration.</summary>
    void Save(WatcherConfiguration configuration);
}

/// <summary>
/// Persistance de l'état surveillé (patron Repository).
/// </summary>
/// <remarks>
/// Même contrat de robustesse que <see cref="IConfigurationStore"/> : un état illisible
/// équivaut à un état vide, ce qui provoque un simple ré-amorçage silencieux
/// (SPEC-POLL-001).
/// </remarks>
public interface IMonitorStateStore
{
    /// <summary>Chemin du fichier d'état.</summary>
    string Location { get; }

    /// <summary>Charge l'état mémorisé, ou un état vide.</summary>
    Domain.Monitoring.MonitorSnapshot Load();

    /// <summary>Enregistre l'état.</summary>
    void Save(Domain.Monitoring.MonitorSnapshot snapshot);

    /// <summary>Efface l'état : le cycle suivant réamorce sans notifier.</summary>
    void Clear();
}
