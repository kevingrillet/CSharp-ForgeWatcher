using CSharpForgeWatcher.Application.Notifications;

namespace CSharpForgeWatcher.Tests.Notifications;

/// <summary>
/// Étiquette de toast : deux faits distincts ne doivent jamais la partager.
/// </summary>
/// <remarks>
/// Windows traite deux notifications de même étiquette comme le même fait et remplace la
/// première. La clé de déduplication étant préfixée par l'identifiant de compte, une
/// troncature par la fin faisait disparaître le compte — deux forges surveillant le même
/// dépôt ne produisaient alors qu'un seul toast (ADR-0005, ADR-0006).
/// </remarks>
[TestFixture]
public sealed class NotificationTagTests
{
    /// <summary>Clé telle que la produit la détection : compte, type, dépôt, numéro.</summary>
    private static string CleDe(string accountId)
        => $"{accountId}|CommentOnMyPullRequest|9f3b1c2d-4e5a-4b6c-8d7e-0a1b2c3d4e5f:1234";

    [Test]
    public void Une_cle_courte_est_reprise_telle_quelle()
    {
        const string Courte = "compte|PipelineFailed|42";

        Assert.That(NotificationTag.For(Courte), Is.EqualTo(Courte));
    }

    [Test]
    public void Une_cle_longue_tient_dans_la_limite_des_toasts()
    {
        var etiquette = NotificationTag.For(CleDe(Guid.NewGuid().ToString("n")));

        Assert.That(etiquette, Has.Length.LessThanOrEqualTo(NotificationTag.MaxLength));
    }

    [Test]
    public void Deux_comptes_surveillant_le_meme_depot_ont_des_etiquettes_differentes()
    {
        // Le cas qui échouait : seul le préfixe diffère, et la clé dépasse la limite.
        var personnel = CleDe("0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a");
        var professionnel = CleDe("ffeeddccbbaa99887766554433221100");

        Assert.That(personnel, Has.Length.GreaterThan(NotificationTag.MaxLength));
        Assert.That(NotificationTag.For(personnel), Is.Not.EqualTo(NotificationTag.For(professionnel)));
    }

    [Test]
    public void Un_meme_fait_retombe_toujours_sur_la_meme_etiquette()
    {
        // Valeur figée volontairement : ce qui compte est la stabilité d'une exécution — et
        // d'une version — à l'autre, c'est elle qui permet à un toast ré-affiché de remplacer
        // le précédent au lieu de s'empiler. Comparer la fonction à elle-même ne vérifierait
        // rien. Changer cette constante revient à faire réapparaître d'anciennes
        // notifications : ce n'est pas une mise à jour anodine.
        Assert.That(
            NotificationTag.For(CleDe("0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a")),
            Is.EqualTo("cde63079dc12e10d5eb7cea65ddf8ecb53d79534bc185bf09a37521eac0a7f86"));
    }

    [Test]
    public void Une_cle_absente_ne_fait_pas_echouer_l_affichage()
    {
        Assert.That(NotificationTag.For(null), Is.Empty);
    }
}
