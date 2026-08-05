using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Detection.Rules;

/// <summary>
/// SPEC-EVT-008 — une discussion qui concerne l'utilisateur est résolue ou réactivée.
/// </summary>
/// <remarks>
/// Azure DevOps n'indique pas qui a changé l'état d'une discussion : l'événement décrit
/// donc le fait, sans acteur. Restreint aux discussions où l'utilisateur a écrit et aux
/// PR dont il est l'auteur, pour éviter de suivre les allers-retours de tout le monde.
/// </remarks>
public sealed class ThreadStatusChangedRule : IPullRequestEventRule
{
    /// <inheritdoc />
    public string Name => "État de discussion";

    /// <inheritdoc />
    public bool RequiresThreads => true;

    /// <inheritdoc />
    public IEnumerable<PullRequestEvent> Detect(DetectionContext context)
    {
        if (context.Threads is not { } threads || context.Previous is not { } previous)
        {
            yield break;
        }

        foreach (var thread in threads)
        {
            if (thread.IsDeleted || thread.IsSystemOnly)
            {
                continue;
            }

            var knownThread = previous.FindThread(thread.Id);
            if (knownThread is null || knownThread.Status == thread.Status)
            {
                continue;
            }

            // État non exposé par l'API : aucune information exploitable.
            if (thread.Status == CommentThreadStatus.Unknown)
            {
                continue;
            }

            if (!knownThread.ViewerParticipates && !context.ViewerIsAuthor)
            {
                continue;
            }

            var label = TextRef.Of(thread.Status.ToLabelKey());
            var message = thread.Status.IsResolved()
                ? TextRef.Of(TextKeys.Event.ThreadResolved, label)
                : TextRef.Of(TextKeys.Event.ThreadReactivated, label);

            var firstComment = thread.NotifiableComments.FirstOrDefault();
            if (firstComment is not null)
            {
                message = TextRef.Of(TextKeys.Event.ThreadWithExcerpt, message, firstComment.ToExcerpt(80));
            }

            yield return context.CreateEvent(
                NotificationKind.ThreadStatusChanged,
                message,
                occurredOn: thread.NotifiableComments.LastOrDefault()?.PublishedOn,
                threadId: thread.Id,
                dedupKey: $"threadstatus|{context.Key}|{thread.Id}|{thread.Status}",
                url: thread.Url);
        }
    }
}
