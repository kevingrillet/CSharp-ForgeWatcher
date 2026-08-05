using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Notifications;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Ui.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;

namespace CSharpForgeWatcher.Ui.Notifications;

/// <summary>
/// Arguments transportés par une notification et interprétés au clic (SPEC-NOTIF-001).
/// </summary>
public static class NotificationArguments
{
    /// <summary>Nom de l'argument portant l'action.</summary>
    public const string ActionKey = "action";

    /// <summary>Nom de l'argument portant l'URL à ouvrir.</summary>
    public const string UrlKey = "url";

    /// <summary>Action : ouvrir une URL.</summary>
    public const string OpenUrlAction = "open-url";

    /// <summary>Action : afficher la fenêtre d'activité.</summary>
    public const string ShowActivityAction = "show-activity";
}

/// <summary>
/// Ce qu'il faut faire quand l'utilisateur clique sur une notification.
/// </summary>
/// <remarks>
/// Type intermédiaire volontaire : les toasts transportent leurs arguments sous forme de
/// chaîne (encodée par la bibliothèque), les bulles d'info non. Convertir les deux vers ce
/// type évite de réutiliser un format de sérialisation là où il n'a pas cours — et donc
/// tout risque d'URL mal décodée, les liens de discussion contenant déjà « ?…= ».
/// </remarks>
/// <param name="Action">Action demandée (voir <see cref="NotificationArguments"/>).</param>
/// <param name="Url">URL à ouvrir, le cas échéant.</param>
public sealed record NotificationActivation(string Action, string? Url = null)
{
    /// <summary>Activation « ouvrir cette URL ».</summary>
    public static NotificationActivation OpenUrl(string url)
        => new(NotificationArguments.OpenUrlAction, url);

    /// <summary>Activation « afficher l'activité récente ».</summary>
    public static readonly NotificationActivation ShowActivity = new(NotificationArguments.ShowActivityAction);
}

/// <summary>
/// Affiche les notifications sous forme de toasts Windows (centre de notifications
/// compris), avec un argument qui rend le clic exploitable même différé.
/// </summary>
/// <remarks>
/// Repose sur <c>Microsoft.Toolkit.Uwp.Notifications</c>, qui enregistre automatiquement
/// l'application (AUMID + serveur COM) au premier affichage. Si cet enregistrement échoue
/// — stratégie de groupe, notifications désactivées, session sans bureau —, une exception
/// est levée : <see cref="FallbackNotificationPresenter"/> bascule alors sur les bulles
/// d'info (SPEC-NOTIF-004).
/// </remarks>
public sealed class ToastNotificationPresenter : INotificationPresenter
{
    private const string ToastGroup = "ForgeWatcher";

    private readonly TextService _text;

    /// <summary>Construit le presenter.</summary>
    /// <param name="text">Formule les textes de synthèse dans la langue choisie.</param>
    public ToastNotificationPresenter(TextService text)
        => _text = text ?? throw new ArgumentNullException(nameof(text));

    /// <inheritdoc />
    public void ShowEvent(INotifiableEvent notification, bool silent)
    {
        var builder = new ToastContentBuilder()
            .AddArgument(NotificationArguments.ActionKey, NotificationArguments.OpenUrlAction)
            .AddArgument(NotificationArguments.UrlKey, notification.Url)
            .AddText(_text.Of(notification.Title))
            .AddText(_text.Of(notification.Message))
            .AddAttributionText(_text.Of(notification.Subject))
            .AddAudio(new ToastAudio { Silent = silent });

        builder.Show(toast =>
        {
            toast.Group = ToastGroup;
            // Le tag dédoublonne côté Windows : un même fait ré-affiché remplace le
            // précédent au lieu de s'empiler dans le centre de notifications.
            toast.Tag = NotificationTag.For(notification.EffectiveDedupKey);
        });
    }

    /// <inheritdoc />
    public void ShowSummary(IReadOnlyList<INotifiableEvent> notifications, bool silent)
    {
        var repositories = notifications
            .Select(notification => notification.Context)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var builder = new ToastContentBuilder()
            .AddArgument(NotificationArguments.ActionKey, NotificationArguments.ShowActivityAction)
            .AddText(_text.Format(TextKeys.Screen.ToastSummaryTitle, notifications.Count))
            .AddText(string.Join(", ", repositories))
            .AddAttributionText(_text[TextKeys.Screen.ToastSummaryHint])
            .AddAudio(new ToastAudio { Silent = silent });

        builder.Show(toast =>
        {
            toast.Group = ToastGroup;
            toast.Tag = "summary";
        });
    }

    /// <inheritdoc />
    public void ShowError(TextRef title, TextRef message)
    {
        new ToastContentBuilder()
            .AddArgument(NotificationArguments.ActionKey, NotificationArguments.ShowActivityAction)
            .AddText(_text.Of(title))
            .AddText(_text.Of(message))
            .AddAudio(new ToastAudio { Silent = true })
            .Show(toast =>
            {
                toast.Group = ToastGroup;
                toast.Tag = "error";
            });
    }
}

