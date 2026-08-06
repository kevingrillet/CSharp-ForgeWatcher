using System.Drawing.Drawing2D;

namespace CSharpForgeWatcher.LogoGenerator;

/// <summary>
/// Traduction en GDI+ de la géométrie de <c>assets/logo/forge-watcher.svg</c>.
/// Toute retouche du SVG doit être répercutée ici (et réciproquement) : le SVG est le
/// maître, cette classe est la seule à savoir le rasteriser.
/// </summary>
internal static class LogoPainter
{
    /// <summary>Côté de la grille de conception : toutes les coordonnées sont en trente-deuxièmes.</summary>
    private const float Grid = 32f;

    private static readonly Color Blue = Color.FromArgb(0, 106, 190);
    private static readonly Color Accent = Color.FromArgb(255, 179, 0);

    /// <summary>Dessine le logo sur toute la surface d'un carré de <paramref name="side"/> pixels.</summary>
    public static void Paint(Graphics graphics, int side)
    {
        var k = side / Grid;

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Disque : 1 unité de marge, les icônes Windows étant rognées de près.
        using var disc = new SolidBrush(Blue);
        graphics.FillEllipse(disc, 1f * k, 1f * k, 30f * k, 30f * k);

        // Branches convergentes + tronc commun, traits épais à extrémités arrondies.
        using var stroke = new Pen(Color.White, 4.5f * k)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        using var branches = new GraphicsPath();
        branches.AddLine(10.5f * k, 22.5f * k, 16f * k, 17f * k);
        branches.AddLine(16f * k, 17f * k, 21.5f * k, 22.5f * k);
        graphics.DrawPath(stroke, branches);

        // Le tronc chevauche la base de la flèche : les deux formes fusionnent sans couture.
        graphics.DrawLine(stroke, 16f * k, 17f * k, 16f * k, 12.5f * k);

        // Pointe de fusion : triangle plein, nettement plus large que le tronc — c'est ce qui
        // reste identifiable à 16 x 16, où le trait ne fait plus que 2 px.
        using var white = new SolidBrush(Color.White);
        graphics.FillPolygon(white, [
            new PointF(16f * k, 5.5f * k),
            new PointF(10f * k, 13f * k),
            new PointF(22f * k, 13f * k),
        ]);

        // Un seul nœud, à droite : la branche entrante (la pull request). L'accent ambre suffit,
        // un second nœud à gauche surchargerait l'icône aux petites tailles.
        using var accent = new SolidBrush(Accent);
        DrawNode(graphics, accent, 21.5f * k, 22.5f * k, 3.4f * k);
    }

    private static void DrawNode(Graphics graphics, Brush brush, float x, float y, float radius)
        => graphics.FillEllipse(brush, x - radius, y - radius, radius * 2f, radius * 2f);
}
