# language: fr
Fonctionnalité: Configuration de la surveillance

  Camille indique une fois pour toutes où regarder : l'adresse de son organisation,
  un jeton d'accès personnel, et les dépôts qui l'intéressent. Tout le reste a un
  défaut raisonnable.

  Deux exigences dominent : le jeton d'accès ne doit jamais se retrouver lisible sur
  le disque, et une configuration incomplète ou abîmée ne doit jamais empêcher
  l'application de démarrer.

  Contexte:
    Etant donné que l'organisation de Camille contient le projet « Backoffice »
    Et que ce projet contient les dépôts « backoffice-api » et « backoffice-web »

  @SPEC-CFG-002
  Scénario: Camille choisit précisément les dépôts à surveiller
    Etant donné que Camille ouvre l'onglet des dépôts
    Quand elle déplie le projet « Backoffice »
    Alors les dépôts du projet sont chargés à ce moment-là, et non tous d'avance
    Et elle peut cocher « backoffice-api » sans cocher « backoffice-web »
    Et les dépôts déjà surveillés apparaissent dans sa sélection même avant tout chargement

  @SPEC-CFG-002
  Scénario: Renommer un dépôt ne casse pas la surveillance
    Etant donné que Camille surveille « backoffice-api »
    Quand ce dépôt est renommé côté forge
    Alors la surveillance continue sans intervention de Camille
    Et le nouveau nom est affiché dès le cycle suivant

  @SPEC-CFG-002
  Scénario: Retirer un dépôt oublie ses pull requests
    Etant donné que les deux dépôts sont surveillés et déjà observés
    Quand Camille retire « backoffice-web » de sa sélection
    Alors les pull requests mémorisées de ce dépôt sont oubliées au cycle suivant
    Et le re-cocher plus tard ne provoque pas une avalanche de notifications de rattrapage

  @SPEC-CFG-001
  Scénario: Le jeton d'accès n'est jamais lisible sur le disque
    Etant donné que Camille saisit son jeton d'accès personnel dans la fenêtre de configuration
    Quand elle enregistre
    Alors le jeton est écrit sous une forme chiffrée, liée à son compte Windows
    Et sa valeur d'origine ne se retrouve nulle part en clair dans le fichier de configuration
    Et l'application le relit correctement au démarrage suivant

  @SPEC-CFG-001
  Scénario: Un jeton illisible équivaut à un jeton absent
    Etant donné une configuration recopiée depuis une autre machine ou un autre compte Windows
    Quand l'application tente de lire le jeton d'accès
    Alors elle se comporte comme si aucun jeton n'était configuré
    Et elle invite Camille à le ressaisir, sans planter

  @SPEC-CFG-003
  Plan du Scénario: Une configuration inutilisable est refusée avec un message clair
    Etant donné une configuration par ailleurs complète
    Quand <situation>
    Alors l'enregistrement signale que « <message> »

    Exemples:
      | situation                                                     | message                                                        |
      | l'adresse de l'organisation est vide                          | l'URL de l'organisation est requise                            |
      | l'adresse de l'organisation n'est pas une adresse web entière | l'URL de l'organisation doit être absolue                      |
      | aucun jeton d'accès n'est saisi                               | le jeton d'accès personnel est requis                          |
      | ni dépôt ni pipeline n'est sélectionné                        | sélectionnez au moins un dépôt ou un pipeline à surveiller     |
      | l'intervalle de sondage est inférieur à trente secondes       | l'intervalle de sondage doit être d'au moins trente secondes   |
      | le nombre maximal de notifications par cycle est nul          | le nombre maximal de notifications par cycle doit être positif |

  @SPEC-CFG-003
  Scénario: Tant que la configuration est inutilisable, l'application se tait
    Etant donné que Camille n'a sélectionné ni dépôt ni pipeline
    Quand un cycle est déclenché
    Alors aucun appel n'est tenté vers la forge
    Et l'icône de la zone de notification signale l'état « non configuré »

  @SPEC-CFG-004
  Scénario: La nouvelle configuration s'applique sans redémarrage
    Etant donné l'application en fonctionnement
    Quand Camille modifie l'intervalle de sondage et valide la fenêtre de configuration
    Alors la configuration est enregistrée
    Et le rythme des cycles suit immédiatement la nouvelle valeur
    Et un cycle est déclenché sans attendre, pour que Camille voie tout de suite l'effet de son changement

  @SPEC-CFG-004
  Scénario: Annuler ne laisse aucune trace
    Etant donné que Camille ouvre la fenêtre de configuration et modifie plusieurs réglages
    Quand elle annule
    Alors la configuration en vigueur est inchangée
    Et rien n'a été écrit sur le disque
    Et son jeton d'accès reste celui qu'elle avait saisi précédemment, même si elle ne l'a pas ressaisi

  @SPEC-CFG-005
  Scénario: Une configuration abîmée ne bloque pas le démarrage
    Etant donné un fichier de configuration absent, tronqué ou illisible
    Quand Camille lance l'application
    Alors l'application démarre avec les réglages par défaut
    Et le fichier illisible est mis de côté au lieu d'être écrasé, pour pouvoir être examiné
    Et l'état mémorisé et le journal de l'application vivent dans le même dossier de données utilisateur

  @SPEC-CFG-006
  Scénario: Démarrer avec Windows
    Etant donné l'onglet des réglages avancés
    Quand Camille coche « démarrer avec Windows »
    Alors l'application est enregistrée auprès de Windows pour démarrer à l'ouverture de sa session
    Et décocher l'option annule cet enregistrement
    Et l'état affiché de l'option reflète ce que Windows connaît réellement, pas un souvenir de la configuration

  @SPEC-CFG-007
  Scénario: Les réglages d'apparence et de fournisseur ont des défauts raisonnables
    Etant donné une première installation
    Quand Camille ouvre la fenêtre de configuration
    Alors le fournisseur proposé est Azure DevOps
    Et l'apparence suit celle de Windows
    Et aucun pipeline n'est surveillé tant qu'elle n'en a pas choisi

  @SPEC-FORGE-002
  Scénario: Un fournisseur non pris en charge est refusé à la validation
    Etant donné une configuration désignant un fournisseur qui n'est pas encore implémenté
    Quand Camille enregistre
    Alors l'enregistrement est refusé avec un message nommant le fournisseur en cause
    Et aucun appel réseau n'est tenté, ce qui évite une erreur technique incompréhensible

  @SPEC-FORGE-001
  Scénario: Un jeton restreint à la lecture suffit
    Etant donné un jeton d'accès personnel qui n'autorise que la lecture du code
    Quand l'application surveille les dépôts de Camille pendant plusieurs cycles
    Alors la surveillance fonctionne normalement
    Et aucune pull request, aucun vote et aucun commentaire n'est modifié sur la forge
