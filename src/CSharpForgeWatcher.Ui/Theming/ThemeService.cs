using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Theming;

namespace CSharpForgeWatcher.Ui.Theming;

/// <summary>
/// Couleurs d'un thème. Deux instances suffisent : clair et sombre.
/// </summary>
/// <remarks>
/// Palette volontairement sobre et contrastée. Les libellés secondaires ont leur propre
/// couleur : <c>SystemColors.GrayText</c> devient illisible sur fond sombre
/// (SPEC-UI-THEME-003).
/// </remarks>
public sealed record ThemePalette
{
    /// <summary>Fond des fenêtres.</summary>
    public required Color Background { get; init; }

    /// <summary>Fond des zones de saisie et des listes.</summary>
    public required Color SurfaceBackground { get; init; }

    /// <summary>Texte principal.</summary>
    public required Color Foreground { get; init; }

    /// <summary>Texte secondaire (explications, statuts).</summary>
    public required Color MutedForeground { get; init; }

    /// <summary>Bordures et séparateurs.</summary>
    public required Color Border { get; init; }

    /// <summary>Fond des boutons.</summary>
    public required Color ButtonBackground { get; init; }

    /// <summary>Couleur d'accentuation (sélection, liens).</summary>
    public required Color Accent { get; init; }

    /// <summary>Palette claire.</summary>
    public static ThemePalette Light { get; } = new()
    {
        Background = Color.FromArgb(243, 243, 243),
        SurfaceBackground = Color.White,
        Foreground = Color.FromArgb(28, 28, 28),
        MutedForeground = Color.FromArgb(96, 96, 96),
        Border = Color.FromArgb(200, 200, 200),
        ButtonBackground = Color.FromArgb(252, 252, 252),
        Accent = Color.FromArgb(0, 106, 190),
    };

    /// <summary>Palette sombre.</summary>
    public static ThemePalette Dark { get; } = new()
    {
        Background = Color.FromArgb(32, 32, 32),
        SurfaceBackground = Color.FromArgb(43, 43, 43),
        Foreground = Color.FromArgb(240, 240, 240),
        MutedForeground = Color.FromArgb(168, 168, 168),
        Border = Color.FromArgb(70, 70, 70),
        ButtonBackground = Color.FromArgb(56, 56, 56),
        Accent = Color.FromArgb(96, 168, 255),
    };

    /// <summary>Palette correspondant au thème effectif.</summary>
    public static ThemePalette For(EffectiveTheme theme)
        => theme == EffectiveTheme.Dark ? Dark : Light;
}

/// <summary>
/// Applique le thème aux fenêtres de l'application (SPEC-UI-THEME-003).
/// </summary>
/// <remarks>
/// <para>
/// WinForms n'a pas de thème sombre complet : on peint donc explicitement les contrôles.
/// Les fenêtres s'enregistrent (<see cref="Register"/>) et sont repeintes à chaque
/// changement de préférence — sans redémarrage.
/// </para>
/// <para>
/// La barre de titre, elle, dépend du mode couleur de l'application, appliqué une fois au
/// démarrage par <c>Program</c> : c'est le seul aspect qui demande un redémarrage pour
/// changer, ce que la fenêtre de configuration indique.
/// </para>
/// </remarks>
public sealed class ThemeService
{
    private readonly ConfigurationService _configuration;
    private readonly ISystemThemeProbe _systemThemeProbe;
    private readonly List<Form> _registeredForms = [];

