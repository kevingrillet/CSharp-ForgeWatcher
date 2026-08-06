namespace CSharpForgeWatcher.Infrastructure.GitHub.Dtos;

// Objets de transfert calqués sur le JSON de l'API REST GitHub (version 2022-11-28).
//
// Ces types sont internes à l'infrastructure et volontairement séparés du domaine : une
// évolution du format de GitHub se corrige ici et dans le mappeur, sans toucher aux règles
// métier (SPEC-FORGE-005). Tous les champs sont facultatifs, l'API omettant ce qui n'est pas
// pertinent.
//
// GitHub nomme ses champs en snake_case. Plutôt que d'annoter une centaine de propriétés, la
// conversion est confiée à une politique de nommage déclarée une fois dans
// GitHubRestGateway : InReplyToId lit donc in_reply_to_id. C'est le seul écart de style avec
// les DTO d'Azure DevOps, et il est motivé par le nombre de champs.

/// <summary>Compte GitHub : utilisateur, organisation ou robot.</summary>
internal sealed class GhAccount
{
    /// <summary>Identifiant lisible — c'est lui qui sert d'identité (ADR-0004).</summary>
    public string? Login { get; set; }

    /// <summary>Identifiant numérique, non utilisé comme identité.</summary>
    public long Id { get; set; }

    /// <summary>Nom affiché, souvent absent.</summary>
    public string? Name { get; set; }

    /// <summary>Description, pour une organisation.</summary>
    public string? Description { get; set; }

    /// <summary><c>User</c>, <c>Organization</c> ou <c>Bot</c>.</summary>
    public string? Type { get; set; }
}

/// <summary>Dépôt.</summary>
internal sealed class GhRepository
{
    public long Id { get; set; }

    public string? Name { get; set; }

    public string? FullName { get; set; }

    public bool Archived { get; set; }

    public bool Disabled { get; set; }

    public GhAccount? Owner { get; set; }
}

/// <summary>Extrémité d'une pull request (branche source ou cible).</summary>
internal sealed class GhCommitRef
{
    /// <summary>Nom court de la branche : GitHub ne préfixe pas par <c>refs/heads/</c>.</summary>
    public string? Ref { get; set; }

    public GhRepository? Repo { get; set; }
}

/// <summary>Pull request.</summary>
internal sealed class GhPullRequest
{
    /// <summary>Numéro affiché, unique dans le dépôt : c'est l'identifiant du domaine.</summary>
    public int Number { get; set; }

    public string? Title { get; set; }

    /// <summary><c>open</c> ou <c>closed</c>.</summary>
    public string? State { get; set; }

    public bool Draft { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>Renseigné uniquement si la pull request a été fusionnée.</summary>
    public DateTimeOffset? MergedAt { get; set; }

    /// <summary>Présent sur la lecture d'une pull request seule, absent des listes.</summary>
    public bool? Merged { get; set; }

    public string? HtmlUrl { get; set; }

    /// <summary>Auteur.</summary>
    public GhAccount? User { get; set; }

    public GhCommitRef? Head { get; set; }

    public GhCommitRef? Base { get; set; }

    /// <summary>
    /// Relectures <b>encore attendues</b>. GitHub en retire quiconque a rendu son avis
    /// (SPEC-FORGE-007).
    /// </summary>
    public List<GhAccount>? RequestedReviewers { get; set; }
}

/// <summary>Relecture soumise sur une pull request.</summary>
internal sealed class GhReview
{
    public long Id { get; set; }

    public GhAccount? User { get; set; }

    /// <summary>Corps du message ; vide pour une approbation sans commentaire.</summary>
    public string? Body { get; set; }

    /// <summary><c>APPROVED</c>, <c>CHANGES_REQUESTED</c>, <c>COMMENTED</c>, <c>DISMISSED</c>, <c>PENDING</c>.</summary>
    public string? State { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public string? HtmlUrl { get; set; }
}

/// <summary>Message de l'onglet <i>Conversation</i> (une pull request est une <i>issue</i>).</summary>
internal sealed class GhIssueComment
{
    public long Id { get; set; }

    public GhAccount? User { get; set; }

    public string? Body { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public string? HtmlUrl { get; set; }
}

/// <summary>Commentaire de ligne, rattaché à un fichier du diff.</summary>
internal sealed class GhReviewComment
{
    public long Id { get; set; }

    /// <summary>Commentaire auquel celui-ci répond : c'est ce qui reconstitue les fils.</summary>
    public long? InReplyToId { get; set; }

    public GhAccount? User { get; set; }

    public string? Body { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public string? HtmlUrl { get; set; }

    /// <summary>Chemin du fichier commenté.</summary>
    public string? Path { get; set; }
}

/// <summary>Définition de workflow GitHub Actions.</summary>
internal sealed class GhWorkflow
{
    public long Id { get; set; }

    /// <summary>Valeur de la clé <c>name:</c> du fichier de workflow.</summary>
    public string? Name { get; set; }

    /// <summary>Chemin du fichier, ex. <c>.github/workflows/ci.yml</c>.</summary>
    public string? Path { get; set; }

    /// <summary><c>active</c>, <c>disabled_manually</c>, <c>disabled_inactivity</c>, <c>deleted</c>…</summary>
    public string? State { get; set; }
}

/// <summary>Réponse de la liste des workflows d'un dépôt.</summary>
internal sealed class GhWorkflowList
{
    public int TotalCount { get; set; }

    public List<GhWorkflow> Workflows { get; set; } = [];
}

/// <summary>Exécution d'un workflow.</summary>
internal sealed class GhWorkflowRun
{
    /// <summary>Identifiant à dix chiffres et plus : hors de portée d'un entier 32 bits.</summary>
    public long Id { get; set; }

    /// <summary>Nom du workflow.</summary>
    public string? Name { get; set; }

    /// <summary>Numéro d'exécution, affiché à l'utilisateur.</summary>
    public int RunNumber { get; set; }

    /// <summary><c>queued</c>, <c>in_progress</c>, <c>completed</c>, <c>waiting</c>, <c>requested</c>, <c>pending</c>.</summary>
    public string? Status { get; set; }

    /// <summary>Résultat, renseigné une fois l'exécution terminée.</summary>
    public string? Conclusion { get; set; }

    /// <summary>Définition dont cette exécution est issue.</summary>
    public long WorkflowId { get; set; }

    /// <summary>Branche déclenchante, nom court.</summary>
    public string? HeadBranch { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public DateTimeOffset? RunStartedAt { get; set; }

    /// <summary>Dernière modification : fait office de date de fin sur une exécution terminée.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    public string? HtmlUrl { get; set; }

    /// <summary>Personne (ou robot) à l'origine du déclenchement.</summary>
    public GhAccount? Actor { get; set; }
}

/// <summary>Réponse de la liste des exécutions d'un workflow.</summary>
internal sealed class GhWorkflowRunList
{
    public int TotalCount { get; set; }

    public List<GhWorkflowRun> WorkflowRuns { get; set; } = [];
}
