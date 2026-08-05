namespace CSharpForgeWatcher.Domain.PullRequests;

/// <summary>
/// Dépôt Git Azure DevOps, situé dans son projet d'équipe.
/// </summary>
/// <remarks>
/// L'identité d'un dépôt est son <paramref name="RepositoryId"/> (GUID) : c'est lui qui
/// est mémorisé dans la configuration et dans l'état, de sorte qu'un renommage de dépôt
/// ou de projet ne casse pas la surveillance (SPEC-CFG-002).
/// Le nom du projet, lui, reste nécessaire pour construire les URL web (SPEC-LINK-001).
/// </remarks>
/// <param name="ProjectName">Nom du projet d'équipe, ex. « Backoffice ».</param>
/// <param name="RepositoryId">Identifiant du dépôt (GUID).</param>
/// <param name="RepositoryName">Nom du dépôt, ex. « backoffice-api ».</param>
public sealed record RepositoryRef(string ProjectName, string RepositoryId, string RepositoryName)
{
    /// <summary>Libellé « projet / dépôt » destiné à l'affichage.</summary>
    public string DisplayPath => $"{ProjectName} / {RepositoryName}";

    public override string ToString() => DisplayPath;
}
