using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Text;
using CSharpForgeWatcher.Application.Theming;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Ui.Views;

// Onglet « Préférences » : types notifiés, rythme, apparence.
// La fenêtre est déclarée en plusieurs fichiers : voir SettingsForm.cs pour ses
// champs, son assemblage et son enregistrement.
public sealed partial class SettingsForm
{
    // ---------------------------------------------------------------- onglet Préférences

    private TabPage BuildPreferencesTab()
    {
        var page = new TabPage(_text[TextKeys.Screen.TabPreferences]) { Padding = new Padding(12) };

        var kinds = new GroupBox
        {
            Text = _text[TextKeys.Screen.PreferencesKinds],
            Dock = DockStyle.Top,
            Height = 260,
        };

        var kindLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(8),
        };

        var tooltip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 300 };

        foreach (var kind in NotificationKindExtensions.All)
        {
            var checkBox = new CheckBox
            {
                Text = _text[TextKeys.KindLabel(kind)],
                AutoSize = true,
                Checked = _draft.Notifications.IsEnabled(kind),
            };

            tooltip.SetToolTip(checkBox, _text[TextKeys.KindDescription(kind)]);
            _kindCheckBoxes[kind] = checkBox;
            kindLayout.Controls.Add(checkBox);
        }

        _operationalErrorsBox.Text = _text[TextKeys.Screen.PreferencesOperationalErrors];
        _operationalErrorsBox.AutoSize = true;
        kindLayout.Controls.Add(_operationalErrorsBox);

        kinds.Controls.Add(kindLayout);

        var options = new GroupBox { Text = _text[TextKeys.Screen.PreferencesBehaviour], Dock = DockStyle.Fill };
        var optionsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            Padding = new Padding(8),
        };
        optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _pollIntervalBox.Minimum = WatcherConfiguration.MinimumPollIntervalSeconds;
        _pollIntervalBox.Maximum = 3600;
        _pollIntervalBox.Increment = 30;
        _pollIntervalBox.Width = 90;

        _maxNotificationsBox.Minimum = 1;
        _maxNotificationsBox.Maximum = 30;
        _maxNotificationsBox.Width = 90;

        _refreshMinutesBox.Minimum = 1;
        _refreshMinutesBox.Maximum = 240;
        _refreshMinutesBox.Increment = 5;
        _refreshMinutesBox.Width = 90;

        _threadScopeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _threadScopeBox.Width = 320;
        _threadScopeBox.Items.Add(_text[TextKeys.Screen.ThreadScopeInvolved]);
        _threadScopeBox.Items.Add(_text[TextKeys.Screen.ThreadScopeAll]);

        _themeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _themeBox.Width = 320;
        foreach (var preference in ThemeResolver.All)
        {
            _themeBox.Items.Add(_text[preference.ToLabelKey()]);
        }

        // Aperçu immédiat : le thème s'applique dès la sélection (SPEC-UI-THEME-003).
        _themeBox.SelectedIndexChanged += (_, _) => PreviewTheme();

        _languageBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _languageBox.Width = 320;
        foreach (var preference in LanguageResolver.All)
        {
            _languageBox.Items.Add(_text.LabelOf(preference));
        }

        _ownActionsBox.Text = _text[TextKeys.Screen.PreferencesOwnActions];
        _ownActionsBox.AutoSize = true;
        _soundBox.Text = _text[TextKeys.Screen.PreferencesSound];
        _soundBox.AutoSize = true;
        _startupBox.Text = _text[TextKeys.Screen.PreferencesStartup];
        _startupBox.AutoSize = true;

        AddRow(optionsLayout, _text[TextKeys.Screen.PreferencesLanguage], _languageBox);
        AddRow(optionsLayout, _text[TextKeys.Screen.PreferencesTheme], _themeBox);
        AddRow(optionsLayout, _text[TextKeys.Screen.PreferencesInterval], _pollIntervalBox);
        AddRow(optionsLayout, _text[TextKeys.Screen.PreferencesMaxNotifications], _maxNotificationsBox);
        AddRow(optionsLayout, _text[TextKeys.Screen.PreferencesThreadScope], _threadScopeBox);
        AddRow(optionsLayout, _text[TextKeys.Screen.PreferencesRefreshMinutes], _refreshMinutesBox);
        AddRow(optionsLayout, string.Empty, _ownActionsBox);
        AddRow(optionsLayout, string.Empty, _soundBox);
        AddRow(optionsLayout, string.Empty, _startupBox);

        options.Controls.Add(optionsLayout);

        page.Controls.Add(options);
        page.Controls.Add(kinds);
        return page;
    }

    /// <summary>
    /// Applique le thème choisi à cette fenêtre, sans enregistrer la configuration.
    /// </summary>
    /// <remarks>
    /// La configuration active n'est pas touchée : si l'utilisateur annule, rien n'a changé.
    /// </remarks>
    private void PreviewTheme()
    {
        if (_themeBox.SelectedIndex < 0)
        {
            return;
        }

        _draft.Theme = ThemeResolver.All[_themeBox.SelectedIndex];
        _themeService.ApplyPreview(this, _draft.Theme);
    }
}
