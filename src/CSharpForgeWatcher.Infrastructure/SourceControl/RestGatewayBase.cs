using System.Globalization;
using System.Net;
using System.Text.Json;
using CSharpForgeWatcher.Application.Abstractions;
using CSharpForgeWatcher.Domain.Text;
using Microsoft.Extensions.Logging;

namespace CSharpForgeWatcher.Infrastructure.SourceControl;

/// <summary>
/// Plomberie HTTP commune aux adaptateurs de forge modernes : requêtes, pagination par
/// en-tête <c>Link</c>, désérialisation, parallélisme borné, classement des erreurs.
/// </summary>
/// <remarks>
/// <para>
/// GitHub et GitLab exposent tous deux une API JSON en <c>snake_case</c>, paginée par
/// l'en-tête <c>Link</c> (RFC 5988). Écrire deux fois la même mécanique aurait signifié
/// corriger deux fois le même défaut : ce qui est identique vit donc ici, et chaque
/// adaptateur ne garde que ce qui lui est propre — son authentification, ses points
/// d'entrée, ses messages d'erreur.
/// </para>
/// <para>
/// La classe reste volontairement <b>sans connaissance du métier</b> : elle ne manipule que
/// des chaînes et des types de transfert. La traduction vers le domaine appartient au mappeur
/// de chaque forge (SPEC-FORGE-005).
/// </para>
/// <para>
/// Azure DevOps n'en dérive pas : son API n'est ni paginée de la même façon, ni nommée de la
/// même façon, et son adaptateur existait avant. L'aligner de force aurait coûté plus que la
/// duplication qu'on évite.
/// </para>
/// </remarks>
public abstract class RestGatewayBase : IDisposable
{
    /// <summary>Taille de page demandée, maximum accepté par les deux forges.</summary>
    protected const int PageSize = 100;

    /// <summary>
    /// Nombre maximal de pages suivies pour une même collection.
    /// </summary>
    /// <remarks>
    /// Garde-fou contre une pagination qui n'en finit pas (10 000 éléments). L'atteindre est
    /// journalisé : une troncature silencieuse se lirait comme une liste complète.
    /// </remarks>
    protected const int MaxPages = 100;

    /// <summary>Nombre maximal d'appels simultanés vers la forge.</summary>
    /// <remarks>
    /// Volontairement bas : les deux forges pénalisent les rafales de requêtes parallèles
    /// (« limites secondaires » chez GitHub).
    /// </remarks>
    protected const int MaxParallel = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly bool _ownsHttpClient;

