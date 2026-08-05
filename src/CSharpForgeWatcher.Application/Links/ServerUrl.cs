namespace CSharpForgeWatcher.Application.Links;

/// <summary>
/// Interprétation de l'URL saisie pour une forge dont l'adresse est celle d'un serveur
/// (SPEC-FORGE-002).
/// </summary>
/// <remarks>
/// GitHub et GitLab attendent la <b>racine</b> du serveur, pas un chemin : l'organisation, le
/// groupe et le projet sont choisis dans l'arborescence de sélection. Azure DevOps est l'autre
/// modèle — son URL comprend l'organisation — et n'utilise donc pas ce type.
/// </remarks>
public static class ServerUrl
{
    /// <summary>
    /// Ramène une URL de serveur à sa seule origine : schéma, hôte et port.
    /// </summary>
    /// <remarks>
    /// L'utilisateur saisit volontiers <c>https://github.com/mon-organisation</c> : le chemin
    /// est ignoré, de sorte que la saisie fonctionne quand même. Une valeur inexploitable est
    /// renvoyée débarrassée de ses espaces et de sa barre oblique finale, ce qui reste le
    /// comportement le moins surprenant tant que la validation n'a pas eu lieu.
    /// </remarks>
    public static string Origin(string? url)
    {
        var trimmed = (url ?? string.Empty).Trim().TrimEnd('/');

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.GetLeftPart(UriPartial.Authority)
            : trimmed;
    }
}
