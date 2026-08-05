# SDD — Software Design Document — Forge Watcher

> Document de conception. Il décrit **quoi** et **pourquoi**.
> Les comportements testables sont dans [`specs/`](specs/), la correspondance
> spec → test dans [`TRACEABILITE.md`](TRACEABILITE.md).

| | |
|---|---|
| **Version** | 1.0 |
| **Statut** | Implémenté |
| **Plateforme** | Windows 10 1809+ / Windows 11, .NET 9 |
| **Type** | Application de bureau résidente (zone de notification) |

---

## 1. Contexte et problème

L'équipe travaille sur **Azure DevOps**, réparti sur **plusieurs projets et plusieurs
dépôts Git**. Les notifications natives d'Azure DevOps sont envoyées par e-mail : elles
sont noyées dans la boîte de réception, arrivent en retard et ne sont pas filtrables
finement par dépôt.

Conséquences observées : des commentaires de relecture restent sans réponse, un vote
« En attente de l'auteur » n'est vu que le lendemain, une nouvelle PR passe inaperçue.

## 2. Objectif

Une application légère, résidente dans la zone de notification Windows, qui **surveille
les pull requests des dépôts choisis** et **notifie immédiatement** ce qui concerne
l'utilisateur, avec un **clic qui ouvre directement le bon endroit** dans le navigateur.

### 2.1 Exigences fonctionnelles

| # | Exigence | Spec |
|---|---|---|
| EF-1 | Notifier la création d'une nouvelle PR dans un dépôt surveillé | SPEC-EVT-001 |
| EF-2 | Notifier quand je suis ajouté comme relecteur | SPEC-EVT-002 |
| EF-3 | Notifier un vote (approuvé, en attente, rejeté) sur une de mes PR | SPEC-EVT-003 |
| EF-4 | Notifier un nouveau commentaire sur une de mes PR | SPEC-EVT-004 |
| EF-5 | Notifier une réponse à un de mes commentaires | SPEC-EVT-005 |
| EF-6 | Notifier une mention (@moi) dans un commentaire | SPEC-EVT-006 |
| EF-7 | Notifier un commentaire sur une PR que je relis | SPEC-EVT-007 |
| EF-8 | Notifier la résolution / réactivation d'une discussion qui me concerne | SPEC-EVT-008 |
| EF-9 | Notifier un changement d'état de PR (complétée, abandonnée, publiée) | SPEC-EVT-009 |
| EF-10 | Choisir précisément les projets **et** dépôts surveillés | SPEC-CFG-002 |
| EF-11 | Activer / désactiver chaque type de notification | SPEC-NOTIF-003 |
| EF-12 | Un clic sur la notification ouvre la PR, ou la discussion exacte | SPEC-NOTIF-001, SPEC-LINK-* |
| EF-13 | Tout se configure depuis une petite interface graphique | SPEC-CFG-* |
| EF-14 | Voir l'état courant des PR suivies depuis la zone de notification | — (UI) |
| EF-15 | Alerter quand un pipeline surveillé échoue, et quand il repasse au vert | SPEC-PIPE-001, SPEC-PIPE-002 |
| EF-16 | Choisir précisément les pipelines surveillés, indépendamment des dépôts | SPEC-PIPE-003 |
| EF-17 | Choisir l'apparence : clair, sombre, ou automatique selon Windows | SPEC-UI-THEME-* |
| EF-18 | Pouvoir brancher une autre forge sans toucher au métier ni à l'interface | SPEC-FORGE-* |

### 2.2 Exigences non fonctionnelles

| # | Exigence | Décision de conception |
|---|---|---|
| ENF-1 | Empreinte mémoire et CPU faibles | Pas de fenêtre au démarrage, sondage périodique (défaut 3 min), pas de WebView |
| ENF-2 | Le PAT ne doit jamais être lisible en clair sur le disque | DPAPI (portée utilisateur) — [ADR-0002](adr/0002-stockage-du-pat.md) |
| ENF-3 | Un dépôt inaccessible ne casse pas la surveillance des autres | Isolation d'erreur par dépôt — SPEC-POLL-002 |
| ENF-4 | Pas de rafale de notifications au premier lancement | Cycle d'amorçage silencieux — SPEC-POLL-001 |
| ENF-5 | Ne pas saturer l'API Azure DevOps | Parallélisme borné (6), lecture des discussions restreinte — SPEC-POLL-003 |
| ENF-6 | Code réutilisable et extensible | Clean architecture + règles en Strategy — §5 |
| ENF-7 | Comportements vérifiables | Cœur métier sans dépendance Windows/réseau, testé unitairement |