    /// <summary>Prépare la plomberie.</summary>
    /// <param name="apiUrl">Racine de l'API, sans barre oblique finale.</param>
    /// <param name="forgeName">Nom de la forge, employé dans les messages d'erreur.</param>
    /// <param name="httpClient">Client à réutiliser ; <c>null</c> pour en créer un dédié.</param>
    /// <param name="logger">Journal, facultatif.</param>
    protected RestGatewayBase(string apiUrl, string forgeName, HttpClient? httpClient, ILogger? logger)
    {
        ApiUrl = apiUrl;
        ForgeName = forgeName;
        Logger = logger;
        _ownsHttpClient = httpClient is null;
        HttpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>Racine de l'API interrogée.</summary>
    protected string ApiUrl { get; }

    /// <summary>Nom de la forge, pour les messages destinés à l'utilisateur.</summary>
    protected string ForgeName { get; }

    /// <summary>Client HTTP, configuré par la classe dérivée (authentification, en-têtes).</summary>
    protected HttpClient HttpClient { get; }

    /// <summary>Journal, facultatif.</summary>
    protected ILogger? Logger { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Libère les ressources détenues.</summary>
    /// <param name="disposing">Vrai lors d'un appel explicite à <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && _ownsHttpClient)
        {
            HttpClient.Dispose();
        }
    }

    /// <summary>Encode un segment variable d'URL.</summary>
    protected static string Escape(string? value) => Uri.EscapeDataString(value ?? string.Empty);

    /// <summary>Exécute un GET et désérialise la réponse.</summary>
    protected async Task<TResponse> GetAsync<TResponse>(string relativeUrl, CancellationToken cancellationToken)
        where TResponse : class
        => await GetOrNullAsync<TResponse>(relativeUrl, cancellationToken).ConfigureAwait(false)
           ?? throw EmptyResponse();

    /// <summary>Exécute un GET ; retourne <c>null</c> sur un 404, lève sinon.</summary>
    protected async Task<TResponse?> GetOrNullAsync<TResponse>(
        string relativeUrl,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var response = await ReadAsync(Absolute(relativeUrl), cancellationToken).ConfigureAwait(false);
        return response is null ? null : Deserialize<TResponse>(response);
    }

    /// <summary>Lit une collection paginée en suivant l'en-tête <c>Link</c>.</summary>
    protected async Task<IReadOnlyList<TItem>> GetPagedAsync<TItem>(
        string relativeUrl,
        CancellationToken cancellationToken)
        => await GetPagedOrNullAsync<TItem>(relativeUrl, cancellationToken).ConfigureAwait(false)
           ?? throw EmptyResponse();

    /// <summary>
    /// Lit une collection paginée ; retourne <c>null</c> si la ressource n'existe pas.
    /// </summary>
    protected async Task<IReadOnlyList<TItem>?> GetPagedOrNullAsync<TItem>(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        var url = Absolute(WithPageSize(relativeUrl));
        var items = new List<TItem>();

        for (var page = 1; page <= MaxPages; page++)
        {
            var response = await ReadAsync(url, cancellationToken).ConfigureAwait(false);

            if (response is null)
            {
                return page == 1 ? null : items;
            }

            items.AddRange(Deserialize<List<TItem>>(response));

            if (response.NextPageUrl is not { } next)
            {
                return items;
            }

            url = next;
        }

        Logger?.LogWarning(
            "Pagination interrompue après {Pages} pages pour {Url} : la liste retournée est incomplète.",
            MaxPages,
            relativeUrl);

        return items;
    }

    /// <summary>
    /// Exécute un traitement sur une collection, avec un nombre maximal d'appels simultanés.
    /// </summary>
    protected static async Task<TResult[]> RunBoundedAsync<TItem, TResult>(
        IReadOnlyCollection<TItem> items,
        Func<TItem, CancellationToken, Task<TResult>> body,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return [];
        }

        using var throttle = new SemaphoreSlim(MaxParallel);

        var tasks = items.Select(async item =>
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await body(item, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                throttle.Release();
            }
        }).ToList();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Transforme une réponse en échec en <see cref="SourceControlException"/> classée.
    /// </summary>
    /// <remarks>
    /// À la charge de chaque forge : le sens d'un code varie. GitHub signale un quota épuisé
    /// par un <c>403</c>, que son adaptateur reclasse en <c>429</c> pour le rendre réessayable
    /// (SPEC-POLL-005).
    /// </remarks>
    protected abstract SourceControlException Describe(HttpResponseMessage response, string body, string url);

    /// <summary>Message générique, utilisable par les classes dérivées comme repli.</summary>
    protected SourceControlException DescribeByStatus(HttpStatusCode statusCode, string body)
    {
        var code = (int)statusCode;

        var text = statusCode switch
        {
            HttpStatusCode.Unauthorized => TextRef.Of(TextKeys.Forge.Unauthorized, ForgeName),
            HttpStatusCode.Forbidden => TextRef.Of(TextKeys.Forge.Forbidden),
            HttpStatusCode.TooManyRequests => TextRef.Of(TextKeys.Forge.RateLimited, ForgeName),
            _ when code >= 500 => TextRef.Of(TextKeys.Forge.ServerError, ForgeName, code),
            _ => TextRef.Of(TextKeys.Forge.CallFailed, ForgeName, code, Summarize(body)),
        };

        return new SourceControlException(text, code);
    }

