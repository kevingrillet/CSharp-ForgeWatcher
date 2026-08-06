using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Text;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Application.Notifications;

/// <summary>
/// Décide ce qui est notifié, et comment (SPEC-NOTIF-002, SPEC-NOTIF-003).
/// </summary>
/// <remarks>
/// La *politique* de notification vit ici, la *technique* d'affichage dans
/// <see cref="INotificationPresenter"/>. Séparation utile : on peut tester la politique
/// sans Windows, et changer de canal d'affichage sans toucher aux règles.
/// </remarks>
public sealed class NotificationDispatcher
{
    private readonly INotificationPresenter _presenter;
    private readonly ILogger<NotificationDispatcher>? _logger;

    /// <summary>Construit le diffuseur.</summary>
    public NotificationDispatcher(
        INotificationPresenter presenter,
        ILogger<NotificationDispatcher>? logger = null)
    {
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _logger = logger;
    }

    /// <summary>
    /// Filtre les événements selon les préférences puis les affiche.
    /// </summary>
    /// <returns>
    /// Les événements retenus, dans l'ordre d'affichage. C'est cette liste — et non la
    /// liste brute — qui alimente la fenêtre d'activité et le compteur de non-lus
    /// (SPEC-NOTIF-003 : un type désactivé n'apparaît nulle part).
    /// </returns>
    public IReadOnlyList<INotifiableEvent> Dispatch(
        IReadOnlyList<INotifiableEvent> events,
        WatcherConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(configuration);

        var retained = events
            .Where(notification => configuration.Notifications.IsEnabled(notification.Kind))
            .OrderBy(notification => (int)notification.Kind)
            .ThenBy(notification => notification.OccurredOn)
            .ToList();

        if (retained.Count == 0)
        {
            return retained;
        }

        var silent = !configuration.PlayNotificationSound;
        var maximum = Math.Max(1, configuration.MaxNotificationsPerPoll);

        try
        {
            if (retained.Count <= maximum)
            {
                foreach (var notification in retained)
                {
                    _presenter.ShowEvent(notification, silent);
                }
            }
            else
            {
                // Au-delà du seuil : une seule synthèse, sinon l'utilisateur reçoit une
                // rafale (typiquement au retour de congés ou après une longue coupure).
                _presenter.ShowSummary(retained, silent);
            }
        }
        catch (Exception exception)
        {
            // Un échec d'affichage ne doit jamais interrompre la surveillance :
            // les événements restent consultables dans la fenêtre d'activité.
            _logger?.LogError(exception, "Échec de l'affichage de {Count} notification(s).", retained.Count);
        }

        return retained;
    }

    /// <summary>
    /// Signale un problème de fonctionnement, si l'utilisateur a laissé ces alertes actives.
    /// </summary>
    public void NotifyProblem(TextRef title, TextRef message, WatcherConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configuration.Notifications.OperationalErrors)
        {
            return;
        }

        try
        {
            _presenter.ShowError(title, message);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Échec de l'affichage d'une alerte de fonctionnement.");
        }
    }
}
