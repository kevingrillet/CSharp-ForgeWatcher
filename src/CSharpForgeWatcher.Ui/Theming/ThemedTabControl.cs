namespace CSharpForgeWatcher.Ui.Theming;

/// <summary>
/// Contrôle à onglets entièrement peint par l'application (SPEC-UI-THEME-003).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TabControl"/> délègue son rendu au contrôle commun Win32, qui ignore
/// <see cref="Control.BackColor"/> : en thème sombre, la bande d'onglets restait claire
/// au-dessus d'une fenêtre sombre. Peindre le contrôle soi-même est le seul recours — c'est
/// la même raison qui impose <c>ThemedMenuRenderer</c> pour le menu de la zone de
/// notification.
/// </para>
/// <para>
/// Les pages, elles, sont de vraies fenêtres filles qui se peignent seules, à condition que
/// <see cref="TabPage.UseVisualStyleBackColor"/> soit désactivé : tant qu'il vaut
/// <c>true</c>, Windows repeint son propre fond clair par-dessus la couleur demandée.
/// </para>
/// </remarks>
public sealed class ThemedTabControl : TabControl
{
    private ThemePalette _palette = ThemePalette.Light;

    /// <summary>Construit le contrôle en prenant la main sur son rendu.</summary>
    public ThemedTabControl()
        => SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);

    /// <summary>Applique une palette au contrôle et à ses pages, puis repeint.</summary>
    public void ApplyPalette(ThemePalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);

        _palette = palette;
        BackColor = palette.Background;
        ForeColor = palette.Foreground;

        foreach (TabPage page in TabPages)
        {
            page.UseVisualStyleBackColor = false;
            page.BackColor = palette.Background;
            page.ForeColor = palette.Foreground;
        }

        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);

        // Un contrôle peint par l'application ne réinvalide que la zone changée : sans cela,
        // l'onglet quitté resterait dessiné comme actif.
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        using (var background = new SolidBrush(_palette.Background))
        {
            e.Graphics.FillRectangle(background, ClientRectangle);
        }

        // Encadre le corps de la page : c'est ce trait que Windows dessinait, et sans lui
        // la bande d'onglets et le contenu se confondent.
        using (var border = new Pen(_palette.Border))
        {
            e.Graphics.DrawRectangle(border, Rectangle.Inflate(DisplayRectangle, 1, 1));
        }

        for (var index = 0; index < TabCount; index++)
        {
            DrawTab(e.Graphics, index);
        }
    }

    /// <summary>Dessine un onglet : fond, liseré et libellé.</summary>
    /// <remarks>
    /// L'onglet actif prend le fond de la page pour se fondre avec elle, et un liseré
    /// d'accentuation remplace le relief que Windows dessinait.
    /// </remarks>
    private void DrawTab(Graphics graphics, int index)
    {
        var bounds = GetTabRect(index);
        var isSelected = index == SelectedIndex;

        using (var fill = new SolidBrush(isSelected ? _palette.Background : _palette.ButtonBackground))
        {
            graphics.FillRectangle(fill, bounds);
        }

        if (isSelected)
        {
            using var accent = new SolidBrush(_palette.Accent);
            graphics.FillRectangle(accent, bounds.X, bounds.Y, bounds.Width, 2);
        }
        else
        {
            using var border = new Pen(_palette.Border);
            graphics.DrawRectangle(border, bounds);
        }

        TextRenderer.DrawText(
            graphics,
            TabPages[index].Text,
            Font,
            bounds,
            isSelected ? _palette.Foreground : _palette.MutedForeground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
