# SPEC-CFG — Configuration

## SPEC-CFG-008 — Plusieurs comptes, plusieurs forges, en même temps

*Étant donné* un utilisateur ayant du code sur plusieurs gestionnaires
*Quand* il configure l'application
*Alors* il déclare autant de **comptes** que nécessaire — chacun avec sa forge, son adresse,
son jeton et sa propre sélection de dépôts et de pipelines — et **tous** sont interrogés au
cours du même cycle.

Règles :

1. Un compte porte un identifiant interne stable, indépendant de son adresse et de son
   fournisseur : corriger l'un ou l'autre ne fait pas perdre la mémoire de surveillance.
2. **Un jeton par compte**, chacun chiffré séparément (SPEC-CFG-001).
3. L'état mémorisé est **cloisonné par compte** : identité, amorçage et instantanés. Ajouter
   un compte n'amorce que celui-là ; le retirer n'oublie que le sien.
4. **Isolation des pannes** : un compte illisible devient un avertissement le nommant, et les
   autres continuent de notifier (`PartialFailure`). Si *tous* échouent, le cycle est en
   échec et **rien n'est écrit** — l'état reste celui du dernier cycle réussi.
5. Un compte peut être **désactivé** : il conserve sa sélection mais n'est plus interrogé.
   Utile le temps de renouveler un jeton, sans perdre une sélection longue à composer.
6. Les notifications, la fenêtre d'activité et le menu **indiquent le compte d'origine** dès
   qu'il y a plus d'un compte — et se taisent sur ce point quand il n'y en a qu'un.
7. Le seuil de synthèse (SPEC-NOTIF-002) s'applique au **total** des événements du cycle,
   toutes forges confondues : c'est bien une seule salve du point de vue de l'utilisateur.

Une configuration écrite par une version antérieure — un fournisseur, une adresse, un jeton —
est convertie au démarrage en un compte unique nommé `principal`, puis réenregistrée au format
courant. La surveillance reprend sans ressaisie. L'**état** de surveillance, lui, n'est pas
migré : c'est un cache, et le premier cycle le réapprend en silence (SPEC-POLL-001).

## SPEC-CFG-001 — Le PAT n'est jamais écrit en clair

*Étant donné* un PAT saisi dans la fenêtre de configuration
*Quand* la configuration est enregistrée
*Alors* `config.json` contient uniquement une forme chiffrée (DPAPI, portée
`CurrentUser`), et la relecture par le même utilisateur Windows restitue la valeur
d'origine — pour **chaque compte**.

Si le déchiffrement échoue (fichier copié depuis une autre machine ou un autre compte),
l'application se comporte comme si aucun PAT n'était configuré et invite à le ressaisir,
sans planter.

## SPEC-CFG-002 — Sélection des projets et dépôts

*Étant donné* une forge comportant plusieurs espaces et plusieurs dépôts
*Quand* l'utilisateur ouvre l'onglet *Dépôts*
*Alors* il peut parcourir les espaces, déplier l'un d'eux pour en charger les dépôts
(chargement à la demande), et cocher précisément les dépôts à surveiller.

Le niveau intermédiaire dépend de la forge : projet d'équipe sur Azure DevOps,
**propriétaire** (compte ou organisation) sur GitHub — cf. SPEC-FORGE-004.

Règles :
1. Un dépôt est mémorisé par son **identifiant** (GUID) : le renommer ne casse pas la
   surveillance ; son nom affiché est rafraîchi à chaque cycle.
