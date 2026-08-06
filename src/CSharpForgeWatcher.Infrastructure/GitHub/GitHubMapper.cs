using System.Globalization;
using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Domain.Identity;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Infrastructure.GitHub.Dtos;

namespace CSharpForgeWatcher.Infrastructure.GitHub;

/// <summary>
/// Traduit le JSON de GitHub en modèle de domaine.
/// </summary>
/// <remarks>
/// Point de contact unique entre le vocabulaire de GitHub et le métier (SPEC-FORGE-005) :
/// une <i>review</i> devient un vote, une <i>issue comment</i> devient un message de
/// discussion, un <i>workflow run</i> devient une exécution de pipeline. Le domaine ne voit
/// jamais ces mots. Le tableau complet de correspondance est dans
/// <c>docs/specs/SPEC-FORGES.md</c>.
/// </remarks>
internal static class GitHubMapper
{
    /// <summary>
    /// Identifiant de la discussion de conversation d'une pull request.
    /// </summary>
    /// <remarks>
    /// L'onglet <i>Conversation</i> de GitHub n'est pas structuré en fils : ses messages
    /// forment une discussion unique, à laquelle il faut bien donner un identifiant. Une
    /// valeur négative garantit l'absence de collision avec un identifiant de commentaire
    /// (toujours positif), et suffit puisque l'unicité n'est requise qu'au sein d'une pull
    /// request. Aucune URL n'en est déduite : chaque message porte la sienne
    /// (SPEC-LINK-004).
    /// </remarks>
    public const long ConversationThreadId = -1;

    /// <summary>Identité de l'utilisateur authentifié.</summary>
    /// <remarks>
    /// L'identité est le <c>login</c>, et non l'identifiant numérique : c'est sous cette
    /// forme que GitHub écrit les mentions dans le texte des commentaires, et la détection
    /// de mention compare le texte à cette valeur (SPEC-EVT-006, ADR-0004).
    /// </remarks>
    public static ViewerIdentity ToViewer(GhAccount? account)
    {
        var login = account?.Login ?? string.Empty;
        var name = FirstNonEmpty(account?.Name, login) ?? "Utilisateur";
        return new ViewerIdentity(login, name, login);
    }

    /// <summary>Propriétaire de dépôts, présenté comme un « espace » (SPEC-FORGE-004).</summary>
    public static ProjectSummary ToOwner(GhAccount dto)
    {
        var login = dto.Login ?? string.Empty;
        return new ProjectSummary(login, login, dto.Description);
    }

    /// <summary>Dépôt.</summary>
    public static RepositoryRef ToRepository(GhRepository dto, string fallbackOwner)
        => new(
            FirstNonEmpty(dto.Owner?.Login, fallbackOwner) ?? string.Empty,
            dto.Id.ToString(CultureInfo.InvariantCulture),
            dto.Name ?? "(sans nom)");

    /// <summary>Identité, avec repli sur <see cref="UserRef.Unknown"/>.</summary>
    public static UserRef ToUser(GhAccount? dto)
        => dto is null || string.IsNullOrEmpty(dto.Login)
            ? UserRef.Unknown
            : new UserRef(dto.Login, FirstNonEmpty(dto.Name, dto.Login) ?? dto.Login);

