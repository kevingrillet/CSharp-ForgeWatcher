using System.Globalization;
using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Domain.Identity;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Infrastructure.GitLab.Dtos;

namespace CSharpForgeWatcher.Infrastructure.GitLab;

/// <summary>
/// Traduit le JSON de GitLab en modèle de domaine.
/// </summary>
/// <remarks>
/// Point de contact unique entre le vocabulaire de GitLab et le métier (SPEC-FORGE-005) :
/// une <i>merge request</i> devient une pull request, une <i>note</i> devient un message, une
/// <i>approbation</i> devient un vote. Le domaine ne voit jamais ces mots. Le tableau complet
/// de correspondance est dans <c>docs/specs/SPEC-FORGES.md</c>.
/// </remarks>
internal static class GitLabMapper
{
    /// <summary>Identité de l'utilisateur authentifié.</summary>
    /// <remarks>
    /// L'identité est le <c>username</c>, pour la même raison que sur GitHub : c'est sous
    /// cette forme que GitLab écrit les mentions dans le corps des notes, et la détection de
    /// mention compare le texte à cette valeur (SPEC-EVT-006, ADR-0004).
    /// </remarks>
    public static ViewerIdentity ToViewer(GlUser? user)
    {
        var username = user?.Username ?? string.Empty;
        var name = FirstNonEmpty(user?.Name, username) ?? "Utilisateur";
        return new ViewerIdentity(username, name, username);
    }

    /// <summary>Groupe, présenté comme un « espace » (SPEC-FORGE-004).</summary>
    public static ProjectSummary ToGroup(GlGroup dto)
    {
        var path = FirstNonEmpty(dto.FullPath, dto.Name) ?? string.Empty;
        return new ProjectSummary(
            dto.Id.ToString(CultureInfo.InvariantCulture),
            path,
            dto.Description);
    }

    /// <summary>
    /// Projet, vu comme un dépôt.
    /// </summary>
    /// <remarks>
    /// L'identité est l'identifiant <b>numérique</b> du projet : GitLab accepte cet
    /// identifiant partout où un chemin est attendu, si bien que renommer un projet — ou le
    /// déplacer dans un autre groupe — ne casse pas la surveillance (SPEC-CFG-002, règle 1).
    /// Le chemin reste nécessaire à la construction des adresses web (SPEC-LINK-001).
    /// </remarks>
    public static RepositoryRef ToRepository(GlProject dto, string fallbackGroup)
        => new(
            FirstNonEmpty(dto.Namespace?.FullPath, GroupOf(dto.PathWithNamespace), fallbackGroup) ?? string.Empty,
            dto.Id.ToString(CultureInfo.InvariantCulture),
            FirstNonEmpty(dto.Path, dto.Name) ?? "(sans nom)");

    /// <summary>Identité, avec repli sur <see cref="UserRef.Unknown"/>.</summary>
    public static UserRef ToUser(GlUser? dto)
        => dto is null || string.IsNullOrEmpty(dto.Username)
            ? UserRef.Unknown
            : new UserRef(dto.Username, FirstNonEmpty(dto.Name, dto.Username) ?? dto.Username);

    /// <summary>
    /// Merge request.
    /// </summary>
    /// <param name="dto">Objet renvoyé par l'API.</param>
    /// <param name="knownRepository">Dépôt interrogé, employé comme référence.</param>
    /// <param name="approvals">
    /// Approbations, quand elles ont été lues. <c>null</c> signifie « non demandées » et non
    /// « aucune » : les votes ne sont lus que sur les merge requests qui concernent
    /// l'utilisateur (SPEC-FORGE-007).
    /// </param>
    /// <param name="reviewers">
    /// États de relecture détaillés, quand la version de GitLab les expose.
    /// </param>
    public static PullRequest ToPullRequest(
        GlMergeRequest dto,
        RepositoryRef knownRepository,
        GlApprovals? approvals,
        IReadOnlyList<GlReviewer>? reviewers)
        => new()
        {
            Id = dto.Iid,
            Repository = knownRepository,
            Title = string.IsNullOrWhiteSpace(dto.Title) ? $"MR !{dto.Iid}" : dto.Title,
            Author = ToUser(dto.Author),
            Status = ToStatus(dto),
            IsDraft = dto.Draft || dto.WorkInProgress,
            CreatedOn = dto.CreatedAt ?? default,
            SourceBranch = dto.SourceBranch ?? string.Empty,
            TargetBranch = dto.TargetBranch ?? string.Empty,
            Reviewers = ToReviewers(dto.Reviewers, approvals, reviewers),
        };

