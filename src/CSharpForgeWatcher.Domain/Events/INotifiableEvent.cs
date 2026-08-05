using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Domain.Events;

/// <summary>
/// Ce que toute activité surveillée doit savoir dire pour être notifiée et listée.
/// </summary>
/// <remarks>
/// <para>
/// L'application ne surveille pas qu'un seul genre d'objet : des pull requests
/// (<see cref="PullRequestEvent"/>) et des pipelines (<see cref="PipelineEvent"/>) — demain
/// peut-être autre chose. Plutôt que d'apprendre chaque genre à la couche de présentation,
/// on la fait dépendre de ce seul contrat : le diffuseur de notifications, les presenters,
/// la fenêtre d'activité et le compteur de non-lus ne connaissent que
/// <see cref="INotifiableEvent"/>.
/// </para>
/// <para>
/// Ajouter un genre d'objet surveillé consiste donc à implémenter cette interface, sans
/// toucher à l'affichage.
/// </para>
/// </remarks>
public interface INotifiableEvent
{
    /// <summary>Type d'événement : détermine le titre et le filtre de préférence.</summary>
    NotificationKind Kind { get; }

    /// <summary>Titre affiché (première ligne de la notification).</summary>
    /// <remarks>
    /// Un message désigné par sa clé, jamais une phrase : l'événement dit ce qui s'est passé,
    /// l'interface le dit dans la langue de l'utilisateur (SPEC-UI-LANG-002). Un événement
    /// mémorisé se relit donc dans la langue courante, même si elle a changé depuis.
    /// </remarks>
    TextRef Title { get; }

    /// <summary>Corps du message.</summary>
    TextRef Message { get; }

    /// <summary>
    /// Sujet concerné, affiché en attribution : « !1234 — titre de la PR », « CI · #418 »…
    /// C'est ce qui évite de répéter le contexte dans <see cref="Message"/>.
    /// </summary>
    TextRef Subject { get; }

    /// <summary>Emplacement du sujet : « projet / dépôt • !1234 », « projet • pipeline »…</summary>
    string Context { get; }

    /// <summary>URL ouverte au clic (SPEC-NOTIF-001).</summary>
    string Url { get; }

    /// <summary>Date de l'événement, ou de sa détection à défaut.</summary>
    DateTimeOffset OccurredOn { get; }

    /// <summary>Personne à l'origine de l'événement, si elle est identifiable.</summary>
    string? ActorName { get; }

    /// <summary>
    /// Clé de déduplication : deux événements de même clé décrivent le même fait et ne
    /// doivent être notifiés qu'une fois.
    /// </summary>
    string EffectiveDedupKey { get; }
}
