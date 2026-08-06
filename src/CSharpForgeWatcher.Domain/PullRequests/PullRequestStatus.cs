using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Domain.PullRequests;

/// <summary>État d'une pull request Azure DevOps.</summary>
public enum PullRequestStatus
{
    /// <summary>État absent ou non reconnu.</summary>
    Unknown = 0,

    /// <summary>PR ouverte.</summary>
    Active,

    /// <summary>PR fusionnée / complétée.</summary>
    Completed,

    /// <summary>PR abandonnée.</summary>
    Abandoned,
}

/// <summary>Conversions et libellés pour <see cref="PullRequestStatus"/>.</summary>
public static class PullRequestStatusExtensions
{
    /// <summary>Convertit la valeur textuelle de l'API (<c>active</c>, <c>completed</c>…).</summary>
    public static PullRequestStatus Parse(string? apiValue) => apiValue?.Trim().ToLowerInvariant() switch
    {
        "active" => PullRequestStatus.Active,
        "completed" => PullRequestStatus.Completed,
        "abandoned" => PullRequestStatus.Abandoned,
        _ => PullRequestStatus.Unknown,
    };

    /// <summary>Clé du libellé de l'état (SPEC-UI-LANG-002).</summary>
    public static string ToLabelKey(this PullRequestStatus status)
        => TextKeys.PullRequestStatusLabel(status);

    /// <summary>Indique si la PR est terminée : elle peut être retirée de l'état surveillé.</summary>
    public static bool IsFinal(this PullRequestStatus status)
        => status is PullRequestStatus.Completed or PullRequestStatus.Abandoned;
}
