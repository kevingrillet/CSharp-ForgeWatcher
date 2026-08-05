# ADR-0005 — Comptes multiples : une liste de comptes, un état cloisonné

* **Statut** : accepté
* **Contexte** : la configuration ne portait qu'un fournisseur, une adresse et un jeton. Elle
  répondait donc à « quelle forge surveilles-tu ? », alors que la vraie question est « où as-tu
  du code ? » — et la réponse est souvent *plusieurs endroits* : Azure DevOps au travail,
  GitHub pour l'open source, une instance GitLab pour l'équipe. Avec un seul jeu de réglages,
  il fallait arbitrer, et donc renoncer à être averti de ce qui se passe ailleurs.

## Options considérées

| Option | Pour | Contre |
|---|---|---|
| **Liste de comptes, tous interrogés dans le même cycle** | Répond exactement au besoin ; une seule salve de notifications, un seul seuil de synthèse ; chaque forge garde son jeton et sa sélection | Touche le format de configuration, l'état persisté, le moniteur et trois onglets de l'interface |
| Profils commutables | Bien moins coûteux | Une seule forge surveillée à la fois : on ne reçoit rien des autres, ce qui ne répond qu'à moitié au besoin |
| Une instance de l'application par forge | Aucun développement | Trois icônes dans la zone de notification, trois fenêtres de configuration, trois fois le même réglage de thème ; l'utilisateur fait le travail que l'outil devrait faire |

## Décision 1 — La configuration porte une liste de comptes

Un `WatchedAccount` réunit ce qui variait ensemble : fournisseur, adresse, jeton, sélection de
dépôts et de pipelines. Ce qui reste global l'est resté : intervalle, thème, préférences de
notification, seuil de synthèse — ce sont des préférences d'utilisateur, pas de forge.

Conséquence directe : il n'existe plus « un » générateur de liens ni « une » passerelle
injectés au démarrage. Les deux se déduisent du compte, et l'enregistrement global de
`IPullRequestLinkBuilder` a disparu.

## Décision 2 — L'identifiant de compte est opaque

Un identifiant tiré du fournisseur et de l'adresse aurait été lisible, mais faux : corriger une
URL mal saisie aurait alors changé l'identité du compte, donc perdu sa mémoire de surveillance
et provoqué un réamorçage. Deux comptes sur le même serveur avec deux jetons différents — un
personnel, un professionnel — auraient de plus collisionné.

L'identifiant est donc un GUID, généré à la création et jamais recalculé. Seule exception,
assumée pour la lisibilité : le compte issu d'une configuration au format 1 s'appelle
`principal`.

## Décision 3 — L'état est cloisonné par compte, et non migré

`state.json` contient désormais un dictionnaire `comptes → état`, chaque entrée portant son
identité, son amorçage et ses instantanés. C'est ce qui permet :

* d'ajouter un compte sans faire taire les autres — seul le nouveau s'amorce ;
* de renouveler un jeton sans tout réamorcer ;
* qu'un compte en panne conserve sa mémoire intacte pendant que les autres avancent.

L'état de la version 1 n'est **pas** migré. C'est un choix : `state.json` est un cache
reconstructible, et le premier cycle le réapprend en silence (SPEC-POLL-001). Écrire un
convertisseur pour éviter un unique cycle silencieux aurait coûté plus que le service rendu.
La configuration, elle, est migrée — elle contient un jeton et des choix que l'utilisateur ne
doit pas avoir à refaire.

## Décision 4 — Les comptes sont sondés séquentiellement

Le parallélisme utile est **à l'intérieur** d'un compte : dépôts, discussions et pipelines sont
déjà lus en parallèle borné. Ajouter un second niveau de parallélisme aurait multiplié la
charge simultanée sur chaque serveur — précisément ce que les forges pénalisent — et obligé à
rendre l'état partagé sûr en accès concurrent.

Les comptes sont donc traités l'un après l'autre. Un cycle plus long de quelques secondes n'a
aucune importance à un intervalle de trois minutes, et le code reste lisible.

## Décision 5 — Une panne de compte est un incident local

| Situation | Issue |
|---|---|
| Un compte échoue, d'autres réussissent | `PartialFailure` : avertissement **nommant le compte**, les autres notifient normalement |
| Tous les comptes échouent | `Failure` : notification de problème, et **rien n'est écrit** — l'état reste celui du dernier cycle réussi |

Le second cas mérite l'attention : c'est un test existant qui a rattrapé l'erreur. La première
version persistait l'état avant d'évaluer les échecs, si bien qu'un jeton refusé laissait une
trace dans `state.json`. Rien n'ayant été appris, rien ne doit être écrit.

## Conséquences

* Les notifications, les vues et les avertissements indiquent leur compte d'origine — mais
  **seulement** s'il y a plus d'un compte, sinon ce serait du bruit.
* L'identifiant de compte entre dans les clés de déduplication : deux comptes surveillant le
  même dépôt notifient chacun de leur côté, ce qui est le comportement attendu.
* L'arborescence de sélection gagne un niveau (compte → espace → élément), chargé à la demande
  à chaque niveau : un compte n'est interrogé que si on le déplie.
* La fenêtre de configuration a perdu son onglet *Connexion* au profit d'un onglet *Comptes*,
  et l'édition d'un compte se fait dans une fenêtre dédiée.
