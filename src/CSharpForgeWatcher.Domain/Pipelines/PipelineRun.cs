using CSharpForgeWatcher.Domain.Identity;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Domain.Pipelines;

/// <summary>
/// Définition de pipeline surveillée : le « modèle » dont chaque exécution est une instance.
/// </summary>
/// <remarks>
/// L'identité est le couple projet + identifiant de définition : les identifiants de
/// définition sont uniques au sein d'un projet, pas de l'organisation. Le nom n'est qu'un
/// libellé, rafraîchi à chaque cycle (SPEC-PIPE-003, règle 1).
/// </remarks>
/// <param name="ProjectName">
/// Espace propriétaire. Sa forme dépend de la forge : projet d'équipe sur Azure DevOps,
/// <c>propriétaire/dépôt</c> sur GitHub, les workflows y appartenant à un dépôt
/// (SPEC-FORGE-004).
/// </param>
/// <param name="DefinitionId">Identifiant de la définition (entier 64 bits, SPEC-FORGE-006).</param>
/// <param name="Name">Nom affiché du pipeline.</param>
public sealed record PipelineDefinitionRef(string ProjectName, long DefinitionId, string Name)
{
    /// <summary>Clé stable, utilisée comme clé de dictionnaire dans l'état persisté.</summary>
    public string Key => $"{ProjectName}:{DefinitionId}";

    /// <summary>Libellé « projet / pipeline ».</summary>
    public string DisplayPath => $"{ProjectName} / {Name}";

    public override string ToString() => DisplayPath;
}

/// <summary>Avancement d'une exécution de pipeline.</summary>
public enum PipelineRunState
{
    /// <summary>État absent ou non reconnu.</summary>
    Unknown = 0,

    /// <summary>En file d'attente.</summary>
    NotStarted,

    /// <summary>En cours.</summary>
    InProgress,

    /// <summary>Terminée : le résultat est exploitable.</summary>
    Completed,

    /// <summary>Annulation en cours.</summary>
    Canceling,
}

/// <summary>Résultat d'une exécution terminée.</summary>
public enum PipelineRunResult
{
    /// <summary>Résultat absent ou non reconnu (exécution non terminée, par exemple).</summary>
    Unknown = 0,

    /// <summary>Succès.</summary>
    Succeeded,

    /// <summary>Succès partiel : au moins une étape en échec non bloquante.</summary>
    PartiallySucceeded,

    /// <summary>Échec.</summary>
    Failed,

    /// <summary>Annulée.</summary>
    Canceled,
}

/// <summary>Conversions et libellés des états d'exécution.</summary>
public static class PipelineRunExtensions
{
    /// <summary>Convertit la valeur textuelle d'avancement de l'API.</summary>
    public static PipelineRunState ParseState(string? apiValue) => apiValue?.Trim().ToLowerInvariant() switch
    {
        "notstarted" => PipelineRunState.NotStarted,
        "inprogress" => PipelineRunState.InProgress,
        "completed" => PipelineRunState.Completed,
        "cancelling" or "canceling" => PipelineRunState.Canceling,
        _ => PipelineRunState.Unknown,
    };

    /// <summary>Convertit la valeur textuelle de résultat de l'API.</summary>
    public static PipelineRunResult ParseResult(string? apiValue) => apiValue?.Trim().ToLowerInvariant() switch
    {
        "succeeded" => PipelineRunResult.Succeeded,
        "partiallysucceeded" => PipelineRunResult.PartiallySucceeded,
        "failed" => PipelineRunResult.Failed,
        "canceled" or "cancelled" => PipelineRunResult.Canceled,
        _ => PipelineRunResult.Unknown,
    };

    /// <summary>Clé du libellé du résultat (SPEC-UI-LANG-002).</summary>
    public static string ToLabelKey(this PipelineRunResult result)
        => TextKeys.PipelineResultLabel(result);

    /// <summary>
    /// Vrai si le résultat doit alerter. Le succès partiel est compté comme un échec :
    /// une étape rouge reste une étape rouge (SPEC-PIPE-001).
    /// </summary>
    public static bool IsFailure(this PipelineRunResult result)
        => result is PipelineRunResult.Failed or PipelineRunResult.PartiallySucceeded;
}

/// <summary>
/// Une exécution de pipeline, telle qu'observée lors d'un cycle.
/// </summary>
public sealed record PipelineRun
{
    /// <summary>
    /// Identifiant de l'exécution (croissant : sert à repérer la plus récente). Entier
    /// 64 bits : ceux de GitHub Actions dépassent largement les 32 bits (SPEC-FORGE-006).
    /// </summary>
    public required long Id { get; init; }

    /// <summary>Définition dont cette exécution est issue.</summary>
    public required PipelineDefinitionRef Definition { get; init; }

    /// <summary>Numéro affiché de l'exécution (ex. « 20260804.3 »).</summary>
    public required string RunName { get; init; }

    /// <summary>Avancement.</summary>
    public PipelineRunState State { get; init; } = PipelineRunState.Unknown;

    /// <summary>Résultat, exploitable uniquement si l'exécution est terminée.</summary>
    public PipelineRunResult Result { get; init; } = PipelineRunResult.Unknown;

    /// <summary>Branche déclenchante, forme courte.</summary>
    public string Branch { get; init; } = string.Empty;

    /// <summary>Personne à l'origine du déclenchement.</summary>
    public UserRef RequestedFor { get; init; } = UserRef.Unknown;

    /// <summary>Début de l'exécution.</summary>
    public DateTimeOffset? StartedOn { get; init; }

    /// <summary>Fin de l'exécution.</summary>
    public DateTimeOffset? FinishedOn { get; init; }

    /// <summary>URL de la page de résultats.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Vrai si l'exécution est terminée : son résultat est alors significatif.</summary>
    public bool IsCompleted => State == PipelineRunState.Completed;

    /// <summary>Vrai si l'exécution terminée est en échec (ou succès partiel).</summary>
    public bool IsFailure => IsCompleted && Result.IsFailure();

    /// <summary>Vrai si l'exécution terminée est en succès.</summary>
    public bool IsSuccess => IsCompleted && Result == PipelineRunResult.Succeeded;

    /// <summary>Date la plus pertinente pour horodater un événement.</summary>
    public DateTimeOffset? OccurredOn => FinishedOn ?? StartedOn;
}
