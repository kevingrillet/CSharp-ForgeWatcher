using CSharpForgeWatcher.Infrastructure.Persistence;
using Microsoft.Win32;

namespace CSharpForgeWatcher.Infrastructure.Startup;

/// <summary>
/// Ce que la reprise de l'ancienne identité a effectivement fait.
/// </summary>
/// <remarks>
/// La reprise a lieu avant que le conteneur — donc le journal — n'existe : elle rend compte
/// par cet objet, que l'appelant journalise une fois le service disponible.
/// </remarks>
/// <param name="Applied">Reprises effectuées, en français, prêtes à journaliser.</param>
/// <param name="Failures">Reprises tentées sans succès. Aucune n'empêche le démarrage.</param>
public sealed record LegacyMigrationReport(IReadOnlyList<string> Applied, IReadOnlyList<string> Failures)
{
    /// <summary>Vrai s'il n'y avait rien à reprendre.</summary>
    public bool IsEmpty => Applied.Count == 0 && Failures.Count == 0;
}

/// <summary>
/// Reprend les données et les enregistrements Windows laissés par « PR Watcher », nom porté
/// par l'application jusqu'à la version 1.1.0 (SPEC-CFG-005).
/// </summary>
/// <remarks>
/// <para>
/// Le renommage déplace trois choses que Windows et l'utilisateur voient : le dossier de
/// données, la valeur de démarrage automatique et le raccourci du menu Démarrer créé pour
/// les toasts. Sans reprise, l'utilisateur retrouverait une application vierge — jeton à
/// ressaisir, dépôts à recocher — et une entrée de démarrage désignant un exécutable
/// disparu.
/// </para>
/// <para>
/// Le jeton chiffré, lui, reste lisible : DPAPI est lié au compte Windows, pas au nom de
/// l'application (ADR-0002). Déplacer le dossier suffit donc à tout conserver.
/// </para>
/// <para>
/// La reprise est <b>idempotente</b> : relancée, elle ne trouve plus rien et ne fait rien.
/// Elle doit être appelée <b>avant</b> toute lecture de la configuration et avant
/// l'ouverture du journal — l'un comme l'autre créent le nouveau dossier, ce qui la ferait
/// renoncer en croyant à une installation neuve.
/// </para>
/// </remarks>
public static class LegacyIdentityMigration
{
    /// <summary>Nom porté par l'application avant le renommage.</summary>
    private const string LegacyName = "PrWatcher";

    /// <summary>Reprend ce qui subsiste de l'ancienne identité.</summary>
    /// <remarks>
    /// Aucune étape ne peut interrompre le démarrage : un échec est consigné dans le rapport
    /// et l'application continue, quitte à repartir d'une configuration vierge.
    /// </remarks>
    public static LegacyMigrationReport Run()
    {
        var applied = new List<string>();
        var failures = new List<string>();

        MoveDataDirectory(applied, failures);
        MoveAutoStartEntry(applied, failures);
        RemoveStartMenuShortcut(applied, failures);

        return new LegacyMigrationReport(applied, failures);
    }

    /// <summary>Renomme <c>%APPDATA%\PrWatcher</c> en <c>%APPDATA%\ForgeWatcher</c>.</summary>
    /// <remarks>
    /// Un dossier neuf déjà présent l'emporte : il signifie que la nouvelle version a déjà
    /// tourné, et l'écraser ferait perdre ce qu'elle a appris depuis.
    /// </remarks>
    private static void MoveDataDirectory(List<string> applied, List<string> failures)
    {
        var legacyDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            LegacyName);

        if (!Directory.Exists(legacyDirectory) || Directory.Exists(AppPaths.DataDirectory))
        {
            return;
        }

        try
        {
            Directory.Move(legacyDirectory, AppPaths.DataDirectory);
            applied.Add($"Données reprises depuis « {legacyDirectory} ».");
        }
        catch (Exception exception)
        {
            failures.Add(
                $"Reprise des données impossible depuis « {legacyDirectory} » : {exception.Message}");
        }
    }

    /// <summary>Réinscrit le démarrage automatique sous le nouveau nom, puis retire l'ancien.</summary>
    /// <remarks>
    /// L'ancienne valeur désigne un exécutable qui n'existe plus : on la remplace par le
    /// chemin du processus courant plutôt que de la recopier telle quelle.
    /// </remarks>
    private static void MoveAutoStartEntry(List<string> applied, List<string> failures)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                RegistryAutoStartService.RunKeyPath,
                writable: true);

            if (key?.GetValue(LegacyName) is not string)
            {
                return;
            }

            // Ne pas écraser un démarrage déjà configuré sous le nouveau nom.
            if (key.GetValue(RegistryAutoStartService.ValueName) is null
                && Environment.ProcessPath is { } executablePath)
            {
                key.SetValue(RegistryAutoStartService.ValueName, $"\"{executablePath}\"");
            }

            key.DeleteValue(LegacyName, throwOnMissingValue: false);
            applied.Add("Démarrage avec Windows repris sous le nouveau nom.");
        }
        catch (Exception exception)
        {
            failures.Add($"Reprise du démarrage automatique impossible : {exception.Message}");
        }
    }

    /// <summary>
    /// Supprime le raccourci du menu Démarrer créé par la bibliothèque de toasts pour
    /// l'ancien nom.
    /// </summary>
    /// <remarks>
    /// Ce raccourci est ce qui identifie l'application auprès du centre de notifications.
    /// Celui du nouveau nom est recréé automatiquement au premier toast ; l'ancien, lui, ne
    /// servirait plus qu'à afficher une entrée fantôme dans le menu Démarrer.
    /// </remarks>
    private static void RemoveStartMenuShortcut(List<string> applied, List<string> failures)
    {
        var shortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            $"{LegacyName}.lnk");

        if (!File.Exists(shortcut))
        {
            return;
        }

        try
        {
            File.Delete(shortcut);
            applied.Add("Raccourci du menu Démarrer de l'ancien nom supprimé.");
        }
        catch (Exception exception)
        {
            failures.Add($"Suppression de « {shortcut} » impossible : {exception.Message}");
        }
    }
}
