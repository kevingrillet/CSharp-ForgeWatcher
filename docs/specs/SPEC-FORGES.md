# SPEC-FORGE — Abstraction de la forge

L'application a été écrite pour Azure DevOps, mais la forge est derrière un **port unique**
afin qu'en brancher une autre (GitHub, GitLab, Bitbucket…) ne demande ni de toucher au
métier, ni de toucher à l'interface.

Forges implémentées à ce jour : **Azure DevOps**, **GitHub** (github.com et GitHub Enterprise
Server) et **GitLab** (gitlab.com et instances auto-hébergées). Elles peuvent être surveillées
**en même temps** : voir SPEC-CFG-008. La procédure pour en ajouter une quatrième est décrite
dans `.claude/skills/ajouter-une-forge/`.

## SPEC-FORGE-001 — Un seul port de sortie

*Étant donné* le code de l'application
*Quand* on cherche tout ce qui parle au serveur de la forge
*Alors* on ne trouve que `ISourceControlGateway` (couche application) et ses
implémentations (couche infrastructure). Aucune autre classe n'émet de requête réseau.

Le port est **en lecture seule** : aucune méthode d'écriture n'existe, ce qui autorise un
jeton restreint à la lecture du code.

## SPEC-FORGE-002 — Le fournisseur est un réglage de compte

*Étant donné* un compte de la configuration (SPEC-CFG-008)
*Quand* on l'inspecte
*Alors* il porte un champ `Provider` (`AzureDevOps` par défaut) qui détermine
l'implémentation utilisée et la façon de construire les liens web.

Une valeur inconnue ou non implémentée est refusée à la validation avec un message
explicite, plutôt que de provoquer un échec réseau incompréhensible.

Le champ d'adresse change de sens selon le fournisseur ; l'interface adapte son libellé, son
exemple et le nom qu'elle donne au niveau intermédiaire de l'arborescence :

| Fournisseur | Sens de l'adresse | Exemple | Espaces |
|---|---|---|---|
| Azure DevOps | URL de l'organisation | `https://dev.azure.com/mon-organisation` | Projets |
| GitHub | URL du serveur (racine) | `https://github.com` ou `https://github.mon-entreprise.fr` | Propriétaires |
| GitLab | URL du serveur (racine) | `https://gitlab.com` ou `https://gitlab.mon-entreprise.fr` | Groupes |

Pour GitHub et GitLab, seule l'origine (schéma, hôte, port) est retenue : un chemin
éventuellement collé derrière est ignoré, de sorte que `https://github.com/mon-organisation`
fonctionne aussi. L'adresse de l'API en est déduite : il n'y a **pas** de second champ à
saisir.

| Adresse saisie | API interrogée |
|---|---|
| `https://github.com` (ou `www.github.com`) | `https://api.github.com` |
| `https://github.mon-entreprise.fr` | `https://github.mon-entreprise.fr/api/v3` |
| `https://gitlab.com` | `https://gitlab.com/api/v4` |
| `https://gitlab.mon-entreprise.fr` | `https://gitlab.mon-entreprise.fr/api/v4` |

Changer le fournisseur ou l'adresse d'un compte invalide sa sélection de dépôts et de
pipelines, dont les identifiants appartiennent à l'ancienne forge : la fenêtre d'édition du
compte propose de la vider, et le cycle suivant réamorce de toute façon ce compte en silence
puisque l'identité de l'utilisateur a changé (SPEC-POLL-001). Les autres comptes ne sont pas
touchés.

## SPEC-FORGE-003 — Les liens sont propres à la forge

*Étant donné* un événement à notifier
*Quand* son URL est construite
*Alors* elle passe par `IPullRequestLinkBuilder`, dont l'implémentation dépend du
fournisseur. Les formats diffèrent :

