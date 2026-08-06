# CSharp-ForgeWatcher

Application Windows résidente (zone de notification) qui **surveille vos pull requests et vos
pipelines** sur **Azure DevOps**, **GitHub** et **GitLab** — au besoin **les trois en même
temps** —, à travers autant d'espaces et de dépôts que vous voulez, et vous **notifie
immédiatement** de ce qui vous concerne. Un clic sur la notification ouvre **directement le bon
endroit** dans le navigateur : la pull request, la discussion exacte, ou l'exécution de
pipeline en échec.

```
┌─ Forge Watcher (Camille) ─────────────────────┐
│  12 PR suivie(s) · 4 pipeline(s) dont 1    │
│  en échec                                  │
├────────────────────────────────────────────┤
│  Rafraîchir maintenant                     │
│  Activité récente (3 non lu(s))…           │
│  Pull requests suivies              ▸      │
│  Pipelines (1 en échec)             ▸      │
├────────────────────────────────────────────┤
│  Tout marquer comme lu                     │
│  Paramètres…                               │
│  Quitter                                   │
└────────────────────────────────────────────┘
```

*Interface en français ou en anglais. Thème clair, sombre ou automatique. Lecture seule :
l'application n'écrit jamais dans la forge.*

---

## Sommaire

1. [Ce qui est notifié](#ce-qui-est-notifié)
2. [Démarrage rapide](#démarrage-rapide)
3. [Configuration](#configuration)
4. [Comment ça marche](#comment-ça-marche)
5. [Architecture du code](#architecture-du-code)
6. [Tests et documentation vivante](#tests-et-documentation-vivante)
7. [Contribuer et faire évoluer](#contribuer-et-faire-évoluer)
8. [Intégration continue](#intégration-continue)
9. [Publier une version distribuable](#publier-une-version-distribuable)
10. [Dépannage](#dépannage)
11. [Documentation de conception](#documentation-de-conception)
12. [Limites connues](#limites-connues)

---

## Ce qui est notifié

Chaque type est activable ou désactivable indépendamment.

| Notification | Quand | Le clic ouvre |
|---|---|---|
| **Vous êtes mentionné** | Quelqu'un écrit `@vous` dans un commentaire | la discussion |
| **Réponse à votre commentaire** | Quelqu'un répond dans une discussion où vous avez écrit | la discussion |
| **Commentaire sur votre PR** | Nouveau commentaire sur une PR dont vous êtes l'auteur | la discussion |
| **Commentaire sur une PR que vous relisez** | Nouveau commentaire sur une PR dont vous êtes relecteur | la discussion |
| **Vote sur votre PR** | Un relecteur approuve, rejette ou attend une correction | la PR |
| **Vous êtes relecteur** | On vous ajoute comme relecteur | la PR |
| **Pipeline en échec** | Une exécution d'un pipeline surveillé se termine en échec | l'exécution |
| **Discussion mise à jour** | Une discussion qui vous concerne est résolue ou réactivée | la discussion |
| **État de PR modifié** | PR complétée, abandonnée, ou brouillon publié | la PR |
| **Pipeline de nouveau au vert** | Le pipeline repasse en succès après un échec | l'exécution |
| **Nouvelle PR** | Une PR est créée dans un dépôt surveillé | la PR |

Un même fait n'est notifié **qu'une fois**, sous son intitulé le plus précis : une mention
dans une réponse sur votre PR donne une notification « Vous êtes mentionné », pas trois.

---

## Démarrage rapide

### Prérequis

* Windows 10 (1809+) ou Windows 11
* [SDK .NET 9](https://dotnet.microsoft.com/download) **≥ 9.0.200** pour compiler
  (nécessaire pour lire la solution `.slnx`). L'exécutable publié n'a besoin que du
  *runtime*, ou de rien du tout en mode autonome.

### 1. Créer un jeton d'accès personnel (PAT)

**Sur Azure DevOps**

1. Ouvrez `https://dev.azure.com/{votre-organisation}/_usersSettings/tokens`
2. **New Token**
3. Portées : **Code → Read** (pull requests), et **Build → Read** si vous surveillez des
   pipelines. L'application ne fait que lire ; elle n'écrit jamais.
4. Copiez le jeton (il ne sera plus affiché ensuite).

**Sur GitHub**

1. Ouvrez `https://github.com/settings/tokens?type=beta` (jeton *fine-grained*, recommandé)
2. Sélectionnez les dépôts concernés, puis les autorisations en **lecture seule** :
   *Metadata*, *Pull requests*, et *Actions* si vous surveillez des workflows. Ajoutez
   *Organization permissions → Members: Read-only* pour que vos organisations apparaissent
   dans la liste.
3. Un jeton **classique** fonctionne aussi (`repo` — ou `public_repo` — plus `read:org`),
   mais GitHub n'y propose aucune portée « lecture seule » sur le code : le jeton
   *fine-grained* est le seul à respecter réellement le moindre privilège.

**Sur GitLab**

1. Ouvrez `https://gitlab.com/-/user_settings/personal_access_tokens` (ou la page équivalente
   de votre instance)
2. Portée : **`read_api`**, et rien de plus. C'est la seule des trois forges à offrir une
   portée réellement limitée à la lecture.

L'application n'émet que des `GET`, quelle que soit la forge.

Vous pouvez déclarer **plusieurs comptes**, y compris sur la même forge (un jeton personnel et
un jeton professionnel, par exemple) : ils sont tous surveillés au cours du même cycle.

### 2. Compiler et lancer

```powershell
git clone <url-du-depot>
cd CSharp-ForgeWatcher

dotnet build                      # compile (les avertissements sont des erreurs)
dotnet test                       # tests unitaires
dotnet run --project src/CSharpForgeWatcher.Ui
```

Au premier lancement, la fenêtre de paramètres s'ouvre (rien n'est encore configuré).

### 3. Configurer

1. **Comptes** → *Ajouter…*, choisissez la **forge**, saisissez son URL et le jeton, puis
   *Tester la connexion* : votre nom doit s'afficher. Répétez pour chaque forge à surveiller.
   * Azure DevOps : l'URL de l'organisation, `https://dev.azure.com/mon-organisation`
   * GitHub : la racine du serveur, `https://github.com` — ou celle de votre instance
     GitHub Enterprise. L'adresse de l'API en est déduite.
   * GitLab : la racine du serveur, `https://gitlab.com` — ou celle de votre instance.
2. **Dépôts** → dépliez un compte, puis un projet (Azure DevOps), un propriétaire (GitHub) ou
   un groupe (GitLab), et cochez les dépôts à surveiller.
3. **Pipelines** → même principe, si vous voulez être alerté des échecs de build. Sur GitHub,
   les pipelines sont les *workflows* Actions, listés dépôt par dépôt ; sur GitLab, chaque
   projet porte le sien.
4. **Préférences** → thème, intervalle, types de notifications.
5. **Enregistrer**. La surveillance démarre immédiatement.

Le premier cycle est **silencieux** : il apprend l'état courant sans vous inonder de
notifications sur l'historique existant. Les notifications commencent au cycle suivant.

---

## Configuration

### Les cinq onglets

| Onglet | Contenu |
|---|---|
| **Comptes** | Liste des comptes de forge (ajouter, modifier, retirer, désactiver) ; un compte = une forge, une URL, un jeton, une sélection |
| **Dépôts** | Arborescence comptes → espaces → dépôts (chargement à la demande à chaque niveau), filtre, liste des dépôts surveillés |
| **Pipelines** | Même arborescence pour les définitions de pipeline |
| **Préférences** | Langue, thème, activation par type de notification, intervalle, seuil de synthèse, portée des discussions, son, démarrage avec Windows |
| **Avancé** | Chemins des fichiers, test de notification, réinitialisation de l'état surveillé |

### Réglages qui méritent une explication

| Réglage | Effet |
|---|---|
| **Langue de l'interface** | *Français*, *English*, ou *Automatique* (suit Windows ; toute langue autre que le français donne l'anglais). Le menu de la zone de notification suit immédiatement, les fenêtres à leur prochaine ouverture. Les dates et les nombres gardent le format de vos paramètres régionaux, et le journal reste en français. |
| **Thème de l'interface** | *Clair*, *Sombre*, ou *Automatique* (suit Windows). Appliqué immédiatement ; seules la barre de titre et les zones peintes par Windows attendent un redémarrage. |
| **Intervalle de surveillance** | Temps entre deux cycles (défaut 180 s, minimum 30 s). |
| **Notifications individuelles maximum par cycle** | Au-delà (défaut 5), une **synthèse** unique remplace la rafale ; tout reste listé dans *Activité récente*. |
| **Lecture des discussions** | `Seulement les PR qui me concernent` (défaut, économe) ou `Toutes les PR des dépôts surveillés` (aucun angle mort, plus d'appels). |
| **Relire les discussions des autres PR toutes les N minutes** | Permet de détecter que vous venez de commenter une PR qui ne vous concernait pas (défaut 30 min). |
| **Me notifier de mes propres actions** | Désactivé par défaut : vos propres commentaires et votes ne vous notifient pas. |

### Fichiers

| Fichier | Rôle |
|---|---|
| `%APPDATA%\ForgeWatcher\config.json` | Configuration (PAT **chiffré** par DPAPI) |
| `%APPDATA%\ForgeWatcher\state.json` | Mémoire de surveillance (ce qui a déjà été vu) |
| `%APPDATA%\ForgeWatcher\log.txt` | Journal, rotation à 1 Mo |

Le PAT est chiffré avec la clé du compte Windows courant : copier `config.json` sur une
autre machine ne divulgue rien (le jeton devra être ressaisi).

> **Vous veniez de PR Watcher ?** L'application s'appelait ainsi jusqu'à la version 1.1.0.
> Au premier lancement, le contenu de `%APPDATA%\PrWatcher` est repris automatiquement, le
> démarrage avec Windows est réinscrit sous le nouveau nom et le raccourci de l'ancien est
> retiré du menu Démarrer. Rien à ressaisir, rien à recocher, aucune notification rejouée
> ([ADR-0006](docs/adr/0006-renommage-forge-watcher.md)).

---

## Comment ça marche

À chaque cycle, l'application :

1. identifie l'utilisateur (`_apis/connectionData`) ;
2. lit les **PR actives** de chaque dépôt surveillé, en parallèle borné (6 appels max) ;
3. relit individuellement les PR connues qui ont disparu de la liste active, pour connaître
   leur état final (complétée / abandonnée) ;
4. lit les **discussions** des PR retenues selon la portée configurée ;
5. lit les **exécutions de pipeline**, à raison d'**un seul appel par espace** quel que
   soit le nombre de pipelines surveillés ;
6. **compare** au dernier instantané mémorisé et en déduit les événements ;
7. filtre selon vos préférences et notifie ;
8. enregistre le nouvel instantané.

La détection repose sur un **diff d'instantané persistant**, pas sur des dates
([ADR-0003](docs/adr/0003-detection-par-diff-instantane.md)). Conséquences concrètes :
redémarrer l'application ne rejoue pas les anciennes notifications, une coupure réseau de
plusieurs heures est rattrapée d'un coup, et aucun décalage d'horloge ne peut créer de
doublon ni de trou.

Un dépôt ou un projet inaccessible (droits, suppression) devient un simple avertissement :
le reste continue d'être surveillé, et l'état de l'élément en erreur est **conservé** — sinon
ses PR sembleraient avoir disparu.

Coût typique : ~20 requêtes par cycle pour 8 dépôts / 40 PR actives, soit ~400 requêtes par
jour à 3 minutes d'intervalle.

---

## Architecture du code

Clean architecture, quatre projets. **Les dépendances pointent toujours vers l'intérieur.**

```
CSharp-ForgeWatcher.slnx
├─ src/
│  ├─ CSharpForgeWatcher.Domain          ← aucune dépendance (ni NuGet, ni projet)
│  │   Text/TextRef, Text/TextKeys ........ un message = une clé + des arguments
│  │   PullRequest, CommentThread, Reviewer, PipelineRun,
│  │   INotifiableEvent, PullRequestEvent, PipelineEvent, MonitorSnapshot…
│  │
│  ├─ CSharpForgeWatcher.Application     ← dépend de Domain
│  │   Monitoring/PullRequestMonitor ....... le cas d'usage « sonder »
│  │   Detection/Rules/* .................. une règle = un type d'événement (Strategy)
│  │   Detection/Pipelines/Rules/* ........ idem pour les pipelines
│  │   Notifications/NotificationDispatcher  politique de notification
│  │   Configuration/ConfigurationService .. source de vérité de la config
│  │   Theming/ThemeResolver .............. résolution clair/sombre/auto (pure)
│  │   Text/Strings.resx, Strings.en.resx . les seules phrases du dépôt
│  │   Text/TextCatalogue, LanguageResolver  formulation et choix de la langue
│  │   Resilience/ResilientSourceControlGateway  réessai (Decorator)
│  │   Abstractions/* ..................... les PORTS (interfaces)
│  │
│  ├─ CSharpForgeWatcher.Infrastructure   ← implémente les ports
│  │   AzureDevOps/AzureDevOpsRestGateway ... API REST 7.1, lecture seule
│  │   AzureDevOps/AzureDevOpsMapper ....... JSON → domaine
│  │   GitHub/GitHubRestGateway ............ API REST 2022-11-28, lecture seule
│  │   GitHub/GitHubMapper ................. JSON → domaine
│  │   GitLab/GitLabRestGateway ........... API REST v4, lecture seule
│  │   GitLab/GitLabMapper ................ JSON → domaine
│  │   SourceControl/RestGatewayBase ...... pagination, erreurs, parallélisme borné
│  │   SourceControl/ProviderGatewayFactory  choix de l'adaptateur selon Provider
│  │   Persistence/Json*Store .............. config.json / state.json
│  │   Security/DpapiSecretProtector ....... chiffrement du PAT
│  │   Startup/WindowsServices ............. registre Run, navigateur, thème système
│  │   Startup/LegacyIdentityMigration .... reprise des données de « PR Watcher »
│  │   Logging/FileLoggerProvider .......... log.txt
│  │
│  └─ CSharpForgeWatcher.Ui               ← WinForms + racine de composition
│      Program.cs ......................... point d'entrée, instance unique
│      Composition/ServiceRegistration .... injection de dépendances
│      Tray/TrayApplicationContext ........ icône, menu, minuteur
│      Tray/TrayIconFactory ............... logo + pastille de non-lus
│      Localization/TextService ........... formule les clés dans la langue choisie
│      Theming/ThemeService ............... application du thème aux fenêtres
│      Theming/ThemedTabControl .......... onglets peints par l'application
│      Notifications/* .................... toasts, bulles d'info, repli
│      Views/SettingsForm.* ............... un fichier par onglet (classe partielle)
│      Views/AccountForm, ActivityForm, SelectionTreeBinder
│
├─ tests/CSharpForgeWatcher.Tests        ← NUnit, ne référence que Domain + Application
├─ assets/logo/                       ← SVG maître, .ico, générateur reproductible
├─ docs/                              ← SDD, specs, scénarios Gherkin, ADR, CI, contribution
├─ .claude/                           ← skills, subagents et réglages d'assistance du projet
└─ .mcp.json                          ← serveurs MCP du projet (CodeGraph, Context7)
```

Ce que cette séparation apporte concrètement :

* **le métier est testable sans Windows, sans réseau et sans disque** — les tests tournent
  en quelques centaines de millisecondes ;
* ajouter une forge = une implémentation de `ISourceControlGateway` et un générateur de
  liens, rien de plus — c'est ce qu'a demandé le support de GitHub, sans qu'une seule règle
  de détection change (voir [SPEC-FORGES](docs/specs/SPEC-FORGES.md), [ADR-0004](docs/adr/0004-adaptateur-github.md)
  et le skill `ajouter-une-forge`) ;
* remplacer WinForms par WPF, ou l'app par un service Windows = seule la couche `Ui` change ;
* envoyer les notifications ailleurs (Teams, webhook) = une implémentation de
  `INotificationPresenter`.

### Patrons utilisés

| Patron | Où | Pourquoi |
|---|---|---|
| Ports & Adapters | `Application/Abstractions` ↔ `Infrastructure` | testabilité, remplaçabilité |
| Strategy | `Detection/Rules/*`, `Detection/Pipelines/Rules/*` | un type d'événement isolé par classe |
| Composite | `PullRequestEventDetector`, `PipelineEventDetector` | traiter N règles comme une seule |
| Repository | `IConfigurationStore`, `IMonitorStateStore` | isoler la persistance |
| Factory | `ISourceControlGatewayFactory`, `TrayIconFactory` | la connexion dépend d'une config modifiable à chaud |
| Decorator | `ResilientSourceControlGateway`, `FallbackNotificationPresenter` | réessai et repli sans toucher au code décoré |
| Observer | `ConfigurationService.Changed` | appliquer la config et le thème à chaud |
| Null Object | `DeferredNotificationPresenter` | rompre une dépendance circulaire à l'amorçage |
| Value Object | `PullRequestKey`, `UserRef`, `RepositoryRef`, `PipelineDefinitionRef` | égalité et clés sans ambiguïté |

---

## Tests et documentation vivante

```powershell
dotnet test                                          # tout
dotnet test --filter TestCategory=SPEC-EVT-005       # une spec précise
dotnet test --filter FullyQualifiedName~NewComment   # une classe
```

La démarche est **pilotée par les specs** :

```
docs/specs/SPEC-*.md      ce qui doit se passer, identifié (SPEC-EVT-005…)
docs/features/*.feature   le même comportement raconté en Gherkin français, tagué
tests/                    un test NUnit portant [Category("SPEC-EVT-005")]
```

Les trois restent synchronisés grâce à des **tests de garde** qui échouent si un scénario
cite une spec sans test, ou si une spec testée n'est racontée nulle part. La correspondance
complète est dans [`docs/TRACEABILITE.md`](docs/TRACEABILITE.md).

Les noms de test décrivent le comportement attendu en français, ce qui rend la sortie
lisible comme une liste d'exigences :

```
Une_reponse_dans_une_discussion_ou_jai_ecrit_est_signalee_comme_reponse
Un_depot_illisible_nempeche_pas_les_autres_et_son_etat_est_conserve
Un_seul_appel_par_projet_quel_que_soit_le_nombre_de_pipelines
```

Les doubles de test sont dans `tests/CSharpForgeWatcher.Tests/Doubles/` : `Build`
(constructeurs d'objets), `FakeSourceControlGateway` (données + pannes simulées),
`MonitorHarness` (un moniteur complet monté en une ligne).

---

## Contribuer et faire évoluer

Le guide complet est dans [`docs/CONTRIBUER.md`](docs/CONTRIBUER.md). En résumé : **spec →
scénario Gherkin → test → code → CHANGELOG**.

Le dépôt embarque des **skills** et **subagents** pour Claude Code (dossier `.claude/`) qui
encodent ces conventions :

| Outil | Rôle |
|---|---|
| skill `cadrer-un-comportement` | Transformer une demande floue en spécification rédigeable, avant d'ouvrir `docs/specs/` |
| skill `ajouter-notification` | La procédure exacte pour ajouter un type d'événement notifié |
| skill `ajouter-une-forge` | Brancher GitHub, GitLab ou un serveur auto-hébergé |
| skill `respecter-architecture` | Ce qui est autorisé dans quelle couche, et comment corriger une violation |
| skill `verifier-avant-commit` | La checklist locale (build, format, tests, cohérence des docs) |
| agent `relecteur-architecture` | Relit un diff à la recherche de fuites entre couches |
| agent `relecteur-couverture-spec` | Détecte un comportement modifié sans spec, scénario ni test |
| agent `relecteur-conventions` | Français, doc XML, style NUnit, absence de secret |

### Vérification avant de pousser

```powershell
dotnet build                                        # 0 avertissement exigé
dotnet format CSharp-ForgeWatcher.slnx --verify-no-changes
dotnet test
```

Ces trois commandes sont exactement ce que vérifie l'intégration continue. La tâche VS Code
« tout vérifier » les enchaîne.

---

## Intégration continue

| Plateforme | Fichier | Ce qui est vérifié |
|---|---|---|
| GitHub Actions | `.github/workflows/ci.yml` | restore, build (Release), mise en forme, tests + artefact de résultats |
| GitHub Actions | `.github/workflows/release.yml` | sur tag `v*` : publie l'exe zippé en release |
| GitLab CI | `.gitlab-ci.yml` | mêmes étapes, runner Windows requis (tâche Linux dégradée en option) |

Détails, prérequis de runner et commandes équivalentes en local :
[`docs/CI.md`](docs/CI.md). Dependabot est configuré pour les paquets NuGet et les actions.

> La solution ne compile **que sur Windows** (WinForms, DPAPI, registre) : les runners
> doivent être Windows avec un SDK ≥ 9.0.200. Sur Linux, seuls `Domain`, `Application` et
> les tests sont compilables.

---

## Publier une version distribuable

Le script `scripts/publier.ps1` enchaîne restauration, compilation de **toute** la solution
(les avertissements sont des erreurs : mieux vaut échouer ici qu'en intégration continue) et
publication dans `publish/`, à la racine du dépôt. Le dossier de sortie est vidé au préalable,
pour qu'aucun fichier d'une publication précédente ne parte dans la livraison.

```powershell
.\scripts\publier.ps1                        # version légère : runtime .NET 9 Desktop requis
.\scripts\publier.ps1 -Autonome              # exécutable unique et autonome (~150 Mo)
.\scripts\publier.ps1 -Version 1.2.3         # grave ce numéro dans le binaire
.\scripts\publier.ps1 -Sortie D:\livraisons  # autre dossier de sortie
```

Les tâches VS Code *publier (win-x64)* et *publier (autonome)* appellent ce script.

Sans le script, les deux commandes équivalentes sont :

```powershell
dotnet publish src/CSharpForgeWatcher.Ui -c Release -r win-x64 --self-contained false
dotnet publish src/CSharpForgeWatcher.Ui -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Elles écrivent dans `src/CSharpForgeWatcher.Ui/bin/Release/net9.0-windows10.0.17763.0/win-x64/publish/`
et non dans `publish/`.

Dans les deux cas : copiez le dossier où vous voulez, lancez `ForgeWatcher.exe`, puis cochez
*Démarrer avec Windows* dans les paramètres.

> Si un poste refuse d'exécuter les scripts (`… n'est pas signé numériquement`), lancez-les
> par `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publier.ps1` : le
> contournement ne vaut que pour ce processus.

### Nettoyer le dépôt

`scripts/nettoyer.ps1` ramène le dépôt à l'état « sorti de clone » : `dotnet clean` en Debug
et en Release, puis suppression de tous les `bin/`, `obj/`, `publish/` et `TestResults/`. La
seconde étape est nécessaire — `dotnet clean` ne retire que ce que la dernière compilation a
produit, et laisse les binaires d'un autre framework cible, d'un autre RID ou d'un projet
retiré de la solution.

```powershell
.\scripts\nettoyer.ps1 -WhatIf          # liste ce qui serait supprimé, sans rien effacer
.\scripts\nettoyer.ps1
.\scripts\nettoyer.ps1 -Tout            # + caches d'IDE (.vs) et rapports .trx
.\scripts\nettoyer.ps1 -SansDotnetClean # quand la solution ne compile plus, ou sans SDK
```

Rien de versionné n'est touché : `.git`, `.codegraph` et vos données locales (`config.json`,
`state.json`) sont explicitement épargnés.

> Le chemin de l'exécutable est écrit tel quel dans la clé de démarrage automatique :
> si vous déplacez le dossier, décochez puis recochez l'option.

> **À savoir** : Windows n'accepte de notifications que d'applications identifiées. Au
> premier toast, la bibliothèque de notifications crée donc automatiquement un raccourci
> `ForgeWatcher` dans le menu Démarrer et une entrée `HKCU\Software\Classes\AppUserModelId`.
> C'est ce qui permet aux notifications d'apparaître dans le centre de notifications et de
> rester cliquables même consultées plus tard. Rien n'est écrit ailleurs sur la machine.

### Régénérer le logo

```powershell
dotnet run --project assets/logo/generator/LogoGenerator.csproj
```

Le SVG est la source de vérité ; le `.ico` multi-résolutions en est dérivé. Voir
[`assets/logo/README.md`](assets/logo/README.md).

---

## Dépannage

| Symptôme | Cause probable et solution |
|---|---|
| **Aucune notification n'apparaît** | Onglet *Avancé* → *Tester une notification*. Si rien ne s'affiche, les toasts sont bloqués (assistant de concentration, stratégie de groupe) : l'application bascule automatiquement sur les bulles d'info. Vérifiez aussi *Paramètres Windows → Notifications*. |
| **« … a refusé le jeton (401) »** | PAT expiré ou révoqué : recréez-le et ressaisissez-le dans l'onglet *Connexion*. |
| **« Accès refusé (403) »** | Portées insuffisantes. Azure DevOps : *Code (Read)*, et *Build (Read)* pour les pipelines. GitHub : *Pull requests* et *Actions* en lecture, sur les dépôts concernés. |
| **Les pipelines n'apparaissent pas** | Azure DevOps : le PAT doit inclure *Build → Read* ; les définitions désactivées ne sont pas proposées. GitHub : les workflows sont listés dépôt par dépôt, et ceux désactivés sont ignorés. |
| **Aucune organisation GitHub dans la liste** | Il manque *Members: Read-only* (jeton *fine-grained*) ou `read:org` (jeton classique). Les dépôts personnels restent proposés. |
| **« GitHub limite temporairement les appels »** | Quota horaire épuisé (5 000 requêtes). Augmentez l'intervalle de surveillance, ou réduisez le nombre de dépôts et de workflows suivis. |
| **Après un changement de forge, tout échoue** | Les dépôts et pipelines cochés appartenaient à l'ancienne forge : resélectionnez-les (l'application le propose au changement). |
| **Un dépôt ou un projet apparaît en avertissement** | Menu → *Avertissements* pour le détail. Le reste continue d'être surveillé. |
| **Trop de notifications** | Baissez *Notifications individuelles maximum par cycle*, ou désactivez *Nouvelle PR* et *Commentaire sur une PR que vous relisez*. |
| **Notifications répétées / suspectes** | Onglet *Avancé* → *Réinitialiser l'état surveillé* : le cycle suivant réapprend l'état sans notifier. |
| **L'icône reste grise** | Configuration incomplète : ouvrez les paramètres, le message d'erreur précise ce qui manque. |
| **Le thème sombre laisse la barre de titre claire** | La barre de titre est peinte par Windows : elle suit le thème au **prochain démarrage** de l'application. |
| **Rien ne se passe au lancement** | Une instance tourne déjà (instance unique) : regardez la zone de notification, icônes masquées comprises. |
| **`dotnet build` échoue sur la solution** | SDK trop ancien : le format `.slnx` exige ≥ 9.0.200 (`dotnet --version`). |
| **Diagnostiquer plus finement** | `%APPDATA%\ForgeWatcher\log.txt` (aucun secret n'y est écrit). |

---

## Documentation de conception

| Document | Contenu |
|---|---|
| [`docs/SDD.md`](docs/SDD.md) | Document de conception : contexte, exigences, architecture, coûts, risques |
| [`docs/specs/`](docs/specs/) | Comportements attendus, identifiés : événements, cycle, configuration, notifications, pipelines, apparence, forges |
| [`docs/features/`](docs/features/) | Les mêmes comportements en scénarios Gherkin français |
| [`docs/TRACEABILITE.md`](docs/TRACEABILITE.md) | Correspondance spec → scénario → test, et ce qui n'est pas testé automatiquement |
| [`docs/adr/`](docs/adr/) | Décisions d'architecture et alternatives écartées |
| [`docs/CI.md`](docs/CI.md) | Ce que vérifie chaque pipeline, et comment le reproduire en local |
| [`docs/CONTRIBUER.md`](docs/CONTRIBUER.md) | Démarche, conventions, checklist |
| [`CHANGELOG.md`](CHANGELOG.md) | Journal des modifications |

---

## Limites connues

* **Lecture seule** : impossible de voter, répondre, compléter une PR ou relancer un
  pipeline depuis l'application (choix assumé : un PAT en lecture seule suffit).
* **Pull requests et pipelines uniquement** : ni work items, ni tickets, ni issues. Le
  projet dont celui-ci descend surveillait des work items Azure DevOps par requête WIQL ;
  la fonction n'a pas été reprise, faute d'équivalent sur GitHub et GitLab. Le geste que
  cela demanderait est décrit dans [SDD §8](docs/SDD.md#8-extensibilité--scénarios-anticipés).
* **Sondage, pas temps réel** : un événement est vu au cycle suivant (défaut 3 min). Le
  temps réel exigerait des *service hooks* Azure DevOps, donc des droits d'administration
  de projet et un point d'entrée HTTP joignable.
* **Windows uniquement** : DPAPI, zone de notification et toasts. Le domaine et la couche
  application, eux, sont multiplateformes.
* **Trois forges** : Azure DevOps, GitHub et GitLab. Une quatrième demanderait un adaptateur
  (`SPEC-FORGE`, skill `ajouter-une-forge`) ; une valeur non implémentée est refusée à la
  validation, avec un message explicite.
* **Limites propres à GitHub** : la résolution d'une discussion n'existe pas dans son API
  REST, donc les événements « discussion résolue / réactivée » ne s'y déclenchent pas ; les
  messages des robots sont traités comme des messages système et ne notifient pas. Le tableau
  complet, forge par forge, est dans [SPEC-FORGES](docs/specs/SPEC-FORGES.md)
  (SPEC-FORGE-007).
* **PAT uniquement** : pas d'authentification Entra ID / OAuth / GitHub App.
* Les comptes sont sondés **l'un après l'autre** (ADR-0005) : avec beaucoup de comptes, un
  cycle est d'autant plus long. Le parallélisme joue à l'intérieur de chaque compte.
* Un **pipeline nouvellement ajouté** est mémorisé silencieusement : sa première alerte
  concerne l'exécution suivante, pas celle en cours au moment de l'ajout.
* **Un vote retiré** est notifié comme « a retiré son vote » sans indiquer le vote précédent.
* Azure DevOps n'indique pas **qui** a résolu une discussion : l'événement décrit le fait,
  sans acteur.