### 2.3 Hors périmètre (v1)

Écrire dans Azure DevOps (voter, répondre, compléter une PR) ; surveillance des
pipelines et des work items ; support GitHub / GitLab (mais l'architecture le permet,
cf. §8) ; authentification Entra ID / OAuth (PAT uniquement).

---

## 3. Vue d'ensemble

```
                    ┌──────────────────────────── Windows ────────────────────────────┐
                    │  Zone de notification         Toast cliquable                   │
                    │        ▲                            ▲                           │
                    └────────┼────────────────────────────┼───────────────────────────┘
                             │ icône + menu               │ clic → navigateur
                    ┌────────┴────────────────────────────┴───────────────────────────┐
                    │  CSharpForgeWatcher.Ui (WinForms)                                    │
                    │  TrayApplicationContext · SettingsForm · ActivityForm            │
                    │  ToastNotificationPresenter · racine de composition (DI)         │
                    └────────────────────────────┬───────────────────────────────────┘
                                                 │ appelle
                    ┌────────────────────────────┴───────────────────────────────────┐
                    │  CSharpForgeWatcher.Application                                      │
                    │  PullRequestMonitor (cas d'usage « sonder »)                     │
                    │  PullRequestEventDetector + 7 règles (Strategy)                  │
                    │  NotificationDispatcher · ConfigurationService · LinkBuilder      │
                    │  PORTS : ISourceControlGateway, IConfigurationStore,                │
                    │          IMonitorStateStore, INotificationPresenter, IClock…      │
                    └────────────────────────────┬───────────────────────────────────┘
                             implémente les ports │            utilise
                    ┌────────────────────────────┴───────────────────────────────────┐
                    │  CSharpForgeWatcher.Infrastructure                                    │
                    │  AzureDevOpsRestGateway (HTTP/REST 7.1) · JsonConfigurationStore  │
                    │  JsonMonitorStateStore · DpapiSecretProtector · AutoStart · Logs  │
                    └────────────────────────────┬───────────────────────────────────┘
                                                 │
                    ┌────────────────────────────┴───────────────────────────────────┐
                    │  CSharpForgeWatcher.Domain — PullRequest, CommentThread, Reviewer,     │
                    │  ReviewerVote, PullRequestEvent, MonitorSnapshot… (aucune dép.)   │
                    └────────────────────────────────────────────────────────────────┘
```

**Règle de dépendance** (Clean Architecture) : les flèches de référence pointent
toujours vers l'intérieur. `Domain` ne référence rien. `Application` ne référence que
`Domain`. `Infrastructure` et `Ui` implémentent les ports déclarés par `Application`.
Conséquence pratique : **on peut tester tout le métier sans Windows, sans réseau et sans
disque**, et remplacer Azure DevOps ou WinForms sans toucher au cœur.

---

## 4. Fonctionnement — le cycle de sondage

Un cycle (`PullRequestMonitor.PollAsync`) enchaîne :

