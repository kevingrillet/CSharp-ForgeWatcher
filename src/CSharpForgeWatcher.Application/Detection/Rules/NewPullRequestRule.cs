using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Detection.Rules;

/// <summary>
/// SPEC-EVT-001 — une pull request active apparaît dans un dépôt surveillé.
/// </summary>
/// <remarks>
/// La règle ne se déclenche que sur une PR **inconnue** de l'état. Le cycle d'amorçage
/// (SPEC-POLL-001) est traité en amont par le monitor : au premier cycle, les règles ne
/// sont pas appelées, sinon tout l'historique actif serait annoncé d'un coup.
/// </remarks>
public sealed class NewPullRequestRule : IPullRequestEventRule
{
    /// <inheritdoc />
    public string Name => "Nouvelle pull request";

    /// <inheritdoc />
    public bool RequiresThreads => false;

    /// <inheritdoc />
    public IEnumerable<PullRequestEvent> Detect(DetectionContext context)
    {
        if (!context.IsFirstSight)
        {
            yield break;
        }

        // Une PR découverte déjà close n'est pas une nouveauté à annoncer.
        if (context.PullRequest.Status != PullRequestStatus.Active)
        {
            yield break;
        }

        if (context.ShouldIgnoreActor(context.PullRequest.Author))
        {
            yield break;
        }

        // SPEC-EVT-001, règle 2 : si l'utilisateur est déjà relecteur, l'événement
        // « Vous êtes relecteur » est plus actionnable ; on lui laisse la main.
        if (context.ViewerIsReviewer)
        {
            yield break;
        }

        var author = context.PullRequest.Author.SafeDisplayName;
        var target = string.IsNullOrEmpty(context.PullRequest.TargetBranch)
            ? TextRef.Empty
            : TextRef.Of(TextKeys.Event.PullRequestTarget, context.PullRequest.TargetBranch);

        var draft = context.PullRequest.IsDraft
            ? TextRef.Of(TextKeys.Event.PullRequestDraft)
            : TextRef.Empty;

        yield return context.CreateEvent(
            NotificationKind.PullRequestCreated,
            TextRef.Of(TextKeys.Event.PullRequestCreated, author, target, draft),
            occurredOn: context.PullRequest.CreatedOn,
            actorName: author,
            dedupKey: $"created|{context.Key}");
    }
}
