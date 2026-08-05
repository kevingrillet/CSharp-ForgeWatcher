using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Detection.Rules;

/// <summary>
/// SPEC-EVT-002 — l'utilisateur est ajouté comme relecteur d'une pull request.
/// </summary>
/// <remarks>
/// Se déclenche aussi à la découverte d'une PR où l'utilisateur est déjà relecteur : du
/// point de vue de l'utilisateur, c'est le même fait — « on attend ma relecture » — et
/// c'est l'information la plus actionnable qu'on puisse lui donner.
/// </remarks>
public sealed class ReviewerAssignedRule : IPullRequestEventRule
{
    /// <inheritdoc />
    public string Name => "Ajout comme relecteur";

    /// <inheritdoc />
    public bool RequiresThreads => false;

    /// <inheritdoc />
    public IEnumerable<PullRequestEvent> Detect(DetectionContext context)
    {
        if (!context.ViewerIsReviewer)
        {
            yield break;
        }

        if (context.PullRequest.Status != PullRequestStatus.Active)
        {
            yield break;
        }

        // Déjà relecteur au cycle précédent : rien de nouveau.
        if (context.Previous is { ViewerIsReviewer: true })
        {
            yield break;
        }

        // L'auteur d'une PR y est parfois listé comme relecteur : ce n'est pas une demande.
        if (context.ViewerIsAuthor)
        {
            yield break;
        }

        var author = context.PullRequest.Author.SafeDisplayName;
        var required = context.PullRequest.FindReviewer(context.ViewerId)?.IsRequired == true
            ? TextRef.Of(TextKeys.Event.ReviewerRequired)
            : TextRef.Empty;

        yield return context.CreateEvent(
            NotificationKind.ReviewerAssigned,
            TextRef.Of(TextKeys.Event.ReviewerAssigned, author, required),
            occurredOn: context.PullRequest.CreatedOn,
            actorName: author,
            dedupKey: $"reviewer|{context.Key}");
    }
}