| Forge | Pull request | Discussion | Exécution de pipeline |
|---|---|---|---|
| Azure DevOps | `{org}/{projet}/_git/{dépôt}/pullrequest/{id}` | `…/pullrequest/{id}?discussionId={thread}` | `{org}/{projet}/_build/results?buildId={run}&view=results` |
| GitHub | `{host}/{owner}/{repo}/pull/{id}` | `…/pull/{id}#discussion_r{comment}` | `{host}/{owner}/{repo}/actions/runs/{run}` |
| GitLab | `{host}/{group}/{projet}/-/merge_requests/{iid}` | `…/merge_requests/{iid}#note_{note}` | `{host}/{group}/{projet}/-/pipelines/{run}` |

Le générateur est sélectionné à chaque appel, jamais figé au démarrage : changer de
fournisseur ou d'URL dans la fenêtre de configuration prend effet immédiatement
(SPEC-CFG-004).

## SPEC-FORGE-004 — Ce qu'une nouvelle forge doit fournir

Pour être surveillée, une forge doit permettre de répondre à ces six questions. C'est le
contrat exact de `ISourceControlGateway` :

| Question | Méthode | Azure DevOps | GitHub | GitLab |
|---|---|---|---|---|
| Qui suis-je ? | `GetViewerAsync` | `_apis/connectionData` | `GET /user` | `GET /user` |
| Quels espaces existent ? | `GetProjectsAsync` | `_apis/projects` | `GET /user/orgs` + le compte | `GET /groups` + les projets personnels |
| Quels dépôts dans un espace ? | `GetRepositoriesAsync` | `git/repositories` | `GET /orgs/{owner}/repos` (repli `/users/{owner}/repos`) | `GET /groups/{groupe}/projects?include_subgroups=true` |
| Quelles PR ouvertes dans un dépôt ? | `GetActivePullRequestsAsync` | `git/repositories/{id}/pullrequests` | `GET /repos/{owner}/{repo}/pulls?state=open` | `GET /projects/{id}/merge_requests?state=opened` |
| Quel est l'état final de cette PR ? | `GetPullRequestAsync` | `_apis/git/pullrequests/{id}` | `GET /repos/{owner}/{repo}/pulls/{n}` | `GET /projects/{id}/merge_requests/{iid}` |
| Quelles discussions sur cette PR ? | `GetThreadsAsync` | `…/pullRequests/{id}/threads` | trois appels, voir § GitHub | `GET /projects/{id}/merge_requests/{iid}/discussions` |

Et, pour les pipelines (SPEC-PIPE) : `GetPipelineDefinitionsAsync` et
`GetRecentPipelineRunsAsync`.

Une forge qui n'expose pas les pipelines retourne des listes vides : la fonctionnalité
disparaît d'elle-même, sans code conditionnel ailleurs.

## SPEC-FORGE-005 — Le domaine reste neutre

*Étant donné* les types du domaine
*Quand* on les lit
*Alors* aucun n'emploie de vocabulaire propre à une forge : on parle de *pull request*, de
*discussion*, de *vote*, de *pipeline* — pas de `MergeRequest`, ni de `Review`, ni de
`WorkflowRun`. Le mappeur de chaque forge fait la traduction.

## SPEC-FORGE-006 — Les identifiants de la forge sont des entiers 64 bits

*Étant donné* un identifiant numérique attribué par la forge — commentaire, discussion,
définition de pipeline, exécution de pipeline
*Quand* il est transporté, mémorisé ou remis dans une URL
*Alors* il est traité comme un entier **64 bits**, et sa valeur est restituée à
l'identique.

Ce n'est pas une précaution théorique : les identifiants d'exécution d'un workflow
GitHub Actions dépassent déjà les dix chiffres, et les identifiants de commentaire
approchent la limite des entiers 32 bits. Un débordement se traduirait par un lien mort ou,
pire, par un commentaire considéré comme « déjà vu » — donc jamais notifié. Voir ADR-0004.

Le numéro d'une pull request reste un entier 32 bits : il est attribué par dépôt (GitHub,
GitLab) ou par organisation (Azure DevOps), et reste petit.

## SPEC-FORGE-007 — Chaque forge assume ses limites

*Étant donné* une forge dont l'API n'expose pas une information utilisée par une spec
*Quand* l'adaptateur est écrit
*Alors* la limite est **documentée ici**, l'adaptateur renvoie une valeur neutre (liste
vide, état inconnu) et la spec concernée cesse simplement de produire des événements sur
cette forge — sans erreur, sans code conditionnel dans le métier.

