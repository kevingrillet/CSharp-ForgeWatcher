using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Domain.Monitoring;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Infrastructure.Persistence;

/// <summary>
/// Configuration utilisateur persistée en JSON dans <c>%APPDATA%</c> (SPEC-CFG-005).
/// </summary>
public sealed class JsonConfigurationStore : IConfigurationStore
{
    private readonly JsonFileStore<WatcherConfiguration> _file;

    /// <summary>Construit le store sur le chemin indiqué.</summary>
    /// <param name="path">Chemin du fichier ; <c>null</c> pour l'emplacement standard.</param>
    /// <param name="logger">Journal, facultatif.</param>
    public JsonConfigurationStore(string? path = null, ILogger<JsonConfigurationStore>? logger = null)
        => _file = new JsonFileStore<WatcherConfiguration>(path ?? AppPaths.ConfigurationFile, logger);

    /// <inheritdoc />
    public string Location => _file.Location;

    /// <inheritdoc />
    public WatcherConfiguration Load() => _file.Load(static () => new WatcherConfiguration());

    /// <inheritdoc />
    public void Save(WatcherConfiguration configuration) => _file.Save(configuration);
}

/// <summary>
/// État surveillé persisté en JSON : la mémoire qui rend la détection idempotente
/// (ADR-0003).
/// </summary>
public sealed class JsonMonitorStateStore : IMonitorStateStore
{
    private readonly JsonFileStore<MonitorSnapshot> _file;

    /// <summary>Construit le store sur le chemin indiqué.</summary>
    /// <param name="path">Chemin du fichier ; <c>null</c> pour l'emplacement standard.</param>
    /// <param name="logger">Journal, facultatif.</param>
    public JsonMonitorStateStore(string? path = null, ILogger<JsonMonitorStateStore>? logger = null)
        => _file = new JsonFileStore<MonitorSnapshot>(path ?? AppPaths.StateFile, logger);

    /// <inheritdoc />
    public string Location => _file.Location;

    /// <inheritdoc />
    public MonitorSnapshot Load() => _file.Load(static () => new MonitorSnapshot());

    /// <inheritdoc />
    public void Save(MonitorSnapshot snapshot) => _file.Save(snapshot);

    /// <inheritdoc />
    public void Clear() => _file.Delete();
}
