---
name: respecter-architecture
description: "Règles de dépendance de la clean architecture de Forge Watcher (Domain, Application, Infrastructure, Ui), interdits par couche et façon de corriger une violation. À charger avant de créer un fichier, d'ajouter un using, une référence de projet ou un paquet NuGet, avant de faire un appel réseau, de lire un fichier, de lire l'horloge ou de toucher à Windows — et pour arbitrer « dans quelle couche mettre ce code ? »."
---

# Respecter l'architecture

Quatre projets, une seule direction de dépendance : **vers l'intérieur**. Toute la
testabilité du dépôt en découle — le cœur métier se teste sans Windows, sans réseau et
sans disque, et c'est ce qui rend les tests instantanés.

```
Ui ──────────► Application ──────────► Domain
 │                  ▲
 └─► Infrastructure ┘   (implémente les ports déclarés par Application)
```

## Ce que chaque couche a le droit de connaître

| Couche | Chemin | Références autorisées | Rôle |
|---|---|---|---|
| **Domain** | `src/CSharpForgeWatcher.Domain/` | **rien** — ni projet, ni paquet NuGet | entités, objets-valeur, énumérations, instantanés, règles métier pures |
| **Application** | `src/CSharpForgeWatcher.Application/` | `Domain` + `Microsoft.Extensions.{DependencyInjection,Logging}.Abstractions` | cas d'usage, détection, politique de notification, **et les ports** (`Abstractions/`) |
| **Infrastructure** | `src/CSharpForgeWatcher.Infrastructure/` | `Application` (donc `Domain`) | adaptateurs concrets — REST, JSON sur disque, DPAPI, registre, journal fichier |
| **Ui** | `src/CSharpForgeWatcher.Ui/` | `Application` + `Infrastructure` | WinForms, zone de notification, toasts, **racine de composition** |
| **Tests** | `tests/CSharpForgeWatcher.Tests/` | `Domain` + `Application` **uniquement** | NUnit ; les ports sont remplacés par les doubles de `Doubles/` |

Le seul endroit qui connaît à la fois un port et son implémentation est
`src/CSharpForgeWatcher.Ui/Composition/ServiceRegistration.cs`. Chaque couche déclare son
propre assemblage (`AddForgeWatcherApplication`, `AddForgeWatcherInfrastructure`) ; la racine les
appelle et fournit ce qui reste.

## Les interdits, et quoi faire à la place

| Interdit | Pourquoi | Correction |
|---|---|---|
| `HttpClient`, `HttpRequestMessage`, une URL d'API ailleurs que derrière `ISourceControlGateway` | SPEC-FORGE-001 : une seule porte de sortie vers la forge, sinon plus rien n'est remplaçable ni testable | ajouter la méthode au port `src/CSharpForgeWatcher.Application/Abstractions/ISourceControlGateway.cs`, l'implémenter dans **chaque** adaptateur (`Infrastructure/AzureDevOps/`, `Infrastructure/GitHub/`), l'ajouter au double `Doubles/FakeSourceControlGateway.cs` |
| `System.Windows.Forms`, `System.Drawing`, `MessageBox`, un `Form` hors de `Ui` | le métier doit tourner sans interface (service Windows, autre UI, tests) | remonter la décision dans `Application` et ne laisser dans `Ui` que l'affichage ; pour notifier, passer par `INotificationPresenter` |
| `File`, `Directory`, `Path.Combine` vers un emplacement réel, `%APPDATA%` hors de `Infrastructure` | la persistance est un détail derrière `IConfigurationStore` / `IMonitorStateStore` | utiliser le port ; les emplacements se centralisent dans `Infrastructure/Persistence/AppPaths.cs` |
| `Registry`, DPAPI, `Process.Start` hors de `Infrastructure` | dépendances Windows non testables | ports existants : `IAutoStartService`, `ISecretProtector`, `IBrowserLauncher` |
| `DateTime.Now`, `DateTimeOffset.UtcNow` dans `Domain` ou `Application` | ADR-0003 : la détection compare des instantanés, jamais des dates d'horloge — un décalage d'horloge créerait doublons et trous | injecter `IClock` ; dans une règle de détection, utiliser `context.ObservedOn` |
| `Task.Delay` dans `Application` | rendrait les tests de réessai lents | `IDelayScheduler` |
| un secret en clair — dans le code, un test, la configuration, la doc, un log | ADR-0002 ; SPEC-CFG-001 | le jeton ne circule qu'en `SourceControlConnection`, chiffré au repos par `DpapiSecretProtector` ; **seul `ConfigurationService` le déchiffre** (`TokenOf`), les tests utilisent `https://dev.azure.com/contoso` et des GUID factices |
| injecter `IPullRequestLinkBuilder` par le conteneur | il n'y en a plus **un** : chaque compte a sa forge et son serveur (SPEC-CFG-008), et l'enregistrement global a disparu | obtenir le générateur du compte concerné : `account.CreateLinkBuilder()`. Un `GetRequiredService<IPullRequestLinkBuilder>()` échouera à la validation du conteneur |
| lire l'état surveillé sans passer par un compte | `state.json` est cloisonné : `MonitorSnapshot` ne porte plus de PR, seulement des comptes | `state.ForAccount(account.Id)` retourne l'`AccountSnapshot`, qui porte `Find`, `Put`, `PruneRepositoriesOutside`… |
| un paquet NuGet ajouté à `Domain` | la contrainte est absolue et documentée dans le `.csproj` | si le besoin semble réel, c'est que le code n'appartient pas au domaine |
| `Infrastructure` ou `Ui` référencé par `tests` | on ne teste pas les adaptateurs Windows en unitaire | tester la couche `Application` avec un double ; ce qui reste non couvert est déclaré dans `docs/TRACEABILITE.md` |
| vocabulaire propre à une forge dans `Domain` (`MergeRequest`, `WorkflowRun`, `Review`) | SPEC-FORGE-005 | garder *pull request*, *discussion*, *vote*, *pipeline* ; la traduction est le travail du mappeur d'infrastructure |
| `int` pour un identifiant attribué par la forge | SPEC-FORGE-006 : ceux de GitHub Actions dépassent les 32 bits, et un débordement fait passer un élément pour « déjà vu » — donc jamais notifié | `long` dans le domaine, le port, les instantanés et les générateurs de liens. Seul le **numéro** d'une pull request reste un `int` |

