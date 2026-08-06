using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Infrastructure.Persistence;

/// <summary>
/// Lecture / écriture d'un document JSON sur disque, avec les garanties attendues des
/// stores de l'application : ne jamais lever, ne jamais perdre le fichier existant.
/// </summary>
/// <remarks>
/// <para>
/// Écriture atomique : le contenu est d'abord écrit dans un fichier temporaire, puis
/// déplacé sur le fichier cible. Une coupure de courant en pleine écriture laisse donc
/// l'ancien fichier intact plutôt qu'un JSON tronqué.
/// </para>
/// <para>
/// Lecture tolérante : un fichier illisible est mis de côté en <c>.corrupt</c> et la
/// valeur par défaut est retournée (SPEC-CFG-005). L'application démarre toujours.
/// </para>
/// </remarks>
/// <typeparam name="TDocument">Type du document sérialisé.</typeparam>
internal sealed class JsonFileStore<TDocument>
    where TDocument : class
{
    /// <summary>
    /// Options communes : sortie indentée et énumérations en clair, pour qu'un
    /// utilisateur curieux puisse lire et corriger les fichiers à la main.
    /// </summary>
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly ILogger? _logger;
    private readonly object _gate = new();

    public JsonFileStore(string path, ILogger? logger)
    {
        _path = path;
        _logger = logger;
    }

    public string Location => _path;

    /// <summary>Charge le document, ou retourne <paramref name="createDefault"/> si besoin.</summary>
    public TDocument Load(Func<TDocument> createDefault)
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(_path))
                {
                    return createDefault();
                }

                var json = File.ReadAllText(_path);

                return string.IsNullOrWhiteSpace(json)
                    ? createDefault()
                    : JsonSerializer.Deserialize<TDocument>(json, SerializerOptions) ?? createDefault();
            }
            catch (JsonException exception)
            {
                _logger?.LogError(exception, "Fichier {Path} illisible : mise de côté et reprise à zéro.", _path);
                QuarantineCorruptFile();
                return createDefault();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger?.LogError(exception, "Lecture de {Path} impossible : valeurs par défaut utilisées.", _path);
                return createDefault();
            }
        }
    }

    /// <summary>Enregistre le document de façon atomique.</summary>
    public void Save(TDocument document)
    {
        lock (_gate)
        {
            try
            {
                AppPaths.EnsureDataDirectory();
                var temporaryPath = _path + ".tmp";
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document, SerializerOptions));

                // Move avec écrasement : opération atomique sur le même volume.
                File.Move(temporaryPath, _path, overwrite: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Un échec d'écriture ne doit pas interrompre la surveillance : au pire,
                // l'état sera réamorcé au prochain démarrage.
                _logger?.LogError(exception, "Écriture de {Path} impossible.", _path);
            }
        }
    }

    /// <summary>Supprime le fichier (réinitialisation).</summary>
    public void Delete()
    {
        lock (_gate)
        {
            try
            {
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger?.LogError(exception, "Suppression de {Path} impossible.", _path);
            }
        }
    }

    private void QuarantineCorruptFile()
    {
        try
        {
            var target = $"{_path}.corrupt";
            File.Move(_path, target, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(exception, "Mise de côté de {Path} impossible.", _path);
        }
    }
}