/// <summary>
/// Repli : affiche les notifications avec les bulles d'info de la zone de notification.
/// </summary>
/// <remarks>
/// Toujours disponible (aucun enregistrement système requis), mais sans historique : la
/// bulle disparaît au bout de quelques secondes. On ne peut suivre que le dernier clic,
/// d'où la mémorisation de la dernière cible.
/// </remarks>
public sealed class BalloonNotificationPresenter : INotificationPresenter
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Action<NotificationActivation> _onActivated;
    private readonly TextService _text;
    private NotificationActivation? _pendingActivation;

    /// <summary>Construit le presenter et branche le clic sur la bulle.</summary>
    /// <param name="notifyIcon">Icône hôte.</param>
    /// <param name="onActivated">Traitement du clic.</param>
    /// <param name="text">Formule les textes de synthèse dans la langue choisie.</param>
    public BalloonNotificationPresenter(
        NotifyIcon notifyIcon,
        Action<NotificationActivation> onActivated,
        TextService text)
    {
        _notifyIcon = notifyIcon ?? throw new ArgumentNullException(nameof(notifyIcon));
        _onActivated = onActivated ?? throw new ArgumentNullException(nameof(onActivated));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _notifyIcon.BalloonTipClicked += (_, _) =>
        {
            if (_pendingActivation is { } activation)
            {
                _onActivated(activation);
            }
        };
    }

    /// <inheritdoc />
    public void ShowEvent(INotifiableEvent notification, bool silent)
    {
        _pendingActivation = NotificationActivation.OpenUrl(notification.Url);
        Show(
            _text.Of(notification.Title),
            $"{_text.Of(notification.Message)}{Environment.NewLine}{_text.Of(notification.Subject)}");
    }

    /// <inheritdoc />
    public void ShowSummary(IReadOnlyList<INotifiableEvent> notifications, bool silent)
    {
        _pendingActivation = NotificationActivation.ShowActivity;
        Show(
            _text.Format(TextKeys.Screen.BalloonSummaryTitle, notifications.Count),
            _text[TextKeys.Screen.BalloonSummaryBody]);
    }

    /// <inheritdoc />
    public void ShowError(TextRef title, TextRef message)
    {
        _pendingActivation = NotificationActivation.ShowActivity;
        _notifyIcon.ShowBalloonTip(8000, _text.Of(title), _text.Of(message), ToolTipIcon.Warning);
    }

    private void Show(string title, string message)
        => _notifyIcon.ShowBalloonTip(6000, title, message, ToolTipIcon.Info);
}

/// <summary>
/// Enchaîne un canal principal et un canal de secours (patron Decorator, SPEC-NOTIF-004).
/// </summary>
/// <remarks>
/// Au premier échec du canal principal, le basculement est <b>définitif</b> pour la durée
/// de la session : inutile de réessayer un mécanisme indisponible à chaque notification,
/// et l'utilisateur garde un comportement stable.
/// </remarks>
public sealed class FallbackNotificationPresenter : INotificationPresenter
{
    private readonly INotificationPresenter _primary;
    private readonly INotificationPresenter _fallback;
    private readonly ILogger? _logger;
    private bool _primaryFailed;

    /// <summary>Construit le presenter composite.</summary>
    public FallbackNotificationPresenter(
        INotificationPresenter primary,
        INotificationPresenter fallback,
        ILogger? logger = null)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _logger = logger;
    }

    /// <summary>Vrai si le canal principal a été abandonné.</summary>
    public bool UsingFallback => _primaryFailed;

    /// <inheritdoc />
    public void ShowEvent(INotifiableEvent notification, bool silent)
        => Execute(presenter => presenter.ShowEvent(notification, silent));

    /// <inheritdoc />
    public void ShowSummary(IReadOnlyList<INotifiableEvent> notifications, bool silent)
        => Execute(presenter => presenter.ShowSummary(notifications, silent));

    /// <inheritdoc />
    public void ShowError(TextRef title, TextRef message)
        => Execute(presenter => presenter.ShowError(title, message));

    private void Execute(Action<INotificationPresenter> action)
    {
        if (!_primaryFailed)
        {
            try
            {
                action(_primary);
                return;
            }
            catch (Exception exception)
            {
                _primaryFailed = true;
                _logger?.LogWarning(
                    exception,
                    "Les toasts Windows sont indisponibles : bascule définitive sur les bulles d'info.");
            }
        }

        try
        {
            action(_fallback);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Aucun canal de notification disponible.");
        }
    }
}

/// <summary>
/// Presenter à liaison tardive (patron Null Object).
/// </summary>
/// <remarks>
/// Résout une dépendance circulaire : le presenter réel a besoin de l'icône de la zone de
/// notification, elle-même créée par le contexte d'application, qui dépend du moniteur,
/// qui dépend du presenter. Cette coquille est injectée partout, puis reliée au vrai
/// presenter une fois l'icône créée. Avant liaison, elle ne fait rien — ce qui est le
/// comportement correct, puisqu'aucune interface utilisateur n'est encore visible.
/// </remarks>
public sealed class DeferredNotificationPresenter : INotificationPresenter
{
    private INotificationPresenter? _target;

    /// <summary>Relie la coquille au presenter réel.</summary>
    public void Bind(INotificationPresenter presenter) => _target = presenter;

    /// <inheritdoc />
    public void ShowEvent(INotifiableEvent notification, bool silent)
        => _target?.ShowEvent(notification, silent);

    /// <inheritdoc />
    public void ShowSummary(IReadOnlyList<INotifiableEvent> notifications, bool silent)
        => _target?.ShowSummary(notifications, silent);

    /// <inheritdoc />
    public void ShowError(TextRef title, TextRef message) => _target?.ShowError(title, message);
}
