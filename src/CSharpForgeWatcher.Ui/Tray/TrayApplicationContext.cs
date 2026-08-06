using System.Globalization;
using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Monitoring;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Ui.Localization;
using CSharpForgeWatcher.Ui.Notifications;
using CSharpForgeWatcher.Ui.Theming;
using CSharpForgeWatcher.Ui.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;

namespace CSharpForgeWatcher.Ui.Tray;

/// <summary>
/// Chef d'orchestre de l'application résidente : icône de la zone de notification, menu,
/// minuteur de surveillance, fenêtres et activation des notifications.
/// </summary>
/// <remarks>
/// <para>
/// C'est le seul composant qui connaît WinForms <i>et</i> les cas d'usage. Tout ce qui est
/// décision métier vit dans <see cref="PullRequestMonitor"/> ; ici on ne fait que
/// déclencher, afficher et router les clics.
/// </para>
/// <para>
/// Le sondage est déclenché par un <see cref="System.Windows.Forms.Timer"/> : son
/// événement arrive sur le thread d'interface, donc le code qui suit chaque <c>await</c>
/// peut manipuler les contrôles sans marshalling explicite.
/// </para>
/// </remarks>
public sealed class TrayApplicationContext : ApplicationContext
{
    private const int MaximumRecentEvents = 200;

    private readonly IServiceProvider _services;
    private readonly ConfigurationService _configuration;
    private readonly PullRequestMonitor _monitor;
    private readonly IBrowserLauncher _browserLauncher;
    private readonly DeferredNotificationPresenter _presenterHost;
    private readonly ThemeService _themeService;
    private readonly TextService _text;
    private readonly ILogger<TrayApplicationContext>? _logger;

    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly List<INotifiableEvent> _recentEvents = [];
    private readonly SynchronizationContext _uiContext;

    private Icon? _currentIcon;
    private int _unreadCount;
    private PollReport? _lastReport;
    private SettingsForm? _settingsForm;
    private ActivityForm? _activityForm;

    /// <summary>
    /// Empreinte de la configuration telle qu'elle était au dernier cycle déclenché.
    /// </summary>
    /// <remarks>
    /// Sert à distinguer un enregistrement qui change ce qui est surveillé — dépôt ajouté,
    /// jeton renouvelé — d'un enregistrement qui ne touche qu'à l'apparence.
    /// </remarks>
    private string _monitoringSignature;

    /// <summary>Monte l'application résidente et lance un premier cycle.</summary>
    public TrayApplicationContext(IServiceProvider services)
    {
        _services = services;
        _configuration = services.GetRequiredService<ConfigurationService>();
        _monitor = services.GetRequiredService<PullRequestMonitor>();
        _browserLauncher = services.GetRequiredService<IBrowserLauncher>();
        _presenterHost = services.GetRequiredService<DeferredNotificationPresenter>();
        _themeService = services.GetRequiredService<ThemeService>();
        _text = services.GetRequiredService<TextService>();
        _logger = services.GetService<ILogger<TrayApplicationContext>>();
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = _text[TextKeys.AppName],
            ContextMenuStrip = new ContextMenuStrip(),
        };

        UpdateIcon(TrayIconState.Normal);

        _notifyIcon.DoubleClick += (_, _) => ShowActivity();
        _notifyIcon.ContextMenuStrip!.Opening += (_, _) => RebuildMenu();

        // Le menu suit le thème : Windows ne thématise pas les menus WinForms
        // (SPEC-UI-THEME-004).
        ApplyMenuTheme();
        _configuration.Changed += (_, _) => ApplyMenuTheme();

        // Liaison tardive du presenter : les toasts d'abord, les bulles d'info en secours.
        _presenterHost.Bind(new FallbackNotificationPresenter(
            new ToastNotificationPresenter(_text),
            new BalloonNotificationPresenter(_notifyIcon, HandleActivation, _text),
            services.GetService<ILogger<FallbackNotificationPresenter>>()));

        RegisterToastActivation();

        _timer = new System.Windows.Forms.Timer();
        _timer.Tick += async (_, _) => await PollAsync();
        _monitoringSignature = _configuration.Current.MonitoringSignature;
        ApplyPollInterval();

