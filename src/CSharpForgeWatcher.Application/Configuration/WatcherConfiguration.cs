using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Configuration;

/// <summary>Dépôt sélectionné par l'utilisateur (SPEC-CFG-002).</summary>
/// <remarks>
/// POCO muable : sérialisé dans <c>config.json</c> et édité par la fenêtre de
/// configuration. L'identité est <see cref="RepositoryId"/> ; le nom est un simple
/// libellé rafraîchi à chaque cycle.
/// </remarks>
public sealed class WatchedRepository
{
    /// <summary>Nom de l'espace propriétaire (projet, propriétaire ou groupe selon la forge).</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Identifiant du dépôt — c'est lui qui fait référence.</summary>
    public string RepositoryId { get; set; } = string.Empty;

    /// <summary>Nom du dépôt, pour l'affichage.</summary>
    public string RepositoryName { get; set; } = string.Empty;

    /// <summary>Conversion vers l'objet-valeur du domaine.</summary>
    public RepositoryRef ToRepositoryRef() => new(ProjectName, RepositoryId, RepositoryName);

    /// <summary>Conversion depuis l'objet-valeur du domaine.</summary>
    public static WatchedRepository From(RepositoryRef reference) => new()
    {
        ProjectName = reference.ProjectName,
        RepositoryId = reference.RepositoryId,
        RepositoryName = reference.RepositoryName,
    };

    /// <summary>Copie indépendante.</summary>
    public WatchedRepository Clone() => (WatchedRepository)MemberwiseClone();

    public override string ToString() => $"{ProjectName} / {RepositoryName}";
}

/// <summary>Pipeline sélectionné par l'utilisateur (SPEC-PIPE-003).</summary>
/// <remarks>
/// Mémorisé par espace + identifiant de définition : renommer le pipeline ne casse pas la
/// surveillance, le nom n'étant qu'un libellé rafraîchi à chaque cycle.
/// </remarks>
public sealed class WatchedPipeline
{
    /// <summary>
    /// Espace propriétaire : projet d'équipe sur Azure DevOps, <c>propriétaire/dépôt</c>
    /// sur GitHub, chemin complet du projet sur GitLab (SPEC-FORGE-004).
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Identifiant de la définition de pipeline (entier 64 bits, SPEC-FORGE-006).</summary>
    public long DefinitionId { get; set; }

    /// <summary>Nom du pipeline, pour l'affichage.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Clé stable, alignée sur celle de l'état persisté.</summary>
    public string Key => $"{ProjectName}:{DefinitionId}";

    /// <summary>Conversion vers l'objet-valeur du domaine.</summary>
    public PipelineDefinitionRef ToDefinitionRef() => new(ProjectName, DefinitionId, Name);

    /// <summary>Conversion depuis l'objet-valeur du domaine.</summary>
    public static WatchedPipeline From(PipelineDefinitionRef definition) => new()
    {
        ProjectName = definition.ProjectName,
        DefinitionId = definition.DefinitionId,
        Name = definition.Name,
    };

    /// <summary>Copie indépendante.</summary>
    public WatchedPipeline Clone() => (WatchedPipeline)MemberwiseClone();

    public override string ToString() => $"{ProjectName} / {Name}";
}

/// <summary>
/// Forge surveillée (SPEC-FORGE-002).
/// </summary>
/// <remarks>
/// Sérialisé <b>par son nom</b> : un <c>config.json</c> écrit par une version antérieure
/// reste lisible, et l'ordre des valeurs peut évoluer sans conséquence. Une valeur non
/// implémentée est refusée à la validation avec un message explicite, plutôt que de
/// provoquer un échec réseau incompréhensible.
/// </remarks>
public enum SourceControlProvider
{
    /// <summary>Azure DevOps Services / Server.</summary>
    AzureDevOps = 0,

    /// <summary>GitHub : github.com ou GitHub Enterprise Server (ADR-0004).</summary>
    GitHub = 1,

    /// <summary>GitLab : gitlab.com ou instance auto-hébergée (ADR-0004).</summary>
    GitLab = 2,
}

/// <summary>Apparence de l'interface (SPEC-UI-THEME-001).</summary>
public enum ThemePreference
{
    /// <summary>Suit le réglage d'apparence de Windows.</summary>
    System = 0,

    /// <summary>Toujours clair.</summary>
    Light = 1,

    /// <summary>Toujours sombre.</summary>
    Dark = 2,
}

