using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace CSharpForgeWatcher.Ui.Tray;

/// <summary>État visuel de l'icône de la zone de notification.</summary>
public enum TrayIconState
{
    /// <summary>Surveillance normale.</summary>
    Normal,

    /// <summary>Configuration incomplète.</summary>
    NotConfigured,

    /// <summary>Cycle partiellement en échec.</summary>
    Warning,

    /// <summary>Surveillance impossible.</summary>
    Error,
}

/// <summary>
/// Compose l'icône de la zone de notification — logo de l'application, liseré d'état et
/// pastille de non-lus (patron Factory, SPEC-UI-ICON-001 et SPEC-NOTIF-005).
/// </summary>
/// <remarks>
/// <para>
/// Le logo n'est pas redessiné à la main : il est <b>relu depuis l'icône de l'exécutable</b>
/// (<c>assets/logo/forge-watcher.ico</c>, embarqué via <c>ApplicationIcon</c>). Fenêtres, barre
/// des tâches, explorateur et zone de notification affichent donc exactement la même image,
/// et il n'y a qu'un seul fichier à régénérer (voir <c>assets/logo/README.md</c>).
/// </para>
/// <para>
/// Seules les surcouches variables — liseré d'état, pastille de non-lus — sont dessinées à
/// l'exécution : elles dépendent du nombre d'événements non lus, ce qu'aucun fichier
/// <c>.ico</c> statique ne saurait porter.
/// </para>
/// <para>
/// Si l'extraction échoue (exécutable introuvable, ressource absente, hôte de test), on
/// retombe sur un tracé GDI+ de la même géométrie : l'application affiche toujours une icône,
/// jamais un carré vide.
/// </para>
/// <para>
/// <b>Important</b> : <see cref="Icon.FromHandle"/> ne libère pas le handle GDI sous-jacent.
/// Tout appel à <see cref="Create"/> doit être suivi, lorsque l'icône n'est plus affichée,
/// d'un appel à <see cref="Destroy"/> — sinon les handles fuient à chaque cycle. En revanche
/// l'icône rendue par <see cref="LoadApplicationIcon"/> est mise en cache et appartient à la
/// fabrique : elle ne doit être ni libérée, ni passée à <see cref="Destroy"/>.
/// </para>
/// </remarks>
public static class TrayIconFactory
{
    /// <summary>Côté de l'icône composée. C'est aussi la taille rendue par l'extraction.</summary>
    private const int Size = 32;

    /// <summary>Grille de conception du logo (cf. <c>assets/logo/forge-watcher.svg</c>).</summary>
    private const float Grid = 32f;

    private static readonly Color BrandBlue = Color.FromArgb(0, 106, 190);
    private static readonly Color BrandAccent = Color.FromArgb(255, 179, 0);

    /// <summary>Protège l'initialisation paresseuse des deux caches ci-dessous.</summary>
    private static readonly object Gate = new();

    private static Icon? _applicationIcon;
    private static bool _applicationIconResolved;
    private static Bitmap? _logo;

