using CSharpForgeWatcher.Application.Detection.Rules;
using CSharpForgeWatcher.Domain.Events;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Application.Detection;

/// <summary>
/// Applique l'ensemble des règles de détection à une pull request (patron Composite).
/// </summary>
/// <remarks>
/// Le détecteur se comporte comme une règle unique vue de l'extérieur. Il apporte trois
/// garanties que les règles n'ont pas à porter :
/// <list type="bullet">
/// <item>déduplication par <see cref="PullRequestEvent.EffectiveDedupKey"/> ;</item>
/// <item>isolation : une règle qui lève n'interrompt pas les autres ;</item>
/// <item>tri stable par priorité d'intitulé puis par date.</item>
/// </list>
/// </remarks>
public sealed class PullRequestEventDetector
{
    private readonly IReadOnlyList<IPullRequestEventRule> _rules;
    private readonly ILogger<PullRequestEventDetector>? _logger;

    /// <summary>Construit un détecteur à partir d'un jeu de règles.</summary>
    public PullRequestEventDetector(
        IEnumerable<IPullRequestEventRule> rules,
        ILogger<PullRequestEventDetector>? logger = null)
    {
        _rules = (rules ?? throw new ArgumentNullException(nameof(rules))).ToArray();
        _logger = logger;

        if (_rules.Count == 0)
        {
            throw new ArgumentException("Au moins une règle de détection est requise.", nameof(rules));
        }
    }

    /// <summary>Règles fournies en standard, dans l'ordre de déclaration.</summary>
    /// <remarks>C'est le point d'extension : ajouter une règle ici la met en service.</remarks>
    public static IReadOnlyList<IPullRequestEventRule> CreateDefaultRules() =>
    [
        new NewPullRequestRule(),
        new ReviewerAssignedRule(),
        new VoteChangedRule(),
        new NewCommentRule(),
        new ThreadStatusChangedRule(),
        new PullRequestStateChangedRule(),
    ];

    /// <summary>Détecteur muni des règles standard.</summary>
    public static PullRequestEventDetector CreateDefault(ILogger<PullRequestEventDetector>? logger = null)
        => new(CreateDefaultRules(), logger);

    /// <summary>Règles actives.</summary>
    public IReadOnlyList<IPullRequestEventRule> Rules => _rules;

    /// <summary>Vrai si au moins une règle active a besoin des discussions.</summary>
    public bool AnyRuleRequiresThreads => _rules.Any(rule => rule.RequiresThreads);

    /// <summary>
    /// Détecte les événements d'une pull request.
    /// </summary>
    /// <returns>
    /// Événements dédoublonnés, triés du plus précis au plus général puis par date.
    /// Liste vide si rien n'a changé.
    /// </returns>
    public IReadOnlyList<PullRequestEvent> Detect(DetectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var detected = new List<PullRequestEvent>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in _rules)
        {
            List<PullRequestEvent> produced;

            try
            {
                // Matérialisé ici : une règle en itérateur différé lèverait pendant
                // l'énumération, hors de ce bloc de protection.
                produced = rule.Detect(context).ToList();
            }
            catch (Exception exception)
            {
                _logger?.LogError(
                    exception,
                    "La règle {Rule} a échoué sur la PR {PullRequest} ; elle est ignorée pour ce cycle.",
                    rule.Name,
                    context.Key);
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
