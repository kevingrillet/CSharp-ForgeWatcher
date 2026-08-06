namespace CSharpForgeWatcher.Domain.Identity;

/// <summary>
/// Référence à une personne Azure DevOps : son identifiant technique et son nom affiché.
/// </summary>
/// <remarks>
/// Objet-valeur : deux <see cref="UserRef"/> sont égaux s'ils portent le même identifiant
/// et le même nom. Les comparaisons d'identité passent toujours par <see cref="Is"/>,
/// car Azure DevOps n'est pas cohérent sur la casse des GUID selon les points d'entrée.
/// </remarks>
/// <param name="Id">Identifiant d'identité Azure DevOps (GUID sous forme de chaîne).</param>
/// <param name="DisplayName">Nom affiché, ex. « Camille Martin ».</param>
public sealed record UserRef(string Id, string DisplayName)
{
    /// <summary>Utilisateur non résolu (auteur manquant dans une réponse d'API).</summary>
    public static readonly UserRef Unknown = new(string.Empty, "Inconnu");

    /// <summary>
    /// Indique si cette référence désigne l'identifiant fourni.
    /// Retourne <c>false</c> si l'un des deux identifiants est vide, afin qu'un identifiant
    /// inconnu ne soit jamais considéré comme « c'est moi ».
    /// </summary>
    public bool Is(string? userId)
        => !string.IsNullOrEmpty(Id)
           && !string.IsNullOrEmpty(userId)
           && string.Equals(Id, userId, StringComparison.OrdinalIgnoreCase);

    /// <summary>Nom affiché, avec repli sur l'identifiant puis sur « Inconnu ».</summary>
    public string SafeDisplayName
        => !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName
         : !string.IsNullOrWhiteSpace(Id) ? Id
         : Unknown.DisplayName;

    public override string ToString() => SafeDisplayName;
}
