using CSharpForgeWatcher.Application.Detection;
using CSharpForgeWatcher.Application.Detection.Pipelines;
using CSharpForgeWatcher.Application.Links;
using CSharpForgeWatcher.Domain.Identity;
using CSharpForgeWatcher.Domain.Monitoring;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Tests.Doubles;

/// <summary>
/// Constructeurs d'objets de test. Objectif : qu'un test tienne en trois lignes et se
/// lise comme la spec qu'il vérifie — seul ce qui compte pour le cas est mentionné,
/// tout le reste a une valeur par défaut plausible.
/// </summary>
internal static class Build
{
    public const string OrganizationUrl = "https://dev.azure.com/contoso";
    public const string ViewerId = "11111111-1111-1111-1111-111111111111";
    public const string AliceId = "22222222-2222-2222-2222-222222222222";
    public const string BobId = "33333333-3333-3333-3333-333333333333";

    /// <summary>Instant de référence des tests : aucune dépendance à l'horloge réelle.</summary>
    public static readonly DateTimeOffset Now = new(2026, 8, 4, 10, 0, 0, TimeSpan.Zero);

    /// <summary>L'utilisateur de l'application.</summary>
    public static readonly UserRef Viewer = new(ViewerId, "Camille");

    /// <summary>Une collègue.</summary>
    public static readonly UserRef Alice = new(AliceId, "Alice");

    /// <summary>Un collègue.</summary>
    public static readonly UserRef Bob = new(BobId, "Bob");

    /// <summary>Dépôt de référence des tests.</summary>
    public static readonly RepositoryRef Repository = new("Backoffice", "repo-backoffice-api", "backoffice-api");

    /// <summary>Second dépôt, pour les scénarios multi-dépôts.</summary>
    public static readonly RepositoryRef OtherRepository = new("Backoffice", "repo-backoffice-web", "backoffice-web");

    /// <summary>Générateur de liens réel : les URL des événements sont donc vérifiables.</summary>
    public static readonly IPullRequestLinkBuilder Links = AzureDevOpsLinkBuilder.For(OrganizationUrl);

    /// <summary>Construit une pull request.</summary>
    public static PullRequest Pull(
        int id = 42,
        UserRef? author = null,
        PullRequestStatus status = PullRequestStatus.Active,
        bool isDraft = false,
        IEnumerable<Reviewer>? reviewers = null,
        string title = "Corrige le calcul des heures",
        RepositoryRef? repository = null) => new()
        {
            Id = id,
            Repository = repository ?? Repository,
            Title = title,
            Author = author ?? Alice,
            Status = status,
            IsDraft = isDraft,
            CreatedOn = Now.AddDays(-1),
            SourceBranch = "feature/heures",
            TargetBranch = "main",
            Reviewers = reviewers?.ToArray() ?? Array.Empty<Reviewer>(),
        };

    /// <summary>Construit un relecteur et son vote.</summary>
    public static Reviewer Vote(UserRef user, ReviewerVote vote = ReviewerVote.NoVote, bool required = false)
        => new(user, vote, required);

    /// <summary>Construit une discussion.</summary>
    public static CommentThread Thread(
        long id = 1,
        CommentThreadStatus status = CommentThreadStatus.Active,
        IEnumerable<CSharpForgeWatcher.Domain.PullRequests.Comment>? comments = null,
        string? filePath = null,
        bool isDeleted = false,
        string url = "") => new()
        {
            Id = id,
            Status = status,
            FilePath = filePath,
            IsDeleted = isDeleted,
            Url = url,
            Comments = comments?.ToArray() ?? Array.Empty<CSharpForgeWatcher.Domain.PullRequests.Comment>(),
        };

    /// <summary>Construit un commentaire.</summary>
    public static CSharpForgeWatcher.Domain.PullRequests.Comment Comment(
        long id,
        UserRef author,
        string content = "Il manque un test ici.",
        bool isSystem = false,
        bool isDeleted = false,
        DateTimeOffset? publishedOn = null,
        string url = "")
        => new(id, null, author, content, publishedOn ?? Now.AddMinutes(-5), isSystem, isDeleted, url);

    /// <summary>
    /// Instantané tel qu'il serait mémorisé après un cycle ayant observé cette PR
    /// (et, éventuellement, ces discussions).
    /// </summary>
    public static PullRequestSnapshot Snapshot(
        PullRequest pullRequest,
        IEnumerable<CommentThread>? threads = null,
        DateTimeOffset? observedOn = null)
        => PullRequestSnapshot.From(
            new PullRequestObservation(pullRequest, threads?.ToArray()),
            ViewerId,
            observedOn ?? Now.AddMinutes(-3));

    /// <summary>Pipeline de référence des tests.</summary>
    public static readonly PipelineDefinitionRef Pipeline = new("Backoffice", 12, "CI backoffice-api");

    /// <summary>Construit une exécution de pipeline terminée.</summary>
    public static PipelineRun Run(
        long id = 100,
        PipelineRunResult result = PipelineRunResult.Succeeded,
        PipelineDefinitionRef? definition = null,
        PipelineRunState state = PipelineRunState.Completed,
        string? runName = null,
        string branch = "main",
        DateTimeOffset? finishedOn = null) => new()
        {
            Id = id,
            Definition = definition ?? Pipeline,
            RunName = runName ?? $"2026080{id % 10}.1",
            State = state,
            Result = result,
            Branch = branch,
            RequestedFor = Alice,
            StartedOn = (finishedOn ?? Now).AddMinutes(-6),
            FinishedOn = finishedOn ?? Now.AddMinutes(-1),
            Url = $"https://dev.azure.com/contoso/Backoffice/_build/results?buildId={id}",
        };

    /// <summary>Instantané de pipeline tel qu'il serait mémorisé après un cycle.</summary>
    public static PipelineSnapshot PipelineState(
        long lastRunId = 100,
        PipelineRunResult result = PipelineRunResult.Succeeded,
        PipelineDefinitionRef? definition = null)
        => PipelineSnapshot.From(
            Run(lastRunId, result, definition),
            Now.AddMinutes(-3));

    /// <summary>Contexte de détection de pipeline prêt à l'emploi.</summary>
    public static PipelineDetectionContext PipelineContext(
        PipelineRun run,
        PipelineSnapshot? previous = null) => new()
        {
            Run = run,
            Previous = previous,
            ObservedOn = Now,
        };

    /// <summary>Contexte de détection prêt à l'emploi.</summary>
    public static DetectionContext Context(
        PullRequest pullRequest,
        PullRequestSnapshot? previous = null,
        IEnumerable<CommentThread>? threads = null,
        bool notifyOwnActions = false) => new()
        {
            ViewerId = ViewerId,
            Observation = new PullRequestObservation(pullRequest, threads?.ToArray()),
            Previous = previous,
            ObservedOn = Now,
            NotifyOwnActions = notifyOwnActions,
            Links = Links,
        };
}
