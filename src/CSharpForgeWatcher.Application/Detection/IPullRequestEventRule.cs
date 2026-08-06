using CSharpForgeWatcher.Domain.Events;

namespace CSharpForgeWatcher.Application.Detection;

/// <summary>
/// Règle de détection d'un type d'activité (patron Strategy).
/// </summary>
/// <remarks>
/// <para>
/// Chaque règle est une fonction pure : même contexte, même résultat. Elle ne connaît ni
/// le réseau, ni le disque, ni l'horloge — ce qui la rend testable en trois lignes et
/// permet d'écrire les tests **avant** l'implémentation (TDD).
/// </para>
/// <para>
/// Pour ajouter un type de notification :
/// 1. ajouter une valeur à <see cref="NotificationKind"/> ;
/// 2. écrire le test attendu dans <c>tests/…/Detection</c> ;
/// 3. implémenter cette interface et enregistrer la règle dans
///    <see cref="PullRequestEventDetector.CreateDefaultRules"/> ;
/// 4. ajouter la préférence correspondante dans
///    <see cref="Configuration.NotificationPreferences"/>.
/// Aucune autre classe n'est à modifier.
/// </para>
/// </remarks>
public interface IPullRequestEventRule
{
    /// <summary>Nom lisible de la règle, utilisé dans les journaux.</summary>
    string Name { get; }

    /// <summary>
    /// Vrai si la règle a besoin des discussions de la PR. Permet au monitor de savoir
    /// s'il est utile de payer un appel supplémentaire (SPEC-POLL-003).
    /// </summary>
    bool RequiresThreads { get; }

    /// <summary>
    /// Examine le contexte et retourne les événements détectés (aucun, un ou plusieurs).
    /// L'implémentation doit être tolérante : données manquantes ⇒ aucun événement,
    /// jamais d'exception.
    /// </summary>
    IEnumerable<PullRequestEvent> Detect(DetectionContext context);
}
