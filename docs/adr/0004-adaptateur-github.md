# ADR-0004 — Adaptateur GitHub : REST, identité par login, identifiants 64 bits

* **Statut** : accepté
* **Contexte** : après Azure DevOps, GitHub est la deuxième forge implémentée. Son modèle
  de données ne se superpose pas à celui d'Azure DevOps : pas de projet d'équipe, pas de
  discussion unique, pas de « vote » mais des relectures, et des pipelines qui appartiennent
  à un dépôt et non à un espace. Trois choix structurants devaient être tranchés avant
  d'écrire une ligne d'adaptateur.

## Décision 1 — API REST plutôt que GraphQL

| Option | Pour | Contre |
|---|---|---|
| **REST v3** | Un point d'entrée par question, calqué sur l'adaptateur existant ; chaque champ se vérifie ligne à ligne dans la documentation publique ; erreurs HTTP classées comme ailleurs | Plusieurs requêtes là où GraphQL n'en demanderait qu'une ; l'état « fil résolu » n'est pas exposé |
| GraphQL v4 | Une requête par dépôt suffirait à ramener PR, relectures, fils et leur état de résolution | Une requête à écrire et à maintenir à la main, non typée côté client ; coût en points difficile à prévoir ; impossible à valider sans jeton, ce qui était le cas au moment de l'écriture |

REST est retenu. La conséquence assumée est que **SPEC-EVT-008** (discussion résolue ou
réactivée) ne se déclenche jamais sur GitHub : la résolution d'un fil n'existe que dans
GraphQL. L'état de discussion vaut donc `Unknown`, la règle se tait d'elle-même, et
la limite est consignée dans SPEC-FORGE-007. Passer à GraphQL est une évolution possible :
elle ne toucherait que `GitHubRestGateway` et son mappeur.

## Décision 2 — L'identité de l'utilisateur est son `login`

`ViewerIdentity.Id` et `UserRef.Id` portent le `login` GitHub, pas l'identifiant numérique.

Ce n'est pas un raccourci : la détection de mention (SPEC-EVT-006) compare le texte d'un
commentaire à `ViewerId`, et GitHub écrit les mentions `@login`. Avec un identifiant
numérique, aucune mention ne serait jamais détectée — sans erreur, sans trace, ce qui est le
pire des échecs.

Deux conséquences :

* renommer son compte GitHub réamorce la surveillance (l'identité ne correspond plus à
  l'état mémorisé) — acceptable, c'est rare et sans perte ;
* comparer une identité à un mot dans un texte demande de la rigueur. La comparaison exige
  désormais que l'identifiant soit précédé de `@` ou de `<` et suivi d'une fin de mot. Sans
  cela, un login court comme `dev` se déclencherait sur n'importe quelle prose. Azure DevOps
  sérialisant ses mentions en `@<GUID>`, la règle est commune aux deux forges et reste dans
  le domaine.

## Décision 3 — Les identifiants de la forge sont des entiers 64 bits

Les identifiants de commentaire, de discussion, de définition et d'exécution de pipeline
passent de `int` à `long` dans tout le domaine (SPEC-FORGE-006).

| Identifiant GitHub | Ordre de grandeur |
|---|---|
| Exécution de workflow (`workflow run`) | plus de 10 chiffres — **hors de portée** d'un entier 32 bits |
| Commentaire (`issue comment`, `review comment`) | proche de la limite des entiers 32 bits |

Un débordement ne produirait pas une exception mais un identifiant faux : lien mort, ou —
beaucoup plus grave — commentaire considéré comme déjà vu, donc jamais notifié. Le
changement est mécanique et sans effet sur Azure DevOps, dont les identifiants sont petits ;
les fichiers `state.json` existants restent lisibles, un entier JSON s'élargissant sans
conversion.

Le numéro d'une pull request reste un `int` : il est attribué par dépôt et reste petit.

## Décision 4 — Une requête par workflow surveillé, et non par espace

`GetRecentPipelineRunsAsync` reçoit une clé de projet et une liste de définitions, et devait
tenir en une requête (SPEC-PIPE-004). L'API Actions ne sait pas filtrer les exécutions sur
plusieurs workflows à la fois. Deux options :

| Option | Pour | Contre |
|---|---|---|
| Une page d'exécutions du dépôt, triée ensuite | Une seule requête | Les exécutions des workflows **non surveillés** occupent la page : dans un dépôt actif, une exécution surveillée peut ne pas y figurer — donc un échec silencieusement manqué |
| **Une requête par workflow surveillé** | Aucune troncature possible | *n* requêtes par cycle, avec *n* = nombre de workflows suivis (typiquement 1 à 5) |

La seconde est retenue : manquer une alerte d'échec est précisément ce que l'outil doit
éviter. La clé de projet GitHub est donc `owner/repo`, ce qui regroupe les workflows d'un
même dépôt et conserve l'isolation des erreurs au niveau du dépôt (SPEC-PIPE-005).

## Conséquences générales

* Le métier n'a pas bougé : aucune règle de détection, aucun cas d'usage, aucune vue ne
  connaît GitHub. Deux points de bascule seulement — la fabrique de passerelles et le
  générateur de liens — s'appuient sur le réglage `Provider`.
* Trois ajouts neutres au domaine ont été nécessaires, chacun utile aux deux forges :
  identifiants 64 bits, adresse portée par un message (SPEC-LINK-004), et détection de
  mention délimitée.
* Le mappeur GitHub n'a pas de test unitaire, comme celui d'Azure DevOps : la couche de
  test ne référence pas l'infrastructure (cf. `docs/TRACEABILITE.md`). Ce qui est
  vérifiable sans réseau — les formats d'URL, la sélection par fournisseur, la validation —
  l'est.
