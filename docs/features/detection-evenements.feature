# language: fr
Fonctionnalité: Détection des événements de pull request

  Forge Watcher observe les pull requests des dépôts surveillés et signale à Camille
  ce qui la concerne : ses propres pull requests, celles qu'elle relit, et les
  discussions auxquelles elle participe.

  Le principe est toujours le même : l'application compare ce qu'elle voit
  maintenant à ce qu'elle avait mémorisé au cycle précédent, et ne parle que
  lorsque quelque chose a changé. Un même changement n'est annoncé qu'une seule
  fois, sous son intitulé le plus précis.

  Chaque scénario porte en étiquette l'identifiant de la spécification qu'il
  illustre ; voir le README de ce dossier.

  Contexte:
    Etant donné que Camille surveille les dépôts « backoffice-api » et « backoffice-web » du projet « Backoffice »
    Et que l'application a déjà mémorisé l'état des pull requests lors d'un cycle précédent
    Et qu'Alice et Bob sont ses collègues

  @SPEC-EVT-001
  Scénario: Une nouvelle pull request ouverte par un collègue est annoncée
    Etant donné qu'aucune pull request d'Alice n'était connue sur « backoffice-api »
    Quand Alice ouvre la pull request « Ajoute le cache »
    Alors Camille est notifiée d'une nouvelle pull request
    Et la notification indique le titre, l'autrice et le dépôt concerné
    Et le clic ouvre la pull request dans le navigateur
    Mais une pull request découverte déjà terminée n'est pas annoncée

  @SPEC-EVT-001
  Plan du Scénario: Les actions de Camille ne la notifient que si elle le demande
    Etant donné que l'option « me notifier de mes propres actions » est <option>
    Quand Camille ouvre elle-même une pull request sur « backoffice-api »
    Alors elle <résultat> notifiée

    Exemples:
      | option   | résultat  |
      | décochée | n'est pas |
      | cochée   | est       |

  @SPEC-EVT-001
  @SPEC-EVT-002
  Scénario: Une pull request où Camille est relectrice ne produit qu'une notification
    Etant donné qu'une pull request d'Alice est inconnue de l'application
    Et que Camille y figure déjà comme relectrice
    Quand le cycle suivant s'exécute
    Alors Camille reçoit une seule notification, « Vous êtes relecteur »
    Mais elle ne reçoit pas en plus l'annonce d'une nouvelle pull request

  @SPEC-EVT-002
  Scénario: Camille est ajoutée comme relectrice d'une pull request en cours
    Etant donné une pull request d'Alice déjà connue, où Camille n'était pas relectrice
    Quand Alice ajoute Camille aux relecteurs
    Alors Camille est notifiée qu'elle est relectrice de la pull request d'Alice
    Et la notification précise si la relecture est obligatoire
    Mais rien n'est signalé au cycle suivant, où elle est relectrice depuis un moment

  @SPEC-EVT-003
  Plan du Scénario: Le vote d'un relecteur sur une pull request de Camille est signalé
    Etant donné que Camille est l'autrice de la pull request « Corrige le calcul des heures »
    Et que le cycle précédent avait mémorisé Alice comme relectrice sans vote
    Quand Alice vote « <vote> »
    Alors Camille reçoit une notification indiquant « <libellé> »

    Exemples:
      | vote                      | libellé                           |
      | approuvé                  | Alice a approuvé                  |
      | approuvé avec suggestions | Alice a approuvé avec suggestions |
      | en attente de l'auteur    | Alice attend une correction       |
      | rejeté                    | Alice a rejeté                    |

  @SPEC-EVT-003
  Scénario: Les votes qui n'appellent aucune action de Camille sont ignorés
    Etant donné une pull request dont Alice est l'autrice et Camille l'une des relectrices
    Quand Bob approuve cette pull request
    Alors Camille n'est pas notifiée, car ce vote ne lui demande rien
    Et son propre vote ne la notifie jamais non plus
    Mais un relecteur simplement retiré de la pull request ne compte pas comme un vote

  @SPEC-EVT-004
  Scénario: Un commentaire d'un collègue sur une pull request de Camille est signalé
    Etant donné que Camille est l'autrice de la pull request « Corrige le calcul des heures »
    Quand Bob écrit « Il reste un cas non couvert » dans une discussion de cette pull request
    Alors Camille est notifiée d'un commentaire sur sa pull request
    Et la notification reprend l'auteur du message et un extrait du commentaire
    Et elle indique le fichier commenté, le cas échéant
    Et le clic ouvre directement la discussion concernée

  @SPEC-EVT-004
  Scénario: Plusieurs messages dans la même discussion ne font qu'une notification
    Etant donné que Camille est l'autrice d'une pull request
    Quand Alice puis Bob ajoutent trois messages dans la même discussion avant le cycle suivant
    Alors Camille reçoit une seule notification
    Et cette notification met en avant le dernier message et compte les autres

  @SPEC-EVT-004
  Scénario: Les messages qui ne viennent pas d'une personne sont ignorés
    Etant donné que Camille est l'autrice d'une pull request
    Quand la forge inscrit d'elle-même un message d'activité dans une discussion, par exemple « Alice a voté »
    Alors Camille n'en est pas notifiée, ces changements étant déjà couverts par les notifications de vote et d'état
    Et un commentaire supprimé entre deux cycles ne notifie rien
    Et ses propres commentaires ne la notifient pas

  @SPEC-EVT-005
  Scénario: Une réponse à un commentaire de Camille est signalée comme telle
    Etant donné une pull request dont Alice est l'autrice
    Et que Camille a écrit « Peux-tu extraire cette méthode ? » dans une discussion
    Quand Alice répond « C'est fait. » dans cette même discussion
    Alors Camille est notifiée d'une réponse à son commentaire
    Et cet intitulé est retenu de préférence aux intitulés plus généraux, pour ne notifier ce message qu'une fois

  @SPEC-EVT-006
  Scénario: Une mention de Camille est l'intitulé le plus prioritaire
    Etant donné une discussion sur une pull request de Camille
    Quand Bob écrit un message qui mentionne Camille
    Alors Camille est notifiée qu'elle est mentionnée
    Et cet intitulé est retenu de préférence à tous les autres intitulés de commentaire

  @SPEC-EVT-007
  Scénario: Un commentaire sur une pull request que Camille relit est signalé
    Etant donné une pull request dont Alice est l'autrice et Camille l'une des relectrices
    Et que Camille n'a encore écrit dans aucune discussion de cette pull request
    Quand Bob écrit « Le nommage est ambigu. »
    Alors Camille est notifiée d'un commentaire sur une pull request qu'elle relit

  @SPEC-EVT-007
  Scénario: Une pull request étrangère à Camille reste silencieuse
    Etant donné une pull request dont Alice est l'autrice et Bob le seul relecteur
    Et que Camille n'a écrit dans aucune de ses discussions
    Quand Bob y ajoute un commentaire
    Alors Camille n'est pas notifiée, cette pull request ne la concernant pas

  @SPEC-EVT-008
  Plan du Scénario: Le changement d'état d'une discussion suivie est signalé
    Etant donné une discussion connue de l'application, sur une pull request de Camille ou à laquelle elle participe
    Et que son état mémorisé est « <avant> »
    Quand son état devient « <après> »
    Alors Camille reçoit une notification indiquant « <mention> »

    Exemples:
      | avant   | après   | mention              |
      | actif   | corrigé | Corrigé              |
      | actif   | résolu  | Résolu               |
      | corrigé | actif   | discussion réactivée |

  @SPEC-EVT-009
  Plan du Scénario: Le changement d'état d'une pull request qui concerne Camille est signalé
    Etant donné une pull request connue de l'application, dont Camille est autrice, relectrice ou participante
    Et que son état mémorisé est « <avant> »
    Quand son état devient « <après> »
    Alors Camille reçoit une notification indiquant « <mention> »
    Mais une pull request qui ne la concerne pas change d'état sans la notifier

    Exemples:
      | avant     | après      | mention          |
      | active    | complétée  | complétée        |
      | active    | abandonnée | abandonnée       |
      | brouillon | publiée    | Brouillon publié |
