using CSharpForgeWatcher.Application.Links;

namespace CSharpForgeWatcher.Application.Configuration;

/// <summary>
/// Ce que l'interface doit dire de chaque forge : son nom, le sens de son champ d'URL, et
/// où fabriquer un jeton (SPEC-FORGE-002).
/// </summary>
/// <remarks>
/// Ces libellés vivent dans la couche application, et non dans la fenêtre de configuration :
/// « URL de l'organisation » n'a aucun sens pour GitHub, et une nouvelle forge doit pouvoir
/// s'annoncer correctement sans qu'on ouvre un fichier WinForms. C'est aussi ce qui les rend
/// vérifiables par un test.
/// </remarks>
public static class SourceControlProviderExtensions
{
    /// <summary>Fournisseurs proposés à l'utilisateur, dans l'ordre d'affichage.</summary>
    /// <remarks>
    /// Seuls les fournisseurs implémentés y figurent : proposer un choix qui sera refusé à
    /// l'enregistrement serait une invitation à perdre son temps. La valeur reste acceptée
    /// dans un <c>config.json</c> écrit à la main, et refusée par la validation avec un
    /// message clair.
    /// </remarks>
    public static readonly IReadOnlyList<SourceControlProvider> Implemented =
    [
        SourceControlProvider.AzureDevOps,
        SourceControlProvider.GitHub,
        SourceControlProvider.GitLab,
    ];

    /// <summary>Vrai si un adaptateur existe pour ce fournisseur.</summary>
    public static bool IsImplemented(this SourceControlProvider provider)
        => Implemented.Contains(provider);

    /// <summary>Énumération lisible des forges prises en charge, pour un message d'erreur.</summary>
    public static string ImplementedLabels()
    {
        var labels = Implemented.Select(provider => provider.ToLabel()).ToList();

        return labels.Count <= 1
            ? labels.FirstOrDefault() ?? string.Empty
            : $"{string.Join(", ", labels.Take(labels.Count - 1))} et {labels[^1]}";
    }

    /// <summary>Nom affiché de la forge.</summary>
    public static string ToLabel(this SourceControlProvider provider) => provider switch
    {
        SourceControlProvider.AzureDevOps => "Azure DevOps",
        SourceControlProvider.GitHub => "GitHub",
        SourceControlProvider.GitLab => "GitLab",
        _ => provider.ToString(),
    };

    /// <summary>Clé du libellé du champ d'URL.</summary>
    /// <remarks>
    /// Ces trois clés se déduisent du fournisseur, que le domaine ne connaît pas : elles ne
    /// peuvent donc pas être des constantes de <c>TextKeys</c> (SPEC-UI-LANG-002). Le nom de
    /// la forge, lui, n'est pas traduit — c'est une marque.
    /// </remarks>
    public static string UrlLabelKey(this SourceControlProvider provider)
        => $"Forge.{provider}.UrlLabel";

    /// <summary>Exemple affiché en filigrane dans le champ d'URL.</summary>
    public static string UrlPlaceholder(this SourceControlProvider provider) => provider switch
    {
        SourceControlProvider.GitHub => "https://github.com",
        SourceControlProvider.GitLab => "https://gitlab.com",
        _ => "https://dev.azure.com/mon-organisation",
    };

    /// <summary>Nom donné au niveau intermédiaire de l'arborescence de sélection.</summary>
    /// <remarks>
    /// Azure DevOps groupe les dépôts par projet d'équipe ; GitHub, par propriétaire
    /// (compte ou organisation). L'arborescence est la même, son libellé change
    /// (SPEC-CFG-002).
    /// </remarks>
    public static string ScopeLabelKey(this SourceControlProvider provider)
        => $"Forge.{provider}.ScopeLabel";

    /// <summary>Clé des portées minimales à demander au jeton, affichées sous le champ.</summary>
    public static string TokenScopeHintKey(this SourceControlProvider provider)
        => $"Forge.{provider}.TokenScopeHint";

    /// <summary>Page de création d'un jeton, déduite de l'URL saisie.</summary>
    /// <param name="provider">Fournisseur configuré.</param>
    /// <param name="serverUrl">URL saisie par l'utilisateur, éventuellement vide.</param>
    public static string TokenPageUrl(this SourceControlProvider provider, string? serverUrl)
    {
        var typed = (serverUrl ?? string.Empty).Trim().TrimEnd('/');

        return provider switch
        {
            SourceControlProvider.GitHub =>
                $"{Origin(typed, "https://github.com")}/settings/tokens?type=beta",
            SourceControlProvider.GitLab =>
                $"{Origin(typed, "https://gitlab.com")}/-/user_settings/personal_access_tokens",

            // Azure DevOps : la page est relative à l'organisation, qui fait partie du chemin.
            _ => typed.Length > 0
                ? $"{typed}/_usersSettings/tokens"
                : "https://dev.azure.com/_usersSettings/tokens",
        };
    }

    /// <summary>Origine de l'URL saisie, ou une valeur de repli si elle est inexploitable.</summary>
    private static string Origin(string typed, string fallback)
    {
        var normalized = ServerUrl.Origin(typed);
        return Uri.TryCreate(normalized, UriKind.Absolute, out _) ? normalized : fallback;
    }
}