---

# GitHub

## Traduction du vocabulaire

C'est le travail du mappeur, et de lui seul (SPEC-FORGE-005) :

| GitHub | Domaine |
|---|---|
| `pull request` (`number`) | `PullRequest.Id` |
| `login` de l'utilisateur | `UserRef.Id` — voir « Identité » |
| `issue comment` (onglet *Conversation*) | `Comment` de la discussion de conversation |
| `review comment` (commentaire de ligne) | `Comment` d'une discussion rattachée à un fichier |
| `review` avec un corps de texte | `Comment` de la discussion de conversation |
| `review.state` = `APPROVED` | `ReviewerVote.Approved` |
| `review.state` = `CHANGES_REQUESTED` | `ReviewerVote.WaitingForAuthor` |
| `review.state` = `COMMENTED`, `DISMISSED`, `PENDING` | `ReviewerVote.NoVote` |
| `requested_reviewers` | `Reviewer` sans vote |
| `state` = `closed` + `merged_at` renseigné | `PullRequestStatus.Completed` |
| `state` = `closed` sans `merged_at` | `PullRequestStatus.Abandoned` |
| `workflow` (Actions) | `PipelineDefinitionRef` |
| `workflow run` | `PipelineRun` |
| `conclusion` = `failure`, `timed_out`, `startup_failure`, `action_required` | `PipelineRunResult.Failed` |
| `conclusion` = `success` | `PipelineRunResult.Succeeded` |
| `conclusion` = `cancelled` | `PipelineRunResult.Canceled` |
| `conclusion` = `neutral`, `skipped`, `stale` | `PipelineRunResult.Unknown` (ni échec, ni retour au vert) |

## Identité

`ViewerIdentity.Id` et `UserRef.Id` valent le **`login`** GitHub, et non l'identifiant
numérique. C'est imposé par SPEC-EVT-006 : GitHub écrit les mentions sous la forme
`@login` dans le corps des commentaires, et la détection de mention compare le texte à
`ViewerId`. Un identifiant numérique n'y apparaîtrait jamais.

