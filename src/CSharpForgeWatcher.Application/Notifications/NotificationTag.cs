namespace CSharpForgeWatcher.Application.Notifications;

/// <summary>
/// Réduit une clé de déduplication à une étiquette courte, sans perdre son pouvoir de
/// distinction (SPEC-NOTIF-002).
/// </summary>
/// <remarks>
/// <para>
/// Les canaux de notification imposent une longueur maximale à l'étiquette qui identifie un
/// message — 64 caractères pour les toasts Windows. Deux messages de même étiquette sont
/// considérés comme le même fait : le second remplace le premier au lieu de s'empiler.
/// </para>
/// <para>
/// <b>Pourquoi hacher plutôt que tronquer.</b> Une clé de déduplication commence par
/// l'identifiant du compte (32 caractères), suivi du type d'événement et de la clé de
/// l'élément concerné. Tronquer par la fin efface donc le compte, et tronquer par le début
/// efface l'élément : dans les deux cas deux événements distincts finissent par se confondre.
/// Sur Azure DevOps, dont les identifiants de dépôt sont des GUID, la clé dépasse
/// systématiquement la limite — deux comptes surveillant le même dépôt n'auraient produit
/// qu'un seul toast, alors que l'ADR-0005 prévoit qu'ils notifient chacun de leur côté.
/// </para>
/// <para>
/// L'empreinte est prise sur la clé entière, par <see cref="StableHash"/> : ce qui compte
/// est qu'un même fait ré-affiché retombe sur la même étiquette d'une exécution à l'autre.
/// </para>
/// </remarks>
public static class NotificationTag
{
    /// <summary>Longueur maximale acceptée par les toasts Windows.</summary>
    public const int MaxLength = StableHash.Length;

    /// <summary>
    /// Étiquette représentant une clé de déduplication, d'au plus
    /// <see cref="MaxLength"/> caractères.
    /// </summary>
    /// <remarks>
    /// Une clé assez courte est reprise telle quelle : elle reste lisible dans les
    /// diagnostics, et l'empreinte n'apporterait rien.
    /// </remarks>
    public static string For(string? dedupKey)
    {
        var key = dedupKey ?? string.Empty;

        return key.Length <= MaxLength ? key : StableHash.Of(key);
    }
}
