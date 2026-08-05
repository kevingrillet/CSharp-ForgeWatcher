using CSharpForgeWatcher.Application.Links;
using CSharpForgeWatcher.Domain.Text;

namespace CSharpForgeWatcher.Application.Configuration;

/// <summary>
/// Un compte de forge surveillé : où regarder, avec quel jeton, et quoi y suivre
/// (SPEC-CFG-008).
/// </summary>
/// <remarks>
/// <para>
/// C'est l'unité qui a permis de surveiller plusieurs forges à la fois. Auparavant la
/// configuration portait un fournisseur, une URL et un jeton ; elle porte maintenant une
/// liste de comptes, chacun avec les siens et sa propre sélection de dépôts et de pipelines.
/// Un poste peut ainsi suivre Azure DevOps au travail et GitHub en dehors, sans arbitrer.
/// </para>
/// <para>
/// POCO muable : sérialisé dans <c>config.json</c> et édité par la fenêtre de configuration.
/// Le jeton n'y figure que <b>chiffré</b> (DPAPI, ADR-0002) ; seul
/// <see cref="ConfigurationService"/> manipule sa forme claire.
/// </para>
/// </remarks>
public sealed class WatchedAccount
{
    /// <summary>
    /// Identifiant interne, stable pour la vie du compte.
    /// </summary>
    /// <remarks>
    /// C'est la clé de l'état mémorisé : elle ne doit donc dépendre ni de l'URL ni du
    /// fournisseur, qu'on peut corriger sans vouloir tout réamorcer. Deux comptes sur le même
    /// serveur avec deux jetons différents — un personnel, un professionnel — restent ainsi
    /// distincts.
    /// </remarks>
    public string Id { get; set; } = string.Empty;

    /// <summary>Libellé choisi par l'utilisateur ; vide pour laisser le libellé par défaut.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Forge de ce compte (SPEC-FORGE-002).</summary>
    public SourceControlProvider Provider { get; set; } = SourceControlProvider.AzureDevOps;

    /// <summary>
    /// URL de la forge. Son sens dépend de <see cref="Provider"/> : organisation pour
    /// Azure DevOps, racine du serveur pour GitHub et GitLab (SPEC-FORGE-002).
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Jeton chiffré (DPAPI, ADR-0002). Jamais en clair sur le disque.</summary>
    public string ProtectedPersonalAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Compte surveillé. Décoché, il est conservé avec sa sélection mais ignoré des cycles.
    /// </summary>
    /// <remarks>
    /// Utile pour taire temporairement une forge — jeton expiré, serveur en maintenance —
    /// sans perdre la sélection de dépôts qu'on a mis du temps à composer.
    /// </remarks>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Dépôts surveillés sur ce compte (SPEC-CFG-002).</summary>
    public List<WatchedRepository> Repositories { get; set; } = [];

    /// <summary>Pipelines surveillés sur ce compte (SPEC-PIPE-003).</summary>
    public List<WatchedPipeline> Pipelines { get; set; } = [];

    /// <summary>Libellé affiché : celui de l'utilisateur, ou un libellé déduit.</summary>
    public string DisplayLabel
        => string.IsNullOrWhiteSpace(Label) ? DefaultLabel(Provider, Url) : Label.Trim();