    /// <summary>
    /// État d'une merge request.
    /// </summary>
    /// <remarks>
    /// GitLab, contrairement à GitHub, distingue explicitement <c>merged</c> de
    /// <c>closed</c> : la traduction est directe. <c>locked</c> désigne une merge request
    /// verrouillée pendant une fusion, donc toujours en cours.
    /// </remarks>
    public static PullRequestStatus ToStatus(GlMergeRequest dto) => dto.State?.Trim().ToLowerInvariant() switch
    {
        "opened" or "locked" => PullRequestStatus.Active,
        "merged" => PullRequestStatus.Completed,
        "closed" => PullRequestStatus.Abandoned,
        _ => dto.MergedAt is not null ? PullRequestStatus.Completed : PullRequestStatus.Unknown,
    };

    /// <summary>
    /// Relecteurs et votes, réunis depuis trois sources.
    /// </summary>
    /// <remarks>
    /// GitLab n'a pas de « vote » unique : il a des <b>approbations</b> (point d'entrée
    /// dédié, disponible sur toutes les éditions), des <b>relecteurs sollicités</b> (portés
    /// par la merge request) et, depuis GitLab 15, un <b>état de relecture</b> par relecteur.
    /// Les trois sont réunis, l'approbation faisant foi : c'est elle qui débloque la fusion.
    /// </remarks>
    public static IReadOnlyList<Reviewer> ToReviewers(
        IReadOnlyList<GlUser>? requestedReviewers,
        GlApprovals? approvals,
        IReadOnlyList<GlReviewer>? reviewerStates)
    {
        var votes = new Dictionary<string, ReviewerVote>(StringComparer.OrdinalIgnoreCase);
        var users = new Dictionary<string, UserRef>(StringComparer.OrdinalIgnoreCase);

        void Remember(GlUser? user, ReviewerVote? vote)
        {
            if (user is null || string.IsNullOrEmpty(user.Username))
            {
                return;
            }

            users[user.Username] = ToUser(user);

            if (vote is { } value)
            {
                votes[user.Username] = value;
            }
            else
            {
                votes.TryAdd(user.Username, ReviewerVote.NoVote);
            }
        }

        foreach (var user in requestedReviewers ?? [])
        {
            Remember(user, vote: null);
        }

        foreach (var reviewer in reviewerStates ?? [])
        {
            Remember(reviewer.User, ToVote(reviewer.State));
        }

        // Lu en dernier : une approbation prime sur un état de relecture plus ancien.
        foreach (var approval in approvals?.ApprovedBy ?? [])
        {
            Remember(approval.User, ReviewerVote.Approved);
        }

        return votes
            .Select(entry => new Reviewer(
                users.TryGetValue(entry.Key, out var user) ? user : new UserRef(entry.Key, entry.Key),
                entry.Value))
            .ToArray();
    }

    /// <summary>
    /// Vote correspondant à un état de relecture, ou <c>null</c> s'il n'exprime pas d'avis.
    /// </summary>
    public static ReviewerVote? ToVote(string? state) => state?.Trim().ToLowerInvariant() switch
    {
        "approved" => ReviewerVote.Approved,

        // « Changements demandés » attend une correction de l'auteur, comme sur GitHub.
        "requested_changes" => ReviewerVote.WaitingForAuthor,

        _ => null,
    };