/// <summary>Langue de l'interface (SPEC-UI-LANG-001).</summary>
/// <remarks>
/// Même forme à trois positions que <see cref="ThemePreference"/>, et pour la même raison :
/// suivre Windows est le comportement attendu par défaut, mais un poste anglophone qui
/// travaille en français — ou l'inverse — doit pouvoir trancher.
/// </remarks>
public enum LanguagePreference
{
    /// <summary>Suit la langue d'affichage de Windows.</summary>
    System = 0,

    /// <summary>Toujours en français.</summary>
    French = 1,

    /// <summary>Toujours en anglais.</summary>
    English = 2,
}

/// <summary>Étendue de la lecture des discussions (SPEC-POLL-003).</summary>
public enum ThreadPollingScope
{
    /// <summary>
    /// Défaut : uniquement les PR où l'utilisateur est auteur, relecteur ou participant.
    /// Les autres sont revisitées périodiquement pour détecter une participation nouvelle.
    /// </summary>
    InvolvedOnly = 0,

    /// <summary>Toutes les PR actives des dépôts surveillés, à chaque cycle.</summary>
    AllWatchedPullRequests = 1,
}

/// <summary>Résultat d'une validation de configuration (SPEC-CFG-003).</summary>
/// <remarks>
/// Les erreurs sont désignées par leur clé et non par leur formulation : la validation dit ce
/// qui manque, l'interface le dit dans la langue de l'utilisateur (SPEC-UI-LANG-002).
/// </remarks>
/// <param name="Errors">Erreurs relevées, vides si la configuration est utilisable.</param>
public sealed record ConfigurationValidationResult(IReadOnlyList<TextRef> Errors)
{
    /// <summary>Configuration valide.</summary>
    public static readonly ConfigurationValidationResult Valid = new(Array.Empty<TextRef>());

    /// <summary>Vrai si aucune erreur n'a été relevée.</summary>
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Configuration complète de l'application — le contenu de <c>config.json</c>.
/// </summary>
/// <remarks>
/// Patron Options : un seul objet transporte les réglages, validé en un point unique
/// (<see cref="Validate"/>), et clonable pour permettre une édition annulable
/// (SPEC-CFG-004).
/// </remarks>
public sealed class WatcherConfiguration
{
    /// <summary>Identifiant donné au compte issu d'une configuration au format 1.</summary>
    /// <remarks>
    /// Valeur littérale et lisible : elle apparaît dans <c>config.json</c> et dans
    /// <c>state.json</c>, et doit rester identique d'une exécution à l'autre sous peine de
    /// réamorcer à chaque démarrage.
    /// </remarks>
    public const string MigratedAccountId = "principal";

    /// <summary>Intervalle de sondage minimal autorisé, en secondes.</summary>
    public const int MinimumPollIntervalSeconds = 30;

    /// <summary>Intervalle de sondage par défaut, en secondes.</summary>
    public const int DefaultPollIntervalSeconds = 180;

    /// <summary>
    /// Comptes de forge surveillés (SPEC-CFG-008).
    /// </summary>
    /// <remarks>
    /// Plusieurs comptes sont interrogés au cours du <b>même</b> cycle : les notifications
    /// d'Azure DevOps, de GitHub et de GitLab arrivent indifféremment, et le seuil de synthèse
    /// s'applique à leur total.
    /// </remarks>
    public List<WatchedAccount> Accounts { get; set; } = [];

    /// <summary>Intervalle entre deux cycles, en secondes.</summary>
    public int PollIntervalSeconds { get; set; } = DefaultPollIntervalSeconds;

    /// <summary>Apparence de l'interface (SPEC-UI-THEME-001).</summary>
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    /// <summary>Langue de l'interface (SPEC-UI-LANG-001).</summary>
    public LanguagePreference Language { get; set; } = LanguagePreference.System;

    /// <summary>Types de notifications activés.</summary>
    public NotificationPreferences Notifications { get; set; } = new();

    /// <summary>Étendue de la lecture des discussions.</summary>
    public ThreadPollingScope ThreadScope { get; set; } = ThreadPollingScope.InvolvedOnly;

    /// <summary>
    /// Délai, en minutes, avant de relire les discussions d'une PR qui ne concerne pas
    /// l'utilisateur (portée <see cref="ThreadPollingScope.InvolvedOnly"/>).
    /// </summary>
    public int UninvolvedThreadRefreshMinutes { get; set; } = 30;

