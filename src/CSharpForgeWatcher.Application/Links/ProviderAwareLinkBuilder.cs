using CSharpForgeWatcher.Application.Configuration;
using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Application.Links;

/// <summary>
/// Aiguille chaque demande d'URL vers le générateur de la forge configurée
/// (patron Strategy, SPEC-FORGE-003).
/// </summary>
/// <remarks>
/// <para>
/// C'est l'un des deux seuls points de la solution qui dépendent du fournisseur — l'autre
/// est la fabrique de passerelles. Le reste du code ne voit qu'un
/// <see cref="IPullRequestLinkBuilder"/>.
/// </para>
/// <para>
/// La sélection est refaite à <b>chaque appel</b>, jamais figée à la construction :
/// changer de forge dans la fenêtre de configuration prend effet immédiatement
/// (SPEC-CFG-004).
/// </para>
/// </remarks>
public sealed class ProviderAwareLinkBuilder : IPullRequestLinkBuilder
{
    private readonly Func<SourceControlProvider> _providerAccessor;
    private readonly IPullRequestLinkBuilder _azureDevOps;
    private readonly IPullRequestLinkBuilder _gitHub;
    private readonly IPullRequestLinkBuilder _gitLab;

    /// <summary>Construit l'aiguilleur.</summary>
    /// <param name="providerAccessor">Retourne le fournisseur configuré.</param>
    /// <param name="serverUrlAccessor">Retourne l'URL de la forge configurée.</param>
    public ProviderAwareLinkBuilder(Func<SourceControlProvider> providerAccessor, Func<string> serverUrlAccessor)
    {
        ArgumentNullException.ThrowIfNull(serverUrlAccessor);

        _providerAccessor = providerAccessor ?? throw new ArgumentNullException(nameof(providerAccessor));
        _azureDevOps = new AzureDevOpsLinkBuilder(serverUrlAccessor);
        _gitHub = new GitHubLinkBuilder(serverUrlAccessor);
        _gitLab = new GitLabLinkBuilder(serverUrlAccessor);
    }

    /// <inheritdoc />
    public string ForPullRequest(RepositoryRef repository, int pullRequestId)
        => Current().ForPullRequest(repository, pullRequestId);

    /// <inheritdoc />
    public string ForThread(RepositoryRef repository, int pullRequestId, long threadId)
        => Current().ForThread(repository, pullRequestId, threadId);

    /// <inheritdoc />
    public string ForRepositoryPullRequests(RepositoryRef repository)
        => Current().ForRepositoryPullRequests(repository);

    /// <inheritdoc />
    public string ForPipelineRun(string projectName, long runId)
        => Current().ForPipelineRun(projectName, runId);

    /// <summary>
    /// Générateur correspondant au fournisseur courant.
    /// </summary>
    /// <remarks>
    /// Aiguillage <b>exhaustif</b> : une forge non implémentée lève un message explicite
    /// plutôt que de retomber en silence sur Azure DevOps, ce qui produirait des liens
    /// plausibles menant nulle part. La validation de configuration refuse de toute façon
    /// ces fournisseurs en amont (SPEC-FORGE-002) : ce cas ne devrait jamais être atteint.
    /// </remarks>
    private IPullRequestLinkBuilder Current()
    {
        var provider = _providerAccessor();

        return provider switch
        {
            SourceControlProvider.AzureDevOps => _azureDevOps,
            SourceControlProvider.GitHub => _gitHub,
            SourceControlProvider.GitLab => _gitLab,
            _ => throw new NotSupportedException(
                $"Aucun générateur de liens pour le fournisseur {provider} : "
                + "cette forge n'est pas encore implémentée."),
        };
    }
}
