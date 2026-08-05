using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Detection.Rules;

/// <summary>
/// SPEC-EVT-009 — la pull request est complétée, abandonnée, réactivée, ou publiée
/// (sortie de brouillon).
/// </summary>
/// <remarks>
/// Ne concerne que les PR où l'utilisateur est impliqué (auteur, relecteur ou
/// participant) : la complétion d'une PR quelconque d'un dépôt surveillé n'est pas une
/// information utile.
/// </remarks>
public sealed class PullRequestStateChangedRule : IPullRequestEventRule
{
    /// <inheritdoc />
    public string Name => "État de pull request";

    /// <inheritdoc />
    public bool RequiresThreads => false;

    /// <inheritdoc />
    public IEnumerable<PullRequestEvent> Detect(DetectionContext context)
    {
        if (context.Previous is not { } previous)
        {
            yield break;
        }

        if (!context.ViewerIsInvolved)
        {
            yield break;
        }

        var current = context.PullRequest;

        if (previous.Status != current.Status && current.Status != PullRequestStatus.Unknown)
        {
            var message = current.Status switch
            {
                PullRequestStatus.Completed => TextRef.Of(TextKeys.Event.PullRequestCompleted),
                PullRequestStatus.Abandoned => TextRef.Of(TextKeys.Event.PullRequestAbandoned),
                PullRequestStatus.Active => TextRef.Of(TextKeys.Event.PullRequestReactivated),
                _ => TextRef.Of(
                    TextKeys.Event.PullRequestStatusOther,
                    TextRef.Of(current.Status.ToLabelKey())),
            };

            yield return context.CreateEvent(
                NotificationKind.PullRequestStateChanged,
                message,
                dedupKey: $"prstatus|{context.Key}|{current.Status}");
        }

        // Passage de brouillon à publiée : la relecture peut commencer.
        if (previous.IsDraft && !current.IsDraft && current.Status == PullRequestStatus.Active)
        {
            yield return context.CreateEvent(
                NotificationKind.PullRequestStateChanged,
                TextRef.Of(TextKeys.Event.PullRequestDraftPublished),
                dedupKey: $"prdraft|{context.Key}|published");
        }
    }
}
