using CSharpForgeWatcher.Domain.Events;

namespace CSharpForgeWatcher.Application.Configuration;

/// <summary>
/// Activation, type par type, des notifications (SPEC-NOTIF-003).
/// </summary>
/// <remarks>
/// Une propriété par type plutôt qu'un dictionnaire : la configuration reste lisible dans
/// <c>config.json</c>, et l'ajout d'un type se voit à la compilation dans
/// <see cref="IsEnabled"/> — le compilateur rappelle qu'il faut traiter le nouveau cas.
/// </remarks>
public sealed class NotificationPreferences
{
    /// <summary>Notifier les nouvelles PR des dépôts surveillés (SPEC-EVT-001).</summary>
    public bool PullRequestCreated { get; set; } = true;

    /// <summary>Notifier quand on m'ajoute comme relecteur (SPEC-EVT-002).</summary>
    public bool ReviewerAssigned { get; set; } = true;

    /// <summary>Notifier les votes sur mes PR (SPEC-EVT-003).</summary>
    public bool VoteChanged { get; set; } = true;

    /// <summary>Notifier les commentaires sur mes PR (SPEC-EVT-004).</summary>
    public bool CommentOnMyPullRequest { get; set; } = true;

    /// <summary>Notifier les réponses à mes commentaires (SPEC-EVT-005).</summary>
    public bool ReplyToMyComment { get; set; } = true;

    /// <summary>Notifier les mentions (@moi) (SPEC-EVT-006).</summary>
    public bool MentionedInComment { get; set; } = true;

    /// <summary>Notifier les commentaires sur les PR que je relis (SPEC-EVT-007).</summary>
    public bool CommentOnReviewedPullRequest { get; set; } = true;

    /// <summary>Notifier les discussions résolues / réactivées (SPEC-EVT-008).</summary>
    public bool ThreadStatusChanged { get; set; } = true;

    /// <summary>Notifier les changements d'état de PR (SPEC-EVT-009).</summary>
    public bool PullRequestStateChanged { get; set; } = true;

    /// <summary>Notifier les échecs de pipeline (SPEC-PIPE-001).</summary>
    public bool PipelineFailed { get; set; } = true;

    /// <summary>Notifier les retours au vert (SPEC-PIPE-002).</summary>
    public bool PipelineRecovered { get; set; } = true;

    /// <summary>Notifier les erreurs de fonctionnement (PAT expiré, dépôt inaccessible).</summary>
    public bool OperationalErrors { get; set; } = true;

    /// <summary>Indique si le type d'événement doit être notifié.</summary>
    public bool IsEnabled(NotificationKind kind) => kind switch
    {
        NotificationKind.PullRequestCreated => PullRequestCreated,
        NotificationKind.ReviewerAssigned => ReviewerAssigned,
        NotificationKind.VoteChanged => VoteChanged,
        NotificationKind.CommentOnMyPullRequest => CommentOnMyPullRequest,
        NotificationKind.ReplyToMyComment => ReplyToMyComment,
        NotificationKind.MentionedInComment => MentionedInComment,
        NotificationKind.CommentOnReviewedPullRequest => CommentOnReviewedPullRequest,
        NotificationKind.ThreadStatusChanged => ThreadStatusChanged,
        NotificationKind.PullRequestStateChanged => PullRequestStateChanged,
        NotificationKind.PipelineFailed => PipelineFailed,
        NotificationKind.PipelineRecovered => PipelineRecovered,
        _ => false,
    };

    /// <summary>Active ou désactive un type (utilisé par la fenêtre de configuration).</summary>
    public void SetEnabled(NotificationKind kind, bool enabled)
    {
        switch (kind)
        {
            case NotificationKind.PullRequestCreated: PullRequestCreated = enabled; break;
            case NotificationKind.ReviewerAssigned: ReviewerAssigned = enabled; break;
            case NotificationKind.VoteChanged: VoteChanged = enabled; break;
            case NotificationKind.CommentOnMyPullRequest: CommentOnMyPullRequest = enabled; break;
            case NotificationKind.ReplyToMyComment: ReplyToMyComment = enabled; break;
            case NotificationKind.MentionedInComment: MentionedInComment = enabled; break;
            case NotificationKind.CommentOnReviewedPullRequest: CommentOnReviewedPullRequest = enabled; break;
            case NotificationKind.ThreadStatusChanged: ThreadStatusChanged = enabled; break;
            case NotificationKind.PullRequestStateChanged: PullRequestStateChanged = enabled; break;
            case NotificationKind.PipelineFailed: PipelineFailed = enabled; break;
            case NotificationKind.PipelineRecovered: PipelineRecovered = enabled; break;
        }
    }

    /// <summary>Empreinte des types activés, un caractère par type.</summary>
    /// <remarks>
    /// Les types activés déterminent quelles règles de détection sont en vigueur, donc si un
    /// cycle doit lire les discussions (SPEC-POLL-003) : ils font à ce titre partie de
    /// <see cref="WatcherConfiguration.MonitoringSignature"/>.
    /// </remarks>
    public string EnabledSignature
        => string.Concat(NotificationKindExtensions.All.Select(kind => IsEnabled(kind) ? '1' : '0'));

    /// <summary>Copie indépendante (édition annulable dans la fenêtre de configuration).</summary>
    public NotificationPreferences Clone() => (NotificationPreferences)MemberwiseClone();
}
