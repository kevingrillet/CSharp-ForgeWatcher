using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Application.Detection;
using CSharpForgeWatcher.Application.Detection.Pipelines;
using CSharpForgeWatcher.Application.Links;
using CSharpForgeWatcher.Application.Notifications;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Monitoring;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Application.Monitoring;

/// <summary>
/// Cas d'usage central : sonder les forges configurées, détecter ce qui a changé, notifier.
/// </summary>
/// <remarks>
/// Le déroulement complet est décrit dans le SDD §4. Quatre invariants guident le code :
/// <list type="number">
/// <item>
/// <b>Amorçage silencieux</b> — au premier cycle d'un compte, on mémorise sans notifier
/// (SPEC-POLL-001), faute de quoi l'utilisateur recevrait tout l'historique actif.
/// </item>
/// <item>
/// <b>Isolation des pannes</b> — un dépôt illisible devient un avertissement et son état
/// est conservé intact (SPEC-POLL-002) : sans cela, ses PR sembleraient avoir disparu. Il en
/// va de même d'un compte entier : les autres forges continuent d'être surveillées
/// (SPEC-CFG-008).
/// </item>
/// <item>
/// <b>Économie d'appels</b> — les discussions ne sont lues que lorsque c'est utile
/// (SPEC-POLL-003).
/// </item>
/// <item>
/// <b>Cloisonnement</b> — chaque compte a son identité, son amorçage et sa mémoire ; rien
/// ne se mélange entre deux forges.
/// </item>
/// </list>
/// </remarks>
public sealed class PullRequestMonitor
{
    private readonly ConfigurationService _configuration;
    private readonly ISourceControlGatewayFactory _gatewayFactory;
    private readonly IMonitorStateStore _stateStore;
    private readonly PullRequestEventDetector _detector;
    private readonly PipelineEventDetector _pipelineDetector;
    private readonly NotificationDispatcher _dispatcher;
    private readonly IClock _clock;
    private readonly ILogger<PullRequestMonitor>? _logger;

    /// <summary>Garantit qu'un seul cycle s'exécute à la fois.</summary>
    private readonly SemaphoreSlim _pollGate = new(1, 1);

    /// <summary>Construit le moniteur.</summary>
    public PullRequestMonitor(
        ConfigurationService configuration,
        ISourceControlGatewayFactory gatewayFactory,
        IMonitorStateStore stateStore,
        PullRequestEventDetector detector,
        PipelineEventDetector pipelineDetector,
        NotificationDispatcher dispatcher,
        IClock clock,
        ILogger<PullRequestMonitor>? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _gatewayFactory = gatewayFactory ?? throw new ArgumentNullException(nameof(gatewayFactory));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _pipelineDetector = pipelineDetector ?? throw new ArgumentNullException(nameof(pipelineDetector));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger;
    }

    /// <summary>
    /// Exécute un cycle complet. Si un cycle est déjà en cours, retourne
    /// <see cref="PollStatus.Skipped"/> sans rien faire.
    /// </summary>
    public async Task<PollReport> PollAsync(CancellationToken cancellationToken = default)
    {
        if (!_pollGate.Wait(0, cancellationToken))
        {
            return PollReport.Skipped(_clock.UtcNow);
        }

        try
        {
            return await PollCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pollGate.Release();
        }
    }

    /// <summary>
    /// Efface l'état mémorisé : le cycle suivant réamorce silencieusement.
    /// Utile après un changement de compte ou en cas de doute sur les données.
    /// </summary>
    public void ResetState()
    {
        _stateStore.Clear();
        _logger?.LogInformation("État surveillé réinitialisé : le prochain cycle sera un amorçage.");
    }

    private async Task<PollReport> PollCoreAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var configuration = _configuration.Current;

        // ---- 1. Configuration utilisable ? ------------------------------------------------
        var validation = configuration.Validate(_configuration.TokenOf);
        if (!validation.IsValid)
        {
            return PollReport.NotConfigured(now);
        }

        var state = _stateStore.Load();
        var accounts = configuration.EnabledAccounts;

