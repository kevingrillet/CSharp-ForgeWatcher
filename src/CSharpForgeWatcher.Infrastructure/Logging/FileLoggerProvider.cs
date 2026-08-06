using System.Globalization;
using System.Text;
using CSharpForgeWatcher.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Infrastructure.Logging;

/// <summary>
/// Journal fichier minimaliste, avec rotation (SDD §7).
/// </summary>
/// <remarks>
/// Une application de zone de notification n'a pas de console : sans trace fichier, un
/// incident chez l'utilisateur est indiagnosticable. Le format est volontairement simple
/// (une ligne par entrée) pour être lisible dans le Bloc-notes.
/// <para>
/// Aucune donnée sensible n'est écrite : ni PAT, ni contenu de commentaire — seuls des
/// identifiants et des compteurs.
/// </para>
/// </remarks>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const long MaxFileSizeBytes = 1024 * 1024;

    private readonly string _path;
    private readonly LogLevel _minimumLevel;
    private readonly object _gate = new();

    /// <summary>Construit le fournisseur.</summary>
    /// <param name="path">Chemin du fichier ; <c>null</c> pour l'emplacement standard.</param>
    /// <param name="minimumLevel">Niveau minimal écrit.</param>
    public FileLoggerProvider(string? path = null, LogLevel minimumLevel = LogLevel.Information)
    {
        _path = path ?? AppPaths.LogFile;
        _minimumLevel = minimumLevel;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
        // Rien à libérer : l'écriture ouvre et referme le fichier à chaque entrée, ce qui
        // permet de consulter le journal pendant que l'application tourne.
    }

    private bool IsEnabled(LogLevel level) => level >= _minimumLevel && level != LogLevel.None;

    private void Write(LogLevel level, string category, string message, Exception? exception)
    {
        var line = new StringBuilder()
            .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append(" [").Append(Abbreviate(level)).Append("] ")
            .Append(ShortCategory(category)).Append(" — ")
            .Append(message);

        if (exception is not null)
        {
            line.Append(" | ").Append(exception.GetType().Name).Append(": ").Append(exception.Message);
        }

        lock (_gate)
        {
            try
            {
                AppPaths.EnsureDataDirectory();
                RotateIfNeeded();
                File.AppendAllText(_path, line.Append(Environment.NewLine).ToString());
            }
            catch (Exception writeFailure) when (writeFailure is IOException or UnauthorizedAccessException)
            {
                // Un journal indisponible ne doit jamais faire échouer l'application.
            }
        }
    }

    private void RotateIfNeeded()
    {
        var file = new FileInfo(_path);
        if (!file.Exists || file.Length < MaxFileSizeBytes)
        {
            return;
        }

        File.Move(_path, _path + ".1", overwrite: true);
    }

    private static string Abbreviate(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "AVT",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRI",
        _ => "???",
    };

    /// <summary>« CSharpForgeWatcher.Application.Monitoring.PullRequestMonitor » → « PullRequestMonitor ».</summary>
    private static string ShortCategory(string category)
    {
        var lastDot = category.LastIndexOf('.');
        return lastDot >= 0 && lastDot < category.Length - 1 ? category[(lastDot + 1)..] : category;
    }

    /// <summary>Journal attaché à une catégorie.</summary>
    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => provider.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            provider.Write(logLevel, category, formatter(state, exception), exception);
        }
    }
}
