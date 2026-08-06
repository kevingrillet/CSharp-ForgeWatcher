# ADR-0006 — Renommage en Forge Watcher, et reprise de l'ancienne identité

* **Statut** : accepté
* **Contexte** : le nom `PrWatcher` datait d'une application qui ne faisait qu'une chose,
  regarder des pull requests Azure DevOps. Elle surveille aujourd'hui aussi les **pipelines**,
  sur **trois forges**, à travers **plusieurs comptes simultanés** — la moitié du travail
  n'était plus annoncée par le nom. Le décalage se voyait dans le dépôt lui-même : le code, les
  specs et les skills disent déjà *forge* partout (`SPEC-FORGES`, `ISourceControlGateway`,
  `ajouter-une-forge`), et le README affirme que « l'application n'écrit jamais dans la forge ».

## Options considérées

| Option | Pour | Contre |
|---|---|---|
| **`ForgeWatcher`** | Reprend le mot déjà employé partout dans le domaine ; couvre PR, pipelines et ce qui viendra (work items, releases) sans renommer une seconde fois ; garde `Watcher`, qui dit juste la lecture seule et le sondage | Un renommage complet, avec une identité runtime à reprendre |
| `DevOpsWatcher` | Compréhensible hors contexte | Suggère Azure DevOps alors que l'outil est multi-forge, et entre en collision avec le projet voisin `CSharp-AzureDevopsNotifier` |
| Ne renommer que le nom affiché | Coût quasi nul | Laisse l'incohérence dans le code, les chemins et le dépôt : le prochain lecteur croira que l'outil ne suit que des PR |
| Ne rien changer | Aucun coût | Le nom continue de sous-vendre l'outil, et le coût du renommage ne fera que croître avec la base installée |

## Décision 1 — Le renommage va jusqu'au bout

Dépôt `CSharp-ForgeWatcher`, espaces de noms `CSharpForgeWatcher.*`, exécutable
`ForgeWatcher.exe`, dossier de données `%APPDATA%\ForgeWatcher`, logo `forge-watcher.svg`.
Un renommage partiel aurait laissé au lecteur le soin de deviner quel nom fait foi.

Seule exception : les entrées **déjà publiées** du CHANGELOG gardent les noms de leur époque.
Un journal des modifications qui se réécrit ne documente plus rien.

## Décision 2 — L'ancienne identité est reprise, pas abandonnée

Le renommage déplace trois choses que Windows et l'utilisateur voient. Sans reprise,
l'utilisateur retrouverait une application vierge et une entrée de démarrage fantôme.

| Élément | Reprise |
|---|---|
| `%APPDATA%\PrWatcher\` | Déplacé vers `%APPDATA%\ForgeWatcher\`, **sauf** si le nouveau dossier existe déjà — il signifierait que la nouvelle version a déjà tourné, et l'écraser ferait perdre ce qu'elle a appris |
| Valeur `PrWatcher` de la clé `Run` | Réinscrite sous `ForgeWatcher` avec le chemin du processus **courant** — l'ancienne désigne un exécutable qui n'existe plus —, puis supprimée |
| Raccourci `PrWatcher.lnk` du menu Démarrer | Supprimé ; celui du nouveau nom est recréé au premier toast par la bibliothèque de notifications |

Le jeton n'a pas à être ressaisi : DPAPI est lié au compte Windows, pas au nom de
l'application (ADR-0002). Déplacer le dossier suffit à tout conserver — y compris l'état
surveillé, donc **aucune notification d'historique n'est rejouée**.

## Décision 3 — La reprise a lieu avant le conteneur, et ne peut pas faire échouer le démarrage

Elle s'exécute en tout premier dans `Program.Main` : ouvrir le journal ou lire la
configuration créerait le nouveau dossier, et la reprise renoncerait en croyant à une
installation neuve. N'ayant donc pas encore de journal à sa disposition, elle rend compte par
un `LegacyMigrationReport` que l'appelant journalise une fois le service disponible.

Chaque étape est indépendante et rattrape ses erreurs : un dossier verrouillé ou un registre
inaccessible produit un avertissement, jamais un échec de démarrage. La reprise est
idempotente — relancée, elle ne trouve plus rien.

## Conséquences

* Un reste inévitable : la bibliothèque de toasts dérive son `AppUserModelId` du chemin de
  l'exécutable. L'enregistrement de l'ancien nom subsiste sous
  `HKCU\Software\Classes\AppUserModelId`, orphelin et inerte — il n'est pas supprimable de
  façon fiable, faute de pouvoir recalculer l'identifiant de l'ancienne installation.
* Les anciens toasts encore présents dans le centre de notifications ne sont plus cliquables :
  ils désignent un serveur COM qui n'existe plus. Ils expirent d'eux-mêmes.
* Le code de reprise a une durée de vie limitée : il pourra disparaître quand plus aucune
  installation antérieure à ce renommage ne sera en service.
* La reprise touche le disque et le registre : elle n'est pas couverte par les tests
  automatiques, comme le reste des adaptateurs Windows (`docs/TRACEABILITE.md`).
