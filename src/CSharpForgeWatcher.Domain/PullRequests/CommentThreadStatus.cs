using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Domain.PullRequests;

/// <summary>
/// État d'une discussion (thread) de pull request, tel qu'exposé par Azure DevOps.
/// </summary>
public enum CommentThreadStatus
{
    /// <summary>État absent ou non reconnu.</summary>
    Unknown = 0,

    /// <summary>Discussion ouverte, en attente d'action.</summary>
    Active,

    /// <summary>Corrigée.</summary>
    Fixed,

    /// <summary>Ne sera pas corrigée.</summary>
    WontFix,

    /// <summary>Fermée.</summary>
    Closed,

    /// <summary>Comportement voulu.</summary>
    ByDesign,

    /// <summary>En attente.</summary>
    Pending,
}

/// <summary>Conversions et libellés pour <see cref="CommentThreadStatus"/>.</summary>
public static class CommentThreadStatusExtensions
{
    /// <summary>Convertit la valeur textuelle de l'API (<c>active</c>, <c>fixed</c>…).</summary>
    public static CommentThreadStatus Parse(string? apiValue) => apiValue?.Trim().ToLowerInvariant() switch
    {
        "active" => CommentThreadStatus.Active,
        "fixed" => CommentThreadStatus.Fixed,
        "wontfix" => CommentThreadStatus.WontFix,
        "closed" => CommentThreadStatus.Closed,
        "bydesign" => CommentThreadStatus.ByDesign,
        "pending" => CommentThreadStatus.Pending,
        _ => CommentThreadStatus.Unknown,
    };

    /// <summary>Clé du libellé de l'état (SPEC-UI-LANG-002).</summary>
    public static string ToLabelKey(this CommentThreadStatus status)
        => TextKeys.ThreadStatusLabel(status);

    /// <summary>
    /// Indique si la discussion est considérée comme close.
    /// <see cref="CommentThreadStatus.Unknown"/> couvre notamment les discussions système
    /// (sans état) : elles ne sont pas comptées comme « à traiter ».
    /// </summary>
    public static bool IsResolved(this CommentThreadStatus status)
        => status is CommentThreadStatus.Fixed
            or CommentThreadStatus.WontFix
            or CommentThreadStatus.Closed
            or CommentThreadStatus.ByDesign;
}
