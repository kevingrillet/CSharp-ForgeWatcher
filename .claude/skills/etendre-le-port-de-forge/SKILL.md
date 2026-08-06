---
name: etendre-le-port-de-forge
description: "Procédure pour ajouter une question posée à la forge dans Forge Watcher — une méthode sur ISourceControlGateway — en la répercutant sur les trois adaptateurs, le décorateur de résilience et les deux doubles de test. À utiliser dès qu'il faut lire une donnée que l'application n'obtient pas encore (étiquettes, jalons, work items, artefacts, statuts de contrôle), ou dès qu'une signature du port change. À ne pas confondre avec « ajouter-une-forge », qui branche un nouveau serveur."
---

# Étendre le port de forge

Ajouter une méthode à `ISourceControlGateway` n'est pas une modification d'un fichier, mais
de **sept**. Aucun compilateur ne signalera l'oubli du dernier : les deux doubles de test
vivent dans un projet à part, et un adaptateur non mis à jour ne casse la compilation que
s'il implémente l'interface directement — ce qui est le cas, heureusement, mais le corps
« retourne une liste vide » compile parfaitement et se tait pour toujours.

Fil rouge de cette fiche : « quelles étiquettes porte cette pull request ? »
(`GetLabelsAsync`). Remplacer par le besoin réel.

## Avant tout : est-ce vraiment au port d'y répondre ?

Trois questions, dans l'ordre :

1. **La donnée existe-t-elle déjà ?** `PullRequest`, `CommentThread`, `PipelineRun` portent
   plus de champs qu'on ne croit, et `DetectionContext` expose des propriétés calculées.
   Relire avant d'ajouter — c'est le réflexe le moins coûteux.
2. **Les trois forges savent-elles y répondre ?** Si une seule le sait, ce n'est pas une
   question du port mais une **capacité optionnelle** : la méthode existe quand même, et les
   adaptateurs qui ne savent pas retournent une valeur neutre (SPEC-FORGE-004). Ce n'est
   jamais une exception, jamais un `switch` sur le fournisseur ailleurs que dans les deux
   points de bascule.
3. **Le coût est-il acceptable à chaque cycle ?** Une méthode appelée par pull request
   multiplie les requêtes par le nombre de PR **et** par le nombre de comptes. Si la réponse
   n'est utile qu'à quelques PR, la garde se met dans le monitor (à l'image de
   `ShouldReadThreads`), pas dans l'adaptateur.

## Les sept endroits

Dans cet ordre : le port d'abord, les doubles ensuite — écrire le test avant les adaptateurs
évite de deviner la forme de la réponse.

| # | Fichier | Ce qu'on y fait |
|---|---|---|
| 1 | `src/CSharpForgeWatcher.Application/Abstractions/ISourceControlGateway.cs` | la signature + sa doc XML, dont **ce que retourne une forge qui ne sait pas** |
| 2 | `src/CSharpForgeWatcher.Application/Resilience/ResilientSourceControlGateway.cs` | la délégation, sinon la méthode perd le réessai des erreurs transitoires (SPEC-POLL-005) |
| 3 | `tests/CSharpForgeWatcher.Tests/Doubles/FakeSourceControlGateway.cs` | les données à déclarer, le journal d'appels, une panne injectable |
| 4 | `tests/CSharpForgeWatcher.Tests/Doubles/Fakes.cs` (`FlakyGateway`) | un corps minimal — il ne sert qu'aux tests de réessai |
| 5 | `src/CSharpForgeWatcher.Infrastructure/AzureDevOps/AzureDevOpsRestGateway.cs` + son mappeur et ses DTO | l'implémentation Azure DevOps |
| 6 | `src/CSharpForgeWatcher.Infrastructure/GitHub/GitHubRestGateway.cs` + mappeur + DTO | l'implémentation GitHub |
| 7 | `src/CSharpForgeWatcher.Infrastructure/GitLab/GitLabRestGateway.cs` + mappeur + DTO | l'implémentation GitLab |

Puis, si la réponse doit survivre d'un cycle à l'autre : `PullRequestSnapshot` ou
`PipelineSnapshot` (cf. skill `ajouter-notification`, étape 8).

## 1. Le port

La signature est **neutre** : elle ne nomme aucune forge, et ses types viennent de `Domain`
(SPEC-FORGE-005). La doc XML doit répondre à trois questions, parce que trois adaptateurs
vont la lire :

```csharp
    /// <summary>
    /// Liste les étiquettes d'une pull request.
    /// </summary>
    /// <remarks>
    /// Une forge sans notion d'étiquette retourne une liste vide : la fonctionnalité
    /// disparaît alors d'elle-même, sans code conditionnel ailleurs (SPEC-FORGE-004).
    /// <para>
    /// Coût : un appel par pull request. Le moniteur décide s'il le paie.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<string>> GetLabelsAsync(
        RepositoryRef repository,
        int pullRequestId,
        CancellationToken cancellationToken);
```

Ce qui se décide ici et nulle part ailleurs :

* **le type de retour**, pris dans `Domain` — jamais un DTO d'infrastructure ;
* **la valeur neutre** d'une capacité absente : liste vide, `null`, `Unknown`. L'écrire dans
  la doc, sinon chaque adaptateur inventera la sienne ;