    /// <summary>Notifier aussi les actions faites par l'utilisateur lui-même (défaut : non).</summary>
    public bool NotifyOwnActions { get; set; }

    /// <summary>Démarrer avec la session Windows (SPEC-CFG-006).</summary>
    public bool LaunchOnWindowsStartup { get; set; }

    /// <summary>Émettre un son avec les notifications.</summary>
    public bool PlayNotificationSound { get; set; } = true;

    /// <summary>Au-delà de ce nombre d'événements, une synthèse remplace les notifications individuelles.</summary>
    public int MaxNotificationsPerPoll { get; set; } = 5;

    /// <summary>Nombre d'appels simultanés à une même forge.</summary>
    public int MaxParallelRequests { get; set; } = 6;

    // ---------------------------------------------------------------- format 1 (hérité)

    /// <summary>Fournisseur unique du format 1. Repris par <see cref="Migrate"/>.</summary>
    public SourceControlProvider Provider { get; set; } = SourceControlProvider.AzureDevOps;

    /// <summary>URL unique du format 1. Reprise par <see cref="Migrate"/>.</summary>
    public string OrganizationUrl { get; set; } = string.Empty;

    /// <summary>Jeton chiffré unique du format 1. Repris par <see cref="Migrate"/>.</summary>
    public string ProtectedPersonalAccessToken { get; set; } = string.Empty;

    /// <summary>Dépôts du format 1. Repris par <see cref="Migrate"/>.</summary>
    public List<WatchedRepository> Repositories { get; set; } = [];

    /// <summary>Pipelines du format 1. Repris par <see cref="Migrate"/>.</summary>
    public List<WatchedPipeline> Pipelines { get; set; } = [];

    // ----------------------------------------------------------------------------------

    /// <summary>Intervalle de sondage, borné par <see cref="MinimumPollIntervalSeconds"/>.</summary>
    public TimeSpan PollInterval
        => TimeSpan.FromSeconds(Math.Max(MinimumPollIntervalSeconds, PollIntervalSeconds));

    /// <summary>Comptes effectivement interrogés à chaque cycle.</summary>
    public IReadOnlyList<WatchedAccount> EnabledAccounts
        => Accounts.Where(account => account.IsEnabled).ToList();

    /// <summary>Identifiants de tous les comptes configurés, activés ou non.</summary>
    public IReadOnlySet<string> AccountIds
        => Accounts
            .Select(account => account.Id)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Compte portant l'identifiant indiqué, ou <c>null</c>.</summary>
    public WatchedAccount? FindAccount(string accountId)
        => Accounts.FirstOrDefault(account => string.Equals(account.Id, accountId, StringComparison.Ordinal));

    /// <summary>
    /// Empreinte de ce qui change le déroulement d'un cycle : deux configurations de même
    /// empreinte produisent le même cycle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sert à décider s'il faut relancer un cycle immédiatement après un enregistrement.
    /// Ajouter un dépôt ou renouveler un jeton doit être pris en compte tout de suite ;
    /// changer de thème, couper le son ou décocher le démarrage avec Windows ne justifie pas
    /// de réinterroger les forges — c'est un appel réseau pour rien, et une rafale de
    /// notifications possible.
    /// </para>
    /// <para>
    /// Les préférences de notification en font partie : elles déterminent quelles règles de
    /// détection sont actives, donc si les discussions doivent être lues (SPEC-POLL-003).
    /// Le jeton, lui, n'y figure pas — un secret n'a pas à circuler dans une empreinte
    /// destinée aux journaux et aux comparaisons (ADR-0002) ; c'est la présence du compte et
    /// son adresse qui sont suivies.
    /// </para>
    /// </remarks>
    public string MonitoringSignature => string.Join(
        "\n",
        [
            $"interval={PollInterval.TotalSeconds}",
            $"scope={ThreadScope}",
            $"refresh={UninvolvedThreadRefreshMinutes}",
            $"own={NotifyOwnActions}",
            $"kinds={Notifications.EnabledSignature}",
            .. Accounts.Select(account => account.MonitoringSignature),
        ]);

