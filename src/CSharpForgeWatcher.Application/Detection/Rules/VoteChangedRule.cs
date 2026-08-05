using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Domain.PullRequests;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Detection.Rules;

/// <summary>
/// SPEC-EVT-003 — un relecteur vote sur une pull request de l'utilisateur.
/// </summary>
/// <remarks>
/// Restreinte aux PR dont l'utilisateur est l'auteur : un vote sur la PR d'un collègue
/// n'appelle aucune action de sa part, et notifier tous les votes de tous les dépôts
/// surveillés rendrait l'outil inutilisable.
/// </remarks>
public sealed class VoteChangedRule : IPullRequestEventRule
{
    /// <inheritdoc />
    public string Name => "Vote de relecteur";

    /// <inheritdoc />
    public bool RequiresThreads => false;

    /// <inheritdoc />
    public IEnumerable<PullRequestEvent> Detect(DetectionContext context)
    {
        // Sans mémoire du cycle précédent, un vote existant n'est pas un vote nouveau.
        if (context.Previous is not { } previous)
        {
            yield break;
        }

        if (!context.ViewerIsAuthor)
        {
            yield break;
        }

        foreach (var reviewer in context.PullRequest.Reviewers)
        {
            if (string.IsNullOrEmpty(reviewer.User.Id))
            {
                continue;
            }

            var wasKnown = previous.ReviewerVotes.ContainsKey(reviewer.User.Id);
            var previousVote = previous.VoteOf(reviewer.User.Id);

            if (previousVote == reviewer.Vote)
            {
                continue;
            }

            // Relecteur tout juste ajouté et n'ayant pas encore voté : ce n'est pas un vote.
            if (!wasKnown && reviewer.Vote == ReviewerVote.NoVote)
            {
                continue;
            }

            if (context.ShouldIgnoreActor(reviewer.User))
            {
                continue;
            }

            var who = reviewer.User.SafeDisplayName;

            yield return context.CreateEvent(
                NotificationKind.VoteChanged,
                TextRef.Of(TextKeys.Event.Vote, who, TextRef.Of(reviewer.Vote.ToActionKey())),
                actorName: who,
                // La valeur du vote fait partie de la clé : deux votes successifs
                // différents du même relecteur sont deux faits distincts.
                dedupKey: $"vote|{context.Key}|{reviewer.User.Id}|{(int)reviewer.Vote}");
        }
    }
}