    /// <summary>Construit le service et s'abonne aux changements de configuration.</summary>
    public ThemeService(ConfigurationService configuration, ISystemThemeProbe systemThemeProbe)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _systemThemeProbe = systemThemeProbe ?? throw new ArgumentNullException(nameof(systemThemeProbe));
        _configuration.Changed += (_, _) => Reapply();
    }

    /// <summary>Thème actuellement effectif.</summary>
    public EffectiveTheme Current
        => ThemeResolver.Resolve(_configuration.Current.Theme, _systemThemeProbe.PrefersDarkTheme());

    /// <summary>Palette actuellement effective.</summary>
    public ThemePalette Palette => ThemePalette.For(Current);

    /// <summary>
    /// Enregistre une fenêtre : elle est peinte immédiatement, puis à chaque changement de
    /// thème, et retirée automatiquement à sa fermeture.
    /// </summary>
    public void Register(Form form)
    {
        ArgumentNullException.ThrowIfNull(form);

        if (!_registeredForms.Contains(form))
        {
            _registeredForms.Add(form);
            form.FormClosed += (_, _) => _registeredForms.Remove(form);
        }

        Apply(form);
    }

    /// <summary>Repeint toutes les fenêtres enregistrées.</summary>
    public void Reapply()
    {
        foreach (var form in _registeredForms.ToList())
        {
            if (!form.IsDisposed)
            {
                Apply(form);
            }
        }
    }

    /// <summary>Applique la palette courante à une fenêtre et à tous ses contrôles.</summary>
    public void Apply(Form form) => Apply(form, Palette);

    /// <summary>
    /// Prévisualise une préférence sur une seule fenêtre, sans modifier la configuration.
    /// </summary>
    /// <remarks>
    /// Utilisé par la fenêtre de configuration : l'utilisateur voit le résultat en
    /// choisissant, et « Annuler » rétablit l'état enregistré (SPEC-UI-THEME-003).
    /// </remarks>
    public void ApplyPreview(Form form, ThemePreference preference)
        => Apply(form, ThemePalette.For(ThemeResolver.Resolve(preference, _systemThemeProbe.PrefersDarkTheme())));

    private static void Apply(Form form, ThemePalette palette)
    {
        ArgumentNullException.ThrowIfNull(form);

        form.BackColor = palette.Background;
        form.ForeColor = palette.Foreground;

        ApplyToChildren(form, palette);
        form.Invalidate(invalidateChildren: true);
    }

    /// <summary>Renderer de menu contextuel accordé au thème (SPEC-UI-THEME-004).</summary>
    public ToolStripRenderer CreateMenuRenderer() => new ThemedMenuRenderer(Palette);

    private static void ApplyToChildren(Control parent, ThemePalette palette)
    {
        foreach (Control control in parent.Controls)
        {
            switch (control)
            {
                case TextBox textBox:
                    textBox.BackColor = palette.SurfaceBackground;
                    textBox.ForeColor = palette.Foreground;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case NumericUpDown numeric:
                    numeric.BackColor = palette.SurfaceBackground;
                    numeric.ForeColor = palette.Foreground;
                    break;

                case ComboBox comboBox:
                    comboBox.BackColor = palette.SurfaceBackground;
                    comboBox.ForeColor = palette.Foreground;
                    comboBox.FlatStyle = FlatStyle.Flat;
                    break;

                case TreeView treeView:
                    treeView.BackColor = palette.SurfaceBackground;
                    treeView.ForeColor = palette.Foreground;
                    treeView.LineColor = palette.Border;
                    treeView.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ListBox listBox:
                    listBox.BackColor = palette.SurfaceBackground;
                    listBox.ForeColor = palette.Foreground;
                    listBox.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ListView listView:
                    listView.BackColor = palette.SurfaceBackground;
                    listView.ForeColor = palette.Foreground;
                    listView.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case Button button:
                    button.BackColor = palette.ButtonBackground;
                    button.ForeColor = palette.Foreground;
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = palette.Border;
                    break;

                case LinkLabel linkLabel:
                    linkLabel.BackColor = Color.Transparent;
                    linkLabel.LinkColor = palette.Accent;
                    linkLabel.ActiveLinkColor = palette.Accent;
                    linkLabel.VisitedLinkColor = palette.Accent;
                    break;

                case Label label:
                    label.BackColor = Color.Transparent;
                    // Un libellé déjà marqué comme secondaire le reste, avec la teinte du thème.
                    label.ForeColor = IsMuted(label) ? palette.MutedForeground : palette.Foreground;
                    break;

                case CheckBox checkBox:
                    checkBox.BackColor = Color.Transparent;
                    checkBox.ForeColor = palette.Foreground;
                    break;

                case ThemedTabControl themedTabControl:
                    themedTabControl.ApplyPalette(palette);
                    break;

                case TabPage tabPage:
                    // Tant que le thème visuel est actif, Windows repeint son fond clair
                    // par-dessus la couleur demandée.
                    tabPage.UseVisualStyleBackColor = false;
                    tabPage.BackColor = palette.Background;
                    tabPage.ForeColor = palette.Foreground;
                    break;

                default:
                    control.BackColor = palette.Background;
                    control.ForeColor = palette.Foreground;
                    break;
            }

            if (control.HasChildren)
            {
                ApplyToChildren(control, palette);
            }
        }
    }

    /// <summary>
    /// Reconnaît un libellé secondaire : il a été créé avec la couleur « texte grisé » du
    /// système, ou l'une des deux teintes secondaires des palettes.
    /// </summary>
    private static bool IsMuted(Label label)
        => label.ForeColor == SystemColors.GrayText
           || label.ForeColor == ThemePalette.Light.MutedForeground
           || label.ForeColor == ThemePalette.Dark.MutedForeground;

    /// <summary>
    /// Rendu du menu de la zone de notification accordé au thème.
    /// </summary>
    /// <remarks>
    /// Windows ne thématise pas les menus WinForms : sans ce renderer, le menu reste blanc
    /// en thème sombre.
    /// </remarks>
    private sealed class ThemedMenuRenderer(ThemePalette palette)
        : ToolStripProfessionalRenderer(new ThemedColorTable(palette))
    {
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item?.Enabled == true ? palette.Foreground : palette.MutedForeground;
            base.OnRenderItemText(e);
        }
    }

    /// <summary>Couleurs du menu contextuel.</summary>
    private sealed class ThemedColorTable(ThemePalette palette) : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => palette.SurfaceBackground;

        public override Color ImageMarginGradientBegin => palette.SurfaceBackground;

        public override Color ImageMarginGradientMiddle => palette.SurfaceBackground;

        public override Color ImageMarginGradientEnd => palette.SurfaceBackground;

        public override Color MenuItemSelected => palette.Accent;

        public override Color MenuItemSelectedGradientBegin => palette.Accent;

        public override Color MenuItemSelectedGradientEnd => palette.Accent;

        public override Color MenuItemBorder => palette.Accent;

        public override Color MenuBorder => palette.Border;

        public override Color SeparatorDark => palette.Border;

        public override Color SeparatorLight => palette.Border;
    }
}