        // Le libellé du compte n'est affiché que s'il y en a plusieurs : sur un poste
        // mono-forge, il n'apporterait que du bruit.
        var showAccountLabel = accounts.Count > 1;

        // ---- 2. Un compte après l'autre ---------------------------------------------------
        // Séquentiel à dessein : chaque compte mute sa part de l'état partagé, et le
        // parallélisme utile est déjà à l'intérieur (dépôts, discussions, pipelines).
        var results = new List<AccountResult>(accounts.Count);

        foreach (var account in accounts)
        {
            results.Add(await PollAccountAsync(
                account,
                configuration,
                state.ForAccount(account.Id),
                showAccountLabel,
                now,
                cancellationToken).ConfigureAwait(false));
        }

        // ---- 3. Synthèse du cycle ---------------------------------------------------------
        var warnings = results.SelectMany(result => result.Warnings).ToList();
        var failed = results.Where(result => result.Error is not null).ToList();

        // Toutes les forges en échec : c'est un problème de fond, pas un incident local. Rien
        // n'a été appris, donc rien n'est écrit — l'état mémorisé reste exactement celui du
        // dernier cycle réussi (SPEC-POLL-004).
        if (failed.Count > 0 && failed.Count == results.Count)
        {
            var message = failed[0].Error!;
            _dispatcher.NotifyProblem(TextRef.Of(TextKeys.Poll.ReadFailedTitle), message, configuration);
            return PollReport.Failed(_clock.UtcNow, message, warnings);
        }

        foreach (var failure in failed)
        {
            warnings.Add(failure.Error!);
        }

        // ---- 4. Purge et persistance ------------------------------------------------------
        var prunedAccounts = state.PruneAccountsOutside(configuration.AccountIds);
        if (prunedAccounts > 0)
        {
            _logger?.LogInformation("{Count} compte(s) purgé(s) : retiré(s) de la configuration.", prunedAccounts);
        }

        state.LastPollOn = now;
        _stateStore.Save(state);

        var detectedEvents = results.SelectMany(result => result.Events).ToList();
        var retainedEvents = _dispatcher.Dispatch(detectedEvents, configuration);

        var wasSeeding = results.Any(result => result.WasSeeding);
        if (wasSeeding)
        {
            _logger?.LogInformation(
                "Amorçage effectué : {Count} PR mémorisée(s), aucune notification émise pour les comptes concernés.",
                state.PullRequestCount);
        }

        var views = results
            .SelectMany(result => result.PullRequests)
            .OrderByDescending(view => view.IsMine)
            .ThenByDescending(view => view.ViewerIsReviewer)
            .ThenByDescending(view => view.CreatedOn)
            .ToList();

