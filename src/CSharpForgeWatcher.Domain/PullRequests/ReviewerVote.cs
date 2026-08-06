using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Domain.PullRequests;

/// <summary>
/// Vote d'un relecteur. Les valeurs numériques sont celles de l'API Azure DevOps
/// (champ <c>reviewers[].vote</c>), ce qui rend la conversion triviale et stable.
/// </summary>
public enum ReviewerVote
{
    /// <summary>Rejeté (-10).</summary>
    Rejected = -10,

    /// <summary>En attente de l'auteur (-5).</summary>
    WaitingForAuthor = -5,

    /// <summary>Aucun vote exprimé (0).</summary>
    NoVote = 0,

    /// <summary>Approuvé avec suggestions (5).</summary>
    ApprovedWithSuggestions = 5,

    /// <summary>Approuvé (10).</summary>
    Approved = 10,
}

/// <summary>Conversions et libellés pour <see cref="ReviewerVote"/>.</summary>
public static class ReviewerVoteExtensions
{
    /// <summary>
    /// Convertit une valeur brute d'API en <see cref="ReviewerVote"/>.
    /// Une valeur inattendue est ramenée au vote connu le plus proche, afin qu'une
    /// évolution de l'API ne provoque pas d'exception.
    /// </summary>
    public static ReviewerVote FromApiValue(int value) => value switch
    {
        <= -10 => ReviewerVote.Rejected,
        < 0 => ReviewerVote.WaitingForAuthor,
        0 => ReviewerVote.NoVote,
        < 10 => ReviewerVote.ApprovedWithSuggestions,
        _ => ReviewerVote.Approved,
    };

    /// <summary>
    /// Clé de l'action du vote, employée dans une phrase (« a approuvé »).
    /// </summary>
    /// <remarks>
    /// Une clé et non une formulation : le domaine dit ce qui s'est passé, pas dans quelle
    /// langue le dire (SPEC-UI-LANG-002). Un test de garde vérifie que chaque valeur de
    /// l'énumération est formulée dans les deux langues.
    /// </remarks>
    public static string ToActionKey(this ReviewerVote vote) => TextKeys.VoteAction(vote);

    /// <summary>Clé du libellé autonome du vote (« Approuvé »), pour une liste ou un menu.</summary>
    public static string ToLabelKey(this ReviewerVote vote) => TextKeys.VoteLabel(vote);

    /// <summary>Indique si le vote bloque la complétion de la PR.</summary>
    public static bool IsBlocking(this ReviewerVote vote)
        => vote is ReviewerVote.Rejected or ReviewerVote.WaitingForAuthor;
}
