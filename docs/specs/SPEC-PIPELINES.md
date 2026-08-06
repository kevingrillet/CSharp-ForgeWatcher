# SPEC-PIPE — Surveillance des pipelines

> Même convention que les autres specs : un identifiant par comportement, couvert par au
> moins un test portant la catégorie correspondante, et par un scénario Gherkin dans
> [`../features/`](../features/).

L'utilisateur choisit **quels pipelines** surveiller, indépendamment des dépôts : on peut
surveiller le pipeline de nuit d'un dépôt dont on ne suit aucune PR, et inversement.

---

## SPEC-PIPE-001 — Un pipeline passe en échec

*Étant donné* un pipeline surveillé dont la dernière exécution terminée connue était en
succès
*Quand* une nouvelle exécution se termine avec le résultat *échec* ou *échec partiel*
*Alors* un événement `PipelineFailed` est émis, indiquant le nom du pipeline, le numéro
d'exécution, la branche et la personne à l'origine du déclenchement ; le clic ouvre la
page de l'exécution.

Règles :
1. Seules les exécutions **terminées** sont évaluées : une exécution en cours ne notifie
   rien (son résultat n'est pas encore connu).
2. Une exécution **annulée** n'est pas un échec.
3. Deux échecs consécutifs (deux exécutions différentes) produisent **deux** événements :
   chaque exécution est un fait distinct.
4. Aucun événement pendant le cycle d'amorçage (SPEC-POLL-001).

## SPEC-PIPE-002 — Retour au vert

*Étant donné* un pipeline surveillé dont la dernière exécution terminée connue était en
échec
*Quand* une nouvelle exécution se termine en succès
*Alors* un événement `PipelineRecovered` est émis (« Pipeline de nouveau au vert »).

Sans cet événement, l'utilisateur notifié d'un échec n'a aucun moyen d'apprendre que le
problème est réglé autrement qu'en allant voir.

## SPEC-PIPE-003 — Sélection des pipelines

*Étant donné* une organisation comportant plusieurs projets, chacun avec ses définitions
de pipeline
*Quand* l'utilisateur ouvre l'onglet *Pipelines*
*Alors* il peut charger les projets, déplier un projet pour lister ses définitions
(chargement à la demande) et cocher celles à surveiller.

Règles :
1. Un pipeline est mémorisé par son **identifiant de définition** et son projet : le
   renommer ne casse pas la surveillance ; le nom affiché est rafraîchi à chaque cycle.
2. Retirer un pipeline de la sélection purge son état mémorisé au cycle suivant.
3. Les définitions désactivées ne sont pas proposées.

## SPEC-PIPE-004 — Coût d'un cycle

*Étant donné* N pipelines surveillés répartis sur P espaces
*Quand* un cycle s'exécute
*Alors* **un seul appel de passerelle par espace** est émis : les exécutions récentes de
toutes les définitions surveillées de l'espace sont demandées ensemble, et non une par
pipeline.

Le nombre de requêtes HTTP que cet appel représente dépend de l'API de la forge. Azure DevOps
accepte plusieurs définitions dans une même requête, donc une requête par projet. L'API Actions
de GitHub ne sait pas filtrer sur plusieurs workflows à la fois : l'adaptateur y émet une
requête par workflow surveillé, pour ne pas risquer de manquer une exécution — écart documenté
en SPEC-FORGE-007 et justifié dans ADR-0004.

## SPEC-PIPE-005 — Isolation des erreurs

*Étant donné* un projet dont les exécutions ne peuvent pas être lues (droits, projet
supprimé)
*Quand* un cycle s'exécute
*Alors* le cycle retourne `PartialFailure` avec un avertissement, l'état des pipelines de
ce projet est **conservé**, et la surveillance des PR et des autres projets continue.

## SPEC-PIPE-006 — Configuration minimale

*Étant donné* une configuration valide par ailleurs
*Quand* l'utilisateur n'a sélectionné **aucun dépôt mais au moins un pipeline**
*Alors* la configuration est **utilisable** : la surveillance des pipelines fonctionne
seule. La configuration n'est invalide que si l'utilisateur n'a sélectionné ni dépôt ni
pipeline.
