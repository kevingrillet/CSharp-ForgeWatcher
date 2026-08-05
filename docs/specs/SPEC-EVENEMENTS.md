# SPEC-EVT — Détection des événements de pull request

> Chaque spec est écrite en *Étant donné / Quand / Alors* et est couverte par au moins
> un test portant la catégorie de même identifiant :
> `dotnet test --filter TestCategory=SPEC-EVT-003`
>
> Vocabulaire : **l'observateur** = l'utilisateur de l'application (identité résolue via
> `_apis/connectionData`). **Instantané** = état de la PR mémorisé au cycle précédent.

---

## SPEC-EVT-001 — Nouvelle pull request créée

*Étant donné* un dépôt surveillé et un état déjà amorcé
*Quand* une PR active apparaît, absente de l'instantané, créée par quelqu'un d'autre
*Alors* un événement `PullRequestCreated` est émis, intitulé « Nouvelle PR », avec le
titre, l'auteur et le dépôt, et l'URL de la PR.

Règles :
1. Si l'observateur est l'**auteur** de la PR, aucun événement — sauf si l'option
   « me notifier de mes propres actions » est activée.
2. Si l'observateur est déjà **relecteur** de cette PR, `SPEC-EVT-002` prend le relais :
   un seul événement, le plus actionnable (« Vous êtes relecteur »).
3. Aucun événement pendant le cycle d'amorçage (`SPEC-POLL-001`).

## SPEC-EVT-002 — L'observateur est ajouté comme relecteur

*Étant donné* une PR
*Quand* l'observateur figure dans les relecteurs alors qu'il n'y figurait pas
(ou qu'il découvre la PR en étant déjà relecteur)
*Alors* un événement `ReviewerAssigned` est émis : « Vous êtes relecteur ».

## SPEC-EVT-003 — Vote sur une pull request de l'observateur

*Étant donné* une PR dont l'observateur est l'auteur, présente dans l'instantané
*Quand* le vote d'un relecteur change
*Alors* un événement `VoteChanged` est émis, mentionnant le relecteur et le libellé du
vote : *Approuvé* (10), *Approuvé avec suggestions* (5), *Sans vote* (0),
*En attente de l'auteur* (-5), *Rejeté* (-10).

Règles :
1. Le propre vote de l'observateur est ignoré, sauf option « mes propres actions ».
2. Un relecteur retiré de la PR ne produit pas d'événement.
3. Les votes sur les PR que l'observateur *relit* (sans en être l'auteur) ne produisent
   pas d'événement : bruit sans action associée.

## SPEC-EVT-004 — Nouveau commentaire sur une pull request de l'observateur

*Étant donné* une PR dont l'observateur est l'auteur
*Quand* un commentaire d'une autre personne apparaît (nouvelle discussion ou nouveau
message dans une discussion existante)
*Alors* un événement `CommentOnMyPullRequest` est émis, avec un extrait du commentaire,
et l'URL pointe **la discussion** concernée.

Règles :
1. Les commentaires **système** (`commentType = system`, ex. « X a voté ») sont ignorés :
   ces changements sont couverts par SPEC-EVT-003 et SPEC-EVT-009.
2. Plusieurs nouveaux commentaires dans **la même discussion** au même cycle produisent
   **un seul** événement, portant le dernier message et le nombre d'autres messages.
3. Les commentaires supprimés sont ignorés.

## SPEC-EVT-005 — Réponse à un commentaire de l'observateur

*Étant donné* une discussion dans laquelle l'observateur a déjà écrit
*Quand* une autre personne y ajoute un message
*Alors* un événement `ReplyToMyComment` est émis (« Réponse à votre commentaire »),
y compris si la PR n'appartient pas à l'observateur.

Cet événement est **prioritaire** sur SPEC-EVT-004 et SPEC-EVT-007 pour un même
message : un message est notifié une seule fois, sous son intitulé le plus précis.

