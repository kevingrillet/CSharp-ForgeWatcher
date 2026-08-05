namespace CSharpForgeWatcher.Infrastructure.GitLab.Dtos;

// Objets de transfert calqués sur le JSON de l'API REST GitLab v4.
//
// Comme pour GitHub, ces types sont internes à l'infrastructure : une évolution du format se
// corrige ici et dans le mappeur, sans toucher aux règles métier (SPEC-FORGE-005). La
// conversion snake_case est assurée par la politique de nommage déclarée dans
// RestGatewayBase : MergedAt lit donc merged_at.

/// <summary>Utilisateur GitLab.</summary>
internal sealed class GlUser
{
    public long Id { get; set; }

    /// <summary>Identifiant lisible — c'est lui qui sert d'identité (ADR-0004).</summary>
    public string? Username { get; set; }

    public string? Name { get; set; }

    /// <summary>Renseigné pour les comptes de service ; sert à repérer les robots.</summary>
    public bool Bot { get; set; }
}

/// <summary>Groupe : l'« espace » de GitLab (SPEC-FORGE-004).</summary>
internal sealed class GlGroup
{
    public long Id { get; set; }

    public string? Name { get; set; }

    /// <summary>Chemin complet, sous-groupes compris : <c>groupe/sous-groupe</c>.</summary>
    public string? FullPath { get; set; }

    public string? Description { get; set; }
}

/// <summary>Espace de noms auquel appartient un projet.</summary>
internal sealed class GlNamespace
{
    public string? FullPath { get; set; }
}

/// <summary>
/// Projet GitLab — c'est-à-dire un dépôt, et aussi le porteur de son pipeline.
/// </summary>
internal sealed class GlProject
{
    public long Id { get; set; }

    /// <summary>Dernier segment du chemin : <c>backoffice-api</c>.</summary>
    public string? Path { get; set; }

    /// <summary>Chemin complet : <c>groupe/sous-groupe/backoffice-api</c>.</summary>
    public string? PathWithNamespace { get; set; }

    public string? Name { get; set; }

    public bool Archived { get; set; }

    public GlNamespace? Namespace { get; set; }

    /// <summary><c>disabled</c>, <c>private</c> ou <c>enabled</c> — l'accès à l'intégration continue.</summary>
    public string? BuildsAccessLevel { get; set; }

    /// <summary>Forme historique du réglage précédent, encore renvoyée par certaines versions.</summary>
    public bool? JobsEnabled { get; set; }
}

/// <summary>Merge request : la pull request de GitLab.</summary>
internal sealed class GlMergeRequest
{
    /// <summary>Numéro affiché, unique dans le projet : c'est l'identifiant du domaine.</summary>
    public int Iid { get; set; }

    public string? Title { get; set; }

    /// <summary><c>opened</c>, <c>closed</c>, <c>merged</c> ou <c>locked</c>.</summary>
    public string? State { get; set; }

    /// <summary>Brouillon (forme actuelle).</summary>
    public bool Draft { get; set; }

    /// <summary>Brouillon (forme historique, encore renvoyée par les versions antérieures).</summary>
    public bool WorkInProgress { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>Renseigné uniquement si la merge request a été fusionnée.</summary>
    public DateTimeOffset? MergedAt { get; set; }

    public string? SourceBranch { get; set; }

    public string? TargetBranch { get; set; }

    public string? WebUrl { get; set; }

    public GlUser? Author { get; set; }

    /// <summary>Relecteurs sollicités.</summary>
    public List<GlUser>? Reviewers { get; set; }

    /// <summary>Identifiant du projet propriétaire.</summary>
    public long ProjectId { get; set; }
}

/// <summary>Réponse de l'état des approbations d'une merge request.</summary>
internal sealed class GlApprovals
{
    /// <summary>Approbations enregistrées.</summary>
    public List<GlApproval>? ApprovedBy { get; set; }
}

/// <summary>Une approbation, qui enveloppe son auteur.</summary>
internal sealed class GlApproval
{
    public GlUser? User { get; set; }
}

/// <summary>
/// Relecteur et l'état de sa relecture.
/// </summary>
/// <remarks>
/// Point d'entrée distinct de la merge request, et absent des versions anciennes : le champ
/// <see cref="State"/> peut donc être vide, ce que le mappeur traite comme « pas d'avis ».
/// </remarks>
internal sealed class GlReviewer
{
    public GlUser? User { get; set; }

    /// <summary><c>unreviewed</c>, <c>reviewed</c>, <c>requested_changes</c>, <c>approved</c>.</summary>
    public string? State { get; set; }
}

/// <summary>Position d'une note rattachée à une ligne de code.</summary>
internal sealed class GlNotePosition
{
    public string? NewPath { get; set; }

    public string? OldPath { get; set; }
}

/// <summary>Note : le message de GitLab.</summary>
internal sealed class GlNote
{
    public long Id { get; set; }

    public string? Body { get; set; }

    public GlUser? Author { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>Vrai pour les messages générés par GitLab (« a assigné », « a poussé »).</summary>
    public bool System { get; set; }

    /// <summary>Vrai si la note peut être marquée comme résolue.</summary>
    public bool Resolvable { get; set; }

    /// <summary>Vrai si elle l'est.</summary>
    public bool Resolved { get; set; }

    public GlNotePosition? Position { get; set; }
}

/// <summary>
/// Discussion : GitLab est la seule des trois forges à exposer directement le regroupement
/// des messages <b>et</b> leur état de résolution.
/// </summary>
internal sealed class GlDiscussion
{
    /// <summary>Identifiant textuel (empreinte) : inutilisable comme identifiant numérique.</summary>
    public string? Id { get; set; }

    public bool IndividualNote { get; set; }

    public List<GlNote>? Notes { get; set; }
}

/// <summary>Exécution de pipeline.</summary>
internal sealed class GlPipeline
{
    public long Id { get; set; }

    /// <summary>Numéro affiché, propre au projet.</summary>
    public long Iid { get; set; }

    /// <summary>
    /// <c>created</c>, <c>waiting_for_resource</c>, <c>preparing</c>, <c>pending</c>,
    /// <c>running</c>, <c>success</c>, <c>failed</c>, <c>canceled</c>, <c>skipped</c>,
    /// <c>manual</c>, <c>scheduled</c>.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>Branche ou étiquette déclenchante.</summary>
    public string? Ref { get; set; }

    public string? WebUrl { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Absent de la liste sur certaines versions : le mappeur s'en passe.</summary>
    public GlUser? User { get; set; }
}
