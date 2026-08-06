using CSharpForgeWatcher.Application.Links;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Identity;
using CSharpForgeWatcher.Domain.Monitoring;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Detection;

/// <summary>
/// Tout ce dont une règle de détection a besoin pour se prononcer sur une pull request :
/// ce qui est observé maintenant, ce qui était mémorisé avant, et qui est l'utilisateur.
/// </summary>
/// <remarks>
/// Objet de contexte volontairement riche en propriétés calculées : les règles restent
/// alors très courtes et se lisent comme les specs qu'elles implémentent.
/// Aucune règle ne lit l'horloge ni le réseau (ADR-0003) : tout passe par ce contexte.
/// </remarks>
public sealed class DetectionContext
{
    /// <summary>Identité de l'utilisateur de l'application, sur le compte observé.</summary>
    public required string ViewerId { get; init; }

    /// <summary>
    /// Compte de forge dont provient l'observation (SPEC-CFG-008).
    /// </summary>
    /// <remarks>
    /// Entre dans les clés de déduplication : deux comptes peuvent surveiller le même dépôt —
    /// un jeton personnel et un jeton professionnel sur le même serveur — et le même fait doit
    /// alors être notifié pour chacun.
    /// </remarks>
    public string AccountId { get; init; } = string.Empty;

    /// <summary>
    /// Libellé du compte, affiché dans la notification. Vide quand un seul compte est
    /// configuré : le préciser n'apporterait alors que du bruit.
    /// </summary>
    public string AccountLabel { get; init; } = string.Empty;

    /// <summary>Ce qui a été observé lors de ce cycle.</summary>
    public required PullRequestObservation Observation { get; init; }

    /// <summary>État mémorisé au cycle précédent, ou <c>null</c> si la PR est découverte.</summary>
    public PullRequestSnapshot? Previous { get; init; }

    /// <summary>Horodatage du cycle, utilisé à défaut de date exploitable côté API.</summary>
    public required DateTimeOffset ObservedOn { get; init; }

    /// <summary>Vrai si l'utilisateur veut être notifié de ses propres actions.</summary>
    public bool NotifyOwnActions { get; init; }

    /// <summary>Générateur d'URL, pour rendre les événements cliquables.</summary>
    public required IPullRequestLinkBuilder Links { get; init; }

    /// <summary>Pull request observée.</summary>
    public PullRequest PullRequest => Observation.PullRequest;

    /// <summary>Discussions lues à ce cycle, ou <c>null</c> si elles ne l'ont pas été.</summary>
    public IReadOnlyList<CommentThread>? Threads => Observation.Threads;

    /// <summary>Dépôt de la PR.</summary>
    public RepositoryRef Repository => PullRequest.Repository;

    /// <summary>Clé de la PR.</summary>
    public PullRequestKey Key => PullRequest.Key;

    /// <summary>Vrai si la PR n'était pas connue : aucune comparaison n'est possible.</summary>
    public bool IsFirstSight => Previous is null;

    /// <summary>Vrai si l'utilisateur est l'auteur de la PR.</summary>
    public bool ViewerIsAuthor => PullRequest.IsAuthoredBy(ViewerId);

    /// <summary>Vrai si l'utilisateur figure parmi les relecteurs.</summary>
    public bool ViewerIsReviewer => PullRequest.FindReviewer(ViewerId) is not null;

    /// <summary>Vrai si l'utilisateur a écrit dans au moins une discussion (connue ou observée).</summary>
    public bool ViewerParticipatesInThreads
        => (Threads?.Any(t => t.HasParticipant(ViewerId)) ?? false)
           || (Previous?.ViewerParticipatesInAnyThread ?? false);

    /// <summary>
    /// Vrai si la PR concerne l'utilisateur à un titre quelconque : auteur, relecteur ou
    /// participant à une discussion. Sert de garde-fou aux règles « bruyantes ».
    /// </summary>
    public bool ViewerIsInvolved => ViewerIsAuthor || ViewerIsReviewer || ViewerParticipatesInThreads;

    /// <summary>Vrai si la référence désigne l'utilisateur.</summary>
    public bool IsViewer(UserRef user) => user.Is(ViewerId);

    /// <summary>
    /// Vrai si l'action de cette personne doit être ignorée : c'est l'utilisateur lui-même
    /// et il n'a pas demandé à être notifié de ses propres actions.
    /// </summary>
    public bool ShouldIgnoreActor(UserRef actor) => IsViewer(actor) && !NotifyOwnActions;

    /// <summary>
    /// Fabrique un événement rattaché à cette PR.
    /// </summary>
    /// <param name="kind">Type d'événement.</param>
    /// <param name="message">Corps du message (le titre vient du type, le contexte du dépôt).</param>
    /// <param name="occurredOn">Date de l'événement ; l'horodatage du cycle est utilisé à défaut.</param>
    /// <param name="actorName">Personne à l'origine de l'événement.</param>
    /// <param name="threadId">Discussion visée : l'URL pointera dessus (SPEC-LINK-002).</param>
    /// <param name="dedupKey">Clé de déduplication ; à défaut, dérivée du type et de la PR.</param>
    /// <param name="url">
    /// Adresse fournie par la forge. Renseignée, elle est utilisée telle quelle plutôt que
    /// reconstruite (SPEC-LINK-004).
    /// </param>
    public PullRequestEvent CreateEvent(
        NotificationKind kind,
        TextRef message,
        DateTimeOffset? occurredOn = null,
        string? actorName = null,
        long? threadId = null,
        string? dedupKey = null,
        string? url = null)
        => new()
        {
            Kind = kind,
            Key = Key,
            Repository = Repository,
            PullRequestTitle = PullRequest.Title,
            Message = message,
            Url = !string.IsNullOrWhiteSpace(url) ? url
                : threadId is { } id ? Links.ForThread(Repository, PullRequest.Id, id)
                : Links.ForPullRequest(Repository, PullRequest.Id),
            OccurredOn = occurredOn is { } date && date != default ? date : ObservedOn,
            ActorName = actorName,
            ThreadId = threadId,
            AccountLabel = AccountLabel,

            // Le compte fait partie de la clé : les règles n'ont donc pas à s'en soucier, et
            // deux comptes surveillant le même dépôt notifient chacun de leur côté.
            DedupKey = $"{AccountId}|{dedupKey ?? $"{kind}|{Key}"}",
        };
}
