using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CSharpForgeWatcher.LogoGenerator;

/// <summary>
/// Fabrique <c>assets/logo/forge-watcher.ico</c> à partir de la géométrie de
/// <see cref="LogoPainter"/>, puis relit le fichier pour vérifier qu'il est exploitable.
/// </summary>
/// <remarks>
/// Utilisation : <c>dotnet run --project assets/logo/generator/LogoGenerator.csproj</c>.
/// Un argument optionnel permet d'écrire ailleurs (répertoire de destination).
/// </remarks>
internal static class Program
{
    /// <summary>Résolutions embarquées dans le conteneur ICO, de la plus petite à la plus grande.</summary>
    private static readonly int[] Sizes = [16, 24, 32, 48, 64, 128, 256];

    /// <summary>Facteur de suréchantillonnage : on dessine en grand puis on réduit, GDI+ étant
    /// médiocre en anticrénelage sur de très petites géométries.</summary>
    private const int Supersample = 4;

    private const string IconFileName = "forge-watcher.ico";
    private const string MasterFileName = "forge-watcher.svg";

    private static int Main(string[] args)
    {
        try
        {
            var directory = ResolveOutputDirectory(args);
            var path = Path.Combine(directory, IconFileName);

            Console.WriteLine($"Écriture de {path}");
            WriteIcon(path);

            return Verify(path) ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ÉCHEC : {exception.Message}");
            return 1;
        }
    }

    // ------------------------------------------------------------------ destination

    /// <summary>
    /// Détermine où écrire l'icône : l'argument de ligne de commande s'il est fourni, sinon le
    /// répertoire contenant le SVG maître, remonté depuis l'exécutable (donc indépendant du
    /// répertoire courant, ce que <c>dotnet run</c> ne garantit pas).
    /// </summary>
    private static string ResolveOutputDirectory(string[] args)
    {
        // On ignore les arguments en forme d'option : « dotnet run » transmet à l'application
        // tout ce qu'il ne reconnaît pas (par exemple --nologo), ce qui écrirait n'importe où.
        var destination = Array.Find(args, argument => !string.IsNullOrWhiteSpace(argument) && !argument.StartsWith('-'));
        if (destination is not null)
        {
            return Path.GetFullPath(destination);
        }

        var candidate = new DirectoryInfo(AppContext.BaseDirectory);
        while (candidate is not null)
        {
            if (File.Exists(Path.Combine(candidate.FullName, MasterFileName)))
            {
                return candidate.FullName;
            }

            candidate = candidate.Parent;
        }

        throw new InvalidOperationException(
            $"Impossible de localiser {MasterFileName} en remontant depuis {AppContext.BaseDirectory} ; " +
            "passez le répertoire de destination en argument.");
    }

    // ------------------------------------------------------------------ écriture ICO