        _configuration.Changed += OnConfigurationChanged;

        // Premier cycle immédiat, ou fenêtre de configuration si rien n'est paramétré.
        if (_configuration.IsUsable)
        {
            _timer.Start();
            _ = PollAsync();
        }
        else
        {
            UpdateIcon(TrayIconState.NotConfigured);
            _notifyIcon.Text = _text[TextKeys.Screen.TrayNotConfigured];
            ShowSettings();
        }
    }

    // ---------------------------------------------------------------- cycle de surveillance

    private void ApplyPollInterval()
    {
        var interval = (int)Math.Clamp(
            _configuration.Current.PollInterval.TotalMilliseconds,
            30_000,
            int.MaxValue);

        // Écrire Interval relance le décompte, même à valeur égale : un enregistrement qui ne
        // touche pas à l'intervalle ne doit pas repousser le cycle en cours d'attente.
        if (_timer.Interval != interval)
        {
            _timer.Interval = interval;
        }
    }

    private void OnConfigurationChanged(object? sender, EventArgs args)
    {
        ApplyPollInterval();

        if (!_configuration.IsUsable)
        {
            _timer.Stop();
            UpdateIcon(TrayIconState.NotConfigured);
            return;
        }

        var signature = _configuration.Current.MonitoringSignature;
        var monitoringChanged = !string.Equals(signature, _monitoringSignature, StringComparison.Ordinal);
        _monitoringSignature = signature;

        if (!_timer.Enabled)
        {
            _timer.Start();
        }

        // Un cycle immédiat ne se justifie que si ce qui est surveillé a changé. Changer de
        // thème, couper le son ou décocher le démarrage avec Windows n'a aucune raison de
        // faire repartir une salve d'appels vers les forges.
        if (monitoringChanged)
        {
            _ = PollAsync();
        }
    }

    private async Task PollAsync()
    {
        try
        {
            var report = await _monitor.PollAsync(CancellationToken.None);
            ApplyReport(report);
        }
        catch (Exception exception)
        {
            // Dernier rempart : une exception non prévue ne doit pas tuer l'application
            // résidente. Elle est journalisée et signalée par l'icône.
            _logger?.LogError(exception, "Cycle de surveillance en échec inattendu.");
            UpdateIcon(TrayIconState.Error);
            _notifyIcon.Text = Truncate(_text.Format(TextKeys.Screen.TrayError, exception.Message));
        }
    }

    private void ApplyReport(PollReport report)
    {
        if (report.Status == PollStatus.Skipped)
        {
            return;
        }

        _lastReport = report;

        if (report.Events.Count > 0)
        {
            _recentEvents.AddRange(report.Events);

            if (_recentEvents.Count > MaximumRecentEvents)
            {
                _recentEvents.RemoveRange(0, _recentEvents.Count - MaximumRecentEvents);
            }

            _unreadCount += report.Events.Count;
            _activityForm?.Display(_recentEvents);
        }

        var state = report.Status switch
        {
            PollStatus.NotConfigured => TrayIconState.NotConfigured,
            PollStatus.Failure => TrayIconState.Error,
            PollStatus.PartialFailure => TrayIconState.Warning,
            _ => TrayIconState.Normal,
        };

        UpdateIcon(state);

        var viewer = string.IsNullOrEmpty(report.ViewerName)
            ? string.Empty
            : _text.Format(TextKeys.Screen.TrayViewer, report.ViewerName);

        _notifyIcon.Text = Truncate(_text.Format(
            TextKeys.Screen.TrayTooltip,
            viewer,
            report.ToStatusLine(_text.Catalogue),
            report.CompletedOn.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture)));
    }

    /// <summary>L'infobulle d'une icône de notification est limitée à 127 caractères.</summary>
    private static string Truncate(string text)
        => text.Length <= 127 ? text : text[..124] + "…";

    private void UpdateIcon(TrayIconState state)
    {
        var icon = TrayIconFactory.Create(_unreadCount, state);
        var previous = _currentIcon;

        _notifyIcon.Icon = icon;
        _currentIcon = icon;

        // L'icône précédente n'est libérée qu'après remplacement, handle GDI compris.
        TrayIconFactory.Destroy(previous);
    }

    // ---------------------------------------------------------------- menu contextuel

    private void RebuildMenu()
    {
        var menu = _notifyIcon.ContextMenuStrip!;
        menu.Items.Clear();

        var status = _lastReport?.ToStatusLine(_text.Catalogue) ?? _text[TextKeys.Poll.Pending];
        menu.Items.Add(new ToolStripMenuItem(status) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(_text[TextKeys.Screen.MenuRefresh], null, async (_, _) => await PollAsync());

        var activityLabel = _unreadCount > 0
            ? _text.Format(TextKeys.Screen.MenuActivityUnread, _unreadCount)
            : _text[TextKeys.Screen.MenuActivity];

        menu.Items.Add(activityLabel, null, (_, _) => ShowActivity());

        menu.Items.Add(BuildPullRequestsMenu());

        if (_lastReport is { Pipelines.Count: > 0 })
        {
            menu.Items.Add(BuildPipelinesMenu());
        }

        if (_lastReport is { Warnings.Count: > 0 } report)
        {
            var warnings = new ToolStripMenuItem(
                _text.Format(TextKeys.Screen.MenuWarnings, report.Warnings.Count));
            foreach (var warning in report.Warnings.Take(10))
            {
                warnings.DropDownItems.Add(
                    new ToolStripMenuItem(Truncate(_text.Of(warning))) { Enabled = false });
            }

            menu.Items.Add(warnings);
        }

        menu.Items.Add(new ToolStripSeparator());

        if (_unreadCount > 0)
        {
            menu.Items.Add(_text[TextKeys.Screen.MenuMarkAllRead], null, (_, _) => MarkAllAsRead());
        }

        menu.Items.Add(_text[TextKeys.Screen.MenuSettings], null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_text[TextKeys.Screen.MenuQuit], null, (_, _) => ExitApplication());
    }

    /// <summary>Sous-menu listant les PR suivies, groupées par dépôt.</summary>
    private ToolStripMenuItem BuildPullRequestsMenu()
    {
        var root = new ToolStripMenuItem(_text[TextKeys.Screen.MenuPullRequests]);
        var pullRequests = _lastReport?.PullRequests ?? [];

        if (pullRequests.Count == 0)
        {
            root.DropDownItems.Add(
                new ToolStripMenuItem(_text[TextKeys.Screen.MenuNoPullRequest]) { Enabled = false });
            return root;
        }

        foreach (var group in pullRequests
            .GroupBy(view => view.Repository.DisplayPath)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var repositoryItem = new ToolStripMenuItem(group.Key);

            foreach (var view in group)
            {
                var marker = view.IsMine ? "★ " : view.ViewerIsReviewer ? "• " : string.Empty;
                var item = new ToolStripMenuItem($"{marker}{view.DisplayLabel}")
                {
                    ToolTipText = view.DisplayDetail(_text.Catalogue),
                };

                var url = view.Url;
                item.Click += (_, _) => _browserLauncher.Open(url);
                repositoryItem.DropDownItems.Add(item);
            }

            root.DropDownItems.Add(repositoryItem);
        }

        return root;
    }

    /// <summary>Sous-menu listant les pipelines surveillés et leur dernier résultat.</summary>
    private ToolStripMenuItem BuildPipelinesMenu()
    {
        var pipelines = _lastReport?.Pipelines ?? [];
        var failing = pipelines.Count(pipeline => pipeline.IsFailing);
        var title = failing > 0
            ? _text.Format(TextKeys.Screen.MenuPipelinesFailing, failing)
            : _text[TextKeys.Screen.MenuPipelines];
        var root = new ToolStripMenuItem(title);

        foreach (var pipeline in pipelines)
        {
            var item = new ToolStripMenuItem(pipeline.DisplayLabel)
            {
                ToolTipText = $"{pipeline.Definition.ProjectName} — {pipeline.DisplayDetail(_text.Catalogue)}",
            };

            var url = pipeline.Url;
            item.Click += (_, _) => _browserLauncher.Open(url);
            root.DropDownItems.Add(item);
        }

        return root;
    }

    /// <summary>Applique le thème courant au menu de la zone de notification.</summary>
    private void ApplyMenuTheme()
    {
        if (_notifyIcon.ContextMenuStrip is { } menu)
        {
            menu.Renderer = _themeService.CreateMenuRenderer();
            menu.BackColor = _themeService.Palette.SurfaceBackground;
            menu.ForeColor = _themeService.Palette.Foreground;
        }
    }

    private void MarkAllAsRead()
    {
        _unreadCount = 0;
        UpdateIcon(_lastReport?.Status switch
        {
            PollStatus.Failure => TrayIconState.Error,
            PollStatus.PartialFailure => TrayIconState.Warning,
            PollStatus.NotConfigured => TrayIconState.NotConfigured,
            _ => TrayIconState.Normal,
        });
    }

    // ---------------------------------------------------------------- fenêtres

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(
            _configuration,
            _services.GetRequiredService<ISourceControlGatewayFactory>(),
            _services.GetRequiredService<IAutoStartService>(),
            _monitor,
            _browserLauncher,
            _presenterHost,
            _themeService,
            _text);

        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void ShowActivity()
    {
        MarkAllAsRead();

        if (_activityForm is { IsDisposed: false })
        {
            _activityForm.Display(_recentEvents);
            _activityForm.Activate();
            return;
        }

        _activityForm = new ActivityForm(_browserLauncher, _text);
        _activityForm.FormClosed += (_, _) => _activityForm = null;
        _themeService.Register(_activityForm);
        _activityForm.Display(_recentEvents);
        _activityForm.Show();
        _activityForm.Activate();
    }

    // ---------------------------------------------------------------- activation des notifications

    /// <summary>
    /// Branche l'activation des toasts. L'événement arrive sur un thread d'arrière-plan :
    /// on repasse sur le thread d'interface avant de toucher aux fenêtres.
    /// </summary>
    private void RegisterToastActivation()
    {
        try
        {
            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                var activation = ParseToastArguments(toastArgs.Argument);
                _uiContext.Post(_ => HandleActivation(activation), null);
            };
        }
        catch (Exception exception)
        {
            // Environnement sans toasts : les bulles d'info prendront le relais.
            _logger?.LogWarning(exception, "Activation des toasts Windows indisponible.");
        }
    }

    /// <summary>
    /// Traduit les arguments d'un toast (chaîne encodée par la bibliothèque) en intention.
    /// </summary>
    private NotificationActivation ParseToastArguments(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return NotificationActivation.ShowActivity;
        }

        try
        {
            var parsed = ToastArguments.Parse(argument);
            var action = parsed.Contains(NotificationArguments.ActionKey)
                ? parsed[NotificationArguments.ActionKey]
                : NotificationArguments.ShowActivityAction;

            return action == NotificationArguments.OpenUrlAction && parsed.Contains(NotificationArguments.UrlKey)
                ? NotificationActivation.OpenUrl(parsed[NotificationArguments.UrlKey])
                : NotificationActivation.ShowActivity;
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Arguments de notification illisibles : {Argument}", argument);
            return NotificationActivation.ShowActivity;
        }
    }

    /// <summary>
    /// Donne suite au clic sur une notification (SPEC-NOTIF-001) : ouvrir l'URL visée, ou
    /// afficher la fenêtre d'activité.
    /// </summary>
    private void HandleActivation(NotificationActivation activation)
    {
        if (activation.Action == NotificationArguments.OpenUrlAction
            && !string.IsNullOrWhiteSpace(activation.Url))
        {
            _browserLauncher.Open(activation.Url);
            MarkAllAsRead();
            return;
        }

        ShowActivity();
    }

    // ---------------------------------------------------------------- fin de vie

    private void ExitApplication()
    {
        _timer.Stop();
        _notifyIcon.Visible = false;
        ExitThread();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _configuration.Changed -= OnConfigurationChanged;
            _timer.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            TrayIconFactory.Destroy(_currentIcon);
            _currentIcon = null;
            _settingsForm?.Dispose();
            _activityForm?.Dispose();
        }

        base.Dispose(disposing);
    }
}
