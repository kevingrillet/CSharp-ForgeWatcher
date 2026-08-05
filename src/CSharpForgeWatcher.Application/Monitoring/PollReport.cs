using CSharpForgeWatcher.Application.Text;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.Pipelines;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Monitoring;

/// <summary>Issue d'un cycle de sondage.</summary>
public enum PollStatus
{
    /// <summary>Configuration incomplète : aucun appel n'a été tenté (SPEC-CFG-003).</summary>
    NotConfigured,

    /// <summary>Cycle complet, sans incident.</summary>
    Success,

    /// <summary>Cycle abouti, mais au moins un dépôt n'a pas pu être lu (SPEC-POLL-002).</summary>
    PartialFailure,

    /// <summary>Cycle impossible : authentification refusée, organisation injoignable…</summary>
    Failure,

    /// <summary>Un cycle était déjà en cours : celui-ci a été ignoré.</summary>
    Skipped,
}

/// <summary>
/// Vue d'une pull request suivie, telle qu'affichée dans le menu de la zone de
/// notification. Contient tout le nécessaire à l'affichage, rien de plus.
/// </summary>
public sealed record PullRequestView
{
    /// <summary>Clé de la PR.</summary>
    public required PullRequestKey Key { get; init; }

    /// <summary>Numéro de la PR.</summary>
    public required int Id { get; init; }

    /// <summary>Titre.</summary>
    public required string Title { get; init; }

    /// <summary>Dépôt.</summary>
    public required RepositoryRef Repository { get; init; }

    /// <summary>Auteur.</summary>
    public required string AuthorName { get; init; }

    /// <summary>URL de la PR.</summary>
    public required string Url { get; init; }

    /// <summary>Vrai si l'utilisateur est l'auteur.</summary>
    public bool IsMine { get; init; }

    /// <summary>Vrai si l'utilisateur est relecteur.</summary>
    public bool ViewerIsReviewer { get; init; }

    /// <summary>Vote de l'utilisateur.</summary>
    public ReviewerVote ViewerVote { get; init; } = ReviewerVote.NoVote;

    /// <summary>Nombre de discussions encore actives.</summary>
    public int UnresolvedThreadCount { get; init; }

    /// <summary>Vrai si la PR est en brouillon.</summary>
    public bool IsDraft { get; init; }

    /// <summary>Date de création.</summary>
    public DateTimeOffset CreatedOn { get; init; }

    /// <summary>Libellé du compte d'origine ; vide quand un seul compte est surveillé.</summary>
    public string AccountLabel { get; init; } = string.Empty;

    /// <summary>Libellé « !1234 — Titre » pour le menu.</summary>
    public string DisplayLabel => $"!{Id} — {Title}";

    /// <summary>Détail « compte • auteur • 2 discussions ouvertes • brouillon ».</summary>
    /// <param name="catalogue">Catalogue de la langue courante (SPEC-UI-LANG-002).</param>
    public string DisplayDetail(TextCatalogue catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);

        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(AccountLabel))
            {
                parts.Add(AccountLabel);
            }

            parts.Add(AuthorName);

            if (UnresolvedThreadCount > 0)
            {
                parts.Add(catalogue.Resolve(
                    TextRef.Of(TextKeys.Poll.ViewUnresolvedThreads, UnresolvedThreadCount)));
            }

            if (ViewerIsReviewer)
            {
                parts.Add(catalogue.Resolve(TextRef.Of(
                    TextKeys.Poll.ViewYourVote,
                    TextRef.Of(ViewerVote.ToLabelKey()))));
            }

            if (IsDraft)
            {
                parts.Add(catalogue.Get(TextKeys.Poll.ViewDraft));
            }

            return string.Join(" • ", parts);
        }
    }
}

/// <summary>
/// Vue d'un pipeline suivi, telle qu'affichée dans le menu de la zone de notification.
/// </summary>
public sealed record PipelineView
{
    /// <summary>Définition surveillée.</summary>
    public required PipelineDefinitionRef Definition { get; init; }

    /// <summary>URL de la dernière exécution terminée.</summary>
    public required string Url { get; init; }

    /// <summary>Numéro de la dernière exécution terminée.</summary>
    public string RunName { get; init; } = string.Empty;

    /// <summary>Résultat de la dernière exécution terminée.</summary>
    public PipelineRunResult Result { get; init; } = PipelineRunResult.Unknown;

    /// <summary>Fin de cette exécution.</summary>
    public DateTimeOffset? FinishedOn { get; init; }

    /// <summary>Libellé du compte d'origine ; vide quand un seul compte est surveillé.</summary>
    public string AccountLabel { get; init; } = string.Empty;

    /// <summary>Vrai si le pipeline est actuellement en échec.</summary>
    public bool IsFailing => Result.IsFailure();

    /// <summary>Libellé affiché dans le menu.</summary>
    public string DisplayLabel => $"{(IsFailing ? "✕ " : Result == PipelineRunResult.Succeeded ? "✓ " : string.Empty)}{Definition.Name}";