* **les identifiants numériques sont des `long`** (SPEC-FORGE-006). Vérifier l'ordre de
  grandeur chez les trois forges avant de supposer qu'un `int` suffit : ceux de GitHub
  Actions dépassent les dix chiffres.

## 2. Le décorateur de résilience

Une ligne, mais son oubli est **silencieux** : la méthode fonctionnera, sans jamais réessayer
un `429` ni un `5xx`.

```csharp
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetLabelsAsync(
        RepositoryRef repository,
        int pullRequestId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            nameof(GetLabelsAsync),
            token => _inner.GetLabelsAsync(repository, pullRequestId, token),
            cancellationToken);
```

## 3. Les doubles de test

`FakeSourceControlGateway` suit toujours le même patron : un dictionnaire de données, un
dictionnaire de pannes, une entrée dans `Calls`, et un `With…` fluide.

```csharp
    /// <summary>Étiquettes par numéro de PR.</summary>
    public Dictionary<int, List<string>> Labels { get; } = [];

    /// <summary>Pannes de lecture des étiquettes, par numéro de PR.</summary>
    public Dictionary<int, SourceControlException> LabelFailures { get; } = [];

    /// <summary>Déclare les étiquettes d'une PR.</summary>
    public FakeSourceControlGateway WithLabels(int pullRequestId, params string[] labels)
    {
        Labels[pullRequestId] = labels.ToList();
        return this;
    }

    public Task<IReadOnlyList<string>> GetLabelsAsync(
        RepositoryRef repository,
        int pullRequestId,
        CancellationToken cancellationToken)
    {
        Calls.Add($"labels:{pullRequestId}");

        if (LabelFailures.TryGetValue(pullRequestId, out var failure))
        {
            throw failure;
        }

        return Task.FromResult<IReadOnlyList<string>>(
            Labels.TryGetValue(pullRequestId, out var labels) ? labels : []);
    }
```

L'entrée dans `Calls` n'est pas décorative : c'est ce qui permet de vérifier qu'on **n'appelle
pas trop** — la garde de coût décidée à l'étape 0 se teste par un compteur d'appels, comme
`ThreadCallCount` et `PipelineRunCallCount`.

`FlakyGateway`, lui, ne sert qu'au réessai : un corps neutre suffit.

## 4. Les trois adaptateurs

| Forge | Ce à quoi il faut penser |
|---|---|
| Azure DevOps | API 7.1, `?api-version=` obligatoire sur chaque appel ; enveloppe `AdoCollection<T>` (`{ count, value }`) ; DTO annotés `[JsonPropertyName]` |
| GitHub | hériter de `RestGatewayBase` ; réponses en tableau nu, pagination par en-tête `Link` ; DTO **sans** annotation (politique `snake_case`) ; un `403` de quota est reclassé en 429, ne pas défaire ce classement |
| GitLab | hériter de `RestGatewayBase` ; identifiant **numérique** de projet dans les chemins ; codes HTTP respectés, aucun reclassement |

Règles communes, non négociables :

* **`GET` uniquement.** Le port est en lecture seule (SPEC-FORGE-001) ; une méthode d'écriture
  ne s'ajoute pas, elle se refuse.
* **La traduction du vocabulaire reste dans le mappeur** : une *label* GitHub, un *label*
  GitLab et un *tag* Azure DevOps deviennent le même type du domaine.
* **Capacité absente ⇒ valeur neutre + ligne dans SPEC-FORGE-007.** Ne jamais lever, ne
  jamais simuler.
* **Une nouvelle question ⇒ une nouvelle portée de jeton ?** Si oui, mettre à jour
  `SourceControlProviderExtensions.TokenScopeHint`, le § « Jeton et portées » de
  SPEC-FORGES et le README : un utilisateur avec un jeton déjà créé recevra sinon un `403`
  incompréhensible.

## 5. Refermer la boucle

- [ ] tableau de **SPEC-FORGE-004** complété : une colonne par forge, avec le point d'entrée
- [ ] **SPEC-FORGE-007** complété si une forge ne sait pas répondre
- [ ] `docs/SDD.md` §5.4 si le coût d'un cycle change
- [ ] spec / scénario / test / traçabilité pour le **comportement** que cette donnée sert —
      la méthode du port n'est qu'un moyen (skill `verifier-avant-commit`, §2)
- [ ] checklist du skill `verifier-avant-commit`, puis balayages de `respecter-architecture`

## Pièges déjà rencontrés

* **Décorateur oublié** : la méthode marche, mais aucune erreur transitoire n'est réessayée.
  Symptôme : des avertissements intermittents que rien n'explique.
* **Signature élargie sans les doubles** : les tests ne compilent plus, et la tentation est
  de retourner `[]` partout pour aller vite. Un double muet rend verts des tests qui ne
  vérifient plus rien.
* **`int` au lieu de `long`** : passe sur Azure DevOps, déborde sur GitHub. Le symptôme n'est
  pas une exception mais un identifiant faux — donc un élément tenu pour « déjà vu », donc
  jamais notifié (SPEC-FORGE-006, ADR-0004).
* **Appel par pull request sans garde** : le coût se multiplie par le nombre de PR *et* de
  comptes. Vérifier avec un compteur d'appels dans un test, pas à l'œil.
