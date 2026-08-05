# language: fr
Fonctionnalité: Surveillance des pipelines

  Camille choisit quels pipelines surveiller, indépendamment des dépôts : elle peut
  suivre le pipeline de nuit d'un dépôt dont elle ne relit aucune pull request, et
  l'inverse.

  Ce qu'elle attend est simple : savoir qu'une exécution a cassé, et savoir que le
  problème est réglé. Sans cette seconde information, une notification d'échec
  l'obligerait à aller vérifier elle-même.

  Contexte:
    Etant donné que Camille surveille le pipeline « backoffice-api - intégration » du projet « Backoffice »
    Et que l'application a déjà mémorisé l'état des pipelines lors d'un cycle précédent

  @SPEC-PIPE-001
  Scénario: Un pipeline qui casse est signalé
    Etant donné que la dernière exécution terminée connue était en succès
    Quand une nouvelle exécution se termine en échec
    Alors Camille est notifiée que le pipeline a échoué
    Et la notification indique le nom du pipeline, le numéro d'exécution, la branche et la personne à l'origine du déclenchement
    Et le clic ouvre la page de cette exécution
    Et un échec partiel est traité comme un échec

  @SPEC-PIPE-001
  Scénario: Une exécution en cours ne notifie rien
    Etant donné que la dernière exécution terminée connue était en succès
    Quand une nouvelle exécution démarre et n'est pas encore terminée
    Alors Camille n'est pas notifiée, son résultat n'étant pas encore connu

  @SPEC-PIPE-001
  Scénario: Une exécution annulée n'est pas un échec
    Etant donné que la dernière exécution terminée connue était en succès
    Quand une exécution est annulée avant la fin
    Alors Camille n'est pas notifiée d'un échec

  @SPEC-PIPE-001
  Scénario: Deux échecs successifs sont deux informations distinctes
    Etant donné une exécution déjà notifiée en échec
    Quand une nouvelle exécution échoue à son tour
    Alors Camille est notifiée une seconde fois
    Et chaque exécution est un fait distinct, avec son propre numéro

  @SPEC-PIPE-001
  Scénario: Le premier cycle des pipelines est silencieux
    Etant donné que l'application n'a encore rien mémorisé pour ce pipeline
    Quand le premier cycle observe une exécution en échec, vieille de trois jours
    Alors Camille n'est pas notifiée d'un échec qu'elle connaît déjà
    Et cet état est mémorisé pour servir de référence aux cycles suivants

  @SPEC-PIPE-002
  Scénario: Le retour au vert est signalé
    Etant donné que la dernière exécution terminée connue était en échec
    Quand une nouvelle exécution se termine en succès
    Alors Camille est notifiée que le pipeline est de nouveau au vert
    Et elle apprend ainsi que le problème est réglé sans avoir à aller le vérifier

  @SPEC-PIPE-003
  Scénario: Camille choisit les pipelines à surveiller
    Etant donné que Camille ouvre l'onglet des pipelines
    Quand elle déplie le projet « Backoffice »
    Alors les définitions de pipeline du projet sont chargées à ce moment-là
    Et elle peut cocher précisément celles qui l'intéressent
    Mais les définitions désactivées ne lui sont pas proposées

  @SPEC-PIPE-003
  Scénario: Renommer ou retirer un pipeline
    Etant donné un pipeline surveillé
    Quand il est renommé côté forge
    Alors la surveillance continue et le nouveau nom est affiché dès le cycle suivant
    Et retirer le pipeline de la sélection oublie son état mémorisé au cycle suivant

  @SPEC-PIPE-004
  Scénario: Le coût d'un cycle ne dépend pas du nombre de pipelines
    Etant donné douze pipelines surveillés, répartis sur deux projets
    Quand un cycle s'exécute
    Alors les exécutions récentes sont demandées projet par projet, en deux requêtes
    Et non pipeline par pipeline, ce qui en aurait demandé douze

  @SPEC-PIPE-005
  Scénario: Un projet illisible n'interrompt pas le reste de la surveillance
    Etant donné deux projets dont les pipelines sont surveillés
    Quand les exécutions d'un projet ne peuvent pas être lues, faute de droits
    Alors le cycle se termine en échec partiel, avec un avertissement
    Et l'état mémorisé des pipelines de ce projet est conservé
    Et les pipelines de l'autre projet, ainsi que la surveillance des pull requests, continuent normalement

  @SPEC-PIPE-006
  Scénario: Surveiller des pipelines sans surveiller aucun dépôt
    Etant donné une configuration complète où Camille n'a coché aucun dépôt
    Quand elle coche au moins un pipeline et enregistre
    Alors la configuration est acceptée
    Et la surveillance des pipelines fonctionne seule
    Mais une configuration sans aucun dépôt ni aucun pipeline reste refusée

  @SPEC-FORGE-004
  Scénario: Une forge sans pipelines fait disparaître la fonctionnalité
    Etant donné un fournisseur qui n'expose pas de pipelines
    Quand Camille ouvre l'onglet des pipelines
    Alors aucune définition ne lui est proposée
    Et la surveillance des pull requests fonctionne normalement, sans message d'erreur
