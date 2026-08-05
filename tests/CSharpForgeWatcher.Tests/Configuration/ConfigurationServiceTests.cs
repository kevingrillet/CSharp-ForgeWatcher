using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Tests.Doubles;

namespace CSharpForgeWatcher.Tests.Configuration;

/// <summary>
/// SPEC-CFG-001 (secret jamais en clair), SPEC-CFG-004 (application à chaud) et
/// SPEC-CFG-008 (un jeton par compte).
/// </summary>
[TestFixture]
public sealed class ConfigurationServiceTests
{
    private const string AccountId = "compte-1";

    /// <summary>Compte minimal, sans jeton : celui-ci est fourni à l'enregistrement.</summary>
    private static WatchedAccount Account(string id = AccountId, string? url = null) => new()
    {
        Id = id,
        Url = url ?? Build.OrganizationUrl,
        Repositories = [WatchedRepository.From(Build.Repository)],
    };

    /// <summary>Dictionnaire de jetons, tel que le passe la fenêtre de configuration.</summary>
    private static Dictionary<string, string?> Tokens(string id, string? token)
        => new(StringComparer.Ordinal) { [id] = token };

    [Test]
    [Category("SPEC-CFG-001")]
    public void Le_PAT_est_stocke_chiffre_et_relu_en_clair()
    {
        var store = new InMemoryConfigurationStore();
        var service = new ConfigurationService(store, new ReversibleSecretProtector());

        var edited = service.Edit();
        edited.Accounts.Add(Account());
        service.Apply(edited, Tokens(AccountId, "mon-pat-secret"));

        var saved = store.Configuration.Accounts.Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                saved.ProtectedPersonalAccessToken,
                Does.Not.Contain("mon-pat-secret"),
                "Le jeton ne doit jamais être stocké tel quel.");
            Assert.That(service.TokenOf(saved), Is.EqualTo("mon-pat-secret"));
        });
    }

    [Test]
    [Category("SPEC-CFG-001")]
    public void Un_secret_illisible_equivaut_a_labsence_de_PAT()
    {
        // Cas d'un config.json copié depuis une autre machine : DPAPI échoue.
        var account = Account();
        account.ProtectedPersonalAccessToken = "chiffre-par-quelquun-dautre";

        var store = new InMemoryConfigurationStore(new WatcherConfiguration { Accounts = [account] });
        var service = new ConfigurationService(store, new ReversibleSecretProtector());

        Assert.Multiple(() =>
        {
            Assert.That(service.TokenOf(service.Current.Accounts.Single()), Is.Empty);
            Assert.That(service.IsUsable, Is.False);
        });
    }

    [Test]
    [Category("SPEC-CFG-008")]
    public void Chaque_compte_a_son_propre_jeton()
    {
        var service = new ConfigurationService(new InMemoryConfigurationStore(), new ReversibleSecretProtector());

        var edited = service.Edit();
        edited.Accounts.Add(Account("azure", "https://dev.azure.com/contoso"));
        edited.Accounts.Add(new WatchedAccount
        {
            Id = "github",
            Provider = SourceControlProvider.GitHub,
            Url = "https://github.com",
        });

        service.Apply(
            edited,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["azure"] = "pat-azure",
                ["github"] = "pat-github",
            });

        var azure = service.Current.FindAccount("azure")!;
        var gitHub = service.Current.FindAccount("github")!;

        Assert.Multiple(() =>
        {
            Assert.That(service.TokenOf(azure), Is.EqualTo("pat-azure"));
            Assert.That(service.TokenOf(gitHub), Is.EqualTo("pat-github"));
            Assert.That(
                azure.ProtectedPersonalAccessToken,
                Is.Not.EqualTo(gitHub.ProtectedPersonalAccessToken),
                "Deux jetons distincts ne doivent pas produire la même forme chiffrée.");
            Assert.That(service.ToConnection(gitHub).Provider, Is.EqualTo(SourceControlProvider.GitHub));
        });
    }

    [Test]
    [Category("SPEC-CFG-008")]
    public void Une_configuration_au_format_precedent_devient_un_compte()
    {
        // config.json écrit par une version antérieure : un fournisseur, une URL, un jeton.
        var protector = new ReversibleSecretProtector();
        var store = new InMemoryConfigurationStore(new WatcherConfiguration
        {
            Provider = SourceControlProvider.AzureDevOps,
            OrganizationUrl = Build.OrganizationUrl,
            ProtectedPersonalAccessToken = protector.Protect("pat-historique"),
            Repositories = [WatchedRepository.From(Build.Repository)],
        });

        var service = new ConfigurationService(store, protector);
        var migrated = service.Current.Accounts.Single();

        Assert.Multiple(() =>
        {
            Assert.That(migrated.Id, Is.EqualTo(WatcherConfiguration.MigratedAccountId));
            Assert.That(migrated.Url, Is.EqualTo(Build.OrganizationUrl));
            Assert.That(migrated.Repositories, Has.Count.EqualTo(1));
            Assert.That(service.TokenOf(migrated), Is.EqualTo("pat-historique"));
            Assert.That(service.IsUsable, Is.True, "La surveillance doit reprendre sans intervention.");
            Assert.That(
                store.SaveCount,
                Is.EqualTo(1),
                "Le fichier est réécrit au format courant, pour ne migrer qu'une fois.");
            Assert.That(
                service.Current.OrganizationUrl,
                Is.Empty,
                "Les champs hérités sont vidés : ils ne doivent plus être relus.");
        });
    }

    [Test]
    [Category("SPEC-CFG-008")]
    public void Une_configuration_deja_migree_nest_pas_retouchee()
    {
        var store = new InMemoryConfigurationStore(new WatcherConfiguration { Accounts = [Account()] });
        var service = new ConfigurationService(store, new ReversibleSecretProtector());

        Assert.Multiple(() =>
        {
            Assert.That(service.Current.Accounts, Has.Count.EqualTo(1));
            Assert.That(store.SaveCount, Is.Zero, "Aucune migration, donc aucun enregistrement.");
        });
    }

    [Test]
    [Category("SPEC-CFG-004")]
    public void Appliquer_une_configuration_previent_les_abonnes()
    {
        var service = new ConfigurationService(new InMemoryConfigurationStore(), new ReversibleSecretProtector());
        var notifications = 0;
        service.Changed += (_, _) => notifications++;

        service.Apply(service.Edit());

        Assert.That(notifications, Is.EqualTo(1));
    }

    [Test]
    [Category("SPEC-CFG-004")]
    public void Editer_sans_appliquer_ne_change_rien()
    {
        var store = new InMemoryConfigurationStore();
        var service = new ConfigurationService(store, new ReversibleSecretProtector());

        var abandoned = service.Edit();
        abandoned.Accounts.Add(Account("jamais-enregistre"));

        Assert.Multiple(() =>
        {
            Assert.That(service.Current.Accounts, Is.Empty);
            Assert.That(store.SaveCount, Is.Zero);
        });
    }

    [Test]
    [Category("SPEC-CFG-004")]
    public void Le_PAT_est_conserve_si_lutilisateur_ne_le_ressaisit_pas()
    {
        var service = new ConfigurationService(new InMemoryConfigurationStore(), new ReversibleSecretProtector());

        var initial = service.Edit();
        initial.Accounts.Add(Account());
        service.Apply(initial, Tokens(AccountId, "pat-initial"));

        var edited = service.Edit();
        edited.PollIntervalSeconds = 600;
        service.Apply(edited);

        Assert.Multiple(() =>
        {
            Assert.That(service.TokenOf(service.Current.Accounts.Single()), Is.EqualTo("pat-initial"));
            Assert.That(service.Current.PollIntervalSeconds, Is.EqualTo(600));
        });
    }
}
