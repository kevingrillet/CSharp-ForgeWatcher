using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Application.Links;

/// <summary>
/// Construit les URL web ouvertes au clic sur une notification (SPEC-LINK-*).
/// </summary>
/// <remarks>
/// Une implémentation par forge : les formats diffèrent complètement (SPEC-FORGE-003).
/// Toutes reconstruisent leurs adresses à partir de l'état mémorisé, sans appel réseau —
/// c'est ce qui permet d'ouvrir une pull request ou une exécution de pipeline connues du
/// seul <c>state.json</c>. Quand la forge a fourni l'adresse exacte d'un message, celle-ci
/// est préférée (SPEC-LINK-004).
/// </remarks>
public interface IPullRequestLinkBuilder
{
    /// <summary>URL de la page d'une pull request.</summary>
    string ForPullRequest(RepositoryRef repository, int pullRequestId);

    /// <summary>URL d'une pull request, discussion dépliée et mise en évidence.</summary>
    string ForThread(RepositoryRef repository, int pullRequestId, long threadId);

    /// <summary>URL de la liste des pull requests d'un dépôt.</summary>
    string ForRepositoryPullRequests(RepositoryRef repository);

    /// <summary>URL de la page de résultats d'une exécution de pipeline (SPEC-PIPE-001).</summary>
    /// <param name="projectName">
    /// Espace propriétaire du pipeline, tel que porté par
    /// <see cref="Domain.Pipelines.PipelineDefinitionRef.ProjectName"/>.
    /// </param>
    /// <param name="runId">Identifiant de l'exécution.</param>
    string ForPipelineRun(string projectName, long runId);
}

/// <summary>Outils communs aux générateurs de liens.</summary>
internal static class LinkBuilderHelpers
{
    /// <summary>Normalise une base d'URL : espaces retirés, barre oblique finale ignorée.</summary>
    public static string Root(Func<string> accessor)
        => (accessor() ?? string.Empty).Trim().TrimEnd('/');

    /// <summary>Encode un segment de chemin variable.</summary>
    public static string Escape(string? value) => Uri.EscapeDataString(value ?? string.Empty);
}
