# language: fr
Fonctionnalité: Cycle de surveillance

  L'application interroge la forge à intervalle régulier. Un cycle lit l'état des
  dépôts surveillés, le compare à celui du cycle précédent, notifie ce qui a
  changé, puis mémorise le nouvel état.

  Ce qui compte pour Camille : ne jamais être noyée au démarrage, ne jamais perdre
  la surveillance des dépôts sains à cause d'un dépôt en panne, et comprendre
  immédiatement quand son jeton d'accès n'est plus valable.

  Contexte:
    Etant donné que Camille surveille les dépôts « backoffice-api » et « backoffice-web » du projet « Backoffice »
    Et que sa configuration est complète

  @SPEC-POLL-001
  Scénario: Le premier cycle mémorise sans rien notifier
    Etant donné que l'application n'a encore rien mémorisé
    Quand le premier cycle s'exécute
    Alors l'état complet des pull requests est mémorisé
    Et Camille ne reçoit aucune notification, même si des dizaines de pull requests sont ouvertes
    Et les pull requests sont visibles dans la fenêtre d'activité
    Mais les cycles suivants notifient normalement les changements

  @SPEC-POLL-001
  Scénario: Changer de compte repart d'un état vierge
    Etant donné un état déjà mémorisé pour le compte de Camille
    Quand le jeton d'accès est remplacé par celui d'un autre compte
    Alors le cycle suivant se comporte comme un premier cycle et ne notifie rien
    Et il en va de même après une réinitialisation manuelle de l'état mémorisé

  @SPEC-POLL-002
  Scénario: Un dépôt inaccessible n'empêche pas de surveiller les autres
    Etant donné que les deux dépôts ont déjà été observés
    Quand « backoffice-web » devient inaccessible, parce qu'il a été supprimé ou que les droits ont changé
    Alors « backoffice-api » continue d'être surveillé normalement
    Et le cycle se termine en échec partiel, avec un avertissement pour le dépôt en cause
    Et l'état mémorisé des pull requests de « backoffice-web » est conservé
    Mais aucune notification de pull request disparue n'est émise

  @SPEC-POLL-002
  Scénario: Une pull request dont l'état final est inconnu reste surveillée
    Etant donné une pull request de Camille déjà mémorisée
    Quand elle n'apparaît plus parmi les pull requests actives et que la forge ne répond pas quand on la relit
    Alors le cycle se termine en échec partiel
    Et la pull request reste mémorisée, faute de savoir si elle a été complétée ou abandonnée

  @SPEC-POLL-003
  Scénario: Par défaut, seules les discussions qui concernent Camille sont relues
    Etant donné une pull request de Camille et une pull request d'Alice qui ne la concerne pas
    Quand un cycle s'exécute
    Alors les discussions de la pull request de Camille sont relues
    Mais celles de la pull request d'Alice ne le sont pas, pour économiser les appels

  @SPEC-POLL-003
  Scénario: Une pull request étrangère est revisitée de temps en temps
    Etant donné une pull request d'Alice dont les discussions ne sont pas relues à chaque cycle
    Quand le délai de rafraîchissement est écoulé
    Alors ses discussions sont relues au cycle suivant
    Et Camille est notifiée si elle y a été mentionnée entre-temps

  @SPEC-POLL-003
  Scénario: La portée complète ne laisse aucun angle mort
    Etant donné que Camille a choisi de lire les discussions de toutes les pull requests surveillées
    Quand un cycle s'exécute
    Alors les discussions de toutes les pull requests actives des dépôts surveillés sont relues
    Et le cycle coûte plus d'appels, ce que Camille accepte en connaissance de cause

  @SPEC-POLL-004
  Scénario: Un jeton d'accès expiré est signalé clairement
    Etant donné que le jeton d'accès personnel de Camille a expiré ou a été révoqué
    Quand un cycle s'exécute
    Alors le cycle se termine en échec
    Et Camille reçoit un message explicite l'invitant à renouveler son jeton
    Et l'état mémorisé n'est pas modifié, pour que rien ne soit re-notifié après le renouvellement

  @SPEC-POLL-005
  Plan du Scénario: Seules les erreurs passagères sont réessayées
    Etant donné un appel à la forge qui échoue avec « <erreur> »
    Quand l'application traite cet échec
    Alors l'appel est <traitement>

    Exemples:
      | erreur                    | traitement                                                           |
      | panne réseau              | réessayé, jusqu'à trois tentatives espacées d'une attente croissante |
      | serveur trop sollicité    | réessayé, jusqu'à trois tentatives espacées d'une attente croissante |
      | erreur interne du serveur | réessayé, jusqu'à trois tentatives espacées d'une attente croissante |
      | jeton d'accès refusé      | abandonné immédiatement, insister ne servirait à rien                |
      | ressource introuvable     | abandonné immédiatement, insister ne servirait à rien                |