1. **Valider la configuration.** Sans URL d'organisation, sans PAT ou sans dépôt
   sélectionné, le cycle retourne `NotConfigured` (l'UI invite à configurer).
2. **Identifier l'utilisateur** (`_apis/connectionData`) → `ViewerId`. Si l'identité
   change (autre compte / autre organisation), l'état mémorisé est réinitialisé.
3. **Lire les PR actives** de chaque dépôt surveillé, en parallèle borné (6).
   Une erreur sur un dépôt devient un *avertissement* : les autres continuent.
4. **Rattraper les PR disparues** : une PR connue absente de la liste active est
   relue individuellement pour connaître son état final (complétée / abandonnée).
5. **Lire les discussions** des PR retenues, selon la portée configurée (SPEC-POLL-003).
6. **Détecter** : pour chaque PR, comparer l'observation à l'instantané précédent en
   appliquant les règles (§5). Au premier cycle, on **mémorise sans notifier**.
7. **Filtrer et notifier** : `NotificationDispatcher` retient les événements dont le
   type est activé, puis affiche un toast par événement — ou un toast de synthèse
   au-delà du seuil configuré.
8. **Persister** le nouvel instantané (`state.json`), purger les PR terminées et les
   dépôts retirés de la configuration.

Le diff est **fondé sur un instantané persistant**, pas sur des dates : redémarrer
l'application ne rejoue pas les notifications déjà vues, et une coupure réseau de
plusieurs heures est rattrapée au cycle suivant.

---

## 5. Décisions de conception structurantes

### 5.1 Une règle = une stratégie

Chaque type d'événement est une classe implémentant `IPullRequestEventRule` :

```csharp
public interface IPullRequestEventRule
{
    string Name { get; }
    bool RequiresThreads { get; }
    IEnumerable<PullRequestEvent> Detect(DetectionContext context);
}
```

`PullRequestEventDetector` (Composite) applique toutes les règles enregistrées et
dédoublonne le résultat. **Ajouter un type de notification = ajouter une classe + un
test + une case dans les préférences**, sans modifier le monitor (principe ouvert/fermé).

### 5.2 Patrons utilisés et pourquoi

| Patron | Où | Raison |
|---|---|---|
| **Ports & Adapters** | `Application/Abstractions` ↔ `Infrastructure` | Testabilité, remplaçabilité d'Azure DevOps |
| **Strategy** | `Detection/Rules/*` | Un type d'événement isolé et testable par classe |
| **Composite** | `PullRequestEventDetector` | Traiter N règles comme une seule |
| **Repository** | `IConfigurationStore`, `IMonitorStateStore` | Isoler la persistance JSON |
| **Factory** | `ISourceControlGatewayFactory`, `TrayIconFactory` | La connexion dépend d'une config modifiable à chaud |
| **Decorator** | `ResilientSourceControlGateway`, `FallbackNotificationPresenter` | Ajouter réessai / repli sans toucher au code décoré |
| **Observer** | `ConfigurationService.Changed`, événements du tray | Réagir à un changement de config sans couplage |
| **Value Object** | `PullRequestKey`, `UserRef`, `RepositoryRef` | Clés et égalité sans ambiguïté |
| **Options** | `WatcherConfiguration` + `Validate()` | Configuration validée en un point |

### 5.3 Modèle de données mémorisé

`%APPDATA%\ForgeWatcher\state.json` — `MonitorSnapshot` :

```
MonitorSnapshot
├─ IsSeeded : bool                     amorçage effectué ?
├─ ViewerId : string                   identité liée à cet état
└─ PullRequests : { "repoId:prId" → PullRequestSnapshot }
                    ├─ Status, IsDraft, Title, Author
                    ├─ ReviewerVotes : { userId → vote }        détection des votes
                    ├─ ViewerIsReviewer : bool                  détection « ajouté comme relecteur »
                    ├─ ThreadsFetchedOn : date?                 pilote le rafraîchissement
                    └─ Threads : { threadId → { Status, CommentIds[], ViewerParticipates } }
```

`CommentIds` est la mémoire qui permet de dire « ce commentaire est nouveau » sans se
fier à l'horloge. `ViewerParticipates` est vrai dès que l'utilisateur a écrit dans la
discussion : c'est ce qui distingue « réponse à mon commentaire » de « commentaire
quelconque ».

### 5.4 Coût des appels API

Par cycle : 1 (identité) + 1 par dépôt + 1 par PR dont on lit les discussions.
La portée `InvolvedOnly` (défaut) limite le dernier terme aux PR où l'utilisateur est
auteur, relecteur ou participant ; les autres sont revisitées au plus toutes les
`InvolvedRefreshMinutes` (défaut 30 min) pour détecter une participation nouvelle.
Ordre de grandeur mesuré pour 8 dépôts / 40 PR actives dont 10 concernées :
≈ 20 requêtes par cycle, soit ≈ 400/jour à 3 minutes d'intervalle — très en dessous
des limites d'Azure DevOps.

Sur GitHub, la même arithmétique donne davantage de requêtes, l'API répartissant les mêmes
informations sur plus de points d'entrée : trois appels pour lire les discussions d'une pull
request au lieu d'un, un appel de relectures par pull request concernant l'utilisateur, et un
appel par workflow surveillé. Le quota étant de 5 000 requêtes par heure et par jeton, la
marge reste large — le détail et les mesures de prudence sont dans
[SPEC-FORGES](specs/SPEC-FORGES.md), § Quotas.

GitLab est la plus économique des trois : un appel pour les discussions d'une merge request,
un pour les pipelines d'un projet.

Ce coût se **multiplie par le nombre de comptes**, chacun ayant son propre quota puisqu'il a
son propre jeton. Les comptes étant sondés l'un après l'autre (ADR-0005), c'est la durée d'un
cycle qui s'additionne, pas la charge instantanée.

---

## 6. Sécurité

* Le PAT est chiffré par **DPAPI** avec la portée `CurrentUser` : le fichier de
  configuration copié sur une autre machine ou lu par un autre compte est inutilisable.
* Aucun secret dans les logs (`log.txt` ne contient ni PAT ni contenu de commentaire).
* Portée de jeton recommandée : la lecture du code seule — **Code (Lecture)** sur
  Azure DevOps, jeton *fine-grained* limité à *Metadata*, *Pull requests* et *Actions* en
  lecture sur GitHub. L'application ne fait que des `GET` ; aucun appel d'écriture n'existe
  dans le code. Le cas du jeton GitHub **classique**, qui n'offre pas de portée en lecture
  seule sur le code, est discuté dans [SPEC-FORGES](specs/SPEC-FORGES.md).
* Le PAT reste en mémoire déchiffré le temps d'un cycle (limite acceptée, cf.
  [ADR-0002](adr/0002-stockage-du-pat.md)).