## SPEC-EVT-006 — Mention de l'observateur dans un commentaire

*Étant donné* un commentaire dont le contenu contient l'identifiant de l'observateur
(Azure DevOps sérialise les mentions sous la forme `@<GUID>`)
*Quand* ce commentaire apparaît
*Alors* un événement `MentionedInComment` est émis (« Vous êtes mentionné »).

C'est l'intitulé le plus prioritaire de tous les événements de commentaire.

## SPEC-EVT-007 — Commentaire sur une pull request relue par l'observateur

*Étant donné* une PR dont l'observateur est relecteur (mais pas auteur)
*Quand* un commentaire d'une autre personne apparaît et qu'aucune règle plus précise
(SPEC-EVT-005, SPEC-EVT-006) ne s'applique
*Alors* un événement `CommentOnReviewedPullRequest` est émis.

Si l'observateur n'est ni auteur, ni relecteur, ni participant à la discussion,
**aucun** événement de commentaire n'est émis.

## SPEC-EVT-008 — Discussion résolue ou réactivée

*Étant donné* une discussion présente dans l'instantané, sur une PR de l'observateur ou
à laquelle il participe
*Quand* son état passe d'*Actif* à *Résolu / Corrigé / Fermé / Par conception / Ne sera
pas corrigé*, ou revient à *Actif*
*Alors* un événement `ThreadStatusChanged` est émis, indiquant le nouvel état.

## SPEC-EVT-009 — Changement d'état d'une pull request

*Étant donné* une PR présente dans l'instantané et concernant l'observateur
(auteur, relecteur ou participant)
*Quand* son état change — *Active* → *Complétée* / *Abandonnée* — ou qu'elle passe de
brouillon à publiée
*Alors* un événement `PullRequestStateChanged` est émis.

Une PR qui disparaît de la liste des PR actives est relue individuellement pour
déterminer son état final ; elle est ensuite retirée de l'état mémorisé.

---

## SPEC-POLL — Cycle de sondage

### SPEC-POLL-001 — Amorçage silencieux

*Étant donné* un état vide (premier lancement, ou après réinitialisation, ou après
changement d'identité)
*Quand* le premier cycle s'exécute
*Alors* l'état complet est mémorisé et **aucun** événement n'est émis ; les cycles
suivants détectent normalement.

### SPEC-POLL-002 — Isolation des erreurs par dépôt

*Étant donné* plusieurs dépôts surveillés dont un est inaccessible (403, supprimé,
renommé)
*Quand* un cycle s'exécute
*Alors* les autres dépôts sont traités normalement, le cycle retourne
`PartialFailure` avec un avertissement par dépôt en échec, et **l'état des PR du dépôt
en échec est conservé** (pas de fausse détection « PR disparue »).

### SPEC-POLL-003 — Portée de lecture des discussions

Deux modes, configurables :

* `InvolvedOnly` (défaut) : les discussions sont lues pour les PR dont l'observateur est
  auteur, relecteur ou participant connu ; une PR non concernée est revisitée au plus
  toutes les `InvolvedRefreshMinutes` afin de détecter une participation nouvelle.
* `AllWatchedPullRequests` : les discussions de toutes les PR actives des dépôts
  surveillés sont lues à chaque cycle (plus coûteux, aucun angle mort).

### SPEC-POLL-004 — Échec d'authentification

*Étant donné* un PAT expiré ou révoqué
*Quand* un cycle s'exécute
*Alors* le cycle retourne `Failure` avec un message explicite invitant à renouveler le
PAT, et l'état mémorisé n'est pas modifié.

### SPEC-POLL-005 — Résilience aux erreurs transitoires

*Étant donné* une erreur réseau, un `429 Too Many Requests` ou une erreur `5xx`
*Quand* un appel est effectué
*Alors* il est réessayé (3 tentatives, attente exponentielle) ; une erreur
d'authentification ou un `404`, eux, ne sont **jamais** réessayés.
