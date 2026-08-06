using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Domain.Events;

/// <summary>
/// Événement détecté sur une pull request : c'est l'unité que l'application notifie et
/// liste dans la fenêtre d'activité.
/// </summary>
/// <remarks>
/// Un événement porte tout ce qu'il faut pour être affiché et cliqué : aucune couche
/// supérieure n'a besoin de retourner interroger Azure DevOps.
/// </remarks>
public sealed record PullRequestEvent : INotifiableEvent
{
    /// <summary>Type d'événement (détermine le titre et le filtre de préférence).</summary>
    public required NotificationKind Kind { get; init; }

    /// <summary>Pull request concernée.</summary>
    public required PullRequestKey Key { get; init; }

    /// <summary>Dépôt concerné, pour l'affichage et le regroupement.</summary>
    public required RepositoryRef Repository { get; init; }

    /// <summary>Titre de la pull request.</summary>
    public required string PullRequestTitle { get; init; }

    /// <summary>Corps du message de notification, désigné par sa clé et ses arguments.</summary>
    public required TextRef Message { get; init; }

    /// <summary>
    /// URL à ouvrir au clic : la discussion visée s'il s'agit d'un commentaire,
    /// la pull request sinon (SPEC-NOTIF-001).
    /// </summary>
    public required string Url { get; init; }

    /// <summary>Date de l'événement (ou de sa détection, à défaut de date d'API exploitable).</summary>
    public required DateTimeOffset OccurredOn { get; init; }

    /// <summary>Personne à l'origine de l'événement, si elle est identifiable.</summary>
    public string? ActorName { get; init; }

    /// <summary>Discussion concernée, si l'événement en cible une.</summary>
    public long? ThreadId { get; init; }

    /// <summary>
    /// Libellé du compte de forge d'origine ; vide quand un seul compte est surveillé.
    /// </summary>
    public string AccountLabel { get; init; } = string.Empty;

    /// <summary>
    /// Clé de déduplication : deux événements de même clé décrivent le même fait et
    /// ne doivent être notifiés qu'une fois. Renseignée par les règles de détection ;
    /// à défaut, la valeur est dérivée du type et de la PR.
    /// </summary>
    public string DedupKey { get; init; } = string.Empty;

    /// <summary>Numéro de la PR.</summary>
    public int PullRequestId => Key.PullRequestId;

    /// <summary>Titre de notification, ex. « Commentaire sur votre PR ».</summary>
    public TextRef Title => TextRef.Of(TextKeys.KindLabel(Kind));

    /// <summary>Sujet « !1234 — titre de la pull request ».</summary>
    public TextRef Subject
        => TextRef.Of(TextKeys.Event.PullRequestSubject, PullRequestId, PullRequestTitle);

    /// <summary>
    /// Ligne de contexte « projet / dépôt • !1234 », précédée du compte quand plusieurs
    /// forges sont surveillées — sans quoi deux dépôts homonymes seraient indistinguables.
    /// </summary>
    public string Context => string.IsNullOrEmpty(AccountLabel)
        ? $"{Repository.DisplayPath} • !{PullRequestId}"
        : $"{AccountLabel} • {Repository.DisplayPath} • !{PullRequestId}";

    /// <summary>Clé effective de déduplication.</summary>
    public string EffectiveDedupKey
        => string.IsNullOrEmpty(DedupKey) ? $"{Kind}|{Key}" : DedupKey;
}
