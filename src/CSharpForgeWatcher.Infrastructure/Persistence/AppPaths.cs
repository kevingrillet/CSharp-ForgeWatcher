namespace CSharpForgeWatcher.Infrastructure.Persistence;

/// <summary>
/// Emplacements des fichiers de l'application (SPEC-CFG-005).
/// </summary>
/// <remarks>
/// Tout est sous <c>%APPDATA%\ForgeWatcher</c> : itinérant avec le profil utilisateur,
/// aucun droit administrateur requis, et rien à côté de l'exécutable (qui peut être
/// déployé en lecture seule).
/// </remarks>
public static class AppPaths
{
    /// <summary>Nom du dossier de données.</summary>
    public const string FolderName = "ForgeWatcher";

    /// <summary>Dossier de données de l'application.</summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        FolderName);

    /// <summary>Fichier de configuration utilisateur.</summary>
    public static string ConfigurationFile => Path.Combine(DataDirectory, "config.json");

    /// <summary>Fichier d'état surveillé (mémoire de la détection).</summary>
    public static string StateFile => Path.Combine(DataDirectory, "state.json");

    /// <summary>Journal applicatif.</summary>
    public static string LogFile => Path.Combine(DataDirectory, "log.txt");

    /// <summary>Crée le dossier de données s'il n'existe pas.</summary>
    public static string EnsureDataDirectory()
    {
        Directory.CreateDirectory(DataDirectory);
        return DataDirectory;
    }
}