    /// <summary>
    /// Discussions d'une merge request.
    /// </summary>
    /// <remarks>
    /// GitLab identifie ses discussions par une empreinte textuelle, inutilisable comme
    /// identifiant numérique du domaine. La <b>première note</b> tient donc ce rôle : elle est
    /// numérique, stable, et c'est aussi celle que désigne l'ancre web <c>#note_…</c>
    /// (SPEC-FORGE-003) — le même nombre sert donc à la mémoire et au lien.
    /// <para>
    /// GitLab est la seule des trois forges à exposer l'état de résolution d'un fil :
    /// SPEC-EVT-008 y fonctionne pleinement, là où GitHub reste muet.
    /// </para>
    /// </remarks>
    /// <param name="discussions">Discussions renvoyées par l'API.</param>
    /// <param name="mergeRequestUrl">Adresse web de la merge request, base des ancres.</param>
    public static IReadOnlyList<CommentThread> ToThreads(
        IReadOnlyList<GlDiscussion> discussions,
        string mergeRequestUrl)
    {
        var threads = new List<CommentThread>();

        foreach (var discussion in discussions)
        {
            var notes = (discussion.Notes ?? [])
                .OrderBy(note => note.CreatedAt ?? DateTimeOffset.MinValue)
                .ThenBy(note => note.Id)
                .ToList();

            if (notes.Count == 0)
            {
                continue;
            }

            var root = notes[0];
            var anchor = string.IsNullOrEmpty(mergeRequestUrl)
                ? string.Empty
                : $"{mergeRequestUrl}#note_{root.Id.ToString(CultureInfo.InvariantCulture)}";

            threads.Add(new CommentThread
            {
                Id = root.Id,
                Status = ToThreadStatus(root),
                FilePath = FirstNonEmpty(root.Position?.NewPath, root.Position?.OldPath),
                Url = anchor,
                Comments = notes.Select(note => ToComment(note, mergeRequestUrl)).ToArray(),
            });
        }

        return threads;
    }

    /// <summary>
    /// État d'une discussion, déduit de sa note racine.
    /// </summary>
    /// <remarks>
    /// Une note non « résolvable » — un simple commentaire de conversation — n'a pas d'état :
    /// <see cref="CommentThreadStatus.Unknown"/> est alors exact, et évite de la compter comme
    /// une discussion à traiter.
    /// </remarks>
    public static CommentThreadStatus ToThreadStatus(GlNote root)
        => !root.Resolvable ? CommentThreadStatus.Unknown
            : root.Resolved ? CommentThreadStatus.Fixed
            : CommentThreadStatus.Active;

    /// <summary>
    /// Définition de pipeline.
    /// </summary>
    /// <remarks>
    /// GitLab n'a pas de notion de « définition » : un projet porte un unique
    /// <c>.gitlab-ci.yml</c>. Le projet <b>est</b> donc le pipeline, ce qui rend la lecture des
    /// exécutions particulièrement économique — une requête par projet surveillé.
    /// </remarks>
    public static PipelineDefinitionRef ToPipelineDefinition(GlProject dto, string fallbackGroup)
    {
        var path = FirstNonEmpty(dto.PathWithNamespace, $"{fallbackGroup}/{dto.Path}") ?? string.Empty;
        return new PipelineDefinitionRef(path, dto.Id, FirstNonEmpty(dto.Name, dto.Path) ?? path);
    }

    /// <summary>
    /// Vrai si l'intégration continue est accessible sur ce projet.
    /// </summary>
    /// <remarks>
    /// Deux formes du même réglage se croisent selon la version : la plus récente
    /// (<c>builds_access_level</c>) prime, l'ancienne (<c>jobs_enabled</c>) sert de repli, et
    /// leur absence est traitée comme « accessible » — mieux vaut proposer un projet sans
    /// pipeline qu'en cacher un qui en a.
    /// </remarks>
    public static bool HasPipelines(GlProject dto)
    {
        if (!string.IsNullOrEmpty(dto.BuildsAccessLevel))
        {
            return !string.Equals(dto.BuildsAccessLevel, "disabled", StringComparison.OrdinalIgnoreCase);
        }

        return dto.JobsEnabled ?? true;
    }

