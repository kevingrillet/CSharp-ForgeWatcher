using CSharpForgeWatcher.Domain.PullRequests;

namespace CSharpForgeWatcher.Application.Links;

/// <summary>
/// Implémentation pour l'interface web de GitLab, gitlab.com comme instance auto-hébergée
/// (SPEC-FORGE-003).
/// </summary>
/// <remarks>
/// <para>
/// Correspondance avec le modèle du domaine : le « projet » d'un <see cref="RepositoryRef"/>
/// est le <b>groupe</b> GitLab — sous-groupes compris, donc un chemin pouvant contenir des
/// barres obliques —, et l'espace d'un pipeline est le chemin complet du projet.
/// </para>
/// <para>
/// Le préfixe <c>/-/</c> qui précède les sections de GitLab n'est pas décoratif : il sépare le
/// chemin du projet du reste de l'URL, et c'est ce qui permet à un chemin de groupe imbriqué
/// de rester non ambigu.
/// </para>
/// </remarks>
public sealed class GitLabLinkBuilder : IPullRequestLinkBuilder
{
    private readonly Func<string> _serverUrlAccessor;

    /// <summary>Construit le générateur de liens.</summary>
    /// <param name="serverUrlAccessor">Retourne l'URL du serveur GitLab courante.</param>
    public GitLabLinkBuilder(Func<string> serverUrlAccessor)
        => _serverUrlAccessor = serverUrlAccessor ?? throw new ArgumentNullException(nameof(serverUrlAccessor));

    /// <summary>Construit un générateur pour un serveur fixe (pratique en test).</summary>
    public static GitLabLinkBuilder For(string serverUrl) => new(() => serverUrl);

    /// <inheritdoc />
    public string ForPullRequest(RepositoryRef repository, int pullRequestId)
        => $"{RepositoryRoot(repository)}/-/merge_requests/{pullRequestId}";

    /// <inheritdoc />
    /// <remarks>
    /// L'ancre désigne une <b>note</b> : l'identifiant de discussion de GitLab étant une
    /// empreinte textuelle, c'est celui de la première note du fil qui fait office
    /// d'identifiant de discussion (cf. <c>GitLabMapper</c>).
    /// </remarks>
    public string ForThread(RepositoryRef repository, int pullRequestId, long threadId)
        => threadId > 0
            ? $"{ForPullRequest(repository, pullRequestId)}#note_{threadId}"
            : ForPullRequest(repository, pullRequestId);

    /// <inheritdoc />
    public string ForRepositoryPullRequests(RepositoryRef repository)
        => $"{RepositoryRoot(repository)}/-/merge_requests";

    /// <inheritdoc />
    /// <remarks>
    /// <paramref name="projectName"/> est le chemin complet du projet, sous-groupes compris.
    /// </remarks>
    public string ForPipelineRun(string projectName, long runId)
    {
        var path = EscapePath(projectName);

        return string.IsNullOrEmpty(path)
            ? Server()
            : $"{Server()}/{path}/-/pipelines/{runId}";
    }

    /// <summary>
    /// Encode un chemin en préservant ses barres obliques.
    /// </summary>
    /// <remarks>
    /// Un groupe GitLab peut être imbriqué (<c>equipe/backoffice</c>) : encoder le chemin d'un
    /// seul bloc transformerait ses séparateurs en <c>%2F</c> et produirait une adresse
    /// invalide. Chaque segment est donc encodé séparément.
    /// </remarks>
    internal static string EscapePath(string? path)
        => string.Join(
            '/',
            (path ?? string.Empty)
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(LinkBuilderHelpers.Escape));

    /// <summary>Racine « serveur / groupe / projet ».</summary>
    private string RepositoryRoot(RepositoryRef repository)
    {
        var group = EscapePath(repository.ProjectName);
        var project = EscapePath(repository.RepositoryName);
        var path = string.IsNullOrEmpty(group) ? project : $"{group}/{project}";

        return string.IsNullOrEmpty(path) ? Server() : $"{Server()}/{path}";
    }

    private string Server() => ServerUrl.Origin(_serverUrlAccessor());
}