    /// <summary>Vrai pour un message écrit par un robot.</summary>
    /// <remarks>
    /// Les robots (rapports de couverture, mises à jour de dépendances, comptes rendus
    /// d'intégration) tiennent sur GitHub la place qu'occupent les messages système
    /// d'Azure DevOps : ils décrivent l'outillage, pas une intention humaine. Ils sont donc
    /// marqués « système » et ne déclenchent aucune notification de commentaire
    /// (SPEC-EVT-004, règle 1 ; limite consignée en SPEC-FORGE-007).
    /// </remarks>
    public static bool IsBot(GhAccount? account)
        => string.Equals(account?.Type, "Bot", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Pull request.
    /// </summary>
    /// <param name="dto">Objet renvoyé par l'API.</param>
    /// <param name="knownRepository">
    /// Dépôt déjà connu (celui interrogé), utilisé si la réponse ne porte pas
    /// l'information ; sinon les valeurs de la réponse gagnent, afin de refléter un
    /// renommage (SPEC-CFG-002, règle 1).
    /// </param>
    /// <param name="reviews">
    /// Relectures soumises, quand elles ont été lues. <c>null</c> signifie « non demandées »
    /// et non « aucune » : les votes ne sont lus que sur les pull requests qui concernent
    /// l'utilisateur (SPEC-FORGE-007).
    /// </param>
    public static PullRequest ToPullRequest(
        GhPullRequest dto,
        RepositoryRef knownRepository,
        IReadOnlyList<GhReview>? reviews)
    {
        var repositoryDto = dto.Base?.Repo;
        var repository = repositoryDto is not null && repositoryDto.Id != 0
            ? ToRepository(repositoryDto, knownRepository.ProjectName)
            : knownRepository;

        return new PullRequest
        {
            Id = dto.Number,
            Repository = repository,
            Title = string.IsNullOrWhiteSpace(dto.Title) ? $"PR !{dto.Number}" : dto.Title,
            Author = ToUser(dto.User),
            Status = ToStatus(dto),
            IsDraft = dto.Draft,
            CreatedOn = dto.CreatedAt ?? default,
            SourceBranch = dto.Head?.Ref ?? string.Empty,
            TargetBranch = dto.Base?.Ref ?? string.Empty,
            Reviewers = ToReviewers(dto.RequestedReviewers, reviews),
        };
    }

    /// <summary>
    /// État d'une pull request.
    /// </summary>
    /// <remarks>
    /// GitHub ne distingue pas « fusionnée » d'« abandonnée » : les deux sont
    /// <c>closed</c>. C'est la présence d'une date de fusion qui tranche.
    /// </remarks>
    public static PullRequestStatus ToStatus(GhPullRequest dto)
    {
        if (string.Equals(dto.State, "open", StringComparison.OrdinalIgnoreCase))
        {
            return PullRequestStatus.Active;
        }

        if (dto.MergedAt is not null || dto.Merged == true)
        {
            return PullRequestStatus.Completed;
        }

        return string.Equals(dto.State, "closed", StringComparison.OrdinalIgnoreCase)
            ? PullRequestStatus.Abandoned
            : PullRequestStatus.Unknown;
    }

    /// <summary>
    /// Relecteurs et votes, réunis depuis deux sources.
    /// </summary>
    /// <remarks>
    /// Un relecteur peut être « sollicité mais silencieux » (présent dans
    /// <c>requested_reviewers</c>) ou « ayant rendu son avis » (présent dans les
    /// relectures, dont GitHub le retire alors des sollicités). Les deux ensembles sont
    /// donc réunis. Pour un même relecteur, seule sa <b>dernière</b> relecture décisive
    /// compte — un simple commentaire ne remplace pas une approbation, ce qui est aussi la
    /// règle appliquée par GitHub pour afficher l'état d'une relecture.
    /// </remarks>
    public static IReadOnlyList<Reviewer> ToReviewers(
        IReadOnlyList<GhAccount>? requestedReviewers,
        IReadOnlyList<GhReview>? reviews)
    {
        var votes = new Dictionary<string, ReviewerVote>(StringComparer.OrdinalIgnoreCase);
        var users = new Dictionary<string, UserRef>(StringComparer.OrdinalIgnoreCase);

        var submitted = (reviews ?? [])
            .Where(review => !string.IsNullOrEmpty(review.User?.Login))
            .OrderBy(review => review.SubmittedAt ?? DateTimeOffset.MinValue)
            .ThenBy(review => review.Id);

        foreach (var review in submitted)
        {
            var login = review.User!.Login!;
            users[login] = ToUser(review.User);

            if (ToVote(review.State) is { } vote)
            {
                votes[login] = vote;
            }
            else if (!votes.ContainsKey(login))
            {
                votes[login] = ReviewerVote.NoVote;
            }
        }

        foreach (var account in requestedReviewers ?? [])
        {
            if (string.IsNullOrEmpty(account.Login))
            {
                continue;
            }

            users[account.Login] = ToUser(account);
            votes.TryAdd(account.Login, ReviewerVote.NoVote);
        }

        return votes
            .Select(entry => new Reviewer(
                users.TryGetValue(entry.Key, out var user) ? user : new UserRef(entry.Key, entry.Key),
                entry.Value))
            .ToArray();
    }

    /// <summary>
    /// Vote correspondant à l'état d'une relecture, ou <c>null</c> si elle n'exprime pas
    /// d'avis (simple commentaire, relecture retirée ou encore en brouillon).
    /// </summary>
    public static ReviewerVote? ToVote(string? state) => state?.Trim().ToUpperInvariant() switch
    {
        "APPROVED" => ReviewerVote.Approved,

        // « Changements demandés » bloque la fusion et attend une correction de l'auteur :
        // c'est exactement le sens de « en attente de l'auteur ».
        "CHANGES_REQUESTED" => ReviewerVote.WaitingForAuthor,

        _ => null,
    };

    /// <summary>
    /// Réunit les trois surfaces de discussion de GitHub en discussions du domaine.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>
    /// les messages de l'onglet <i>Conversation</i> et les corps de relecture forment une
    /// discussion unique (<see cref="ConversationThreadId"/>) ;
    /// </item>
    /// <item>
    /// chaque fil de commentaires de ligne devient une discussion, identifiée par son
    /// message racine, retrouvé en remontant les <c>in_reply_to_id</c>.
    /// </item>
    /// </list>
    /// L'état de résolution d'un fil n'existe pas dans l'API REST : les discussions restent
    /// <see cref="CommentThreadStatus.Unknown"/>, et SPEC-EVT-008 ne se déclenche donc
    /// jamais sur GitHub (SPEC-FORGE-007, ADR-0004).
    /// </remarks>
    public static IReadOnlyList<CommentThread> ToThreads(
        IReadOnlyList<GhIssueComment> issueComments,
        IReadOnlyList<GhReview> reviews,
        IReadOnlyList<GhReviewComment> reviewComments)
    {
        var threads = new List<CommentThread>();

        var conversation = issueComments
            .Select(ToComment)
            .Concat(reviews.Where(review => !string.IsNullOrWhiteSpace(review.Body)).Select(ToComment))
            .OrderBy(comment => comment.PublishedOn)
            .ThenBy(comment => comment.Id)
            .ToArray();

        if (conversation.Length > 0)
        {
            threads.Add(new CommentThread
            {
                Id = ConversationThreadId,
                Comments = conversation,
                Url = conversation[^1].Url,
            });
        }

        foreach (var group in GroupByThread(reviewComments))
        {
            var comments = group.Value
                .OrderBy(comment => comment.CreatedAt ?? DateTimeOffset.MinValue)
                .ThenBy(comment => comment.Id)
                .ToList();

            var root = comments[0];

            threads.Add(new CommentThread
            {
                Id = group.Key,
                Comments = comments.Select(ToComment).ToArray(),
                FilePath = root.Path,
                Url = root.HtmlUrl ?? string.Empty,
            });
        }

        return threads;
    }

    /// <summary>Définition de pipeline, à partir d'un workflow d'un dépôt.</summary>
    /// <param name="dto">Workflow renvoyé par l'API.</param>
    /// <param name="owner">Propriétaire du dépôt.</param>
    /// <param name="repositoryName">Nom du dépôt hébergeant le workflow.</param>
    public static PipelineDefinitionRef ToPipelineDefinition(GhWorkflow dto, string owner, string repositoryName)
        => new(
            PipelineScope(owner, repositoryName),
            dto.Id,
            DefinitionLabel(repositoryName, FirstNonEmpty(dto.Name, FileNameOf(dto.Path))));

    /// <summary>Vrai si le workflow est actif : les workflows désactivés ne s'exécutent plus.</summary>
    public static bool IsWorkflowEnabled(GhWorkflow dto)
        => string.IsNullOrEmpty(dto.State)
           || string.Equals(dto.State, "active", StringComparison.OrdinalIgnoreCase);

    /// <summary>Exécution de pipeline.</summary>
    /// <param name="dto">Exécution renvoyée par l'API.</param>
    /// <param name="owner">Propriétaire du dépôt.</param>
    /// <param name="repositoryName">Nom du dépôt.</param>
    public static PipelineRun ToPipelineRun(GhWorkflowRun dto, string owner, string repositoryName)
    {
        var isCompleted = string.Equals(dto.Status, "completed", StringComparison.OrdinalIgnoreCase);

        return new PipelineRun
        {
            Id = dto.Id,
            Definition = new PipelineDefinitionRef(
                PipelineScope(owner, repositoryName),
                dto.WorkflowId,
                DefinitionLabel(repositoryName, dto.Name)),
            RunName = dto.RunNumber > 0 ? $"#{dto.RunNumber}" : $"#{dto.Id}",
            State = ToRunState(dto.Status),
            Result = ToRunResult(dto.Conclusion),
            Branch = dto.HeadBranch ?? string.Empty,
            RequestedFor = ToUser(dto.Actor),
            StartedOn = dto.RunStartedAt ?? dto.CreatedAt,

            // GitHub ne publie pas de date de fin : sur une exécution terminée, la dernière
            // modification en tient lieu.
            FinishedOn = isCompleted ? dto.UpdatedAt : null,
            Url = dto.HtmlUrl ?? string.Empty,
        };
    }

    /// <summary>Avancement d'une exécution.</summary>
    public static PipelineRunState ToRunState(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "completed" => PipelineRunState.Completed,
        "in_progress" => PipelineRunState.InProgress,
        "queued" or "requested" or "waiting" or "pending" => PipelineRunState.NotStarted,
        _ => PipelineRunState.Unknown,
    };

    /// <summary>
    /// Résultat d'une exécution terminée.
    /// </summary>
    /// <remarks>
    /// <c>neutral</c>, <c>skipped</c> et <c>stale</c> restent inconnus à dessein : ce ne sont
    /// ni des échecs — inutile d'alerter — ni des succès — annoncer un « retour au vert »
    /// après une exécution ignorée serait faux (SPEC-PIPE-002).
    /// </remarks>
    public static PipelineRunResult ToRunResult(string? conclusion) => conclusion?.Trim().ToLowerInvariant() switch
    {
        "success" => PipelineRunResult.Succeeded,
        "failure" or "timed_out" or "startup_failure" or "action_required" => PipelineRunResult.Failed,
        "cancelled" or "canceled" => PipelineRunResult.Canceled,
        _ => PipelineRunResult.Unknown,
    };

    /// <summary>
    /// Espace d'un pipeline GitHub : <c>propriétaire/dépôt</c>.
    /// </summary>
    /// <remarks>
    /// Les workflows appartiennent à un dépôt et non à une organisation. Cette clé regroupe
    /// donc les pipelines surveillés d'un même dépôt, ce qui préserve à la fois l'économie
    /// d'appels et l'isolation des erreurs (SPEC-PIPE-004, SPEC-PIPE-005).
    /// </remarks>
    public static string PipelineScope(string owner, string repositoryName) => $"{owner}/{repositoryName}";

    /// <summary>Libellé « dépôt / workflow » : deux dépôts ont souvent un workflow « CI ».</summary>
    private static string DefinitionLabel(string repositoryName, string? workflowName)
    {
        var name = FirstNonEmpty(workflowName, "Workflow");
        return string.IsNullOrEmpty(repositoryName) ? name! : $"{repositoryName} / {name}";
    }

    /// <summary>Message de l'onglet <i>Conversation</i>.</summary>
    private static Comment ToComment(GhIssueComment dto) => new(
        dto.Id,
        ParentCommentId: null,
        ToUser(dto.User),
        dto.Body ?? string.Empty,
        dto.CreatedAt ?? default,
        IsSystem: IsBot(dto.User),
        IsDeleted: false,
        Url: dto.HtmlUrl ?? string.Empty);

    /// <summary>
    /// Corps d'une relecture, versé dans la discussion de conversation.
    /// </summary>
    /// <remarks>
    /// L'identifiant est <b>négativé</b> : les relectures et les messages de conversation
    /// proviennent de deux séquences d'identifiants distinctes, et se retrouvent ici dans la
    /// même discussion. Sans cette précaution, une collision — improbable mais possible —
    /// ferait passer un message pour « déjà vu », donc jamais notifié. L'adresse réelle
    /// étant portée par le message (SPEC-LINK-004), cet identifiant n'apparaît dans aucune
    /// URL.
    /// </remarks>
    private static Comment ToComment(GhReview dto) => new(
        -dto.Id,
        ParentCommentId: null,
        ToUser(dto.User),
        dto.Body ?? string.Empty,
        dto.SubmittedAt ?? default,
        IsSystem: IsBot(dto.User),
        IsDeleted: false,
        Url: dto.HtmlUrl ?? string.Empty);

    /// <summary>Commentaire de ligne.</summary>
    private static Comment ToComment(GhReviewComment dto) => new(
        dto.Id,
        dto.InReplyToId,
        ToUser(dto.User),
        dto.Body ?? string.Empty,
        dto.CreatedAt ?? default,
        IsSystem: IsBot(dto.User),
        IsDeleted: false,
        Url: dto.HtmlUrl ?? string.Empty);

    /// <summary>
    /// Regroupe les commentaires de ligne par fil, identifié par son message racine.
    /// </summary>
    private static IReadOnlyDictionary<long, List<GhReviewComment>> GroupByThread(
        IReadOnlyList<GhReviewComment> comments)
    {
        var byId = new Dictionary<long, GhReviewComment>();
        foreach (var comment in comments)
        {
            byId[comment.Id] = comment;
        }

        var groups = new Dictionary<long, List<GhReviewComment>>();
        foreach (var comment in comments)
        {
            var rootId = RootIdOf(comment, byId);

            if (!groups.TryGetValue(rootId, out var group))
            {
                group = [];
                groups[rootId] = group;
            }

            group.Add(comment);
        }

        return groups;
    }

    /// <summary>
    /// Remonte les réponses jusqu'au message racine du fil.
    /// </summary>
    /// <remarks>
    /// Le nombre de sauts est borné : une réponse dont le parent est absent de la page lue,
    /// ou un cycle qui ne devrait pas exister, ne doit pas boucler indéfiniment.
    /// </remarks>
    private static long RootIdOf(GhReviewComment comment, IReadOnlyDictionary<long, GhReviewComment> byId)
    {
        const int MaxHops = 64;
        var current = comment;

        for (var hop = 0; hop < MaxHops; hop++)
        {
            if (current.InReplyToId is not { } parentId
                || parentId == current.Id
                || !byId.TryGetValue(parentId, out var parent))
            {
                return current.Id;
            }

            current = parent;
        }

        return current.Id;
    }

    private static string? FileNameOf(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var slash = path.LastIndexOf('/');
        return slash >= 0 && slash < path.Length - 1 ? path[(slash + 1)..] : path;
    }

    private static string? FirstNonEmpty(params string?[] candidates)
        => candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
}