    /// <summary>Identifiants des dépôts surveillés sur ce compte.</summary>
    public IReadOnlySet<string> WatchedRepositoryIds
        => Repositories
            .Select(repository => repository.RepositoryId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Clés <c>espace:définition</c> des pipelines surveillés sur ce compte.</summary>
    public IReadOnlySet<string> WatchedPipelineKeys
        => Pipelines
            .Where(pipeline => pipeline.DefinitionId > 0 && !string.IsNullOrWhiteSpace(pipeline.ProjectName))
            .Select(pipeline => pipeline.Key)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Vrai si ce compte a quelque chose à surveiller.</summary>
    public bool HasSelection => Repositories.Count > 0 || Pipelines.Count > 0;

    /// <summary>Empreinte de ce que ce compte fait lire à un cycle.</summary>
    /// <remarks>
    /// Le jeton y figure sous forme d'empreinte, jamais recopié : renouveler un jeton expiré
    /// doit relancer un cycle tout de suite — c'est le geste de dépannage le plus courant —,
    /// mais un secret n'a pas à circuler dans une chaîne de comparaison (ADR-0002). La
    /// sélection est triée : recocher les mêmes dépôts dans un autre ordre ne change rien à
    /// ce qui sera lu.
    /// </remarks>
    public string MonitoringSignature => string.Join(
        "|",
        [
            Id,
            Provider.ToString(),
            Url.Trim(),
            IsEnabled ? "on" : "off",
            StableHash.Of(ProtectedPersonalAccessToken),
            .. WatchedRepositoryIds.Order(StringComparer.OrdinalIgnoreCase),
            .. WatchedPipelineKeys.Order(StringComparer.Ordinal),
        ]);

    /// <summary>Libellé déduit du fournisseur et de l'URL, ex. « GitHub · github.com ».</summary>
    public static string DefaultLabel(SourceControlProvider provider, string? url)
    {
        var trimmed = (url ?? string.Empty).Trim();

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return provider.ToLabel();
        }

        // Azure DevOps porte l'organisation dans le chemin : elle est plus parlante que l'hôte.
        var detail = provider == SourceControlProvider.AzureDevOps && uri.Segments.Length > 1
            ? uri.Segments[^1].Trim('/')
            : uri.Host;

        return string.IsNullOrEmpty(detail) ? provider.ToLabel() : $"{provider.ToLabel()} · {detail}";
    }

    /// <summary>Nouvel identifiant de compte.</summary>
    /// <remarks>
    /// Appelé par la fenêtre de configuration à la création d'un compte, jamais pendant un
    /// cycle : la valeur est aussitôt persistée et ne change plus.
    /// </remarks>
    public static string NewId() => Guid.NewGuid().ToString("n");

    /// <summary>
    /// Vérifie que ce compte permet de travailler (SPEC-CFG-003).
    /// </summary>
    /// <param name="personalAccessToken">
    /// Jeton en clair. Passé en paramètre car la validation ne doit pas savoir déchiffrer.
    /// </param>
    public ConfigurationValidationResult Validate(string? personalAccessToken)
    {
        var errors = new List<TextRef>();

        // Le libellé du champ d'adresse dépend de la forge : il est passé comme fragment, que
        // le catalogue formulera avec le reste de la phrase.
        var urlLabel = TextRef.Of(Provider.UrlLabelKey());

        if (string.IsNullOrWhiteSpace(Url))
        {
            errors.Add(TextRef.Of(
                TextKeys.Config.UrlMissing,
                DisplayLabel,
                urlLabel,
                Provider.UrlPlaceholder()));
        }
        else if (!Uri.TryCreate(Url.Trim(), UriKind.Absolute, out var uri)
                 || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add(TextRef.Of(TextKeys.Config.UrlInvalid, DisplayLabel, urlLabel));
        }

        if (string.IsNullOrWhiteSpace(personalAccessToken))
        {
            errors.Add(TextRef.Of(TextKeys.Config.TokenMissing, DisplayLabel));
        }

        // Refus explicite plutôt qu'un échec réseau incompréhensible (SPEC-FORGE-002).
        if (!Provider.IsImplemented())
        {
            errors.Add(TextRef.Of(
                TextKeys.Config.ProviderUnsupported,
                DisplayLabel,
                Provider.ToLabel(),
                SourceControlProviderExtensions.ImplementedLabels()));
        }

        return errors.Count == 0 ? ConfigurationValidationResult.Valid : new ConfigurationValidationResult(errors);
    }

    /// <summary>Générateur d'adresses web propre à ce compte (SPEC-FORGE-003).</summary>
    /// <remarks>
    /// Chaque compte a sa forge et son serveur : un même cycle construit donc des adresses de
    /// plusieurs formes, et c'est le compte — non un réglage global — qui détermine laquelle.
    /// </remarks>
    public IPullRequestLinkBuilder CreateLinkBuilder()
        => new ProviderAwareLinkBuilder(() => Provider, () => Url);

    /// <summary>Copie indépendante, pour une édition annulable (SPEC-CFG-004).</summary>
    public WatchedAccount Clone() => new()
    {
        Id = Id,
        Label = Label,
        Provider = Provider,
        Url = Url,
        ProtectedPersonalAccessToken = ProtectedPersonalAccessToken,
        IsEnabled = IsEnabled,
        Repositories = Repositories.Select(repository => repository.Clone()).ToList(),
        Pipelines = Pipelines.Select(pipeline => pipeline.Clone()).ToList(),
    };

    public override string ToString() => DisplayLabel;
}
