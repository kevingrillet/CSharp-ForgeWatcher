using CSharpForgeWatcher.Domain.Events;

namespace CSharpForgeWatcher.Application.Detection.Pipelines;

/// <summary>
/// Règle de détection sur les pipelines (patron Strategy).
/// </summary>
/// <remarks>
/// Interface distincte de <see cref="IPullRequestEventRule"/> parce que le contexte
/// d'observation est différent (une exécution de pipeline n'est pas une pull request), mais
/// la mécanique est identique : une règle par comportement, composée par
/// <see cref="PipelineEventDetector"/>.
/// </remarks>
public interface IPipelineEventRule
{
    /// <summary>Nom lisible de la règle, utilisé dans les journaux.</summary>
    string Name { get; }

    /// <summary>
    /// Examine le contexte et retourne les événements détectés. Données insuffisantes ⇒
    /// aucun événement, jamais d'exception.
    /// </summary>
    IEnumerable<PipelineEvent> Detect(PipelineDetectionContext context);
}
