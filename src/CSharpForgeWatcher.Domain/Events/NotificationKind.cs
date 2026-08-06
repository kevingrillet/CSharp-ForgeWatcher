namespace CSharpForgeWatcher.Domain.Events;

/// <summary>
/// Types d'activité détectables. L'ordre de déclaration sert de priorité d'affichage :
/// pour un même fait, seul l'intitulé le plus précis (la plus petite valeur) est retenu, et
/// les notifications d'un cycle sont présentées dans cet ordre.
/// </summary>
public enum NotificationKind
{
    /// <summary>Quelqu'un a mentionné l'utilisateur dans un commentaire (SPEC-EVT-006).</summary>
    MentionedInComment = 0,

    /// <summary>Réponse à un commentaire de l'utilisateur (SPEC-EVT-005).</summary>
    ReplyToMyComment = 1,

    /// <summary>Nouveau commentaire sur une PR de l'utilisateur (SPEC-EVT-004).</summary>
    CommentOnMyPullRequest = 2,

    /// <summary>Nouveau commentaire sur une PR relue par l'utilisateur (SPEC-EVT-007).</summary>
    CommentOnReviewedPullRequest = 3,

    /// <summary>Vote d'un relecteur sur une PR de l'utilisateur (SPEC-EVT-003).</summary>
    VoteChanged = 4,

    /// <summary>L'utilisateur a été ajouté comme relecteur (SPEC-EVT-002).</summary>
    ReviewerAssigned = 5,

    /// <summary>
    /// Un pipeline surveillé est passé en échec (SPEC-PIPE-001).
    /// Placé haut dans l'ordre : c'est le type d'événement le plus urgent à traiter.
    /// </summary>
    PipelineFailed = 6,

    /// <summary>Une discussion a été résolue ou réactivée (SPEC-EVT-008).</summary>
    ThreadStatusChanged = 7,

    /// <summary>La PR a changé d'état : complétée, abandonnée, publiée (SPEC-EVT-009).</summary>
    PullRequestStateChanged = 8,

    /// <summary>Un pipeline surveillé est repassé au vert (SPEC-PIPE-002).</summary>
    PipelineRecovered = 9,

    /// <summary>Une nouvelle PR a été créée dans un dépôt surveillé (SPEC-EVT-001).</summary>
    PullRequestCreated = 10,
}

/// <summary>
/// Propriétés des types d'activité.
/// </summary>
/// <remarks>
/// Les libellés et descriptions ne sont plus ici : ils se déduisent du type par
/// <see cref="Text.TextKeys.KindLabel"/> et <see cref="Text.TextKeys.KindDescription"/>, et
/// c'est le catalogue qui les formule dans la langue courante (SPEC-UI-LANG-002).
/// </remarks>
public static class NotificationKindExtensions
{
    /// <summary>Vrai si le type concerne un pipeline (sert à grouper les préférences dans l'UI).</summary>
    public static bool IsPipelineKind(this NotificationKind kind)
        => kind is NotificationKind.PipelineFailed or NotificationKind.PipelineRecovered;

    /// <summary>Tous les types, dans l'ordre d'affichage des préférences.</summary>
    public static IReadOnlyList<NotificationKind> All { get; } =
        Enum.GetValues<NotificationKind>().OrderBy(k => (int)k).ToArray();
}
