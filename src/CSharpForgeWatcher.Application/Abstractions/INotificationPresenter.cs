using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Abstractions;

/// <summary>
/// Port d'affichage des notifications.
/// </summary>
/// <remarks>
/// Ce port n'expose que des primitives d'affichage : la *politique* (quels événements
/// notifier, individuellement ou en synthèse) appartient à
/// <see cref="Notifications.NotificationDispatcher"/>. On peut donc changer de canal
/// — toast Windows, bulle d'info, Teams, journal — sans toucher aux règles.
/// </remarks>
public interface INotificationPresenter
{
    /// <summary>Affiche un événement. Un clic doit ouvrir <see cref="INotifiableEvent.Url"/>.</summary>
    /// <param name="notification">Événement à afficher.</param>
    /// <param name="silent">Vrai pour n'émettre aucun son.</param>
    void ShowEvent(INotifiableEvent notification, bool silent);

    /// <summary>
    /// Affiche une synthèse quand les événements sont trop nombreux (SPEC-NOTIF-002).
    /// Un clic doit ouvrir la fenêtre d'activité.
    /// </summary>
    void ShowSummary(IReadOnlyList<INotifiableEvent> notifications, bool silent);

    /// <summary>Signale un problème de fonctionnement (PAT expiré, dépôt inaccessible…).</summary>
    void ShowError(TextRef title, TextRef message);
}
