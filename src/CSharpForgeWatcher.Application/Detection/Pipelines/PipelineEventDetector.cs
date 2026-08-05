using CSharpForgeWatcher.Application.Detection.Pipelines.Rules;
using CSharpForgeWatcher.Domain.Events;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Application.Detection.Pipelines;

/// <summary>
/// Applique l'ensemble des règles de pipeline (patron Composite).
/// </summary>
/// <remarks>
/// Mêmes garanties que <see cref="PullRequestEventDetector"/> : déduplication, isolation
/// d'une règle défaillante, tri stable.
/// </remarks>
public sealed class PipelineEventDetector
{
    private readonly IReadOnlyList<IPipelineEventRule> _rules;
    private readonly ILogger<PipelineEventDetector>? _logger;

    /// <summary>Construit un détecteur à partir d'un jeu de règles.</summary>
    public PipelineEventDetector(
        IEnumerable<IPipelineEventRule> rules,
        ILogger<PipelineEventDetector>? logger = null)
    {
        _rules = (rules ?? throw new ArgumentNullException(nameof(rules))).ToArray();
        _logger = logger;

        if (_rules.Count == 0)
        {
            throw new ArgumentException("Au moins une règle de pipeline est requise.", nameof(rules));
        }
    }

    /// <summary>Règles fournies en standard. C'est ici qu'on en ajoute une.</summary>
    public static IReadOnlyList<IPipelineEventRule> CreateDefaultRules() =>
    [
        new PipelineFailedRule(),
        new PipelineRecoveredRule(),
    ];

    /// <summary>Détecteur muni des règles standard.</summary>
    public static PipelineEventDetector CreateDefault(ILogger<PipelineEventDetector>? logger = null)
        => new(CreateDefaultRules(), logger);

    /// <summary>Règles actives.</summary>
    public IReadOnlyList<IPipelineEventRule> Rules => _rules;

    /// <summary>Détecte les événements d'un pipeline.</summary>
    public IReadOnlyList<PipelineEvent> Detect(PipelineDetectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var detected = new List<PipelineEvent>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in _rules)
        {
            List<PipelineEvent> produced;

            try
            {
                produced = rule.Detect(context).ToList();
            }
            catch (Exception exception)
            {
                _logger?.LogError(
                    exception,
                    "La règle {Rule} a échoué sur le pipeline {Pipeline} ; elle est ignorée pour ce cycle.",
                    rule.Name,
                    context.Definition.Key);
                continue;
            }

            foreach (var candidate in produced.Where(candidate => seenKeys.Add(candidate.EffectiveDedupKey)))
            {
                detected.Add(candidate);
            }
        }

        return detected
            .OrderBy(e => (int)e.Kind)
            .ThenBy(e => e.OccurredOn)
            .ToList();
    }
}
