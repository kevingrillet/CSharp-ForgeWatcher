using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Domain.Identity;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Infrastructure.AzureDevOps.Dtos;

namespace CSharpForgeWatcher.Infrastructure.AzureDevOps;

/// <summary>
/// Traduit le JSON d'Azure DevOps en modèle de domaine.
/// </summary>
/// <remarks>
/// Point de contact unique entre le format externe et le métier : c'est ici — et nulle
/// part ailleurs — qu'on absorbe les particularités de l'API (préfixes <c>refs/heads/</c>,
/// champs absents, libellés d'état en anglais).
/// </remarks>
internal static class AzureDevOpsMapper
{
    /// <summary>Identité de l'utilisateur authentifié.</summary>
    public static ViewerIdentity ToViewer(AdoConnectionData? data)
    {
        var user = data?.AuthenticatedUser;
        var name = FirstNonEmpty(user?.CustomDisplayName, user?.ProviderDisplayName) ?? "Utilisateur";
        return new ViewerIdentity(user?.Id ?? string.Empty, name);
    }

    /// <summary>Projet d'équipe.</summary>
    public static ProjectSummary ToProject(AdoProject dto)
        => new(dto.Id ?? string.Empty, dto.Name ?? "(sans nom)", dto.Description);

    /// <summary>Dépôt Git.</summary>
    public static RepositoryRef ToRepository(AdoRepository dto, string fallbackProjectName)
        => new(
            FirstNonEmpty(dto.Project?.Name, fallbackProjectName) ?? string.Empty,
            dto.Id ?? string.Empty,
            dto.Name ?? "(sans nom)");

    /// <summary>Identité, avec repli sur <see cref="UserRef.Unknown"/>.</summary>
    public static UserRef ToUser(AdoIdentityRef? dto)
        => dto is null || string.IsNullOrEmpty(dto.Id)
            ? UserRef.Unknown
            : new UserRef(dto.Id, dto.DisplayName ?? dto.UniqueName ?? dto.Id);

    /// <summary>
    /// Pull request.
    /// </summary>
    /// <param name="dto">Objet renvoyé par l'API.</param>
    /// <param name="knownRepository">
    /// Dépôt déjà connu (celui interrogé). Utilisé si la réponse ne porte pas
    /// l'information ; sinon les noms de la réponse gagnent, afin de refléter un
    /// renommage (SPEC-CFG-002, règle 1).
    /// </param>
    public static PullRequest ToPullRequest(AdoPullRequest dto, RepositoryRef knownRepository)
    {
        var repository = dto.Repository is { } repositoryDto && !string.IsNullOrEmpty(repositoryDto.Id)
            ? new RepositoryRef(
                FirstNonEmpty(repositoryDto.Project?.Name, knownRepository.ProjectName) ?? string.Empty,
                repositoryDto.Id,
                FirstNonEmpty(repositoryDto.Name, knownRepository.RepositoryName) ?? string.Empty)
            : knownRepository;

        return new PullRequest
        {
            Id = dto.PullRequestId,
            Repository = repository,
            Title = string.IsNullOrWhiteSpace(dto.Title) ? $"PR !{dto.PullRequestId}" : dto.Title,
            Author = ToUser(dto.CreatedBy),
            Status = PullRequestStatusExtensions.Parse(dto.Status),
            IsDraft = dto.IsDraft,
            CreatedOn = dto.CreationDate ?? default,
            SourceBranch = ToShortBranchName(dto.SourceRefName),
            TargetBranch = ToShortBranchName(dto.TargetRefName),
            Reviewers = (dto.Reviewers ?? [])
                .Select(reviewer => new Reviewer(
                    ToUser(reviewer),
                    ReviewerVoteExtensions.FromApiValue(reviewer.Vote),
                    reviewer.IsRequired))
                .ToArray(),
        };
    }

    /// <summary>Discussion et ses messages.</summary>
    public static CommentThread ToThread(AdoThread dto) => new()
    {
        Id = dto.Id,
        Status = CommentThreadStatusExtensions.Parse(dto.Status),
        IsDeleted = dto.IsDeleted,
        FilePath = dto.ThreadContext?.FilePath,
        Comments = (dto.Comments ?? [])
            .Select(comment => new Comment(
                comment.Id,
                comment.ParentCommentId is > 0 ? comment.ParentCommentId : null,
                ToUser(comment.Author),
                comment.Content ?? string.Empty,
                comment.PublishedDate ?? dto.PublishedDate ?? default,
                IsSystemComment(comment.CommentType),
                comment.IsDeleted))
            .ToArray(),
    };

    /// <summary>Définition de pipeline.</summary>
    public static PipelineDefinitionRef ToPipelineDefinition(AdoBuildDefinition dto, string fallbackProjectName)
        => new(
            FirstNonEmpty(dto.Project?.Name, fallbackProjectName) ?? string.Empty,
            dto.Id,
            dto.Name ?? $"Pipeline {dto.Id}");

    /// <summary>Vrai si la définition est utilisable (ni désactivée, ni en pause).</summary>
    public static bool IsPipelineEnabled(AdoBuildDefinition dto)
        => !string.Equals(dto.QueueStatus, "disabled", StringComparison.OrdinalIgnoreCase);

    /// <summary>Exécution de pipeline.</summary>
    public static PipelineRun ToPipelineRun(AdoBuild dto, string projectName)
    {
        var definition = dto.Definition is { } definitionDto
            ? ToPipelineDefinition(definitionDto, projectName)
            // Définition absente de la réponse : on affiche le numéro d'exécution, seule
            // information disponible. Volontairement sans mot : un nom de pipeline est une
            // donnée de la forge, pas un message — le rendre traduisible imposerait un
            // TextRef à tous les noms de pipeline pour ce seul cas de repli.
            : new PipelineDefinitionRef(projectName, 0, $"#{dto.Id}");

        return new PipelineRun
        {
            Id = dto.Id,
            Definition = definition,
            RunName = string.IsNullOrWhiteSpace(dto.BuildNumber) ? $"#{dto.Id}" : dto.BuildNumber,
            State = PipelineRunExtensions.ParseState(dto.Status),
            Result = PipelineRunExtensions.ParseResult(dto.Result),
            Branch = ToShortBranchName(dto.SourceBranch),
            RequestedFor = ToUser(dto.RequestedFor),
            StartedOn = dto.StartTime,
            FinishedOn = dto.FinishTime,
            // L'URL web n'est pas toujours fournie : le moniteur la reconstruit au besoin.
            Url = dto.Links?.Web?.Href ?? string.Empty,
        };
    }

    /// <summary>« refs/heads/feature/x » devient « feature/x ».</summary>
    public static string ToShortBranchName(string? refName)
    {
        if (string.IsNullOrEmpty(refName))
        {
            return string.Empty;
        }

        const string HeadsPrefix = "refs/heads/";
        return refName.StartsWith(HeadsPrefix, StringComparison.OrdinalIgnoreCase)
            ? refName[HeadsPrefix.Length..]
            : refName;
    }

    private static bool IsSystemComment(string? commentType)
        => string.Equals(commentType, "system", StringComparison.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] candidates)
        => candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
}