    /// <summary>Exécution de pipeline.</summary>
    public static PipelineRun ToPipelineRun(GlPipeline dto, PipelineDefinitionRef definition)
    {
        var state = ToRunState(dto.Status);

        return new PipelineRun
        {
            Id = dto.Id,
            Definition = definition,
            RunName = dto.Iid > 0 ? $"#{dto.Iid}" : $"#{dto.Id}",
            State = state,
            Result = ToRunResult(dto.Status),
            Branch = dto.Ref ?? string.Empty,
            RequestedFor = ToUser(dto.User),
            StartedOn = dto.CreatedAt,

            // GitLab ne publie pas de date de fin dans la liste : sur une exécution terminée,
            // la dernière modification en tient lieu.
            FinishedOn = state == PipelineRunState.Completed ? dto.UpdatedAt : null,
            Url = dto.WebUrl ?? string.Empty,
        };
    }

    /// <summary>
    /// Avancement d'une exécution.
    /// </summary>
    /// <remarks>
    /// GitLab n'a qu'un seul champ pour l'avancement et le résultat : les statuts terminaux
    /// (<c>success</c>, <c>failed</c>, <c>canceled</c>, <c>skipped</c>) valent donc
    /// « terminée », et c'est <see cref="ToRunResult"/> qui les départage.
    /// </remarks>
    public static PipelineRunState ToRunState(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "success" or "failed" or "canceled" or "cancelled" or "skipped" => PipelineRunState.Completed,
        "running" => PipelineRunState.InProgress,
        "canceling" or "cancelling" => PipelineRunState.Canceling,
        "created" or "waiting_for_resource" or "preparing" or "pending" or "manual" or "scheduled"
            => PipelineRunState.NotStarted,
        _ => PipelineRunState.Unknown,
    };

    /// <summary>
    /// Résultat d'une exécution terminée.
    /// </summary>
    /// <remarks>
    /// <c>skipped</c> reste inconnu à dessein : ce n'est ni un échec — inutile d'alerter — ni
    /// un succès — annoncer un « retour au vert » après une exécution ignorée serait faux
    /// (SPEC-PIPE-002).
    /// </remarks>
    public static PipelineRunResult ToRunResult(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "success" => PipelineRunResult.Succeeded,
        "failed" => PipelineRunResult.Failed,
        "canceled" or "cancelled" => PipelineRunResult.Canceled,
        _ => PipelineRunResult.Unknown,
    };

    /// <summary>Note, avec son ancre web propre.</summary>
    private static Comment ToComment(GlNote dto, string mergeRequestUrl) => new(
        dto.Id,
        ParentCommentId: null,
        ToUser(dto.Author),
        dto.Body ?? string.Empty,
        dto.CreatedAt ?? default,

        // GitLab marque explicitement ses messages générés : aucune heuristique nécessaire.
        IsSystem: dto.System || (dto.Author?.Bot ?? false),
        IsDeleted: false,
        Url: string.IsNullOrEmpty(mergeRequestUrl)
            ? string.Empty
            : $"{mergeRequestUrl}#note_{dto.Id.ToString(CultureInfo.InvariantCulture)}");

    /// <summary>Partie « groupe » d'un chemin <c>groupe/sous-groupe/projet</c>.</summary>
    private static string? GroupOf(string? pathWithNamespace)
    {
        if (string.IsNullOrEmpty(pathWithNamespace))
        {
            return null;
        }

        var slash = pathWithNamespace.LastIndexOf('/');
        return slash > 0 ? pathWithNamespace[..slash] : null;
    }

    private static string? FirstNonEmpty(params string?[] candidates)
        => candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
}
