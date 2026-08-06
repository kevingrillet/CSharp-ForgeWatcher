using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Detection;
using CSharpForgeWatcher.Application.Detection.Pipelines;
using CSharpForgeWatcher.Application.Monitoring;
using CSharpForgeWatcher.Application.Notifications;
using CSharpForgeWatcher.Domain.Monitoring;
using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Tests.Doubles;

/// <summary>
/// Assemble un <see cref="PullRequestMonitor"/> complet avec des doubles de test, pour
/// écrire des scénarios de cycle en quelques lignes.
/// </summary>
/// <remarks>
/// Un seul compte par défaut, ce qui couvre la grande majorité des scénarios. Les tests
/// multi-comptes (SPEC-CFG-008) ajoutent le leur via <see cref="Reconfigure"/>.
/// </remarks>
internal sealed class MonitorHarness
{
    /// <summary>Identifiant du compte de test.</summary>
    public const string AccountId = "compte-de-test";

    /// <summary>Jeton en clair employé par tous les comptes de test.</summary>
    public const string Token = "pat-de-test";

    /// <summary>Monte un moniteur surveillant les dépôts indiqués.</summary>
    public MonitorHarness(params RepositoryRef[] repositories)
    {
        var protector = new ReversibleSecretProtector();

        var configuration = new WatcherConfiguration
        {
            Accounts =
            [
                new WatchedAccount
                {
                    Id = AccountId,
                    Provider = SourceControlProvider.AzureDevOps,
                    Url = Build.OrganizationUrl,

                    // Jeton déjà « chiffré », comme il le serait dans config.json.
                    ProtectedPersonalAccessToken = protector.Protect(Token),
                    Repositories = (repositories.Length == 0 ? [Build.Repository] : repositories)
                        .Select(WatchedRepository.From)
                        .ToList(),
                },
            ],
        };

        ConfigurationStore = new InMemoryConfigurationStore(configuration);
        Configuration = new ConfigurationService(ConfigurationStore, protector);
        StateStore = new InMemoryMonitorStateStore();
        Dispatcher = new NotificationDispatcher(Presenter);
        GatewayFactory = new StubGatewayFactory(Gateway);

        Monitor = new PullRequestMonitor(
            Configuration,
            GatewayFactory,
            StateStore,
            PullRequestEventDetector.CreateDefault(),
            PipelineEventDetector.CreateDefault(),
            Dispatcher,
            Clock);
    }

    public FakeSourceControlGateway Gateway { get; } = new();

    /// <summary>Fabrique de passerelles : sert à brancher une seconde forge.</summary>
    public StubGatewayFactory GatewayFactory { get; }

    public RecordingNotificationPresenter Presenter { get; } = new();

    public FixedClock Clock { get; } = new(Build.Now);

    public InMemoryConfigurationStore ConfigurationStore { get; }

    public InMemoryMonitorStateStore StateStore { get; }

    public ConfigurationService Configuration { get; }

    public NotificationDispatcher Dispatcher { get; }

    public PullRequestMonitor Monitor { get; }

    /// <summary>Compte de test de la configuration active.</summary>
    public WatchedAccount Account => Configuration.Current.Accounts[0];

    /// <summary>
    /// État mémorisé du compte de test — celui que les tests inspectent.
    /// </summary>
    /// <remarks>
    /// L'état persisté est cloisonné par compte (SPEC-CFG-008) ; ce raccourci évite de le
    /// rappeler dans chaque assertion.
    /// </remarks>
    public AccountSnapshot State => StateStore.Load().ForAccount(AccountId);

    /// <summary>Exécute un cycle.</summary>
    public Task<PollReport> PollAsync() => Monitor.PollAsync(CancellationToken.None);

    /// <summary>Modifie la configuration active (comme le ferait la fenêtre de configuration).</summary>
    public void Reconfigure(Action<WatcherConfiguration> mutate)
    {
        var edited = Configuration.Edit();
        mutate(edited);
        Configuration.Apply(
            edited,
            edited.Accounts.ToDictionary(account => account.Id, _ => (string?)Token, StringComparer.Ordinal));
    }

    /// <summary>Modifie le compte de test (raccourci du cas le plus fréquent).</summary>
    public void ReconfigureAccount(Action<WatchedAccount> mutate)
        => Reconfigure(configuration => mutate(configuration.Accounts[0]));

    /// <summary>Vide le journal d'appels de la passerelle (pour n'observer que le cycle suivant).</summary>
    public void ForgetCalls() => Gateway.Calls.Clear();
}
