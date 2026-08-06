using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Text;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Tests.Doubles;

namespace CSharpForgeWatcher.Tests.Configuration;

/// <summary>
/// SPEC-CFG-003 — validation, SPEC-CFG-004 — édition annulable, SPEC-CFG-008 — comptes
/// multiples.
/// </summary>
[TestFixture]
public sealed class WatcherConfigurationTests
{
    /// <summary>Jeton renvoyé pour tous les comptes, sauf indication contraire.</summary>
    private static string? Token(WatchedAccount account) => "un-pat";

    /// <summary>
    /// Erreurs de validation formulées en français, pour les assertions.
    /// </summary>
    /// <remarks>
    /// La validation rend des clés (SPEC-UI-LANG-002) : les vérifier sur leur formulation
    /// française reste le plus lisible, et le test de parité du catalogue garantit que
    /// l'anglais suit.
    /// </remarks>
    private static string Fr(ConfigurationValidationResult result)
        => string.Join(
            Environment.NewLine,
            result.Errors.Select(TextCatalogue.For(EffectiveLanguage.French).Resolve));

    /// <summary>Formulation française d'une clé de catalogue.</summary>
    private static string Fr(string key) => TextCatalogue.For(EffectiveLanguage.French).Get(key);

    /// <summary>Compte Azure DevOps surveillant un dépôt.</summary>
    private static WatchedAccount Account(
        string id = "compte-1",
        SourceControlProvider provider = SourceControlProvider.AzureDevOps,
        string? url = null) => new()
        {
            Id = id,
            Provider = provider,
            Url = url ?? Build.OrganizationUrl,
            Repositories = [WatchedRepository.From(Build.Repository)],
        };

    private static WatcherConfiguration Usable() => new() { Accounts = [Account()] };

    [Test]
    [Category("SPEC-CFG-003")]
    public void Une_configuration_complete_est_valide()
    {
        Assert.That(Usable().Validate(Token).IsValid, Is.True);
    }

