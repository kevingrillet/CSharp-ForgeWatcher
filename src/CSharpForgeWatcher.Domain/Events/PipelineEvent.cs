using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Domain.Events;

/// <summary>
/// Événement détecté sur un pipeline surveillé (SPEC-PIPE-001, SPEC-PIPE-002).
/// </summary>
/// <remarks>
/// Frère de <see cref="PullRequestEvent"/> : les deux implémentent
/// <see cref="INotifiableEvent"/>, donc la diffusion, l'affichage et le compteur de non-lus
/// les traitent sans distinction.
/// </remarks>
public sealed record PipelineEvent : INotifiableEvent
{
    /// <summary>Type d'événement.</summary>
    public required NotificationKind Kind { get; init; }

    /// <summary>Pipeline concerné.</summary>
    public required PipelineDefinitionRef Definition { get; init; }

    /// <summary>Identifiant de l'exécution en cause (entier 64 bits, SPEC-FORGE-006).</summary>
    public required long RunId { get; init; }

    /// <summary>Numéro affiché de l'exécution.</summary>
    public required string RunName { get; init; }

    /// <summary>Corps du message.</summary>
    public required TextRef Message { get; init; }

    /// <summary>URL de la page de résultats de l'exécution.</summary>
    public required string Url { get; init; }

    /// <summary>Date de l'événement.</summary>
    public required DateTimeOffset OccurredOn { get; init; }

    /// <summary>Branche déclenchante.</summary>
    public string Branch { get; init; } = string.Empty;

    /// <summary>Personne à l'origine du déclenchement.</summary>
    public string? ActorName { get; init; }

    /// <summary>Clé de déduplication explicite ; dérivée à défaut.</summary>
    public string DedupKey { get; init; } = string.Empty;

    /// <summary>
    /// Libellé du compte de forge d'origine ; vide quand un seul compte est surveillé.
    /// </summary>
    public string AccountLabel { get; init; } = string.Empty;

    /// <inheritdoc />
    public TextRef Title => TextRef.Of(TextKeys.KindLabel(Kind));

    /// <inheritdoc />
    public TextRef Subject => TextRef.Of(TextKeys.Event.PipelineSubject, Definition.Name, RunName);

    /// <inheritdoc />
    public string Context => string.IsNullOrEmpty(AccountLabel)
        ? $"{Definition.ProjectName} • {Definition.Name}"
        : $"{AccountLabel} • {Definition.ProjectName} • {Definition.Name}";

    /// <inheritdoc />
    /// <remarks>
    /// L'identifiant d'exécution fait partie de la clé : deux échecs successifs sont deux
    /// faits distincts et doivent tous deux être notifiés (SPEC-PIPE-001, règle 3).
    /// </remarks>
    public string EffectiveDedupKey
        => string.IsNullOrEmpty(DedupKey) ? $"{Kind}|{Definition.Key}|{RunId}" : DedupKey;
}
