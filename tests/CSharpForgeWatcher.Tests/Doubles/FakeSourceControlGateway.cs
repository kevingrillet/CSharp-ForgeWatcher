using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Tests.Doubles;

/// <summary>
/// Passerelle Azure DevOps simulée : on lui donne des données, éventuellement des pannes,
/// et elle enregistre les appels reçus (pour vérifier qu'on n'appelle pas trop).
/// </summary>
internal sealed class FakeSourceControlGateway : ISourceControlGateway
{
    /// <summary>Identité retournée par <see cref="GetViewerAsync"/>.</summary>
    public ViewerIdentity Viewer { get; set; } = new(Build.ViewerId, "Camille");

    /// <summary>Panne à lever lors de la résolution de l'identité.</summary>
    public SourceControlException? ViewerFailure { get; set; }

    /// <summary>PR actives par identifiant de dépôt.</summary>
    public Dictionary<string, List<PullRequest>> ActivePullRequests { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Pannes de lecture, par identifiant de dépôt (SPEC-POLL-002).</summary>
    public Dictionary<string, SourceControlException> RepositoryFailures { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>PR accessibles individuellement, par numéro (PR disparues de la liste active).</summary>
    public Dictionary<int, PullRequest?> PullRequestsById { get; } = [];

    /// <summary>Pannes de lecture individuelle, par numéro de PR.</summary>
    public Dictionary<int, SourceControlException> PullRequestFailures { get; } = [];

    /// <summary>Discussions par numéro de PR.</summary>
    public Dictionary<int, List<CommentThread>> Threads { get; } = [];

    /// <summary>Pannes de lecture des discussions, par numéro de PR.</summary>
    public Dictionary<int, SourceControlException> ThreadFailures { get; } = [];

    /// <summary>Projets listés dans la fenêtre de configuration.</summary>
    public List<ProjectSummary> Projects { get; } = [];

    /// <summary>Dépôts par nom de projet.</summary>
    public Dictionary<string, List<RepositoryRef>> Repositories { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Définitions de pipeline par nom de projet.</summary>
    public Dictionary<string, List<PipelineDefinitionRef>> PipelineDefinitions { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Exécutions de pipeline par nom de projet.</summary>
    public Dictionary<string, List<PipelineRun>> PipelineRuns { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Pannes de lecture des exécutions, par nom de projet (SPEC-PIPE-005).</summary>
    public Dictionary<string, SourceControlException> PipelineFailures { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Journal des appels reçus, sous la forme « opération:cible ».</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Nombre de lectures de discussions.</summary>
    public int ThreadCallCount => Calls.Count(call => call.StartsWith("threads:", StringComparison.Ordinal));

    /// <summary>Déclare les PR actives d'un dépôt.</summary>
    public FakeSourceControlGateway WithActive(RepositoryRef repository, params PullRequest[] pullRequests)
    {
        ActivePullRequests[repository.RepositoryId] = pullRequests.ToList();
        return this;
    }

    /// <summary>Déclare les discussions d'une PR.</summary>
    public FakeSourceControlGateway WithThreads(int pullRequestId, params CommentThread[] threads)
    {
        Threads[pullRequestId] = threads.ToList();
        return this;
    }

    /// <summary>Déclare les exécutions de pipeline d'un projet.</summary>
    public FakeSourceControlGateway WithPipelineRuns(string projectName, params PipelineRun[] runs)
    {
        PipelineRuns[projectName] = runs.ToList();
        return this;
    }

    /// <summary>Nombre de lectures d'exécutions de pipeline (vérifie SPEC-PIPE-004).</summary>
    public int PipelineRunCallCount => Calls.Count(call => call.StartsWith("runs:", StringComparison.Ordinal));

    public Task<ViewerIdentity> GetViewerAsync(CancellationToken cancellationToken)
    {
        Calls.Add("viewer:");
        return ViewerFailure is not null ? throw ViewerFailure : Task.FromResult(Viewer);
    }

    public Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        Calls.Add("projects:");
        return Task.FromResult<IReadOnlyList<ProjectSummary>>(Projects);
    }

    public Task<IReadOnlyList<RepositoryRef>> GetRepositoriesAsync(string projectName, CancellationToken cancellationToken)
    {
        Calls.Add($"repositories:{projectName}");
        return Task.FromResult<IReadOnlyList<RepositoryRef>>(
            Repositories.TryGetValue(projectName, out var repositories) ? repositories : []);
    }

    public Task<IReadOnlyList<PullRequest>> GetActivePullRequestsAsync(RepositoryRef repository, CancellationToken cancellationToken)
    {
        Calls.Add($"active:{repository.RepositoryId}");

        if (RepositoryFailures.TryGetValue(repository.RepositoryId, out var failure))
        {
            throw failure;
        }

        return Task.FromResult<IReadOnlyList<PullRequest>>(
            ActivePullRequests.TryGetValue(repository.RepositoryId, out var pullRequests) ? pullRequests : []);
    }

    public Task<PullRequest?> GetPullRequestAsync(RepositoryRef repository, int pullRequestId, CancellationToken cancellationToken)
    {
        Calls.Add($"pullrequest:{pullRequestId}");

        if (PullRequestFailures.TryGetValue(pullRequestId, out var failure))
        {
            throw failure;
        }

        return Task.FromResult(PullRequestsById.TryGetValue(pullRequestId, out var pullRequest) ? pullRequest : null);
    }

    public Task<IReadOnlyList<CommentThread>> GetThreadsAsync(RepositoryRef repository, int pullRequestId, CancellationToken cancellationToken)
    {
        Calls.Add($"threads:{pullRequestId}");

        if (ThreadFailures.TryGetValue(pullRequestId, out var failure))
        {
            throw failure;
        }

        return Task.FromResult<IReadOnlyList<CommentThread>>(
            Threads.TryGetValue(pullRequestId, out var threads) ? threads : []);
    }

    public Task<IReadOnlyList<PipelineDefinitionRef>> GetPipelineDefinitionsAsync(
        string projectName,
        CancellationToken cancellationToken)
    {
        Calls.Add($"definitions:{projectName}");
        return Task.FromResult<IReadOnlyList<PipelineDefinitionRef>>(
            PipelineDefinitions.TryGetValue(projectName, out var definitions) ? definitions : []);
    }

    public Task<IReadOnlyList<PipelineRun>> GetRecentPipelineRunsAsync(
        string projectName,
        IReadOnlyCollection<long> definitionIds,
        int maxRuns,
        CancellationToken cancellationToken)
    {
        Calls.Add($"runs:{projectName}:{string.Join(',', definitionIds)}");

        if (PipelineFailures.TryGetValue(projectName, out var failure))
        {
            throw failure;
        }

        var runs = PipelineRuns.TryGetValue(projectName, out var all)
            ? all.Where(run => definitionIds.Contains(run.Definition.DefinitionId)).Take(maxRuns).ToList()
            : [];

        return Task.FromResult<IReadOnlyList<PipelineRun>>(runs);
    }
}

/// <summary>
/// Passerelle qui échoue un nombre donné de fois avant de réussir : sert à vérifier la
/// politique de réessai (SPEC-POLL-005).
/// </summary>
internal sealed class FlakyGateway(SourceControlException failure, int failures) : ISourceControlGateway
{
    public int Attempts { get; private set; }

    public Task<ViewerIdentity> GetViewerAsync(CancellationToken cancellationToken)
    {
        Attempts++;
        return Attempts <= failures
            ? throw failure
            : Task.FromResult(new ViewerIdentity(Build.ViewerId, "Camille"));
    }

    public Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ProjectSummary>>([]);

    public Task<IReadOnlyList<RepositoryRef>> GetRepositoriesAsync(string projectName, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<RepositoryRef>>([]);

    public Task<IReadOnlyList<PullRequest>> GetActivePullRequestsAsync(RepositoryRef repository, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PullRequest>>([]);

    public Task<PullRequest?> GetPullRequestAsync(RepositoryRef repository, int pullRequestId, CancellationToken cancellationToken)
        => Task.FromResult<PullRequest?>(null);

    public Task<IReadOnlyList<CommentThread>> GetThreadsAsync(RepositoryRef repository, int pullRequestId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CommentThread>>([]);

    public Task<IReadOnlyList<PipelineDefinitionRef>> GetPipelineDefinitionsAsync(string projectName, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PipelineDefinitionRef>>([]);

    public Task<IReadOnlyList<PipelineRun>> GetRecentPipelineRunsAsync(
        string projectName,
        IReadOnlyCollection<long> definitionIds,
        int maxRuns,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PipelineRun>>([]);
}
