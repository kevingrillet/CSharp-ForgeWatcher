using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Detection.Rules;

/// <summary>
/// SPEC-EVT-004 à SPEC-EVT-007 — nouveaux commentaires : mention, réponse, commentaire sur
/// ma PR, commentaire sur une PR que je relis.
/// </summary>
/// <remarks>
/// <para>
/// Une seule règle traite les quatre cas, car ils portent sur le **même fait** (« un
/// message est apparu ») et ne diffèrent que par l'intitulé le plus pertinent. Les
/// séparer produirait des notifications en double pour un unique message.
/// </para>
/// <para>
/// Priorité d'intitulé, du plus précis au plus général :
/// mention → réponse à mon commentaire → commentaire sur ma PR → commentaire sur une PR
/// que je relis. Si aucun cas ne s'applique, l'utilisateur n'est pas concerné : silence.
/// </para>
/// <para>
/// Regroupement : plusieurs messages apparus dans la même discussion au même cycle
/// donnent **un** événement (SPEC-EVT-004, règle 2).
/// </para>
/// </remarks>
public sealed class NewCommentRule : IPullRequestEventRule
{
    /// <inheritdoc />
    public string Name => "Nouveau commentaire";

    /// <inheritdoc />
    public bool RequiresThreads => true;

    /// <inheritdoc />
    public IEnumerable<PullRequestEvent> Detect(DetectionContext context)
    {
        // Il faut à la fois avoir lu les discussions et disposer d'une mémoire :
        // sinon impossible de distinguer un message nouveau d'un message existant.
        if (context.Threads is not { } threads || context.Previous is not { } previous)
        {
            yield break;
        }

        foreach (var thread in threads)
        {
            if (thread.IsDeleted)
            {
                continue;
            }

            var knownThread = previous.FindThread(thread.Id);

            var fresh = thread.NotifiableComments
                .Where(comment => knownThread is null || !knownThread.Knows(comment.Id))
                .Where(comment => !context.ShouldIgnoreActor(comment.Author))
                .ToList();

            if (fresh.Count == 0)
            {
                continue;
            }

            // Participation de l'utilisateur : mémorisée, ou visible dans un message
            // antérieur de la discussion.
            var viewerParticipates = knownThread?.ViewerParticipates == true
                || thread.Comments.Any(c => context.IsViewer(c.Author) && !fresh.Contains(c));

            var mentionsViewer = fresh.Any(c => c.Mentions(context.ViewerId));

            var kind = mentionsViewer ? NotificationKind.MentionedInComment
                : viewerParticipates ? NotificationKind.ReplyToMyComment
                : context.ViewerIsAuthor ? NotificationKind.CommentOnMyPullRequest
                : context.ViewerIsReviewer ? NotificationKind.CommentOnReviewedPullRequest
                : (NotificationKind?)null;

            // Ni auteur, ni relecteur, ni participant : la PR ne concerne pas l'utilisateur.
            if (kind is not { } eventKind)
            {
                continue;
            }

            var last = fresh[^1];

            // Les deux parties facultatives sont passées comme fragments : chaque langue les
            // place où elle veut dans la phrase (SPEC-UI-LANG-002).
            var others = fresh.Count > 1
                ? TextRef.Of(TextKeys.Event.CommentMore, fresh.Count - 1)
                : TextRef.Empty;

            var file = string.IsNullOrEmpty(thread.FilePath)
                ? TextRef.Empty
                : TextRef.Of(TextKeys.Event.CommentFile, TrimPath(thread.FilePath!));

            yield return context.CreateEvent(
                eventKind,
                TextRef.Of(
                    TextKeys.Event.Comment,
                    last.Author.SafeDisplayName,
                    file,
                    last.ToExcerpt(),
                    others),
                occurredOn: last.PublishedOn,
                actorName: last.Author.SafeDisplayName,
                threadId: thread.Id,
                dedupKey: $"comment|{context.Key}|{thread.Id}|{last.Id}",
                // Quand la forge donne l'ancre du message, elle vaut mieux que la nôtre :
                // elle désigne le message précis, pas seulement sa discussion
                // (SPEC-LINK-004).
                url: last.Url);
        }
    }

    /// <summary>Ne garde que le nom du fichier commenté, pour tenir dans un toast.</summary>
    private static string TrimPath(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash >= 0 && slash < path.Length - 1 ? path[(slash + 1)..] : path.TrimStart('/');
    }
}