    [Test]
    [Category("SPEC-CFG-003")]
    public void Le_PAT_est_obligatoire()
    {
        var result = Usable().Validate(_ => null);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(Fr(result), Does.Contain("PAT"));
        });
    }

    [Test]
    [Category("SPEC-CFG-003")]
    public void Un_compte_est_obligatoire()
    {
        var result = new WatcherConfiguration().Validate(Token);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(Fr(result), Does.Contain("Ajoutez un compte"));
        });
    }

    [Test]
    [Category("SPEC-CFG-003")]
    public void Au_moins_un_depot_est_obligatoire()
    {
        var configuration = Usable();
        configuration.Accounts[0].Repositories.Clear();

        Assert.That(Fr(configuration.Validate(Token)), Does.Contain("dépôt"));
    }

    [Test]
    [Category("SPEC-CFG-003")]
    public void Lurl_de_la_forge_doit_etre_absolue()
    {
        var configuration = Usable();
        configuration.Accounts[0].Url = "contoso";

        Assert.That(Fr(configuration.Validate(Token)), Does.Contain("absolue"));
    }

    [Test]
    [Category("SPEC-CFG-003")]
    public void Lintervalle_minimal_est_impose()
    {
        var configuration = Usable();
        configuration.PollIntervalSeconds = 5;

        Assert.Multiple(() =>
        {
            Assert.That(configuration.Validate(Token).IsValid, Is.False);
            Assert.That(
                configuration.PollInterval,
                Is.EqualTo(TimeSpan.FromSeconds(WatcherConfiguration.MinimumPollIntervalSeconds)),
                "L'intervalle effectif reste borné même si la valeur stockée est plus faible.");
        });
    }

    [Test]
    [Category("SPEC-CFG-008")]
    public void Trois_forges_peuvent_etre_surveillees_ensemble()
    {
        var configuration = new WatcherConfiguration
        {
            Accounts =
            [
                Account("azure", SourceControlProvider.AzureDevOps, "https://dev.azure.com/contoso"),
                Account("github", SourceControlProvider.GitHub, "https://github.com"),
                Account("gitlab", SourceControlProvider.GitLab, "https://gitlab.com"),
            ],
        };

        Assert.Multiple(() =>
        {
            Assert.That(configuration.Validate(Token).IsValid, Is.True);
            Assert.That(configuration.EnabledAccounts, Has.Count.EqualTo(3));
            Assert.That(configuration.AccountIds, Is.EquivalentTo(new[] { "azure", "github", "gitlab" }));
            Assert.That(configuration.FindAccount("github")!.Provider, Is.EqualTo(SourceControlProvider.GitHub));
        });
    }

    [Test]
    [Category("SPEC-CFG-008")]
    public void Un_compte_desactive_est_conserve_mais_ignore()
    {
        // Taire une forge le temps de renouveler son jeton ne doit pas coûter la sélection de
        // dépôts qu'on a mis du temps à composer.
        var configuration = new WatcherConfiguration
        {
            Accounts = [Account("azure"), Account("github", SourceControlProvider.GitHub, "https://github.com")],
        };

        configuration.Accounts[1].IsEnabled = false;

        Assert.Multiple(() =>
        {
            Assert.That(configuration.EnabledAccounts.Select(account => account.Id), Is.EqualTo(new[] { "azure" }));
            Assert.That(configuration.Accounts, Has.Count.EqualTo(2));
            Assert.That(configuration.Accounts[1].Repositories, Has.Count.EqualTo(1));
            Assert.That(configuration.Validate(Token).IsValid, Is.True);
        });
    }

    [Test]
    [Category("SPEC-CFG-008")]
    public void Tous_les_comptes_desactives_rend_la_configuration_inutilisable()
    {
        var configuration = Usable();
        configuration.Accounts[0].IsEnabled = false;

        var result = configuration.Validate(Token);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(Fr(result), Does.Contain("désactivés"));
        });
    }

    [Test]
    [Category("SPEC-CFG-008")]
    public void Un_seul_compte_selectionne_suffit_a_la_validation()
    {
        // La sélection peut se trouver sur n'importe quel compte : un poste qui ne surveille
        // que des pipelines GitLab est une configuration légitime (SPEC-PIPE-006).
        var configuration = new WatcherConfiguration
        {
            Accounts =
            [
                new WatchedAccount { Id = "azure", Url = Build.OrganizationUrl },
                new WatchedAccount
                {
                    Id = "gitlab",
                    Provider = SourceControlProvider.GitLab,
                    Url = "https://gitlab.com",
                    Pipelines = [WatchedPipeline.From(Build.Pipeline)],
                },
            ],
        };

        Assert.That(configuration.Validate(Token).IsValid, Is.True);
    }

    [Test]
    [Category("SPEC-CFG-008")]
    public void Le_message_derreur_nomme_le_compte_concerne()
    {
        // Avec plusieurs forges, « l'URL est requise » ne dirait pas laquelle corriger.
        var configuration = new WatcherConfiguration
        {
            Accounts = [Account("azure"), new WatchedAccount { Id = "github", Provider = SourceControlProvider.GitHub }],
        };

        var message = Fr(configuration.Validate(Token));

        Assert.That(message, Does.Contain("GitHub").And.Contain("serveur GitHub"));
    }

    [Test]
    [Category("SPEC-CFG-008")]
    public void Le_libelle_par_defaut_dun_compte_se_deduit_de_son_adresse()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                WatchedAccount.DefaultLabel(SourceControlProvider.GitHub, "https://github.com"),
                Is.EqualTo("GitHub · github.com"));
            Assert.That(
                WatchedAccount.DefaultLabel(SourceControlProvider.AzureDevOps, "https://dev.azure.com/contoso"),
                Is.EqualTo("Azure DevOps · contoso"),
                "Pour Azure DevOps, l'organisation est plus parlante que l'hôte.");
            Assert.That(
                WatchedAccount.DefaultLabel(SourceControlProvider.GitLab, "pas-une-url"),
                Is.EqualTo("GitLab"),
                "Une adresse inexploitable ne doit pas produire un libellé absurde.");
            Assert.That(
                new WatchedAccount { Label = "  Perso  " }.DisplayLabel,
                Is.EqualTo("Perso"),
                "Le libellé choisi par l'utilisateur prime.");
        });
    }

    [Test]
    [Category("SPEC-CFG-007")]
    [Category("SPEC-FORGE-002")]
    public void Le_fournisseur_par_defaut_est_Azure_DevOps_et_GitHub_est_accepte()
    {
        var account = Account();

        Assert.That(account.Provider, Is.EqualTo(SourceControlProvider.AzureDevOps));
        Assert.That(account.Validate("un-pat").IsValid, Is.True);

        account.Provider = SourceControlProvider.GitHub;
        account.Url = "https://github.com";

        Assert.That(account.Validate("un-pat").IsValid, Is.True);
    }

    [Test]
    [Category("SPEC-FORGE-002")]
    public void Un_fournisseur_sans_adaptateur_est_refuse_avec_un_message_explicite()
    {
        // Les trois forges de l'énumération sont désormais implémentées : la valeur hors
        // domaine tient donc le rôle du fournisseur inconnu — celui qu'un config.json rédigé
        // à la main peut produire, et celui qu'on obtiendra en ajoutant une quatrième forge à
        // l'énumération sans écrire son adaptateur. Refuser ici plutôt qu'échouer au réseau
        // évite un message HTTP incompréhensible à chaque cycle.
        const SourceControlProvider Unimplemented = (SourceControlProvider)99;

        var account = Account();
        account.Provider = Unimplemented;

        var result = account.Validate("un-pat");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(Fr(result), Does.Contain("pas encore pris en charge"));
            Assert.That(Unimplemented.IsImplemented(), Is.False);
            Assert.That(
                SourceControlProviderExtensions.ImplementedLabels(),
                Is.EqualTo("Azure DevOps, GitHub et GitLab"),
                "Le message doit énumérer lisiblement les forges disponibles.");
        });
    }

    [Test]
    [Category("SPEC-FORGE-002")]
    public void Le_champ_dURL_sannonce_selon_la_forge()
    {
        // « URL de l'organisation » n'a aucun sens pour GitHub : le message d'erreur, le
        // libellé et l'exemple suivent le fournisseur.
        var account = new WatchedAccount { Id = "github", Provider = SourceControlProvider.GitHub };
        var message = Fr(account.Validate("un-pat"));

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("serveur GitHub").And.Contain("https://github.com"));
            Assert.That(Fr(SourceControlProvider.GitHub.ScopeLabelKey()), Is.EqualTo("Propriétaires"));
            Assert.That(Fr(SourceControlProvider.GitLab.ScopeLabelKey()), Is.EqualTo("Groupes"));
            Assert.That(Fr(SourceControlProvider.AzureDevOps.ScopeLabelKey()), Is.EqualTo("Projets"));
            Assert.That(
                SourceControlProvider.GitHub.TokenPageUrl("https://github.com/mon-organisation"),
                Is.EqualTo("https://github.com/settings/tokens?type=beta"),
                "La page de jeton se déduit du serveur, chemin éventuel ignoré.");
            Assert.That(
                SourceControlProvider.AzureDevOps.TokenPageUrl("https://dev.azure.com/contoso"),
                Is.EqualTo("https://dev.azure.com/contoso/_usersSettings/tokens"));
        });
    }

    [Test]
    [Category("SPEC-FORGE-002")]
    public void Seuls_les_fournisseurs_implementes_sont_proposes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                SourceControlProviderExtensions.Implemented,
                Is.EquivalentTo(new[]
                {
                    SourceControlProvider.AzureDevOps,
                    SourceControlProvider.GitHub,
                    SourceControlProvider.GitLab,
                }),
                "Proposer une forge qui sera refusée à l'enregistrement ferait perdre du temps.");

            foreach (var provider in SourceControlProviderExtensions.Implemented)
            {
                // Le nom de la forge est une marque : il n'est pas traduit. Les trois autres
                // libellés sont des clés, et doivent être formulés dans les deux langues.
                Assert.That(provider.ToLabel(), Is.Not.Empty);
                Assert.That(provider.UrlPlaceholder(), Does.StartWith("https://"));

                foreach (var langue in new[] { EffectiveLanguage.French, EffectiveLanguage.English })
                {
                    var catalogue = TextCatalogue.For(langue);

                    Assert.That(catalogue.Knows(provider.UrlLabelKey()), Is.True);
                    Assert.That(catalogue.Knows(provider.ScopeLabelKey()), Is.True);
                    Assert.That(catalogue.Knows(provider.TokenScopeHintKey()), Is.True);
                }
            }
        });
    }

    [Test]
    [Category("SPEC-CFG-007")]
    public void Le_clone_conserve_les_comptes_et_le_theme()
    {
        var original = Usable();
        original.Theme = ThemePreference.Dark;

        var copy = original.Clone();

        Assert.Multiple(() =>
        {
            Assert.That(copy.Accounts.Single().Provider, Is.EqualTo(SourceControlProvider.AzureDevOps));
            Assert.That(copy.Theme, Is.EqualTo(ThemePreference.Dark));
        });
    }

    [Test]
    [Category("SPEC-CFG-004")]
    public void Le_clone_est_independant_de_loriginal()
    {
        var original = Usable();
        var copy = original.Clone();

        copy.Accounts[0].Url = "https://autre";
        copy.Accounts[0].Repositories.Clear();
        copy.Accounts.Add(Account("ajoute-dans-la-copie"));
        copy.Notifications.VoteChanged = false;

        Assert.Multiple(() =>
        {
            Assert.That(original.Accounts, Has.Count.EqualTo(1));
            Assert.That(original.Accounts[0].Url, Is.EqualTo(Build.OrganizationUrl));
            Assert.That(original.Accounts[0].Repositories, Has.Count.EqualTo(1));
            Assert.That(original.Notifications.VoteChanged, Is.True);
        });
    }

    [Test]
    [Category("SPEC-NOTIF-003")]
    public void Chaque_type_devenement_a_une_preference()
    {
        var preferences = new NotificationPreferences();

        foreach (var kind in NotificationKindExtensions.All)
        {
            Assert.That(
                preferences.IsEnabled(kind),
                Is.True,
                $"Le type {kind} doit être activé par défaut et connu de IsEnabled.");

            preferences.SetEnabled(kind, false);
            Assert.That(preferences.IsEnabled(kind), Is.False, $"Le type {kind} doit pouvoir être désactivé.");
        }
    }
}