    /// <summary>
    /// Convertit une configuration au format 1 en un compte unique (SPEC-CFG-008).
    /// </summary>
    /// <remarks>
    /// Appelée juste après la lecture du fichier. Elle est <b>idempotente</b> : une
    /// configuration déjà au format 2 n'est pas touchée, et les champs hérités sont vidés pour
    /// que le prochain enregistrement ne les réécrive pas.
    /// </remarks>
    /// <returns>Vrai si une migration a eu lieu.</returns>
    public bool Migrate()
    {
        var hasLegacyContent = !string.IsNullOrWhiteSpace(OrganizationUrl)
                               || !string.IsNullOrEmpty(ProtectedPersonalAccessToken)
                               || Repositories.Count > 0
                               || Pipelines.Count > 0;

        if (Accounts.Count > 0 || !hasLegacyContent)
        {
            return false;
        }

        Accounts.Add(new WatchedAccount
        {
            Id = MigratedAccountId,
            Provider = Provider,
            Url = OrganizationUrl,
            ProtectedPersonalAccessToken = ProtectedPersonalAccessToken,
            Repositories = Repositories.Select(repository => repository.Clone()).ToList(),
            Pipelines = Pipelines.Select(pipeline => pipeline.Clone()).ToList(),
        });

        OrganizationUrl = string.Empty;
        ProtectedPersonalAccessToken = string.Empty;
        Repositories = [];
        Pipelines = [];

        return true;
    }

    /// <summary>
    /// Vérifie que la configuration permet de travailler (SPEC-CFG-003).
    /// </summary>
    /// <param name="tokenAccessor">
    /// Retourne le jeton en clair d'un compte. Passé en paramètre car la validation ne doit
    /// pas savoir déchiffrer.
    /// </param>
    public ConfigurationValidationResult Validate(Func<WatchedAccount, string?> tokenAccessor)
    {
        ArgumentNullException.ThrowIfNull(tokenAccessor);

        var errors = new List<TextRef>();
        var enabled = EnabledAccounts;

        if (Accounts.Count == 0)
        {
            errors.Add(TextRef.Of(TextKeys.Config.NoAccount));
        }
        else if (enabled.Count == 0)
        {
            errors.Add(TextRef.Of(TextKeys.Config.AllAccountsDisabled));
        }

        foreach (var account in enabled)
        {
            errors.AddRange(account.Validate(tokenAccessor(account)).Errors);
        }

        // Un dépôt OU un pipeline suffit, et il peut se trouver sur n'importe quel compte :
        // surveiller uniquement des pipelines est un usage légitime (SPEC-PIPE-006).
        if (enabled.Count > 0 && !enabled.Any(account => account.HasSelection))
        {
            errors.Add(TextRef.Of(TextKeys.Config.NoSelection));
        }

        if (PollIntervalSeconds < MinimumPollIntervalSeconds)
        {
            errors.Add(TextRef.Of(TextKeys.Config.PollIntervalTooShort, MinimumPollIntervalSeconds));
        }

        if (MaxNotificationsPerPoll < 1)
        {
            errors.Add(TextRef.Of(TextKeys.Config.MaxNotificationsTooLow));
        }

        return errors.Count == 0 ? ConfigurationValidationResult.Valid : new ConfigurationValidationResult(errors);
    }

    /// <summary>
    /// Copie profonde, pour éditer sans impacter la configuration active
    /// (le bouton « Annuler » ne doit rien laisser derrière lui, SPEC-CFG-004).
    /// </summary>
    public WatcherConfiguration Clone() => new()
    {
        Accounts = Accounts.Select(account => account.Clone()).ToList(),
        PollIntervalSeconds = PollIntervalSeconds,
        Theme = Theme,
        Notifications = Notifications.Clone(),
        ThreadScope = ThreadScope,
        UninvolvedThreadRefreshMinutes = UninvolvedThreadRefreshMinutes,
        NotifyOwnActions = NotifyOwnActions,
        LaunchOnWindowsStartup = LaunchOnWindowsStartup,
        PlayNotificationSound = PlayNotificationSound,
        MaxNotificationsPerPoll = MaxNotificationsPerPoll,
        MaxParallelRequests = MaxParallelRequests,

        // Les champs hérités sont recopiés pour qu'un aller-retour Edit/Apply reste fidèle,
        // même si Migrate() les a normalement déjà vidés.
        Provider = Provider,
        OrganizationUrl = OrganizationUrl,
        ProtectedPersonalAccessToken = ProtectedPersonalAccessToken,
        Repositories = Repositories.Select(repository => repository.Clone()).ToList(),
        Pipelines = Pipelines.Select(pipeline => pipeline.Clone()).ToList(),
    };
}
