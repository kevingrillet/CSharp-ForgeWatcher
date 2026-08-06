using System.Globalization;
using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Text;
using CSharpForgeWatcher.Ui.Localization;

namespace CSharpForgeWatcher.Ui.Views;

/// <summary>
/// Fenêtre « Activité récente » : la liste de tout ce qui a été détecté depuis le
/// démarrage, avec ouverture directe dans le navigateur.
/// </summary>
/// <remarks>
/// C'est le filet de sécurité des notifications : un toast manqué, ignoré ou remplacé par
/// une synthèse (SPEC-NOTIF-002) reste consultable ici.
/// </remarks>
public sealed class ActivityForm : Form
{
    private readonly IBrowserLauncher _browserLauncher;
    private readonly TextService _text;
    private readonly ListView _list;
    private readonly Button _openButton;
    private readonly Label _emptyLabel;

    /// <summary>Construit la fenêtre.</summary>
    /// <param name="browserLauncher">Ouverture des liens.</param>
    /// <param name="text">Formule les libellés dans la langue choisie.</param>
    public ActivityForm(IBrowserLauncher browserLauncher, TextService text)
    {
        _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
        _text = text ?? throw new ArgumentNullException(nameof(text));

        Text = _text[TextKeys.Screen.ActivityTitle];
        Icon = Tray.TrayIconFactory.LoadApplicationIcon();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 380);
        Size = new Size(980, 520);
        ShowInTaskbar = true;
        Font = new Font("Segoe UI", 9F);

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            GridLines = false,
        };

        _list.Columns.Add(_text[TextKeys.Screen.ActivityColumnTime], 110);
        _list.Columns.Add(_text[TextKeys.Screen.ActivityColumnKind], 220);
        _list.Columns.Add(_text[TextKeys.Screen.ActivityColumnSubject], 260);
        _list.Columns.Add(_text[TextKeys.Screen.ActivityColumnDetail], 340);
        _list.DoubleClick += (_, _) => OpenSelected();
        _list.KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Enter)
            {
                OpenSelected();
            }
        };

        _emptyLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = _text[TextKeys.Screen.ActivityEmpty],
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = SystemColors.GrayText,
            Visible = false,
        };

        _openButton = new Button
        {
            Text = _text[TextKeys.Screen.ActivityOpen],
            AutoSize = true,
            Enabled = false,
        };
        _openButton.Click += (_, _) => OpenSelected();
        _list.SelectedIndexChanged += (_, _) => _openButton.Enabled = _list.SelectedItems.Count > 0;

        var closeButton = new Button { Text = _text[TextKeys.Screen.ButtonClose], AutoSize = true };
        closeButton.Click += (_, _) => Close();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(8),
        };
        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(_openButton);

        var host = new Panel { Dock = DockStyle.Fill };
        host.Controls.Add(_list);
        host.Controls.Add(_emptyLabel);

        Controls.Add(host);
        Controls.Add(buttons);

        CancelButton = closeButton;
    }

    /// <summary>Remplit la liste, du plus récent au plus ancien.</summary>
    public void Display(IEnumerable<INotifiableEvent> events)
    {
        var ordered = events.OrderByDescending(notification => notification.OccurredOn).ToList();

        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();

            foreach (var notification in ordered)
            {
                var horodatage = notification.OccurredOn
                    .ToLocalTime()
                    .ToString("dd/MM HH:mm", CultureInfo.CurrentCulture);

                var item = new ListViewItem(horodatage) { Tag = notification };

                item.SubItems.Add(_text.Of(notification.Title));
                item.SubItems.Add(_text.Of(notification.Subject));
                item.SubItems.Add(_text.Format(
                    TextKeys.Screen.ActivityDetail,
                    _text.Of(notification.Message),
                    notification.Context));
                item.ToolTipText = notification.Url;

                _list.Items.Add(item);
            }
        }
        finally
        {
            _list.EndUpdate();
        }

        _emptyLabel.Visible = ordered.Count == 0;
        _emptyLabel.BringToFront();
        _openButton.Enabled = false;
    }

    private void OpenSelected()
    {
        if (_list.SelectedItems.Count == 0)
        {
            return;
        }

        if (_list.SelectedItems[0].Tag is INotifiableEvent notification)
        {
            _browserLauncher.Open(notification.Url);
        }
    }
}
