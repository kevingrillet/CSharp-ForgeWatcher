using CSharpForgeWatcher.Application.Configuration;

namespace CSharpForgeWatcher.Application.Abstractions;

/// <summary>Paramètres de connexion à une forge.</summary>
/// <param name="OrganizationUrl">
/// URL de la forge : organisation pour Azure DevOps (<c>https://dev.azure.com/contoso</c>),
/// racine du serveur pour GitHub (<c>https://github.com</c>) — cf. SPEC-FORGE-002.
/// </param>
/// <param name="PersonalAccessToken">Jeton en clair (déchiffré juste avant l'appel).</param>
/// <param name="Provider">
/// Forge visée : c'est elle qui détermine l'implémentation construite par la fabrique.
/// </param>
public sealed record SourceControlConnection(
    string OrganizationUrl,
    string PersonalAccessToken,
    SourceControlProvider Provider = SourceControlProvider.AzureDevOps)
{
    /// <summary>
    /// Empreinte servant à réutiliser une passerelle déjà construite tant que la
    /// connexion ne change pas. Le jeton n'apparaît pas en clair (on n'en garde que la
    /// longueur et le condensé de la chaîne), afin de ne jamais risquer de le voir
    /// atterrir dans un log ou un message de diagnostic.
    /// </summary>
    public string CacheKey
        => $"{Provider}|{OrganizationUrl.TrimEnd('/')}|{PersonalAccessToken.Length}"
           + $"|{PersonalAccessToken.GetHashCode(StringComparison.Ordinal)}";
}

/// <summary>
/// Fabrique de passerelles de forge (patron Factory).
/// </summary>
/// <remarks>
/// La connexion dépend d'une configuration modifiable à chaud : impossible d'injecter
/// une passerelle une fois pour toutes au démarrage. La fabrique reçoit les paramètres
/// courants — <b>fournisseur compris</b> — et se charge, côté infrastructure, de choisir
/// l'adaptateur et de mutualiser les clients HTTP.
/// </remarks>
public interface ISourceControlGatewayFactory
{
    /// <summary>Crée (ou réutilise) une passerelle pour la connexion indiquée.</summary>
    ISourceControlGateway Create(SourceControlConnection connection);
}
