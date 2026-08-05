using CSharpForgeWatcher.Domain.Pipelines;

namespace CSharpForgeWatcher.Domain.Monitoring;

/// <summary>
/// Mémoire d'un pipeline entre deux cycles : la dernière exécution terminée connue.
/// </summary>
/// <remarks>
/// POCO muable, sérialisé dans <c>state.json</c>. Deux informations suffisent à détecter
/// les deux événements de pipeline : l'identifiant de la dernière exécution terminée (pour
/// savoir si celle qu'on observe est nouvelle) et son résultat (pour distinguer un échec
/// d'un retour au vert).
/// </remarks>
public sealed class PipelineSnapshot
{
    /// <summary>Projet propriétaire.</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Identifiant de la définition (entier 64 bits, SPEC-FORGE-006).</summary>
    public long DefinitionId { get; set; }

    /// <summary>Nom affiché au dernier cycle.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Identifiant de la dernière exécution terminée observée.</summary>
    public long LastCompletedRunId { get; set; }

    /// <summary>Numéro affiché de cette exécution.</summary>
    public string LastRunName { get; set; } = string.Empty;

    /// <summary>Résultat de cette exécution.</summary>
    public PipelineRunResult LastResult { get; set; } = PipelineRunResult.Unknown;

    /// <summary>Date de la dernière observation.</summary>
    public DateTimeOffset LastSeenOn { get; set; }

    /// <summary>Clé stable de ce pipeline.</summary>
    public string Key => $"{ProjectName}:{DefinitionId}";

    /// <summary>Définition d'origine, reconstruite depuis l'instantané.</summary>
    public PipelineDefinitionRef ToDefinitionRef() => new(ProjectName, DefinitionId, Name);

    /// <summary>Vrai si la dernière exécution connue était en échec.</summary>
    public bool WasFailing => LastResult.IsFailure();

    /// <summary>Construit l'instantané d'une exécution terminée observée.</summary>
    public static PipelineSnapshot From(PipelineRun run, DateTimeOffset observedOn) => new()
    {
        ProjectName = run.Definition.ProjectName,
        DefinitionId = run.Definition.DefinitionId,
        Name = run.Definition.Name,
        LastCompletedRunId = run.Id,
        LastRunName = run.RunName,
        LastResult = run.Result,
        LastSeenOn = observedOn,
    };
}
