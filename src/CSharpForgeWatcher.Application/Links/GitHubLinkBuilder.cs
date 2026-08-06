using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Application.Links;

/// <summary>
/// Implémentation pour l'interface web de GitHub, github.com comme GitHub Enterprise Server
/// (SPEC-FORGE-003).
/// </summary>
/// <remarks>
/// <para>
/// Correspondance avec le modèle du domaine : le « projet » d'un
/// <see cref="RepositoryRef"/> est le <b>propriétaire</b> GitHub (compte ou organisation),
/// et l'espace d'un pipeline est le couple <c>propriétaire/dépôt</c>, les workflows
/// appartenant à un dépôt (SPEC-FORGE-004).
/// </para>
/// <para>
/// Comme pour Azure DevOps, l'URL du serveur est lue à chaque appel : la changer dans la
/// fenêtre de configuration prend effet sans redémarrage (SPEC-CFG-004).
/// </para>
/// </remarks>
public sealed class GitHubLinkBuilder : IPullRequestLinkBuilder
{
    private readonly Func<string> _serverUrlAccessor;

    /// <summary>Construit le générateur de liens.</summary>
    /// <param name="serverUrlAccessor">Retourne l'URL du serveur GitHub courante.</param>
    public GitHubLinkBuilder(Func<string> serverUrlAccessor)
        => _serverUrlAccessor = serverUrlAccessor ?? throw new ArgumentNullException(nameof(serverUrlAccessor));

    /// <summary>Construit un générateur pour un serveur fixe (pratique en test).</summary>
    public static GitHubLinkBuilder For(string serverUrl) => new(() => serverUrl);

    /// <inheritdoc />
    public string ForPullRequest(RepositoryRef repository, int pullRequestId)
        => $"{RepositoryRoot(repository)}/pull/{pullRequestId}";

    /// <inheritdoc />
    /// <remarks>
    /// Le fragment <c>#discussion_r</c> désigne un commentaire de ligne. Les discussions
    /// synthétiques — la conversation d'une pull request, que GitHub ne structure pas en
    /// fils — portent un identifiant négatif : il n'y a alors rien à ancrer, et l'adresse
    /// exacte de chaque message est de toute façon fournie par l'API (SPEC-LINK-004).
    /// </remarks>
    public string ForThread(RepositoryRef repository, int pullRequestId, long threadId)
        => threadId > 0
            ? $"{ForPullRequest(repository, pullRequestId)}#discussion_r{threadId}"
            : ForPullRequest(repository, pullRequestId);

    /// <inheritdoc />
    public string ForRepositoryPullRequests(RepositoryRef repository)
        => $"{RepositoryRoot(repository)}/pulls";

    /// <inheritdoc />
    /// <remarks>
    /// <paramref name="projectName"/> vaut <c>propriétaire/dépôt</c>. Une valeur sans barre
    /// oblique vient d'une configuration héritée d'une autre forge : on retourne alors la
    /// page du serveur, plutôt qu'une adresse fabriquée qui mènerait à une erreur 404.
    /// </remarks>
    public string ForPipelineRun(string projectName, long runId)
    {
        var server = Server();
        var (owner, repository) = SplitOwnerAndRepository(projectName);

        return string.IsNullOrEmpty(repository)
            ? server
            : $"{server}/{owner}/{repository}/actions/runs/{runId}";
    }

    /// <summary>Sépare un espace de pipeline <c>propriétaire/dépôt</c>.</summary>
    /// <returns>Le dépôt est vide si la valeur n'a pas la forme attendue.</returns>
    internal static (string Owner, string Repository) SplitOwnerAndRepository(string? projectName)
    {
        var value = (projectName ?? string.Empty).Trim().Trim('/');
        var separator = value.IndexOf('/', StringComparison.Ordinal);

        return separator <= 0 || separator == value.Length - 1
            ? (LinkBuilderHelpers.Escape(value), string.Empty)
            : (LinkBuilderHelpers.Escape(value[..separator]), LinkBuilderHelpers.Escape(value[(separator + 1)..]));
    }

    /// <summary>Racine « serveur / propriétaire / dépôt ».</summary>
    private string RepositoryRoot(RepositoryRef repository)
        => $"{Server()}/{LinkBuilderHelpers.Escape(repository.ProjectName)}"
           + $"/{LinkBuilderHelpers.Escape(repository.RepositoryName)}";

    private string Server() => ServerUrl.Origin(_serverUrlAccessor());
}