## 7. Journalisation et diagnostic

`%APPDATA%\ForgeWatcher\log.txt` (rotation à 1 Mo, un fichier de sauvegarde).
Le menu de la zone de notification expose *Ouvrir le dossier de données* et
*Réinitialiser l'état surveillé* (force un nouvel amorçage).

## 8. Extensibilité — scénarios anticipés

| Besoin futur | Geste |
|---|---|
| Nouveau type de notification | Une classe dans `Detection/Rules` + `IsEnabled` + un test (skill `ajouter-notification`) |
| Nouveau genre d'objet surveillé (work items, alertes…) | Un type implémentant `INotifiableEvent` + un détecteur dédié ; l'affichage n'est pas touché |
| Support d'une autre forge | **Fait pour GitHub et GitLab** : une implémentation de `ISourceControlGateway` + un générateur de liens, sélectionnés par le fournisseur du compte, sans qu'aucune règle de détection ni aucune vue change (SPEC-FORGE, [ADR-0004](adr/0004-adaptateur-github.md), skill `ajouter-une-forge`). Une quatrième forge de type REST/`snake_case` hérite en plus de `RestGatewayBase` |
| Surveiller plusieurs forges à la fois | **Fait** : la configuration porte une liste de comptes, l'état est cloisonné par compte ([ADR-0005](adr/0005-comptes-multiples.md), SPEC-CFG-008) |
| Notifications Teams / webhook | Une implémentation de `INotificationPresenter` |
| Passage en service Windows | Remplacer la couche `Ui`, réutiliser `Application` telle quelle |
| Interface WPF ou WinUI | Idem : seule la couche `Ui` change |
| Filtrer par branche cible | Un prédicat dans `WatchedRepository` + filtre dans le monitor |

## 9. Risques connus et parades

| Risque | Parade en place |
|---|---|
| PAT expiré (durée max 1 an) | Erreur 401 détectée → notification explicite « PAT invalide ou expiré » |
| Toasts indisponibles (stratégie de groupe, mode Ne pas déranger) | Repli automatique sur les bulles d'info de la zone de notification |
| Sondage lourd si des centaines de PR | Parallélisme borné + portée des discussions + intervalle réglable |
| Horloge / fuseau | Aucune détection basée sur l'horloge : diff d'instantanés uniquement |
| Renommage d'un dépôt | Suivi par `RepositoryId` (GUID), pas par nom |

## 10. Décisions consignées (ADR)

* [ADR-0001 — WinForms pour l'application résidente](adr/0001-winforms-pour-le-tray.md)
* [ADR-0002 — Stockage du PAT via DPAPI](adr/0002-stockage-du-pat.md)
* [ADR-0003 — Détection par diff d'instantanés](adr/0003-detection-par-diff-instantane.md)
* [ADR-0004 — Adaptateur GitHub : REST, identité par login, identifiants 64 bits](adr/0004-adaptateur-github.md)
* [ADR-0005 — Comptes multiples : une liste de comptes, un état cloisonné](adr/0005-comptes-multiples.md)
