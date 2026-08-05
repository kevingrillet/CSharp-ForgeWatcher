namespace CSharpForgeWatcher.Domain.PullRequests;

/// <summary>
/// Ce qui a été effectivement observé pour une pull request lors d'un cycle.
/// </summary>
/// <remarks>
/// La distinction porte sur <see cref="Threads"/> : <c>null</c> signifie
/// « les discussions n'ont pas été lues à ce cycle » (portée <c>InvolvedOnly</c>,
/// cf. SPEC-POLL-003), et non « il n'y a pas de discussion ». Cette nuance est
/// essentielle : sans elle, l'état mémorisé des discussions serait effacé à chaque
/// cycle où on ne les relit pas, et tous les commentaires seraient re-notifiés
/// à la lecture suivante.
/// </remarks>
/// <param name="PullRequest">Métadonnées de la PR (titre, auteur, votes, état).</param>
/// <param name="Threads">Discussions lues, ou <c>null</c> si elles n'ont pas été lues.</param>
public sealed record PullRequestObservation(
    PullRequest PullRequest,
    IReadOnlyList<CommentThread>? Threads = null)
{
    /// <summary>Clé de la PR observée.</summary>
    public PullRequestKey Key => PullRequest.Key;

    /// <summary>Vrai si les discussions ont été lues lors de ce cycle.</summary>
    public bool ThreadsWereRead => Threads is not null;
}