    /// <summary>
    /// Assemble le conteneur ICO : en-tête ICONDIR, une ICONDIRENTRY par résolution, puis les
    /// charges utiles PNG concaténées (format accepté par Windows depuis Vista).
    /// </summary>
    private static void WriteIcon(string path)
    {
        var payloads = Array.ConvertAll(Sizes, RenderPng);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        // ICONDIR : réservé (0), type (1 = icône), nombre d'images.
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)Sizes.Length);

        // Les charges utiles commencent après l'en-tête et le tableau d'entrées (16 octets chacune).
        var offset = 6 + (16 * Sizes.Length);
        for (var index = 0; index < Sizes.Length; index++)
        {
            // 256 se code 0 : la largeur et la hauteur ne tiennent que sur un octet.
            var dimension = (byte)(Sizes[index] >= 256 ? 0 : Sizes[index]);

            writer.Write(dimension);          // bWidth
            writer.Write(dimension);          // bHeight
            writer.Write((byte)0);            // bColorCount : 0 = pas de palette
            writer.Write((byte)0);            // bReserved
            writer.Write((ushort)1);          // wPlanes
            writer.Write((ushort)32);         // wBitCount : 32 bits, canal alpha compris
            writer.Write((uint)payloads[index].Length);
            writer.Write((uint)offset);

            offset += payloads[index].Length;
        }

        foreach (var payload in payloads)
        {
            writer.Write(payload);
        }
    }

    /// <summary>Rasterise le logo à la taille demandée et renvoie les octets du PNG.</summary>
    private static byte[] RenderPng(int side)
    {
        using var oversized = new Bitmap(side * Supersample, side * Supersample, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(oversized))
        {
            LogoPainter.Paint(graphics, side * Supersample);
        }

        using var bitmap = new Bitmap(side, side, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            // Bilinéaire : à réduction entière (facteur 4) GDI+ moyenne les pixels, ce qui reste net.
            // Le bicubique, à support plus large, rend les traits de 2 px flous à 16 x 16.
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(oversized, new Rectangle(0, 0, side, side), 0, 0, oversized.Width, oversized.Height, GraphicsUnit.Pixel);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    // ------------------------------------------------------------------ vérification

    /// <summary>
    /// Relit le fichier produit : taille non nulle, contenu réel de chaque image, sélection par
    /// <see cref="Icon(string, Size)"/> et transparence préservée sur l'image 32 x 32.
    /// </summary>
    private static bool Verify(string path)
    {
        var length = new FileInfo(path).Length;
        Console.WriteLine($"Taille du fichier : {length} octets");
        if (length <= 0)
        {
            Console.Error.WriteLine("ÉCHEC : fichier vide.");
            return false;
        }

        var ok = VerifyPayloads(path);
        ok &= VerifySelection(path);
        ok &= VerifyTransparency(path);

        Console.WriteLine(ok ? "Vérification : OK" : "Vérification : ÉCHEC");
        return ok;
    }

    /// <summary>
    /// Relit l'annuaire du conteneur et décode chaque charge utile : c'est la seule preuve que les
    /// sept résolutions sont réellement présentes, aux bonnes dimensions.
    /// </summary>
    private static bool VerifyPayloads(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var count = BitConverter.ToUInt16(bytes, 4);
        Console.WriteLine($"Images déclarées : {count} (attendu {Sizes.Length})");

        var ok = count == Sizes.Length;
        for (var index = 0; index < count; index++)
        {
            var entry = 6 + (16 * index);
            var declared = bytes[entry] == 0 ? 256 : bytes[entry];
            var payloadLength = BitConverter.ToInt32(bytes, entry + 8);
            var payloadOffset = BitConverter.ToInt32(bytes, entry + 12);

            using var stream = new MemoryStream(bytes, payloadOffset, payloadLength);
            using var image = Image.FromStream(stream);

            var expected = index < Sizes.Length ? Sizes[index] : -1;
            var match = declared == expected && image.Width == expected && image.Height == expected;
            Console.WriteLine(
                $"  image {index} : déclarée {declared} px, PNG décodé {image.Width}x{image.Height}, " +
                $"{payloadLength} octets {(match ? "OK" : "INATTENDU")}");
            ok &= match;
        }

        return ok;
    }

    /// <summary>
    /// Vérifie que <see cref="Icon(string, Size)"/> sait extraire chaque résolution.
    /// </summary>
    /// <remarks>
    /// Cas particulier de 256 px : la norme ICO code cette dimension sur un octet valant 0, et le
    /// sélecteur de <see cref="Icon"/> lit cet octet littéralement — il retombe donc sur 128 px.
    /// C'est une limite de System.Drawing, pas du fichier : l'explorateur Windows, la barre des
    /// tâches et les ressources Win32 exploitent bien l'image 256 px (voir <see cref="VerifyPayloads"/>).
    /// </remarks>
    private static bool VerifySelection(string path)
    {
        var ok = true;
        foreach (var side in Sizes)
        {
            using var icon = new Icon(path, new Size(side, side));
            var expected = side == 256 ? 128 : side;
            var match = icon.Width == expected && icon.Height == expected;
            var comment = side == 256 ? " (256 codé 0 : System.Drawing plafonne à 128)" : string.Empty;
            Console.WriteLine($"  {side,3} px demandé -> {icon.Width}x{icon.Height} {(match ? "OK" : "INATTENDU")}{comment}");
            ok &= match;
        }

        return ok;
    }

    /// <summary>Vérifie que le canal alpha survit à l'aller-retour : coin transparent, centre opaque.</summary>
    private static bool VerifyTransparency(string path)
    {
        using var frame = new Icon(path, new Size(32, 32));
        using var bitmap = frame.ToBitmap();
        var corner = bitmap.GetPixel(0, 0);
        var center = bitmap.GetPixel(16, 16);
        var ok = corner.A == 0 && center.A == 255;
        Console.WriteLine(
            $"  coin (0,0) alpha = {corner.A} (attendu 0), centre (16,16) alpha = {center.A} (attendu 255) " +
            $"{(ok ? "OK" : "INATTENDU")}");
        return ok;
    }
}
