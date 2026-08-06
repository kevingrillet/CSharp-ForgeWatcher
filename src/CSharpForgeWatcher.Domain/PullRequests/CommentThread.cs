using CSharpForgeWatcher.Domain.Identity;

namespace CSharpForgeWatcher.Domain.PullRequests;

/// <summary>Un message dans une discussion de pull request.</summary>
/// <param name="Id">
/// Identifiant du commentaire, unique dans sa discussion. Entier 64 bits : les
/// identifiants de certaines forges dépassent la capacité d'un entier 32 bits
/// (SPEC-FORGE-006, ADR-0004).
/// </param>
/// <param name="ParentCommentId">Commentaire parent (0 ou <c>null</c> si racine).</param>
/// <param name="Author">Auteur du message.</param>
/// <param name="Content">Contenu Markdown brut.</param>
/// <param name="PublishedOn">Date de publication.</param>
/// <param name="IsSystem">
/// Vrai pour les messages générés par la forge (« X a voté », « branche mise à jour »).
/// Ces messages ne déclenchent jamais de notification de commentaire (SPEC-EVT-004, règle 1).
/// </param>
/// <param name="IsDeleted">Vrai si le message a été supprimé.</param>
/// <param name="Url">
/// Adresse web exacte du message, quand la forge la fournit. Elle est alors préférée à
/// l'adresse reconstruite (SPEC-LINK-004).
/// </param>
public sealed record Comment(
    long Id,
    long? ParentCommentId,
    UserRef Author,
    string Content,
    DateTimeOffset PublishedOn,
    bool IsSystem = false,
    bool IsDeleted = false,
    string Url = "")
{
    /// <summary>
    /// Indique si le contenu mentionne explicitement l'utilisateur fourni (SPEC-EVT-006).
    /// </summary>
    /// <remarks>
    /// L'identifiant doit être <b>délimité</b> : précédé de <c>@</c> ou de <c>&lt;</c>, et
    /// suivi d'une fin de mot. Les deux forges implémentées écrivent leurs mentions ainsi —
    /// <c>@&lt;GUID&gt;</c> pour Azure DevOps, <c>@login</c> pour GitHub. Sans cette
    /// délimitation, un identifiant court (un login GitHub est un mot ordinaire) se
    /// déclencherait sur n'importe quelle prose le contenant.
    /// </remarks>
    public bool Mentions(string? userId)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(Content))
        {
            return false;
        }

        for (var start = 0; start <= Content.Length - userId.Length;)
        {
            var index = Content.IndexOf(userId, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var end = index + userId.Length;
            var introduced = index > 0 && Content[index - 1] is '@' or '<';
            var delimited = end == Content.Length || !IsIdentifierChar(Content[end]);

            if (introduced && delimited)
            {
                return true;
            }

            start = index + 1;
        }

        return false;
    }

    /// <summary>
    /// Extrait court, sur une seule ligne, destiné au corps d'une notification.
    /// Le Markdown le plus courant est allégé pour rester lisible dans un toast.
    /// </summary>
    /// <param name="maxLength">Longueur maximale, ellipse comprise.</param>
    public string ToExcerpt(int maxLength = 140)
    {
        if (string.IsNullOrWhiteSpace(Content))
        {
            return string.Empty;
        }

        var flattened = Content
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal);

        while (flattened.Contains("  ", StringComparison.Ordinal))
        {
            flattened = flattened.Replace("  ", " ", StringComparison.Ordinal);
        }

        flattened = flattened.Trim();
        return flattened.Length <= maxLength ? flattened : flattened[..Math.Max(0, maxLength - 1)].TrimEnd() + "…";
    }

    /// <summary>Caractère pouvant faire partie d'un identifiant d'utilisateur.</summary>
    private static bool IsIdentifierChar(char character)
        => char.IsLetterOrDigit(character) || character is '-' or '_' or '.';
}

/// <summary>Discussion (thread) attachée à une pull request.</summary>
public sealed record CommentThread
{
    /// <summary>
    /// Identifiant de la discussion : c'est le <c>discussionId</c> des URL web
    /// (SPEC-LINK-002). Entier 64 bits (SPEC-FORGE-006) ; une forge sans notion de fil peut
    /// employer une valeur synthétique, unique au sein de la pull request.
    /// </summary>
    public required long Id { get; init; }

    /// <summary>État de la discussion.</summary>
    public CommentThreadStatus Status { get; init; } = CommentThreadStatus.Unknown;

    /// <summary>Vrai si la discussion a été supprimée.</summary>
    public bool IsDeleted { get; init; }

    /// <summary>Messages, dans l'ordre de publication.</summary>
    public IReadOnlyList<Comment> Comments { get; init; } = Array.Empty<Comment>();

    /// <summary>Chemin du fichier commenté, s'il s'agit d'un commentaire de code.</summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Adresse web de la discussion, quand la forge la fournit (SPEC-LINK-004) ;
    /// chaîne vide sinon.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Messages non système et non supprimés : les seuls qui peuvent notifier.</summary>
    public IEnumerable<Comment> NotifiableComments
        => Comments.Where(c => !c.IsSystem && !c.IsDeleted);

    /// <summary>Indique si l'utilisateur fourni a écrit dans cette discussion.</summary>
    public bool HasParticipant(string? userId)
        => Comments.Any(c => c.Author.Is(userId));

    /// <summary>Vrai s'il s'agit d'une discussion purement système (aucun message humain).</summary>
    public bool IsSystemOnly => Comments.Count > 0 && Comments.All(c => c.IsSystem);
}