        // Les pipelines en échec remontent en tête : c'est ce qu'on veut voir en premier.
        var pipelines = results
            .SelectMany(result => result.Pipelines)
            .OrderByDescending(view => view.IsFailing)
            .ThenBy(view => view.AccountLabel, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(view => view.Definition.ProjectName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(view => view.Definition.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new PollReport
        {
            Status = warnings.Count > 0 ? PollStatus.PartialFailure : PollStatus.Success,
            CompletedOn = _clock.UtcNow,
            Pipelines = pipelines,
            Events = retainedEvents,
            PullRequests = views,
            Warnings = warnings,
            ViewerName = string.Join(
                " · ",
                results.Select(result => result.ViewerName).Where(name => !string.IsNullOrEmpty(name)).Distinct()),
            WasSeeding = wasSeeding,
        };
    }

    /// <summary>
    /// Sonde un compte : PR actives, sort des PR disparues, discussions, pipelines.
    /// </summary>
    /// <remarks>
    /// Toute panne est convertie en <see cref="AccountResult.Error"/> ou en avertissement :
    /// cette méthode ne laisse jamais échapper d'exception, sans quoi un compte fâché
    /// priverait les autres de leur cycle (SPEC-CFG-008).
    /// </remarks>
    private async Task<AccountResult> PollAccountAsync(
        WatchedAccount account,
        WatcherConfiguration configuration,
        AccountSnapshot accountState,
        bool showAccountLabel,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var label = showAccountLabel ? account.DisplayLabel : string.Empty;
        var links = account.CreateLinkBuilder();
        var gateway = _gatewayFactory.Create(_configuration.ToConnection(account));
        var warnings = new List<TextRef>();

        // ---- Qui suis-je, sur ce compte ? -------------------------------------------------
        ViewerIdentity viewer;
        try
        {
            viewer = await gateway.GetViewerAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SourceControlException exception)
        {
            _logger?.LogError(
                exception,
                "Impossible d'identifier l'utilisateur sur le compte {Account}.",
                account.DisplayLabel);

            return AccountResult.Failed(TextRef.Of(
                TextKeys.Poll.AccountFailed,
                account.DisplayLabel,
                exception.ToUserText()));
        }

        // Changement de compte ou d'organisation : l'état précédent ne veut plus rien dire.
        if (!string.Equals(accountState.ViewerId, viewer.Id, StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogInformation(
                "Identité différente de l'état mémorisé sur {Account} : réamorçage.",
                account.DisplayLabel);

            accountState.Reset(viewer.Id);
        }

        var isSeeding = !accountState.IsSeeded;
        var detectedEvents = new List<INotifiableEvent>();

        // ---- PR actives, dépôt par dépôt (en parallèle borné) ----------------------------
        var repositories = account.Repositories
            .Where(repository => !string.IsNullOrWhiteSpace(repository.RepositoryId))
            .Select(repository => repository.ToRepositoryRef())
            .ToList();

        var fetches = await RunBoundedAsync(
            repositories,
            configuration.MaxParallelRequests,
            async (repository, token) =>
            {
                try
                {
                    var pullRequests = await gateway
                        .GetActivePullRequestsAsync(repository, token)
                        .ConfigureAwait(false);
                    return new RepositoryFetch(repository, pullRequests, null);
                }
                catch (SourceControlException exception)
                {
                    return new RepositoryFetch(repository, Array.Empty<PullRequest>(), exception.ToUserText());
                }
            },
            cancellationToken).ConfigureAwait(false);

        var readRepositoryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var activePullRequests = new List<PullRequest>();

        foreach (var fetch in fetches)
        {
            if (fetch.Error is null)
            {
                readRepositoryIds.Add(fetch.Repository.RepositoryId);
                activePullRequests.AddRange(fetch.PullRequests);
            }
            else
            {
                warnings.Add(Prefixed(
                    label,
                    TextRef.Of(TextKeys.Poll.RepositoryUnreadable, fetch.Repository.DisplayPath, fetch.Error)));
                _logger?.LogWarning("Dépôt {Repository} illisible : {Error}", fetch.Repository.DisplayPath, fetch.Error);
            }
        }

        // Échec total de la lecture des dépôts de ce compte. La garde ne s'applique que s'il y
        // avait des dépôts à lire : surveiller uniquement des pipelines est un usage valide
        // (SPEC-PIPE-006).
        if (repositories.Count > 0 && readRepositoryIds.Count == 0)
        {
            return AccountResult.Failed(
                warnings.Count > 0
                    ? warnings[0]
                    : Prefixed(label, TextRef.Of(TextKeys.Poll.NoRepositoryReadable)));
        }

        // ---- PR connues absentes de la liste active : quel est leur sort ? ---------------
        // Comparaison sur l'objet-valeur lui-même : sa forme texte n'est qu'un format de
        // sérialisation, elle n'a pas à servir de clé en mémoire.
        var activeKeys = activePullRequests.Select(pullRequest => pullRequest.Key).ToHashSet();

        var vanishedSnapshots = accountState.PullRequests.Values
            .Where(snapshot => readRepositoryIds.Contains(snapshot.RepositoryId))
            .Where(snapshot => !activeKeys.Contains(snapshot.ToKey()))
            .ToList();

        var vanishedFetches = await RunBoundedAsync(
            vanishedSnapshots,
            configuration.MaxParallelRequests,
            async (snapshot, token) =>
            {
                try
                {
                    var pullRequest = await gateway
                        .GetPullRequestAsync(snapshot.ToRepositoryRef(), snapshot.Id, token)
                        .ConfigureAwait(false);
                    return new VanishedFetch(snapshot, pullRequest, null);
                }
                catch (SourceControlException exception)
                {
                    return new VanishedFetch(snapshot, null, exception.ToUserText());
                }
            },
            cancellationToken).ConfigureAwait(false);

        var closedObservations = new List<PullRequestObservation>();

        foreach (var fetch in vanishedFetches)
        {
            if (fetch.Error is not null)
            {
                // On ne sait pas : on conserve l'état, on retentera au cycle suivant.
                warnings.Add(Prefixed(
                    label,
                    TextRef.Of(
                        TextKeys.Poll.PullRequestUnreadable,
                        fetch.Snapshot.Id,
                        fetch.Snapshot.RepositoryName,
                        fetch.Error)));
                continue;
            }

            if (fetch.PullRequest is null)
            {
                // Introuvable : supprimée ou hors de portée du jeton.
                accountState.Remove(fetch.Snapshot.ToKey());
                continue;
            }

            closedObservations.Add(new PullRequestObservation(fetch.PullRequest));
        }

        // ---- Discussions des PR retenues -------------------------------------------------
        var threadFetches = await RunBoundedAsync(
            activePullRequests.Where(pullRequest =>
                ShouldReadThreads(pullRequest, accountState.Find(pullRequest.Key), configuration, viewer.Id, now)),
            configuration.MaxParallelRequests,
            async (pullRequest, token) =>
            {
                try
                {
                    var threads = await gateway
                        .GetThreadsAsync(pullRequest.Repository, pullRequest.Id, token)
                        .ConfigureAwait(false);
                    return new ThreadFetch(pullRequest.Key, threads, null);
                }
                catch (SourceControlException exception)
                {
                    return new ThreadFetch(pullRequest.Key, null, exception.ToUserText());
                }
            },
            cancellationToken).ConfigureAwait(false);

        var threadsByPullRequest = new Dictionary<PullRequestKey, IReadOnlyList<CommentThread>>();

        foreach (var fetch in threadFetches)
        {
            if (fetch.Threads is not null)
            {
                threadsByPullRequest[fetch.Key] = fetch.Threads;
            }
            else if (fetch.Error is not null)
            {
                warnings.Add(Prefixed(
                    label,
                    TextRef.Of(TextKeys.Poll.ThreadsUnreadable, fetch.Key.PullRequestId, fetch.Error)));
            }
        }

        // ---- Détection et mise à jour de l'état ------------------------------------------
        var observations = activePullRequests
            .Select(pullRequest => new PullRequestObservation(
                pullRequest,
                threadsByPullRequest.TryGetValue(pullRequest.Key, out var threads) ? threads : null))
            .Concat(closedObservations)
            .ToList();

        foreach (var observation in observations)
        {
            var previous = accountState.Find(observation.Key);

            if (!isSeeding)
            {
                detectedEvents.AddRange(_detector.Detect(new DetectionContext
                {
                    ViewerId = viewer.Id,
                    AccountId = account.Id,
                    AccountLabel = label,
                    Observation = observation,
                    Previous = previous,
                    ObservedOn = now,
                    NotifyOwnActions = configuration.NotifyOwnActions,
                    Links = links,
                }));
            }

            if (observation.PullRequest.Status.IsFinal())
            {
                // PR terminée : l'événement est émis ci-dessus, plus rien à surveiller.
                accountState.Remove(observation.Key);
            }
            else
            {
                accountState.Put(
                    observation.Key,
                    PullRequestSnapshot.From(observation, viewer.Id, now, previous));
            }
        }

        // ---- Pipelines surveillés (SPEC-PIPE-*) ------------------------------------------
        var pipelines = await ScanPipelinesAsync(
            new AccountContext(account, gateway, links, accountState, label),
            configuration,
            isSeeding,
            now,
            cancellationToken).ConfigureAwait(false);

        detectedEvents.AddRange(pipelines.Events);
        warnings.AddRange(pipelines.Warnings);

        // ---- Purges propres à ce compte --------------------------------------------------
        var pruned = accountState.PruneRepositoriesOutside(account.WatchedRepositoryIds);
        if (pruned > 0)
        {
            _logger?.LogInformation("{Count} PR purgée(s) : dépôt retiré de la configuration.", pruned);
        }

        var prunedPipelines = accountState.PrunePipelinesOutside(account.WatchedPipelineKeys);
        if (prunedPipelines > 0)
        {
            _logger?.LogInformation("{Count} pipeline(s) purgé(s) : retiré(s) de la configuration.", prunedPipelines);
        }

        accountState.ViewerId = viewer.Id;
        accountState.IsSeeded = true;

        var views = activePullRequests
            .Select(pullRequest => ToView(pullRequest, accountState.Find(pullRequest.Key), viewer.Id, links, label))
            .ToList();

        return new AccountResult(
            isSeeding ? Array.Empty<INotifiableEvent>() : detectedEvents,
            views,
            pipelines.Views,
            warnings,
            Error: null,
            viewer.DisplayName,
            isSeeding);
    }

    /// <summary>
    /// Lit les exécutions récentes des pipelines surveillés d'un compte, détecte les
    /// changements et met à jour l'état (SPEC-PIPE-001 à SPEC-PIPE-005).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Un seul appel de passerelle par espace, quel que soit le nombre de pipelines
    /// surveillés (SPEC-PIPE-004) : les définitions d'un même espace sont demandées
    /// ensemble. Le nombre de requêtes HTTP que cela représente dépend de la forge
    /// (SPEC-FORGE-007).
    /// </para>
    /// <para>
    /// Un espace illisible devient un avertissement, son état est conservé, et ses pipelines
    /// restent affichés avec leur dernier résultat connu (SPEC-PIPE-005).
    /// </para>
    /// </remarks>
    private async Task<PipelineScan> ScanPipelinesAsync(
        AccountContext context,
        WatcherConfiguration configuration,
        bool isSeeding,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var watched = context.Account.Pipelines
            .Where(pipeline => pipeline.DefinitionId > 0 && !string.IsNullOrWhiteSpace(pipeline.ProjectName))
            .ToList();

        if (watched.Count == 0)
        {
            return PipelineScan.Empty;
        }

        var detectedEvents = new List<INotifiableEvent>();
        var warnings = new List<TextRef>();

        var byProject = watched
            .GroupBy(pipeline => pipeline.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var fetches = await RunBoundedAsync(
            byProject,
            configuration.MaxParallelRequests,
            async (group, token) =>
            {
                var definitionIds = group.Select(pipeline => pipeline.DefinitionId).Distinct().ToList();

                try
                {
                    // Quelques exécutions par définition suffisent : seule la dernière
                    // terminée est exploitée.
                    var maxRuns = Math.Clamp(definitionIds.Count * 5, 10, 200);
                    var runs = await context.Gateway
                        .GetRecentPipelineRunsAsync(group.Key, definitionIds, maxRuns, token)
                        .ConfigureAwait(false);
                    return new PipelineFetch(group.Key, runs, null);
                }
                catch (SourceControlException exception)
                {
                    return new PipelineFetch(group.Key, Array.Empty<PipelineRun>(), exception.ToUserText());
                }
            },
            cancellationToken).ConfigureAwait(false);

        var views = new List<PipelineView>();

        foreach (var fetch in fetches)
        {
            var projectPipelines = watched
                .Where(pipeline => string.Equals(
                    pipeline.ProjectName,
                    fetch.ProjectName,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (fetch.Error is not null)
            {
                warnings.Add(Prefixed(
                    context.Label,
                    TextRef.Of(TextKeys.Poll.PipelinesUnreadable, fetch.ProjectName, fetch.Error)));
                _logger?.LogWarning(
                    "Pipelines de l'espace {Project} illisibles : {Error}",
                    fetch.ProjectName,
                    fetch.Error);

                // L'état n'est pas touché : on réessaiera au prochain cycle.
                views.AddRange(projectPipelines
                    .Select(pipeline => context.State.FindPipeline(pipeline.Key))
                    .Where(snapshot => snapshot is not null)
                    .Select(snapshot => ToView(snapshot!, context)));
                continue;
            }

            foreach (var pipeline in projectPipelines)
            {
                var latest = fetch.Runs
                    .Where(run => run.Definition.DefinitionId == pipeline.DefinitionId && run.IsCompleted)
                    .OrderByDescending(run => run.Id)
                    .FirstOrDefault();

                var previous = context.State.FindPipeline(pipeline.Key);

                if (latest is null)
                {
                    // Aucune exécution terminée (pipeline neuf, ou uniquement des exécutions
                    // en cours) : rien à comparer.
                    if (previous is not null)
                    {
                        views.Add(ToView(previous, context));
                    }

                    continue;
                }

                // L'API ne fournit pas toujours l'URL web : on la reconstruit.
                var run = string.IsNullOrEmpty(latest.Url)
                    ? latest with { Url = context.Links.ForPipelineRun(pipeline.ProjectName, latest.Id) }
                    : latest;

                if (!isSeeding)
                {
                    detectedEvents.AddRange(_pipelineDetector.Detect(new PipelineDetectionContext
                    {
                        Run = run,
                        Previous = previous,
                        ObservedOn = now,
                        AccountId = context.Account.Id,
                        AccountLabel = context.Label,
                    }));
                }

                context.State.PutPipeline(PipelineSnapshot.From(run, now));

                views.Add(new PipelineView
                {
                    Definition = run.Definition,
                    Url = run.Url,
                    RunName = run.RunName,
                    Result = run.Result,
                    FinishedOn = run.FinishedOn,
                    AccountLabel = context.Label,
                });
            }
        }

        return new PipelineScan(views, detectedEvents, warnings);
    }

    /// <summary>Vue construite depuis l'état mémorisé, quand la lecture a échoué.</summary>
    private static PipelineView ToView(PipelineSnapshot snapshot, AccountContext context) => new()
    {
        Definition = snapshot.ToDefinitionRef(),
        Url = context.Links.ForPipelineRun(snapshot.ProjectName, snapshot.LastCompletedRunId),
        RunName = snapshot.LastRunName,
        Result = snapshot.LastResult,
        FinishedOn = null,
        AccountLabel = context.Label,
    };

    /// <summary>
    /// Décide s'il faut payer un appel pour lire les discussions d'une PR (SPEC-POLL-003).
    /// </summary>
    private bool ShouldReadThreads(
        PullRequest pullRequest,
        PullRequestSnapshot? previous,
        WatcherConfiguration configuration,
        string viewerId,
        DateTimeOffset now)
    {
        // Aucune règle active n'a besoin des discussions : inutile de les lire.
        if (!_detector.AnyRuleRequiresThreads)
        {
            return false;
        }

        if (configuration.ThreadScope == ThreadPollingScope.AllWatchedPullRequests)
        {
            return true;
        }

        // L'utilisateur est directement concerné.
        if (pullRequest.IsAuthoredBy(viewerId) || pullRequest.FindReviewer(viewerId) is not null)
        {
            return true;
        }

        // PR inconnue : une lecture initiale est nécessaire, ne serait-ce que pour savoir
        // si l'utilisateur participe à une discussion.
        if (previous is null || previous.ThreadsReadOn is null)
        {
            return true;
        }

        if (previous.ViewerParticipatesInAnyThread)
        {
            return true;
        }

        var refresh = TimeSpan.FromMinutes(Math.Max(1, configuration.UninvolvedThreadRefreshMinutes));
        return now - previous.ThreadsReadOn.Value >= refresh;
    }

    /// <summary>Projette une PR observée en vue d'affichage.</summary>
    private static PullRequestView ToView(
        PullRequest pullRequest,
        PullRequestSnapshot? snapshot,
        string viewerId,
        IPullRequestLinkBuilder links,
        string accountLabel) => new()
        {
            Key = pullRequest.Key,
            Id = pullRequest.Id,
            Title = pullRequest.Title,
            Repository = pullRequest.Repository,
            AuthorName = pullRequest.Author.SafeDisplayName,
            Url = links.ForPullRequest(pullRequest.Repository, pullRequest.Id),
            IsMine = pullRequest.IsAuthoredBy(viewerId),
            ViewerIsReviewer = pullRequest.FindReviewer(viewerId) is not null,
            ViewerVote = pullRequest.VoteOf(viewerId),
            UnresolvedThreadCount = snapshot?.UnresolvedThreadCount ?? 0,
            IsDraft = pullRequest.IsDraft,
            CreatedOn = pullRequest.CreatedOn,
            AccountLabel = accountLabel,
        };

    /// <summary>Préfixe un message du libellé de compte, quand il y a plusieurs comptes.</summary>
    private static TextRef Prefixed(string label, TextRef message)
        => string.IsNullOrEmpty(label) ? message : TextRef.Of(TextKeys.Poll.AccountPrefixed, label, message);

    /// <summary>
    /// Exécute un traitement sur une collection, avec un nombre maximal d'opérations
    /// simultanées (ENF-5 : ne pas saturer l'API).
    /// </summary>
    private static async Task<TResult[]> RunBoundedAsync<TItem, TResult>(
        IEnumerable<TItem> items,
        int maxParallel,
        Func<TItem, CancellationToken, Task<TResult>> body,
        CancellationToken cancellationToken)
    {
        using var throttle = new SemaphoreSlim(Math.Max(1, maxParallel));

        var tasks = items.Select(async item =>
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await body(item, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                throttle.Release();
            }
        }).ToList();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>Ce qu'un balayage de pipelines a produit.</summary>
    /// <remarks>
    /// Retourné plutôt qu'écrit dans des listes reçues en paramètre : un appelant qui doit
    /// deviner que la méthode remplit ses collections est un appelant qu'on finit par casser.
    /// </remarks>
    private sealed record PipelineScan(
        IReadOnlyList<PipelineView> Views,
        IReadOnlyList<INotifiableEvent> Events,
        IReadOnlyList<TextRef> Warnings)
    {
        /// <summary>Aucun pipeline surveillé sur ce compte.</summary>
        public static readonly PipelineScan Empty = new(
            Array.Empty<PipelineView>(),
            Array.Empty<INotifiableEvent>(),
            Array.Empty<TextRef>());
    }

    /// <summary>Ce qu'il faut savoir pour sonder un compte, réuni en un objet.</summary>
    private sealed record AccountContext(
        WatchedAccount Account,
        ISourceControlGateway Gateway,
        IPullRequestLinkBuilder Links,
        AccountSnapshot State,
        string Label);

    /// <summary>Ce qu'un compte a produit pendant le cycle.</summary>
    private sealed record AccountResult(
        IReadOnlyList<INotifiableEvent> Events,
        IReadOnlyList<PullRequestView> PullRequests,
        IReadOnlyList<PipelineView> Pipelines,
        IReadOnlyList<TextRef> Warnings,
        TextRef? Error,
        string? ViewerName,
        bool WasSeeding)
    {
        /// <summary>Compte dont le cycle n'a pas pu se faire du tout.</summary>
        public static AccountResult Failed(TextRef error) => new(
            Array.Empty<INotifiableEvent>(),
            Array.Empty<PullRequestView>(),
            Array.Empty<PipelineView>(),
            Array.Empty<TextRef>(),
            error,
            ViewerName: null,
            WasSeeding: false);
    }

    /// <summary>Résultat de la lecture des PR actives d'un dépôt.</summary>
    private sealed record RepositoryFetch(
        RepositoryRef Repository,
        IReadOnlyList<PullRequest> PullRequests,
        TextRef? Error);

    /// <summary>Résultat de la relecture d'une PR disparue de la liste active.</summary>
    private sealed record VanishedFetch(
        PullRequestSnapshot Snapshot,
        PullRequest? PullRequest,
        TextRef? Error);

    /// <summary>Résultat de la lecture des discussions d'une PR.</summary>
    private sealed record ThreadFetch(
        PullRequestKey Key,
        IReadOnlyList<CommentThread>? Threads,
        TextRef? Error);

    /// <summary>Résultat de la lecture des exécutions de pipeline d'un espace.</summary>
    private sealed record PipelineFetch(
        string ProjectName,
        IReadOnlyList<PipelineRun> Runs,
        TextRef? Error);
}