2. Les dépôts déjà surveillés apparaissent dans la liste de droite même avant tout
   chargement (pas besoin d'être connecté pour voir sa sélection).
3. Retirer un dépôt de la sélection purge, au cycle suivant, les PR mémorisées de ce
   dépôt.

## SPEC-CFG-003 — Validation

Une configuration est **utilisable** si et seulement si :

| Champ | Contrainte | Message |
|---|---|---|
| `Accounts` | au moins un compte | « Ajoutez un compte : une forge, son adresse et un jeton… » |
| `Accounts` | au moins un compte activé | « Tous les comptes sont désactivés… » |
| `Url` (par compte) | non vide, URI absolue http/https | « *compte* : l'URL … est requise… » (libellé selon le fournisseur) |
| jeton (par compte) | non vide (après déchiffrement) | « *compte* : le jeton d'accès personnel (PAT) est requis. » |
| `Provider` (par compte) | fournisseur implémenté (SPEC-FORGE-002) | « *compte* : le fournisseur … n'est pas encore pris en charge. » |
| sélection | au moins un dépôt **ou** un pipeline, sur n'importe quel compte (SPEC-PIPE-006) | « Sélectionnez au moins un dépôt ou un pipeline à surveiller. » |
| `PollIntervalSeconds` | ≥ 30 s | « L'intervalle de sondage doit être d'au moins 30 secondes. » |
| `MaxNotificationsPerPoll` | ≥ 1 | « Le nombre maximal de notifications par cycle doit être ≥ 1. » |

Les messages propres à un compte sont **préfixés de son libellé** : avec trois forges,
« l'URL est requise » ne dirait pas laquelle corriger.

Tant que la configuration n'est pas utilisable, aucun appel réseau n'est tenté et l'icône
de la zone de notification signale l'état « non configuré ».

## SPEC-CFG-004 — Application à chaud

*Étant donné* l'application en fonctionnement
*Quand* l'utilisateur valide la fenêtre de configuration
*Alors* la nouvelle configuration est enregistrée, l'intervalle du minuteur est mis à
jour et un cycle est déclenché immédiatement — **sans redémarrer** l'application.

La fenêtre travaille sur une **copie** de la configuration : *Annuler* ne laisse aucune
trace.

## SPEC-CFG-005 — Emplacements sur disque

| Fichier | Rôle |
|---|---|
| `%APPDATA%\ForgeWatcher\config.json` | Configuration utilisateur (PAT chiffré) |
| `%APPDATA%\ForgeWatcher\state.json` | Instantané des PR pour la détection |
| `%APPDATA%\ForgeWatcher\log.txt` | Journal (rotation 1 Mo) |

Un `config.json` absent ou corrompu produit une configuration par défaut : l'application
démarre toujours (le fichier illisible est renommé en `.corrupt` pour analyse).

**Reprise de l'ancien emplacement.** L'application s'est appelée `PrWatcher` jusqu'à la
version 1.1.0 incluse. Au démarrage, si `%APPDATA%\PrWatcher` existe et que
`%APPDATA%\ForgeWatcher` n'existe pas encore, le dossier est déplacé tel quel : configuration,
état surveillé et journal sont conservés, et le jeton chiffré reste lisible (ADR-0006). La
reprise ne fait rien si le nouveau dossier existe déjà, et son échec n'empêche jamais le
démarrage.

## SPEC-CFG-007 — Réglages d'apparence et de fournisseur

La configuration porte également :

| Réglage | Portée | Valeurs | Défaut | Spec associée |
|---|---|---|---|---|
| `Provider` | compte | `AzureDevOps`, `GitHub`, `GitLab` | `AzureDevOps` | SPEC-FORGE-002 |
| `Url` | compte | adresse de la forge | vide | SPEC-FORGE-002 |
| `IsEnabled` | compte | vrai / faux | vrai | SPEC-CFG-008 |
| `Repositories` | compte | liste espace + identifiant de dépôt | vide | SPEC-CFG-002 |
| `Pipelines` | compte | liste espace + identifiant de définition | vide | SPEC-PIPE-003 |
| `Theme` | global | `Light`, `Dark`, `System` | `System` | SPEC-UI-THEME-001 |

Les énumérations sont sérialisées **par leur nom** : un `config.json` écrit par une version
antérieure reste lisible, et le fournisseur absent vaut `AzureDevOps`.

Changer le fournisseur ou l'adresse d'un compte rend sa sélection inutilisable — les
identifiants appartiennent à l'ancienne forge. La fenêtre d'édition du compte propose alors de
la vider (SPEC-FORGE-002).

## SPEC-CFG-006 — Démarrage avec Windows

*Quand* l'option est cochée
*Alors* une valeur `CSharpForgeWatcher` est écrite sous
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` et pointe l'exécutable courant.
Décocher la supprime. L'état affiché reflète le registre, pas la configuration.