## Où placer un code nouveau — arbitrage en trois questions

1. **Est-ce une notion métier, indépendante de tout outil ?** → `Domain`. Test : la phrase
   qui la décrit reste vraie sans nommer Azure DevOps, Windows ni JSON.
2. **Est-ce une décision, une orchestration, une politique ?** → `Application`. Si elle a
   besoin du monde extérieur, elle le demande par un **port** qu'elle déclare elle-même
   dans `Abstractions/` — jamais l'inverse.
3. **Est-ce une technique — protocole, format, système d'exploitation ?** →
   `Infrastructure`, en implémentant un port existant. Reste dans `Ui` uniquement ce qui
   se voit et se clique.

Si la réponse est « un peu des deux », c'est que deux responsabilités sont mélangées :
séparer avant de coder.

## Vérifier une modification

Depuis la racine du dépôt. **Aucune sortie** est le résultat attendu pour les quatre
premières commandes.

```bash
FILTRE="--include=*.cs --exclude-dir=bin --exclude-dir=obj"

# 1. Interface graphique hors de la couche Ui
grep -rn $FILTRE "System.Windows.Forms\|System.Drawing" \
  src/CSharpForgeWatcher.Domain src/CSharpForgeWatcher.Application src/CSharpForgeWatcher.Infrastructure tests

# 2. Appel HTTP hors de la couche Infrastructure
grep -rn $FILTRE "HttpClient\|HttpRequestMessage" \
  src/CSharpForgeWatcher.Domain src/CSharpForgeWatcher.Application src/CSharpForgeWatcher.Ui tests

# 3. Disque et registre hors de la couche Infrastructure
grep -rn $FILTRE "System\.IO\|File\.\|Directory\.\|Registry\." \
  src/CSharpForgeWatcher.Domain src/CSharpForgeWatcher.Application

# 4. Horloge réelle dans le métier
grep -rn $FILTRE "DateTime\.Now\|DateTimeOffset\.Now\|DateTime\.UtcNow\|DateTimeOffset\.UtcNow" \
  src/CSharpForgeWatcher.Domain src/CSharpForgeWatcher.Application

# 5. Graphe de dépendances : à comparer au tableau ci-dessus
grep -rn "ProjectReference\|PackageReference" src tests --include=*.csproj
```

La compilation confirme le reste : une référence interdite ne compile pas, faute de
`ProjectReference`. Le vrai risque n'est donc pas la référence de projet — c'est le code
technique glissé dans la bonne couche par facilité.

## Signaux d'alerte dans une revue

* un `using` d'infrastructure au sommet d'un fichier de `Application` ;
* un port ajouté dans `Infrastructure` au lieu de `Application/Abstractions` — la
  dépendance est alors inversée dans le mauvais sens ;
* une classe de `Application` impossible à instancier dans un test sans monter un vrai
  client HTTP, un fichier ou un formulaire ;
* une règle de détection qui a besoin d'un `await` : les règles sont synchrones et pures,
  toute lecture supplémentaire relève du monitor ;
* `catch (HttpRequestException)` hors de `Infrastructure` : les échecs remontent en
  `SourceControlException`, classée à la frontière, pour que les couches hautes n'aient
  jamais à connaître HTTP.