Conséquence assumée : renommer son compte GitHub réamorce la surveillance (l'identité ne
correspond plus à l'état mémorisé, SPEC-POLL-001).

Pour que cette égalité « identité = mot dans un texte » reste sûre, une mention n'est
reconnue que si l'identifiant est précédé de `@` ou de `<` et suivi d'une fin de mot : le
login `dev` ne se déclenche donc pas sur le mot « développement ». Azure DevOps sérialisant
ses mentions en `@<GUID>`, la même règle vaut pour les deux forges.

## Espaces de noms : « projet » = propriétaire

GitHub n'a pas de projet d'équipe. Le niveau intermédiaire de l'arborescence de sélection
est le **propriétaire** (`owner`) : le compte de l'utilisateur, puis chacune de ses
organisations.

Pour les pipelines, en revanche, la « clé de projet » est le couple
**`owner/repo`** : les workflows appartiennent à un dépôt, et c'est ce qui permet de ne
faire qu'un lot de requêtes par dépôt et par cycle. Un `WatchedPipeline` GitHub porte donc
`ProjectName = "owner/repo"`.

## Discussions

GitHub répartit ce qu'Azure DevOps appelle « discussions » sur trois points d'entrée, tous
lus pour une même pull request :

| Point d'entrée | Devient |
|---|---|
| `GET /repos/{o}/{r}/issues/{n}/comments` | la **discussion de conversation** (identifiant synthétique `-1`) |
| `GET /repos/{o}/{r}/pulls/{n}/reviews` | les corps de relecture, ajoutés à cette même discussion |
| `GET /repos/{o}/{r}/pulls/{n}/comments` | une discussion **par fil de commentaires de ligne**, identifiée par le commentaire racine |

Les fils de commentaires de ligne sont reconstitués en suivant `in_reply_to_id` jusqu'à la
racine. L'onglet *Conversation* de GitHub n'est pas structuré en fils : tous ses messages
forment une seule discussion, ce qui rend « quelqu'un a répondu après moi » (SPEC-EVT-005)
équivalent à « quelqu'un a écrit dans une conversation où j'étais intervenu » — c'est
exactement ce qu'un utilisateur de GitHub attend.

Chaque message porte son `html_url`, utilisé tel quel (SPEC-LINK-004) : l'ancre exacte
fournie par GitHub vaut mieux qu'une ancre reconstruite, et elle distingue
`#issuecomment-…`, `#discussion_r…` et `#pullrequestreview-…` sans que le métier ait à
connaître ces formes.

## Limites assumées (SPEC-FORGE-007)

| Spec | Comportement sur GitHub | Raison |
|---|---|---|
| SPEC-EVT-008 (discussion résolue / réactivée) | jamais déclenchée | l'état « resolved » d'un fil n'existe que dans l'API GraphQL ; l'état de discussion est donc `Unknown` et le compteur de discussions ouvertes reste à zéro |
| SPEC-EVT-003 (vote) | les votes sont lus uniquement sur les PR qui concernent l'utilisateur — dont il est l'auteur ou relecteur sollicité | les relectures demandent une requête par pull request ; or la règle ne notifie de toute façon que les PR dont l'utilisateur est l'auteur |
| SPEC-EVT-002 (ajout comme relecteur) | un relecteur qui a déjà rendu son avis n'est plus « sollicité » | GitHub retire l'intéressé de `requested_reviewers` dès qu'une relecture est soumise ; une nouvelle demande de relecture redéclenche l'événement |
| SPEC-PIPE-004 (une requête par projet) | une requête par **workflow surveillé** et par cycle | l'API Actions ne sait pas filtrer les exécutions sur plusieurs workflows à la fois ; interroger chaque workflow reste plus fiable que trier une page d'exécutions communes, dont les plus anciennes seraient tronquées |
| SPEC-PIPE-003 (sélection des pipelines) | lister les workflows d'un propriétaire coûte une requête par dépôt | les workflows appartiennent à un dépôt, pas à une organisation ; le coût n'est payé qu'à l'ouverture de l'onglet *Pipelines*, et le nombre de dépôts parcourus est journalisé |

## Jeton et portées

Authentification par en-tête `Authorization: Bearer <jeton>`, avec
`Accept: application/vnd.github+json` et `X-GitHub-Api-Version`. Fonctionne avec les deux
familles de jetons :

| Jeton | Portées suffisantes |
|---|---|
| Jeton personnel *fine-grained* (recommandé) | dépôt : *Metadata*, *Pull requests*, *Actions* en **lecture seule** ; organisation : *Members* en lecture pour lister les organisations |
| Jeton personnel classique | `repo` (dépôts privés) ou `public_repo`, plus `read:org` |

Le jeton classique est un compromis subi et non choisi : GitHub n'y propose pas de portée
« code en lecture seule », si bien qu'un jeton `repo` autorise aussi l'écriture. La
recommandation est donc le jeton *fine-grained*, seul à respecter réellement le principe
de moindre privilège annoncé en SPEC-FORGE-001. L'application, elle, n'émet que des `GET`.

## Quotas

5 000 requêtes par heure et par jeton sur github.com. Un dépassement se manifeste par un
`403` accompagné de `x-ratelimit-remaining: 0` — reclassé en erreur **transitoire**, donc
réessayé (SPEC-POLL-005), là où un `403` ordinaire reste un problème d'autorisation. Coût
d'un cycle : une requête par dépôt surveillé, plus une par pull request dont les
discussions sont relues (SPEC-POLL-003), plus une par workflow surveillé.

---

# GitLab

C'est la forge dont le modèle colle le mieux au domaine : les discussions sont **déjà**
regroupées et portent leur état de résolution, un projet est à la fois un dépôt et son
pipeline, et la portée de jeton `read_api` est réellement limitée à la lecture. Les seules
traductions notables sont donc le vocabulaire, et l'identifiant des discussions.

## Traduction du vocabulaire

| GitLab | Domaine |
|---|---|
| `merge request` (`iid`) | `PullRequest.Id` |
| `username` de l'utilisateur | `UserRef.Id` — même raison que GitHub (ADR-0004) |
| `note` | `Comment` |
| `discussion` | `CommentThread` — voir « Identifiant des discussions » |
| `note.system` = vrai | `Comment.IsSystem` — GitLab marque explicitement ses messages générés |
| approbation (`approved_by`) | `ReviewerVote.Approved` |
| `reviewers[].state` = `requested_changes` | `ReviewerVote.WaitingForAuthor` |
| `reviewers` sans avis | `Reviewer` sans vote |
| `state` = `opened` ou `locked` | `PullRequestStatus.Active` |
| `state` = `merged` | `PullRequestStatus.Completed` |
| `state` = `closed` | `PullRequestStatus.Abandoned` |
| `project` | `PipelineDefinitionRef` — un projet porte un unique `.gitlab-ci.yml` |
| `pipeline` | `PipelineRun` |
| `status` = `success` / `failed` / `canceled` | `Succeeded` / `Failed` / `Canceled` |
| `status` = `skipped` | `PipelineRunResult.Unknown` (ni échec, ni retour au vert) |

## Espaces de noms : « projet » = groupe

L'espace de sélection est le **groupe**, sous-groupes compris — donc un chemin pouvant
contenir des barres obliques (`equipe/backoffice`). Une entrée dédiée regroupe les projets
personnels, qui n'appartiennent à aucun groupe.

L'identité d'un dépôt est l'**identifiant numérique** du projet, que GitLab accepte partout où
un chemin est attendu : renommer un projet, ou le déplacer dans un autre groupe, ne casse donc
pas la surveillance. Le chemin reste nécessaire aux adresses web, où chaque segment est encodé
séparément afin que les barres obliques du groupe restent des séparateurs.

## Identifiant des discussions

GitLab identifie ses discussions par une **empreinte textuelle**, inutilisable comme
identifiant numérique du domaine. C'est donc l'identifiant de la **première note** du fil qui
tient ce rôle : il est numérique, stable, et c'est aussi celui que désigne l'ancre web
`#note_…` — le même nombre sert ainsi à la mémoire et au lien.

## Pipelines

Un projet GitLab n'a pas de « définitions » multiples : son `.gitlab-ci.yml` est unique. Le
projet **est** le pipeline, ce qui rend cette forge la plus économique des trois — lister les
pipelines d'un groupe ne coûte qu'une requête, et lire les exécutions d'un projet surveillé
une seule aussi.

## Limites assumées (SPEC-FORGE-007)

| Spec | Comportement sur GitLab | Raison |
|---|---|---|
| SPEC-EVT-008 (discussion résolue) | **pleinement fonctionnelle**, contrairement à GitHub | l'état de résolution figure dans l'API REST |
| SPEC-EVT-003 (vote) | les approbations sont lues uniquement sur les merge requests qui concernent l'utilisateur | un appel par merge request ; la règle ne notifie de toute façon que celles dont il est l'auteur |
| SPEC-EVT-003 (« changements demandés ») | dépend de la version du serveur | l'état détaillé par relecteur n'existe que depuis GitLab 15 ; son absence prive du seul libellé « attend une correction », pas de l'approbation |
| SPEC-PIPE-003 (sélection) | tout projet non archivé dont l'intégration continue est accessible est proposé | l'API ne dit pas si un `.gitlab-ci.yml` existe ; un projet sans pipeline se contente de ne rien remonter |

## Jeton et portées

Authentification par en-tête `Authorization: Bearer <jeton>`. Portée suffisante : **`read_api`**
— la seule des trois forges à offrir une portée réellement limitée à la lecture, ce qui en
fait le meilleur élève du principe de moindre privilège annoncé en SPEC-FORGE-001.

## Quotas

GitLab respecte les codes : `429` pour un quota dépassé, `403` pour une autorisation
manquante. Aucun reclassement n'est nécessaire, contrairement à GitHub. Les limites exactes
dépendent de l'instance.
