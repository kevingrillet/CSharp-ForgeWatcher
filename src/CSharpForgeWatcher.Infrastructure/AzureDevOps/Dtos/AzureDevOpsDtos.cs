using System.Text.Json.Serialization;

namespace CSharpForgeWatcher.Infrastructure.AzureDevOps.Dtos;

/// <summary>
/// Objets de transfert calqués sur le JSON d'Azure DevOps (API 7.1).
/// </summary>
/// <remarks>
/// Ces types sont <b>internes à l'infrastructure</b> et volontairement séparés du domaine :
/// une évolution du format d'Azure DevOps se corrige ici, dans le mappeur, sans toucher aux
/// règles métier. Tous les champs sont facultatifs : l'API omet ce qui n'est pas pertinent.
/// </remarks>
/// <typeparam name="TItem">Type des éléments de la collection.</typeparam>
internal sealed class AdoCollection<TItem>
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("value")]
    public List<TItem> Value { get; set; } = [];
}

/// <summary>Réponse de <c>_apis/connectionData</c>.</summary>
internal sealed class AdoConnectionData
{
    [JsonPropertyName("authenticatedUser")]
    public AdoAuthenticatedUser? AuthenticatedUser { get; set; }
}

/// <summary>Utilisateur authentifié par le jeton.</summary>
internal sealed class AdoAuthenticatedUser
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("providerDisplayName")]
    public string? ProviderDisplayName { get; set; }

    [JsonPropertyName("customDisplayName")]
    public string? CustomDisplayName { get; set; }
}

/// <summary>Référence à une identité (auteur, relecteur, auteur de commentaire).</summary>
internal class AdoIdentityRef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("uniqueName")]
    public string? UniqueName { get; set; }
}

/// <summary>Relecteur et son vote.</summary>
internal sealed class AdoReviewer : AdoIdentityRef
{
    [JsonPropertyName("vote")]
    public int Vote { get; set; }

    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; }
}

/// <summary>Projet d'équipe.</summary>
internal sealed class AdoProject
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>Dépôt Git.</summary>
internal sealed class AdoRepository
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("isDisabled")]
    public bool? IsDisabled { get; set; }

    [JsonPropertyName("project")]
    public AdoProject? Project { get; set; }
}

/// <summary>Pull request.</summary>
internal sealed class AdoPullRequest
{
    [JsonPropertyName("pullRequestId")]
    public int PullRequestId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("isDraft")]
    public bool IsDraft { get; set; }

    [JsonPropertyName("creationDate")]
    public DateTimeOffset? CreationDate { get; set; }

    [JsonPropertyName("sourceRefName")]
    public string? SourceRefName { get; set; }

    [JsonPropertyName("targetRefName")]
    public string? TargetRefName { get; set; }

    [JsonPropertyName("createdBy")]
    public AdoIdentityRef? CreatedBy { get; set; }

    [JsonPropertyName("reviewers")]
    public List<AdoReviewer>? Reviewers { get; set; }

    [JsonPropertyName("repository")]
    public AdoRepository? Repository { get; set; }
}

/// <summary>Contexte d'une discussion attachée à un fichier.</summary>
internal sealed class AdoThreadContext
{
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }
}

/// <summary>Discussion (thread) d'une pull request.</summary>
internal sealed class AdoThread
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("publishedDate")]
    public DateTimeOffset? PublishedDate { get; set; }

    [JsonPropertyName("lastUpdatedDate")]
    public DateTimeOffset? LastUpdatedDate { get; set; }

    [JsonPropertyName("comments")]
    public List<AdoComment>? Comments { get; set; }

    [JsonPropertyName("threadContext")]
    public AdoThreadContext? ThreadContext { get; set; }
}

/// <summary>Définition de pipeline (« build definition »).</summary>
internal sealed class AdoBuildDefinition
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary><c>enabled</c>, <c>paused</c> ou <c>disabled</c>.</summary>
    [JsonPropertyName("queueStatus")]
    public string? QueueStatus { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("project")]
    public AdoProject? Project { get; set; }
}

/// <summary>Lien web d'une ressource.</summary>
internal sealed class AdoWebLink
{
    [JsonPropertyName("href")]
    public string? Href { get; set; }
}

/// <summary>Conteneur des liens d'une ressource.</summary>
internal sealed class AdoLinks
{
    [JsonPropertyName("web")]
    public AdoWebLink? Web { get; set; }
}

/// <summary>Exécution de pipeline (« build »).</summary>
internal sealed class AdoBuild
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("buildNumber")]
    public string? BuildNumber { get; set; }

    /// <summary><c>notStarted</c>, <c>inProgress</c>, <c>completed</c>, <c>cancelling</c>.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary><c>succeeded</c>, <c>partiallySucceeded</c>, <c>failed</c>, <c>canceled</c>.</summary>
    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("sourceBranch")]
    public string? SourceBranch { get; set; }

    [JsonPropertyName("startTime")]
    public DateTimeOffset? StartTime { get; set; }

    [JsonPropertyName("finishTime")]
    public DateTimeOffset? FinishTime { get; set; }

    [JsonPropertyName("definition")]
    public AdoBuildDefinition? Definition { get; set; }

    [JsonPropertyName("requestedFor")]
    public AdoIdentityRef? RequestedFor { get; set; }

    [JsonPropertyName("_links")]
    public AdoLinks? Links { get; set; }
}

/// <summary>Message d'une discussion.</summary>
internal sealed class AdoComment
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("parentCommentId")]
    public long? ParentCommentId { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// <c>text</c>, <c>codeChange</c> ou <c>system</c>. Les messages système ne
    /// déclenchent jamais de notification de commentaire (SPEC-EVT-004, règle 1).
    /// </summary>
    [JsonPropertyName("commentType")]
    public string? CommentType { get; set; }

    [JsonPropertyName("publishedDate")]
    public DateTimeOffset? PublishedDate { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("author")]
    public AdoIdentityRef? Author { get; set; }
}
