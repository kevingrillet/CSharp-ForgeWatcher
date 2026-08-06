using CSharpForgeWatcher.Domain.Identity;

namespace CSharpForgeWatcher.Domain.PullRequests;

/// <summary>Relecteur d'une pull request et son vote courant.</summary>
/// <param name="User">Identité du relecteur.</param>
/// <param name="Vote">Vote exprimé.</param>
/// <param name="IsRequired">Relecteur obligatoire (imposé par une stratégie de branche).</param>
public sealed record Reviewer(UserRef User, ReviewerVote Vote, bool IsRequired = false);

/// <summary>
/// Pull request telle qu'observée lors d'un cycle de sondage.
/// </summary>
/// <remarks>
/// Modèle de domaine, immuable : il ne dépend ni du format JSON d'Azure DevOps
/// (voir <c>Infrastructure/AzureDevOps/Dtos</c>) ni du format persisté
/// (voir <see cref="Monitoring.PullRequestSnapshot"/>).
/// </remarks>
public sealed record PullRequest
{
    /// <summary>Numéro de la PR, tel qu'affiché dans Azure DevOps.</summary>
    public required int Id { get; init; }

    /// <summary>Dépôt hébergeant la PR.</summary>
    public required RepositoryRef Repository { get; init; }

    /// <summary>Titre de la PR.</summary>
    public required string Title { get; init; }

    /// <summary>Auteur de la PR.</summary>
    public required UserRef Author { get; init; }

    /// <summary>État courant.</summary>
    public PullRequestStatus Status { get; init; } = PullRequestStatus.Active;

    /// <summary>Vrai si la PR est encore en brouillon.</summary>
    public bool IsDraft { get; init; }

    /// <summary>Date de création.</summary>
    public DateTimeOffset CreatedOn { get; init; }

    /// <summary>Branche source, forme courte (ex. « feature/x »).</summary>
    public string SourceBranch { get; init; } = string.Empty;

    /// <summary>Branche cible, forme courte (ex. « main »).</summary>
    public string TargetBranch { get; init; } = string.Empty;

    /// <summary>Relecteurs et leurs votes.</summary>
    public IReadOnlyList<Reviewer> Reviewers { get; init; } = Array.Empty<Reviewer>();

    /// <summary>Clé stable de cette PR.</summary>
    public PullRequestKey Key => new(Repository.RepositoryId, Id);

    /// <summary>Indique si l'identifiant fourni est celui de l'auteur.</summary>
    public bool IsAuthoredBy(string? userId) => Author.Is(userId);

    /// <summary>Retourne le relecteur correspondant à l'identifiant, ou <c>null</c>.</summary>
    public Reviewer? FindReviewer(string? userId)
        => userId is null ? null : Reviewers.FirstOrDefault(r => r.User.Is(userId));

    /// <summary>Vote de l'utilisateur indiqué (<see cref="ReviewerVote.NoVote"/> s'il n'est pas relecteur).</summary>
    public ReviewerVote VoteOf(string? userId) => FindReviewer(userId)?.Vote ?? ReviewerVote.NoVote;

    /// <summary>Libellé « !1234 — Titre » utilisé dans les notifications.</summary>
    public string DisplayLabel => $"!{Id} — {Title}";
}