    /// <summary>Crée une icône pour l'état et le compteur indiqués.</summary>
    /// <param name="unreadCount">Nombre d'événements non lus ; 0 pour masquer la pastille.</param>
    /// <param name="state">État de la surveillance.</param>
    public static Icon Create(int unreadCount, TrayIconState state = TrayIconState.Normal)
    {
        using var bitmap = new Bitmap(Size, Size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            ConfigureQuality(graphics);

            // AntiAliasGridFit et non ClearTypeGridFit : le rendu sous-pixel de ClearType laisse
            // des franges rouges et vertes sur les chiffres de la pastille, l'image ayant un
            // canal alpha (il n'y a pas de fond connu sur lequel filtrer les sous-pixels).
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            DrawLogo(graphics);
            DrawStateRing(graphics, state);

            if (unreadCount > 0)
            {
                DrawBadge(graphics, unreadCount);
            }
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    /// <summary>Libère une icône créée par <see cref="Create"/>, handle GDI compris.</summary>
    public static void Destroy(Icon? icon)
    {
        if (icon is null)
        {
            return;
        }

        var handle = icon.Handle;
        icon.Dispose();
        DestroyIcon(handle);
    }

    /// <summary>
    /// Renvoie le logo de l'application, sans pastille ni liseré : l'icône à donner aux
    /// fenêtres (<c>Form.Icon</c>) pour qu'elles portent la même image que l'exécutable.
    /// </summary>
    /// <returns>
    /// L'icône partagée, ou <see langword="null"/> si ni l'extraction ni le repli n'ont abouti
    /// — l'appelant doit alors laisser l'icône par défaut de Windows.
    /// </returns>
    /// <remarks>
    /// L'instance est mise en cache et vit le temps du processus : elle appartient à la
    /// fabrique. Ne pas la libérer, ne pas la passer à <see cref="Destroy"/> — sinon les
    /// fenêtres encore ouvertes afficheraient un handle mort.
    /// </remarks>
    public static Icon? LoadApplicationIcon()
    {
        lock (Gate)
        {
            return LoadApplicationIconLocked();
        }
    }

    // ------------------------------------------------------------------------ logo

    /// <summary>Lit l'icône embarquée dans l'exécutable ; <see langword="null"/> si impossible.</summary>
    private static Icon? ExtractFromExecutable()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        try
        {
            return Icon.ExtractAssociatedIcon(path);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or ExternalException)
        {
            // Chemin inexploitable, fichier verrouillé ou exécutable sans ressource d'icône :
            // aucune de ces situations ne doit empêcher l'application de démarrer.
            return null;
        }
    }

    /// <summary>Dessine le logo sur toute la surface de l'icône en cours de composition.</summary>
    private static void DrawLogo(Graphics graphics)
    {
        var logo = LogoBitmap();
        if (logo is null)
        {
            DrawLogoGeometry(graphics, Size);
            return;
        }

        graphics.DrawImage(logo, new Rectangle(0, 0, Size, Size));
    }

    /// <summary>
    /// Rasterise une fois pour toutes le logo en 32 x 32 : recomposer l'icône à chaque cycle de
    /// surveillance ne doit pas relire une ressource Win32.
    /// </summary>
    private static Bitmap? LogoBitmap()
    {
        lock (Gate)
        {
            if (_logo is not null)
            {
                return _logo;
            }

            var icon = LoadApplicationIconLocked();
            if (icon is null)
            {
                return null;
            }

            try
            {
                using var source = icon.ToBitmap();
                var bitmap = new Bitmap(Size, Size);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    ConfigureQuality(graphics);
                    graphics.DrawImage(source, new Rectangle(0, 0, Size, Size));
                }

                _logo = bitmap;
                return _logo;
            }
            catch (Exception exception) when (exception is ArgumentException or ExternalException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Résolution effective du logo, à appeler verrou déjà pris. Une seule tentative : si
    /// l'extraction échoue elle échouera toujours, et l'icône est demandée à chaque ouverture
    /// de fenêtre comme à chaque cycle de surveillance.
    /// </summary>
    private static Icon? LoadApplicationIconLocked()
    {
        if (!_applicationIconResolved)
        {
            _applicationIconResolved = true;
            _applicationIcon = ExtractFromExecutable() ?? CreateFallbackIcon();
        }

        return _applicationIcon;
    }

    /// <summary>
    /// Fabrique l'icône de repli. Le handle GDI n'est volontairement jamais libéré : c'est un
    /// handle unique, conservé le temps du processus, que Windows récupère à la sortie.
    /// </summary>
    private static Icon? CreateFallbackIcon()
    {
        try
        {
            using var bitmap = new Bitmap(Size, Size);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                ConfigureQuality(graphics);
                DrawLogoGeometry(graphics, Size);
            }

            return Icon.FromHandle(bitmap.GetHicon());
        }
        catch (Exception exception) when (exception is ArgumentException or ExternalException)
        {
            return null;
        }
    }

    /// <summary>
    /// Trace le logo en GDI+ : disque bleu, deux branches convergeant vers une flèche de fusion,
    /// nœud ambre sur la branche entrante.
    /// </summary>
    /// <remarks>
    /// Transcription de <c>assets/logo/forge-watcher.svg</c>, qui reste le maître ; toute retouche
    /// du SVG doit être répercutée ici. Ce tracé ne sert qu'au repli : en fonctionnement normal,
    /// c'est le <c>.ico</c> de l'exécutable qui est affiché.
    /// </remarks>
    private static void DrawLogoGeometry(Graphics graphics, int side)
    {
        var k = side / Grid;

        using var disc = new SolidBrush(BrandBlue);
        graphics.FillEllipse(disc, 1f * k, 1f * k, 30f * k, 30f * k);

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

        using var white = new SolidBrush(Color.White);
        graphics.FillPolygon(white, [
            new PointF(16f * k, 5.5f * k),
            new PointF(10f * k, 13f * k),
            new PointF(22f * k, 13f * k),
        ]);

        using var accent = new SolidBrush(BrandAccent);
        graphics.FillEllipse(accent, (21.5f - 3.4f) * k, (22.5f - 3.4f) * k, 6.8f * k, 6.8f * k);
    }

    // ------------------------------------------------------------------- surcouches

    /// <summary>
    /// Souligne l'état de la surveillance par un liseré épousant le bord du disque. L'état normal
    /// n'en porte aucun : le logo doit rester le logo tant que tout va bien.
    /// </summary>
    private static void DrawStateRing(Graphics graphics, TrayIconState state)
    {
        var color = StateColor(state);
        if (color is null)
        {
            return;
        }

        // Épaisseur 2,5 px centrée sur le rayon 14,75 : le liseré couvre le bord du disque sans
        // jamais mordre sur le glyphe (dont le point le plus excentré est à 11,9).
        const float Inset = 1.25f;
        using var pen = new Pen(color.Value, 2.5f);
        graphics.DrawEllipse(pen, Inset, Inset, Size - (2f * Inset), Size - (2f * Inset));
    }

    /// <summary>Couleur du liseré d'état, ou <see langword="null"/> pour l'état normal.</summary>
    private static Color? StateColor(TrayIconState state) => state switch
    {
        TrayIconState.NotConfigured => Color.FromArgb(120, 120, 120),
        TrayIconState.Warning => Color.FromArgb(196, 129, 0),
        TrayIconState.Error => Color.FromArgb(190, 45, 45),
        _ => null,
    };

    private static void DrawBadge(Graphics graphics, int unreadCount)
    {
        const int Diameter = 17;
        var bounds = new Rectangle(Size - Diameter - 1, Size - Diameter - 1, Diameter, Diameter);

        using var badgeBrush = new SolidBrush(Color.FromArgb(200, 30, 30));
        using var outline = new Pen(Color.White, 1.5f);
        graphics.FillEllipse(badgeBrush, bounds);
        graphics.DrawEllipse(outline, bounds);

        var text = unreadCount > 9 ? "9+" : unreadCount.ToString();
        DrawCenteredText(graphics, text, Color.White, unreadCount > 9 ? 9f : 11f, bounds);
    }

    private static void DrawCenteredText(Graphics graphics, string text, Color color, float emSize, RectangleF bounds)
    {
        using var font = new Font("Segoe UI", emSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        graphics.DrawString(text, font, brush, bounds, format);
    }

    /// <summary>Réglages communs : sans eux, un disque de 32 px est un escalier.</summary>
    private static void ConfigureQuality(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
