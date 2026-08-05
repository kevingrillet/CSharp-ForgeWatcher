---
name: ajouter-une-forge
description: "Procédure pour brancher une seconde forge (GitHub, GitLab, Bitbucket, serveur auto-hébergé) sur Forge Watcher en respectant SPEC-FORGE. À utiliser dès qu'il est question de « supporter GitHub », « ajouter GitLab », « rendre la forge configurable », « champ Provider », d'une nouvelle implémentation de ISourceControlGateway ou d'un nouveau générateur de liens."
---

# Ajouter une forge

Référence normative : `docs/specs/SPEC-FORGES.md`. Toute la mécanique tient dans une règle :
**le métier ne doit apprendre aucun mot nouveau.** Une forge de plus, c'est un adaptateur
de plus dans `Infrastructure` et un générateur de liens de plus — rien d'autre.

## État du dépôt à connaître avant de commencer

Déjà en place :

| Élément | Emplacement |
|---|---|
| Port unique, neutre et en lecture seule | `src/CSharpForgeWatcher.Application/Abstractions/ISourceControlGateway.cs` |
| Fabrique de passerelles + paramètres de connexion | `src/CSharpForgeWatcher.Application/Abstractions/ISourceControlGatewayFactory.cs` |
| Erreur classée à la frontière | `src/CSharpForgeWatcher.Application/Abstractions/SourceControlException.cs` |
| Port de liens abstrait | `src/CSharpForgeWatcher.Application/Links/IPullRequestLinkBuilder.cs` |
| Aiguillage des liens par fournisseur | `src/CSharpForgeWatcher.Application/Links/ProviderAwareLinkBuilder.cs` |
| Aiguillage des passerelles par fournisseur | `src/CSharpForgeWatcher.Infrastructure/SourceControl/ProviderGatewayFactory.cs` |
| Libellés dépendant de la forge (champ d'URL, portées, arborescence) | `src/CSharpForgeWatcher.Application/Configuration/SourceControlProviderExtensions.cs` |
| Plomberie HTTP mutualisée (pagination `Link`, `snake_case`, erreurs, parallélisme borné) | `src/CSharpForgeWatcher.Infrastructure/SourceControl/RestGatewayBase.cs` |
| Adaptateurs existants | `Infrastructure/AzureDevOps/`, `Infrastructure/GitHub/`, `Infrastructure/GitLab/` |
| Comptes surveillés (un par forge, ou plusieurs par forge) | `src/CSharpForgeWatcher.Application/Configuration/WatchedAccount.cs` |
| Double de test du port | `tests/CSharpForgeWatcher.Tests/Doubles/FakeSourceControlGateway.cs` |

**Déjà en place** : le réglage `Provider`, porté par **chaque compte** (SPEC-CFG-008), avec
les valeurs `AzureDevOps`, `GitHub` et `GitLab`, sa recopie dans `Clone()`, sa validation
contre la liste `SourceControlProviderExtensions.Implemented`, les deux aiguillages ci-dessus,
et le choix de la forge dans la fenêtre d'édition d'un compte.

**Le chemin est donc balisé** : GitHub puis GitLab ont été ajoutés ainsi, et l'expérience est
consignée dans [`docs/adr/0004-adaptateur-github.md`](../../../docs/adr/0004-adaptateur-github.md)
— à lire avant de commencer, ses quatre décisions se reposeront presque à l'identique. Pour une
quatrième forge, il reste à ajouter la valeur à `Implemented`, ses libellés, son adaptateur et
son générateur de liens ; si son API est du même genre (REST, `snake_case`, pagination `Link`),
hériter de `RestGatewayBase` réduit l'adaptateur à ses points d'entrée. Vérifier l'état réel
des fichiers avant d'écrire : le dépôt évolue.

## 1. Spécifier avant de coder

* compléter `docs/specs/SPEC-FORGES.md` : la ligne de la forge dans le tableau des formats
  d'URL (SPEC-FORGE-003) et ses limites éventuelles (une forge sans pipelines retourne des
  listes vides, cf. SPEC-FORGE-004) ;
* si la forge impose un choix structurant — authentification différente d'un jeton
  personnel, pagination, absence d'une des six questions du contrat —, écrire un ADR dans
  `docs/adr/` (numérotation continue) ;
* mettre à jour `docs/specs/SPEC-CONFIGURATION.md` si un champ de configuration apparaît.

## 2. Le réglage `Provider`