    /// <summary>Détail « compte • succès • 20260804.3 • il y a 12 min ».</summary>
    /// <param name="catalogue">Catalogue de la langue courante (SPEC-UI-LANG-002).</param>
    public string DisplayDetail(TextCatalogue catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);

        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(AccountLabel))
            {
                parts.Add(AccountLabel);
            }

            parts.Add(catalogue.Get(Result.ToLabelKey()));

            if (!string.IsNullOrEmpty(RunName))
            {
                parts.Add(RunName);
            }

            if (FinishedOn is { } finished)
            {
                parts.Add(finished.ToLocalTime().ToString("dd/MM HH:mm"));
            }

            return string.Join(" • ", parts);
        }
    }
}

/// <summary>
/// Compte rendu d'un cycle : ce qui a été détecté, ce qui est suivi, ce qui a échoué.
/// </summary>
/// <remarks>
/// Objet de retour explicite plutôt qu'exceptions ou variables partagées : l'UI n'a qu'à
/// afficher ce compte rendu, sans rien savoir du déroulement du cycle.
/// </remarks>
public sealed record PollReport
{
    /// <summary>Issue du cycle.</summary>
    public required PollStatus Status { get; init; }

    /// <summary>Fin du cycle.</summary>
    public required DateTimeOffset CompletedOn { get; init; }

    /// <summary>Événements retenus après filtrage par les préférences.</summary>
    public IReadOnlyList<INotifiableEvent> Events { get; init; } = Array.Empty<INotifiableEvent>();

    /// <summary>Pull requests actives des dépôts surveillés.</summary>
    public IReadOnlyList<PullRequestView> PullRequests { get; init; } = Array.Empty<PullRequestView>();

    /// <summary>Pipelines surveillés et leur dernier résultat connu.</summary>
    public IReadOnlyList<PipelineView> Pipelines { get; init; } = Array.Empty<PipelineView>();

    /// <summary>Incidents non bloquants (un dépôt inaccessible, par exemple).</summary>
    public IReadOnlyList<TextRef> Warnings { get; init; } = Array.Empty<TextRef>();

    /// <summary>Message d'erreur bloquante, le cas échéant.</summary>
    public TextRef? ErrorMessage { get; init; }

    /// <summary>Nom de l'utilisateur authentifié.</summary>
    public string? ViewerName { get; init; }

    /// <summary>Vrai s'il s'agissait du cycle d'amorçage (aucune notification, SPEC-POLL-001).</summary>
    public bool WasSeeding { get; init; }

    /// <summary>Vrai si le cycle signale un problème.</summary>
    public bool HasProblem => Status is PollStatus.Failure or PollStatus.PartialFailure;

    /// <summary>Cycle impossible faute de configuration.</summary>
    public static PollReport NotConfigured(DateTimeOffset completedOn, TextRef? message = null) => new()
    {
        Status = PollStatus.NotConfigured,
        CompletedOn = completedOn,
        ErrorMessage = message,
    };

    /// <summary>Cycle en échec.</summary>
    public static PollReport Failed(
        DateTimeOffset completedOn,
        TextRef message,
        IReadOnlyList<TextRef>? warnings = null) => new()
        {
            Status = PollStatus.Failure,
            CompletedOn = completedOn,
            ErrorMessage = message,
            Warnings = warnings ?? Array.Empty<TextRef>(),
        };

    /// <summary>Cycle ignoré : un autre était en cours.</summary>
    public static PollReport Skipped(DateTimeOffset completedOn) => new()
    {
        Status = PollStatus.Skipped,
        CompletedOn = completedOn,
    };

    /// <summary>Nombre de pipelines actuellement en échec.</summary>
    public int FailingPipelineCount => Pipelines.Count(pipeline => pipeline.IsFailing);

    /// <summary>
    /// Ligne d'état affichée en infobulle de la zone de notification.
    /// </summary>
    /// <param name="catalogue">
    /// Catalogue de la langue courante : le rapport sait ce qu'il a à dire, pas dans quelle
    /// langue le dire (SPEC-UI-LANG-002).
    /// </param>
    public string ToStatusLine(TextCatalogue catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);

        return Status switch
        {
            PollStatus.NotConfigured => catalogue.Get(TextKeys.Poll.NotConfigured),
            PollStatus.Failure => catalogue.Resolve(TextRef.Of(TextKeys.Poll.Failure, ErrorMessage)),
            PollStatus.PartialFailure => catalogue.Resolve(
                TextRef.Of(TextKeys.Poll.PartialFailure, Followed(catalogue), Warnings.Count)),
            PollStatus.Skipped => catalogue.Get(TextKeys.Poll.Skipped),
            _ => Followed(catalogue),
        };
    }

    /// <summary>Résumé « 12 PR suivie(s) · 3 pipeline(s) dont 1 en échec ».</summary>
    private string Followed(TextCatalogue catalogue)
    {
        var parts = new List<string>
        {
            catalogue.Resolve(TextRef.Of(TextKeys.Poll.FollowedPullRequests, PullRequests.Count)),
        };

        if (Pipelines.Count > 0)
        {
            var failing = FailingPipelineCount > 0
                ? TextRef.Of(TextKeys.Poll.FollowedPipelinesFailing, FailingPipelineCount)
                : TextRef.Empty;

            parts.Add(catalogue.Resolve(
                TextRef.Of(TextKeys.Poll.FollowedPipelines, Pipelines.Count, failing)));
        }

        return string.Join(" · ", parts);
    }
}
