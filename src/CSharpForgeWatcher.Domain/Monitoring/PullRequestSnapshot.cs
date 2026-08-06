using CSharpForgeWatcher.Domain.Identity;
using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Domain.Monitoring;

/// <summary>
/// Mémoire d'une discussion entre deux cycles.
/// </summary>
/// <remarks>
/// POCO muable : ce type est sérialisé tel quel en JSON dans <c>state.json</c>.
/// C'est un choix assumé, distinct des modèles de domaine immuables
/// (cf. <see cref="CommentThread"/>).
/// </remarks>
public sealed class CommentThreadSnapshot
{
    /// <summary>Identifiant de la discussion (entier 64 bits, SPEC-FORGE-006).</summary>
    public long Id { get; set; }

    /// <summary>Dernier état connu.</summary>
    public CommentThreadStatus Status { get; set; } = CommentThreadStatus.Unknown;

    /// <summary>
    /// Identifiants des commentaires déjà vus : c'est cette liste — et non une date —
    /// qui définit ce qui est « nouveau » (ADR-0003).
    /// </summary>
    public List<long> CommentIds { get; set; } = [];

    /// <summary>Vrai si l'utilisateur a écrit dans cette discussion (SPEC-EVT-005).</summary>
    public bool ViewerParticipates { get; set; }

    /// <summary>Construit l'instantané d'une discussion observée.</summary>
    public static CommentThreadSnapshot From(CommentThread thread, string viewerId) => new()
    {
        Id = thread.Id,
        Status = thread.Status,
        CommentIds = thread.Comments.Select(c => c.Id).ToList(),
        ViewerParticipates = thread.HasParticipant(viewerId),
    };

    /// <summary>Indique si le commentaire avait déjà été vu.</summary>
    public bool Knows(long commentId) => CommentIds.Contains(commentId);
}

/// <summary>
/// Mémoire d'une pull request entre deux cycles : tout ce que la détection doit comparer.
/// </summary>
/// <remarks>POCO muable, sérialisé en JSON (voir <see cref="CommentThreadSnapshot"/>).</remarks>
public sealed class PullRequestSnapshot
{
    /// <summary>Numéro de la PR.</summary>
    public int Id { get; set; }

    /// <summary>Nom du projet (nécessaire pour reconstruire les URL sans appel réseau).</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Identifiant du dépôt.</summary>
    public string RepositoryId { get; set; } = string.Empty;

    /// <summary>Nom du dépôt.</summary>
    public string RepositoryName { get; set; } = string.Empty;

    /// <summary>Titre au dernier cycle.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Identifiant de l'auteur.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>Nom affiché de l'auteur.</summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>État au dernier cycle.</summary>
    public PullRequestStatus Status { get; set; } = PullRequestStatus.Active;

    /// <summary>Brouillon au dernier cycle.</summary>
    public bool IsDraft { get; set; }

    /// <summary>Vrai si l'utilisateur figurait parmi les relecteurs (SPEC-EVT-002).</summary>
    public bool ViewerIsReviewer { get; set; }

    /// <summary>Votes connus, par identifiant de relecteur (SPEC-EVT-003).</summary>
    public Dictionary<string, ReviewerVote> ReviewerVotes { get; set; } = [];

    /// <summary>Noms affichés des relecteurs, pour libeller une notification sans appel réseau.</summary>
    public Dictionary<string, string> ReviewerNames { get; set; } = [];

    /// <summary>Discussions connues, par identifiant.</summary>
    public Dictionary<long, CommentThreadSnapshot> Threads { get; set; } = [];

    /// <summary>Date de la dernière lecture des discussions (pilote le rafraîchissement, SPEC-POLL-003).</summary>
    public DateTimeOffset? ThreadsReadOn { get; set; }

    /// <summary>Date de la dernière observation de cette PR.</summary>
    public DateTimeOffset LastSeenOn { get; set; }

    /// <summary>Dépôt d'origine, reconstruit depuis l'instantané.</summary>
    public RepositoryRef ToRepositoryRef() => new(ProjectName, RepositoryId, RepositoryName);

    /// <summary>Clé de la PR.</summary>
    public PullRequestKey ToKey() => new(RepositoryId, Id);

    /// <summary>Vote mémorisé pour un relecteur.</summary>
    public ReviewerVote VoteOf(string reviewerId)
        => ReviewerVotes.TryGetValue(reviewerId, out var vote) ? vote : ReviewerVote.NoVote;

    /// <summary>Instantané mémorisé d'une discussion, ou <c>null</c> si elle est nouvelle.</summary>
    public CommentThreadSnapshot? FindThread(long threadId)
        => Threads.TryGetValue(threadId, out var thread) ? thread : null;

    /// <summary>Vrai si l'utilisateur participe à au moins une discussion connue.</summary>
    public bool ViewerParticipatesInAnyThread => Threads.Values.Any(t => t.ViewerParticipates);

    /// <summary>
    /// Construit l'instantané d'une PR observée.
    /// </summary>
    /// <param name="observation">Observation du cycle courant.</param>
    /// <param name="viewerId">Identité de l'utilisateur.</param>
    /// <param name="observedOn">Horodatage du cycle.</param>
    /// <param name="previous">
    /// Instantané précédent : ses discussions sont conservées quand elles n'ont pas été
    /// relues à ce cycle (cf. <see cref="PullRequestObservation.ThreadsWereRead"/>).
    /// </param>
    public static PullRequestSnapshot From(
        PullRequestObservation observation,
        string viewerId,
        DateTimeOffset observedOn,
        PullRequestSnapshot? previous = null)
    {
        var pullRequest = observation.PullRequest;
        var snapshot = new PullRequestSnapshot
        {
            Id = pullRequest.Id,
            ProjectName = pullRequest.Repository.ProjectName,
            RepositoryId = pullRequest.Repository.RepositoryId,
            RepositoryName = pullRequest.Repository.RepositoryName,
            Title = pullRequest.Title,
            AuthorId = pullRequest.Author.Id,
            AuthorName = pullRequest.Author.SafeDisplayName,
            Status = pullRequest.Status,
            IsDraft = pullRequest.IsDraft,
            ViewerIsReviewer = pullRequest.FindReviewer(viewerId) is not null,
            LastSeenOn = observedOn,
        };

        foreach (var reviewer in pullRequest.Reviewers)
        {
            if (string.IsNullOrEmpty(reviewer.User.Id))
            {
                continue;
            }

            snapshot.ReviewerVotes[reviewer.User.Id] = reviewer.Vote;
            snapshot.ReviewerNames[reviewer.User.Id] = reviewer.User.SafeDisplayName;
        }

        if (observation.Threads is { } threads)
        {
            foreach (var thread in threads.Where(t => !t.IsDeleted))
            {
                snapshot.Threads[thread.Id] = CommentThreadSnapshot.From(thread, viewerId);
            }

            snapshot.ThreadsReadOn = observedOn;
        }
        else if (previous is not null)
        {
            // Discussions non relues à ce cycle : on conserve la mémoire précédente,
            // sinon tous les commentaires seraient re-notifiés à la prochaine lecture.
            snapshot.Threads = previous.Threads;
            snapshot.ThreadsReadOn = previous.ThreadsReadOn;
        }

        return snapshot;
    }

    /// <summary>Nombre de discussions ouvertes connues (indicateur affiché dans le menu).</summary>
    public int UnresolvedThreadCount
        => Threads.Values.Count(t => t.Status == CommentThreadStatus.Active);

    /// <summary>Auteur mémorisé, sous forme de <see cref="UserRef"/>.</summary>
    public UserRef ToAuthorRef() => new(AuthorId, AuthorName);
}