L'énumération et la propriété existent déjà dans
`src/CSharpForgeWatcher.Application/Configuration/WatcherConfiguration.cs`. La validation refuse
toute valeur absente de la liste des forges implémentées :

```csharp
public static readonly IReadOnlyList<SourceControlProvider> Implemented =
[
    SourceControlProvider.AzureDevOps,
    SourceControlProvider.GitHub,
    SourceControlProvider.GitLab,
];
```

**Ce qu'il faut modifier** : ajouter la valeur à cette liste, et lui donner ses libellés dans
le même fichier — `ToLabel`, `UrlLabel`, `UrlPlaceholder`, `ScopeLabel`, `TokenScopeHint`,
`TokenPageUrl`. C'est ce qui fait que l'onglet *Connexion* s'annonce correctement sans
qu'aucun fichier WinForms soit touché : « URL de l'organisation » n'a de sens que pour
Azure DevOps.

Vérifier aussi que `Provider` est bien recopié dans `Clone()` — **oubli classique, et
silencieux**, la fenêtre de configuration travaillant sur une copie.

Le message de refus doit rester explicite : SPEC-FORGE-002 exige un refus à la validation
plutôt qu'un échec réseau incompréhensible. Ajouter un test dans
`tests/CSharpForgeWatcher.Tests/Configuration/WatcherConfigurationTests.cs`, catégorie
`SPEC-FORGE-002`. Les enums étant sérialisés **par leur nom**, un `config.json` existant
reste lisible.

## 3. L'adaptateur — `src/CSharpForgeWatcher.Infrastructure/<Forge>/`

Calquer la structure existante, elle sépare trois responsabilités qui ne doivent pas se
mélanger :

| Fichier | Responsabilité |
|---|---|
| `<Forge>RestGateway.cs` | points d'entrée, en-têtes d'authentification, messages d'erreur propres à la forge. Hériter de `RestGatewayBase` si l'API est en `snake_case` et paginée par `Link` : requêtes, pagination, désérialisation et parallélisme borné y sont déjà |
| `<Forge>Mapper.cs` | JSON → types du domaine ; c'est **ici et nulle part ailleurs** que le vocabulaire de la forge est traduit |
| `Dtos/<Forge>Dtos.cs` | `record` de désérialisation, au plus près de la charge utile réelle |
| `<Forge>GatewayFactory.cs` | mise en cache par `SourceControlConnection.CacheKey` et enveloppe `ResilientSourceControlGateway` |

Points de vigilance :

* **lecture seule** : n'implémenter aucune méthode d'écriture, ne demander aucune portée de
  jeton en écriture (SPEC-FORGE-001 et SDD §6) ;
* **traduction du vocabulaire** : une *merge request* GitLab devient une `PullRequest`, une
  *note* devient un `Comment`, une *approbation* devient un `ReviewerVote`. Le domaine ne
  doit jamais voir le mot d'origine (SPEC-FORGE-005) ;
* **identité stable** : `RepositoryRef` est identifiée par `RepositoryId`, pas par son nom,
  pour survivre à un renommage ;
* **mention de l'utilisateur** : Azure DevOps sérialise les mentions en `@<GUID>`, GitHub en
  `@login`. La règle de détection compare avec `context.ViewerId` — c'est donc au mappeur de
  fournir un `ViewerId` **cohérent avec ce que contient le texte des commentaires**, sinon
  SPEC-EVT-006 ne détecte plus rien, et sans le moindre message d'erreur. L'adaptateur GitHub
  emploie pour cette raison le `login` comme identité, y compris dans les instantanés
  (ADR-0004, décision 2) ;
* **identifiants numériques** : ils sont des `long` dans tout le domaine (SPEC-FORGE-006).
  Vérifier l'ordre de grandeur de ceux de la forge visée avant de supposer quoi que ce soit :
  ceux de GitHub Actions dépassent les dix chiffres ;
* **adresse d'un message** : si la forge fournit l'ancre exacte d'un commentaire, la placer
  dans `Comment.Url` / `CommentThread.Url` plutôt que de la faire reconstruire
  (SPEC-LINK-004) ;
* **capacité absente** : ne pas la simuler. Retourner une valeur neutre — liste vide, état
  `Unknown` — pour que la règle concernée se taise d'elle-même, et **consigner la limite**
  dans SPEC-FORGE-007 (SPEC-FORGE-007) ;
