using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Monitoring;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Detection.Pipelines;

/// <summary>
/// Ce dont une règle de pipeline a besoin : la dernière exécution terminée observée, et
/// celle qui était mémorisée.
/// </summary>
/// <remarks>
/// Pendant symétrique de <see cref="DetectionContext"/> pour les pull requests. Même
/// principe : fonction pure, aucune horloge, aucun réseau — donc testable en trois lignes.
/// </remarks>
public sealed class PipelineDetectionContext
{
    /// <summary>Dernière exécution <b>terminée</b> observée pour ce pipeline.</summary>
    public required PipelineRun Run { get; init; }

    /// <summary>État mémorisé au cycle précédent, ou <c>null</c> si le pipeline est découvert.</summary>
    public PipelineSnapshot? Previous { get; init; }

    /// <summary>Horodatage du cycle, utilisé à défaut de date d'API exploitable.</summary>
    public required DateTimeOffset ObservedOn { get; init; }

    /// <summary>Compte de forge dont provient l'observation (SPEC-CFG-008).</summary>
    public string AccountId { get; init; } = string.Empty;

    /// <summary>Libellé du compte ; vide quand un seul compte est configuré.</summary>
    public string AccountLabel { get; init; } = string.Empty;

    /// <summary>Définition concernée.</summary>
    public PipelineDefinitionRef Definition => Run.Definition;

    /// <summary>Vrai si le pipeline n'était pas connu : aucune comparaison n'est possible.</summary>
    public bool IsFirstSight => Previous is null;

    /// <summary>
    /// Vrai si l'exécution observée est bien <b>nouvelle</b> par rapport à la mémoire.
    /// Sans cette garde, le même échec serait re-notifié à chaque cycle.
    /// </summary>
    public bool IsNewRun => Previous is not null && Previous.LastCompletedRunId != Run.Id;

    /// <summary>Fabrique un événement rattaché à ce pipeline.</summary>
    /// <param name="kind">Type d'événement.</param>
    /// <param name="message">Corps du message.</param>
    /// <param name="dedupKey">Clé de déduplication ; dérivée à défaut.</param>
    public PipelineEvent CreateEvent(NotificationKind kind, TextRef message, string? dedupKey = null) => new()
    {
        Kind = kind,
        Definition = Definition,
        RunId = Run.Id,
        RunName = Run.RunName,
        Message = message,
        Url = Run.Url,
        OccurredOn = Run.OccurredOn is { } date && date != default ? date : ObservedOn,
        Branch = Run.Branch,
        ActorName = Run.RequestedFor.Id.Length > 0 ? Run.RequestedFor.SafeDisplayName : null,
        AccountLabel = AccountLabel,
        DedupKey = $"{AccountId}|{dedupKey ?? $"{kind}|{Definition.Key}|{Run.Id}"}",
    };
}