    /// <summary>Réponse vide là où un contenu était attendu.</summary>
    protected SourceControlException EmptyResponse()
        => new(TextRef.Of(TextKeys.Forge.EmptyResponse, ForgeName), (int)HttpStatusCode.NotFound);

    /// <summary>Résumé court du corps de réponse, pour les diagnostics.</summary>
    protected static string Summarize(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var flattened = body.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return flattened.Length <= 200 ? flattened : flattened[..200] + "…";
    }

    /// <summary>Vrai si l'en-tête indiqué contient un entier inférieur ou égal à zéro.</summary>
    protected static bool IsHeaderExhausted(HttpResponseMessage response, string header)
        => response.Headers.TryGetValues(header, out var values)
           && int.TryParse(values.FirstOrDefault(), CultureInfo.InvariantCulture, out var left)
           && left <= 0;

    /// <summary>Ajoute la taille de page si l'appelant ne l'a pas fixée.</summary>
    private static string WithPageSize(string relativeUrl)
    {
        if (relativeUrl.Contains("per_page=", StringComparison.OrdinalIgnoreCase))
        {
            return relativeUrl;
        }

        var separator = relativeUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{relativeUrl}{separator}per_page={PageSize}";
    }

    private string Absolute(string relativeUrl)
        => relativeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? relativeUrl
            : ApiUrl + relativeUrl;

    /// <summary>Exécute la requête et classe l'échec éventuel.</summary>
    /// <returns><c>null</c> si la ressource est absente (404).</returns>
    private async Task<RestResponse?> ReadAsync(string url, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Délai dépassé : transitoire, donc réessayable par le décorateur de résilience.
            throw new SourceControlException(
                TextRef.Of(TextKeys.Forge.Timeout, ForgeName),
                null,
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new SourceControlException(
                TextRef.Of(TextKeys.Forge.Unreachable, ForgeName, exception.Message),
                exception.StatusCode is { } status ? (int)status : null,
                exception);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                Logger?.LogWarning("Appel {Url} en échec ({Code}).", url, (int)response.StatusCode);
                throw Describe(response, body, url);
            }

            return new RestResponse(body, NextPageUrl(response));
        }
    }

    private TResponse Deserialize<TResponse>(RestResponse response)
        where TResponse : class
    {
        try
        {
            return JsonSerializer.Deserialize<TResponse>(response.Body, JsonOptions) ?? throw EmptyResponse();
        }
        catch (JsonException exception)
        {
            Logger?.LogError(exception, "Réponse {Forge} illisible.", ForgeName);
            throw new SourceControlException(
                TextRef.Of(TextKeys.Forge.UnreadableResponse, ForgeName),
                null,
                exception);
        }
    }

    /// <summary>
    /// Adresse de la page suivante, extraite de l'en-tête <c>Link</c>.
    /// </summary>
    /// <remarks>
    /// Les deux forges y publient plusieurs relations (<c>next</c>, <c>last</c>, <c>prev</c>)
    /// séparées par des virgules. Seule <c>next</c> nous intéresse, et son absence signifie
    /// « dernière page ».
    /// </remarks>
    private static string? NextPageUrl(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values))
        {
            return null;
        }

        foreach (var segment in string.Join(',', values).Split(','))
        {
            if (!segment.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = segment.IndexOf('<', StringComparison.Ordinal);
            var end = segment.IndexOf('>', StringComparison.Ordinal);

            if (start >= 0 && end > start + 1)
            {
                return segment[(start + 1)..end];
            }
        }

        return null;
    }

    /// <summary>Corps d'une réponse, avec l'adresse de la page suivante s'il y en a une.</summary>
    private sealed record RestResponse(string Body, string? NextPageUrl);
}