* **erreurs** : reprendre exactement le classement attendu par SPEC-POLL-004 et
  SPEC-POLL-005 — authentification et `404` ne sont **jamais** réessayés, `429` et `5xx` le
  sont. Le décorateur de résilience est déjà écrit, ne pas le dupliquer. Attention aux forges
  qui détournent un code : GitHub signale un quota épuisé par un **403**, que
  `GitHubRestGateway` reclasse en 429 après lecture de `x-ratelimit-remaining` — sans quoi une
  limite de débit passerait pour un jeton invalide, et ne serait jamais réessayée ;
* **pagination** : vérifier comment la forge la signale (GitHub : en-tête `Link`), et borner
  le nombre de pages suivies en journalisant l'atteinte de la borne — une troncature
  silencieuse se lit comme une liste complète.

## 4. Le générateur de liens — `src/CSharpForgeWatcher.Application/Links/<Forge>LinkBuilder.cs`

Implémenter `IPullRequestLinkBuilder` (quatre méthodes : pull request, discussion, liste des
PR d'un dépôt, exécution de pipeline). Les formats attendus sont dans le tableau de
SPEC-FORGE-003. Reprendre les deux invariants de l'implémentation existante :

* l'URL de base est fournie par un `Func<string>`, jamais figée, pour rester valable après
  un changement de configuration à chaud (SPEC-CFG-004) ;
* les segments variables passent par `Uri.EscapeDataString`.

Tests dans `tests/CSharpForgeWatcher.Tests/Links/`, catégories `SPEC-LINK-*` et
`SPEC-FORGE-003`, en vérifiant l'URL **exacte** — c'est le seul endroit du dépôt où une
chaîne littérale complète est la bonne façon de tester.

## 5. La sélection

Deux points de bascule, et deux seulement — tous deux existent déjà, il n'y a qu'un `case` à
y ajouter :

1. `src/CSharpForgeWatcher.Infrastructure/SourceControl/ProviderGatewayFactory.cs` : selon
   `connection.Provider`, retourner l'adaptateur voulu. Ne pas oublier d'enregistrer la
   nouvelle fabrique dans `InfrastructureServiceCollectionExtensions` ;
2. `src/CSharpForgeWatcher.Application/Links/ProviderAwareLinkBuilder.cs` : le générateur de
   liens correspondant.

Les deux `switch` doivent rester **exhaustifs et explicites** : un cas par défaut qui lève
avec un message clair, jamais un repli silencieux sur Azure DevOps — il produirait des liens
plausibles menant nulle part, et des appels vers le mauvais serveur.

La sélection est refaite **à chaque appel**, jamais figée à la construction : c'est ce qui rend
le changement de forge effectif sans redémarrage (SPEC-CFG-004).

Aucun autre fichier n'est concerné. Si le `PullRequestMonitor`, une règle de détection ou
une vue doit être modifiée pour accueillir la forge, c'est le signe qu'une notion propre à
la forge a fui hors de l'adaptateur : corriger le mappeur plutôt que le métier.

## 6. Interface

Rien à écrire dans `SettingsForm` si les libellés de `SourceControlProviderExtensions` sont
renseignés (§2) : l'onglet *Connexion* lit la liste des forges implémentées, et le test de
connexion passe par le port. Ne toucher à la vue que si la forge exige un champ
supplémentaire — ce qui mérite alors une entrée dans SPEC-CONFIGURATION.

## 7. Refermer la boucle

- [ ] tests verts, y compris les nouvelles catégories `SPEC-FORGE-*`
- [ ] `docs/TRACEABILITE.md` complété
- [ ] `CHANGELOG.md` complété
- [ ] `README.md` : la forge apparaît dans la création de jeton, la configuration et le
      dépannage ; la liste des forges de *Limites connues* est corrigée
- [ ] `docs/SDD.md` §5.4 (coût des appels), §6 (portées de jeton), §8 et §10 (ADR)
- [ ] un scénario par nouvelle spec dans `docs/features/forges.feature`, sans quoi les tests
      de garde de `FeatureCoverageTests` échouent — dans un sens comme dans l'autre
- [ ] `FeatureCoverageTests.VerificationManuelleOuAVenir` : retirer les identifiants
      désormais couverts par un test
- [ ] checklist du skill `verifier-avant-commit`, puis contrôles du skill
      `respecter-architecture` — en particulier : aucun `HttpClient` hors de
      `Infrastructure`, aucun nom de forge dans `Domain`
