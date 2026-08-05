using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Application.Links;

/// <summary>
/// Implémentation pour l'interface web d'Azure DevOps (SPEC-FORGE-003).
/// </summary>
/// <remarks>
/// L'URL d'organisation est fournie par un accesseur et non par une valeur figée :
/// l'utilisateur peut la changer dans la fenêtre de configuration sans redémarrage
/// (SPEC-CFG-004), et cette classe reste sans état.
/// </remarks>
public sealed class AzureDevOpsLinkBuilder : IPullRequestLinkBuilder
{
    private readonly Func<string> _organizationUrlAccessor;

    /// <summary>Construit le générateur de liens.</summary>
    /// <param name="organizationUrlAccessor">Retourne l'URL d'organisation courante.</param>
    public AzureDevOpsLinkBuilder(Func<string> organizationUrlAccessor)
        => _organizationUrlAccessor = organizationUrlAccessor
            ?? throw new ArgumentNullException(nameof(organizationUrlAccessor));

    /// <summary>Construit un générateur pour une organisation fixe (pratique en test).</summary>
    public static AzureDevOpsLinkBuilder For(string organizationUrl) => new(() => organizationUrl);

    /// <inheritdoc />
    public string ForPullRequest(RepositoryRef repository, int pullRequestId)
        => $"{RepositoryRoot(repository)}/pullrequest/{pullRequestId}";

    /// <inheritdoc />
    public string ForThread(RepositoryRef repository, int pullRequestId, long threadId)
        => $"{ForPullRequest(repository, pullRequestId)}?discussionId={threadId}";

    /// <inheritdoc />
    public string ForRepositoryPullRequests(RepositoryRef repository)
        => $"{RepositoryRoot(repository)}/pullrequests";

    /// <inheritdoc />
    /// <remarks>
    /// URL déduite de l'identifiant plutôt que reprise du champ <c>_links</c> de l'API :
    /// elle reste construisible depuis l'état mémorisé, sans nouvel appel réseau.
    /// </remarks>
    public string ForPipelineRun(string projectName, long runId)
    {
        var organization = LinkBuilderHelpers.Root(_organizationUrlAccessor);
        var project = LinkBuilderHelpers.Escape(projectName);
        return $"{organization}/{project}/_build/results?buildId={runId}&view=results";
    }

    /// <summary>
    /// Racine « organisation / projet / _git / dépôt ».
    /// Les noms sont encodés : un projet nommé « Mon Projet » donne « Mon%20Projet ».
    /// </summary>
    private string RepositoryRoot(RepositoryRef repository)
    {
        var organization = LinkBuilderHelpers.Root(_organizationUrlAccessor);
        var project = LinkBuilderHelpers.Escape(repository.ProjectName);
        var name = LinkBuilderHelpers.Escape(repository.RepositoryName);
        return $"{organization}/{project}/_git/{name}";
    }
}
