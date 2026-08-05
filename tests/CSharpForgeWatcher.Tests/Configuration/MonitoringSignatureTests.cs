using System.Text;
using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Tests.Doubles;

namespace CSharpForgeWatcher.Tests.Configuration;

/// <summary>
/// Empreinte de surveillance : ce qui justifie un cycle immédiat après un enregistrement,
/// et ce qui ne le justifie pas.
/// </summary>
/// <remarks>
/// Enregistrer la fenêtre de configuration relançait un cycle quoi qu'on ait touché, y
/// compris un simple changement de thème. L'empreinte distingue les réglages qui changent ce
/// qui est lu de ceux qui ne changent que l'affichage.
/// </remarks>
[TestFixture]
public sealed class MonitoringSignatureTests
{
    private static WatcherConfiguration Configuration() => new()
    {
        Accounts =
        [
            new WatchedAccount
            {
                Id = "compte-1",
                Url = Build.OrganizationUrl,
                ProtectedPersonalAccessToken = "chiffré-1",
                Repositories = [WatchedRepository.From(Build.Repository)],
            },
        ],
    };

    [Test]
    public void Changer_de_theme_ne_change_pas_l_empreinte()
    {
        var configuration = Configuration();
        var avant = configuration.MonitoringSignature;

        configuration.Theme = ThemePreference.Dark;

        Assert.That(configuration.MonitoringSignature, Is.EqualTo(avant));
    }

    [Test]
    public void Couper_le_son_ou_le_demarrage_automatique_ne_change_pas_l_empreinte()
    {
        var configuration = Configuration();
        var avant = configuration.MonitoringSignature;

        configuration.PlayNotificationSound = false;
        configuration.LaunchOnWindowsStartup = true;
        configuration.MaxNotificationsPerPoll = 12;

        Assert.That(configuration.MonitoringSignature, Is.EqualTo(avant));
    }

    [Test]
    public void Ajouter_un_depot_change_l_empreinte()
    {
        var configuration = Configuration();
        var avant = configuration.MonitoringSignature;

        configuration.Accounts[0].Repositories.Add(new WatchedRepository
        {
            ProjectName = "Autre",
            RepositoryId = "d5b1a0c3-0000-4000-8000-000000000001",
            RepositoryName = "autre-depot",
        });

        Assert.That(configuration.MonitoringSignature, Is.Not.EqualTo(avant));
    }

    [Test]
    public void Renouveler_un_jeton_change_l_empreinte()
    {
        // Le geste de dépannage le plus courant : il doit relancer un cycle tout de suite.
        var configuration = Configuration();
        var avant = configuration.MonitoringSignature;

        configuration.Accounts[0].ProtectedPersonalAccessToken = "chiffré-2";

        Assert.That(configuration.MonitoringSignature, Is.Not.EqualTo(avant));
    }

    [Test]
    public void L_empreinte_ne_contient_jamais_le_jeton()
    {
        var configuration = Configuration();
        configuration.Accounts[0].ProtectedPersonalAccessToken = "valeur-reconnaissable";

        Assert.That(configuration.MonitoringSignature, Does.Not.Contain("valeur-reconnaissable"));
    }

    [Test]
    public void Desactiver_un_compte_change_l_empreinte()
    {
        var configuration = Configuration();
        var avant = configuration.MonitoringSignature;

        configuration.Accounts[0].IsEnabled = false;

        Assert.That(configuration.MonitoringSignature, Is.Not.EqualTo(avant));
    }

    [Test]
    public void Desactiver_un_type_de_notification_change_l_empreinte()
    {
        // Les types activés déterminent les règles de détection, donc la lecture des
        // discussions (SPEC-POLL-003).
        var configuration = Configuration();
        var avant = configuration.MonitoringSignature;

        configuration.Notifications.SetEnabled(NotificationKind.MentionedInComment, false);

        Assert.That(configuration.MonitoringSignature, Is.Not.EqualTo(avant));
    }

    [Test]
    public void Un_enregistrement_qui_ne_ressaisit_pas_le_jeton_laisse_l_empreinte_intacte()
    {
        var service = new ConfigurationService(new InMemoryConfigurationStore(), new SaltedSecretProtector());

        var premier = service.Edit();
        premier.Accounts.Add(new WatchedAccount
        {
            Id = "compte-1",
            Url = Build.OrganizationUrl,
            Repositories = [WatchedRepository.From(Build.Repository)],
        });
        service.Apply(premier, new Dictionary<string, string?>(StringComparer.Ordinal) { ["compte-1"] = "pat" });

        var avant = service.Current.MonitoringSignature;

        // Réouvrir la fenêtre, changer de thème, enregistrer : le jeton n'est pas ressaisi et
        // ne doit donc pas être rechiffré, sinon un compte intact passerait pour modifié.
        var second = service.Edit();
        second.Theme = ThemePreference.Dark;
        service.Apply(second);

        Assert.That(service.Current.MonitoringSignature, Is.EqualTo(avant));
    }

    [Test]
    public void Ressaisir_un_autre_jeton_change_bien_l_empreinte()
    {
        var service = new ConfigurationService(new InMemoryConfigurationStore(), new SaltedSecretProtector());

        var premier = service.Edit();
        premier.Accounts.Add(new WatchedAccount
        {
            Id = "compte-1",
            Url = Build.OrganizationUrl,
            Repositories = [WatchedRepository.From(Build.Repository)],
        });
        service.Apply(premier, new Dictionary<string, string?>(StringComparer.Ordinal) { ["compte-1"] = "pat" });

        var avant = service.Current.MonitoringSignature;

        var second = service.Edit();
        service.Apply(second, new Dictionary<string, string?>(StringComparer.Ordinal) { ["compte-1"] = "pat-renouvele" });

        Assert.That(service.Current.MonitoringSignature, Is.Not.EqualTo(avant));
    }

    [Test]
    public void L_ordre_de_la_selection_est_sans_effet()
    {
        var configuration = Configuration();
        configuration.Accounts[0].Repositories.Add(new WatchedRepository
        {
            ProjectName = "Autre",
            RepositoryId = "d5b1a0c3-0000-4000-8000-000000000001",
            RepositoryName = "autre-depot",
        });

        var avant = configuration.MonitoringSignature;
        configuration.Accounts[0].Repositories.Reverse();

        Assert.That(configuration.MonitoringSignature, Is.EqualTo(avant));
    }

    /// <summary>
    /// Protecteur qui, comme DPAPI, produit un chiffré différent à chaque appel.
    /// </summary>
    /// <remarks>
    /// C'est précisément cette propriété qui rendait tout enregistrement « modifiant » :
    /// rechiffrer un jeton inchangé suffisait à faire croire à un changement de surveillance.
    /// Le double réversible ordinaire, déterministe, ne pourrait pas le montrer.
    /// </remarks>
    private sealed class SaltedSecretProtector : ISecretProtector
    {
        private int _calls;

        public string Protect(string plainText)
            => $"enc{++_calls}:{Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText))}";

        public bool TryUnprotect(string protectedText, out string plainText)
        {
            plainText = string.Empty;
            var separator = protectedText.IndexOf(':', StringComparison.Ordinal);

            if (separator < 0)
            {
                return false;
            }

            plainText = Encoding.UTF8.GetString(Convert.FromBase64String(protectedText[(separator + 1)..]));
            return true;
        }
    }
}
