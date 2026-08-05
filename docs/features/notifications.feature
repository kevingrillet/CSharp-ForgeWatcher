# language: fr
Fonctionnalité: Notifications et liens directs

  Une notification n'a de valeur que si elle mène en un clic à l'endroit exact
  dont elle parle, et si elle ne se transforme jamais en rafale.

  Camille doit aussi pouvoir décider de ce qui l'intéresse : chaque type
  d'événement peut être coupé indépendamment, et ce qu'elle a coupé ne doit pas
  lui revenir plus tard.

  Contexte:
    Etant donné que Camille surveille le dépôt « backoffice-api » du projet « Backoffice »
    Et que l'adresse de son organisation est « https://dev.azure.com/mon-organisation »

  @SPEC-NOTIF-001
  Scénario: Un clic mène à la discussion exacte
    Etant donné une notification annonçant un commentaire de Bob sur une pull request de Camille
    Quand Camille clique sur la notification
    Alors son navigateur par défaut s'ouvre sur la discussion concernée, déroulée et mise en évidence
    Mais pour un événement qui ne concerne pas une discussion, il s'ouvre sur la pull request elle-même

  @SPEC-NOTIF-001
  Scénario: Une notification consultée plus tard reste cliquable
    Etant donné une notification que Camille n'a pas vue passer
    Quand elle la retrouve dans le centre de notifications de Windows une heure après
    Alors le clic ouvre toujours la bonne adresse
    Et le bouton « Ouvrir » de la notification a le même effet que le clic

  @SPEC-NOTIF-002
  Scénario: En deçà du seuil, chaque événement a sa notification
    Etant donné que Camille autorise cinq notifications par cycle au maximum
    Quand un cycle détecte deux événements
    Alors deux notifications sont affichées
    Et aucune notification de synthèse n'est affichée

  @SPEC-NOTIF-002
  Scénario: Au-delà du seuil, une seule notification de synthèse
    Etant donné que Camille autorise cinq notifications par cycle au maximum
    Quand un cycle détecte sept événements, par exemple à son retour de vacances
    Alors une seule notification annonce le nombre de nouvelles activités
    Et son clic ouvre la fenêtre d'activité récente
    Et les sept événements y sont tous listés, aucun n'est perdu

  @SPEC-NOTIF-003
  Scénario: Un type d'événement désactivé est définitivement ignoré
    Etant donné que Camille a désactivé les notifications de vote
    Quand Alice approuve une pull request de Camille
    Alors aucune notification n'est affichée
    Et l'événement n'apparaît pas dans la fenêtre d'activité récente
    Mais le nouvel état est mémorisé, si bien que ce vote ne surgira pas si Camille réactive le type plus tard

  @SPEC-NOTIF-004
  Scénario: Repli automatique si les notifications Windows sont indisponibles
    Etant donné un poste où les notifications Windows sont bloquées par une stratégie d'entreprise
    Quand l'application tente d'afficher une notification
    Alors elle bascule sur les bulles d'information de la zone de notification
    Et elle y reste pour la suite de la session, sans réessayer à chaque fois
    Et le clic conserve exactement le même comportement
    Mais un canal d'affichage défaillant ne fait jamais échouer le cycle de surveillance

  @SPEC-NOTIF-005
  Scénario: L'icône porte le nombre d'événements non lus
    Etant donné trois événements notifiés que Camille n'a pas consultés
    Quand elle regarde la zone de notification
    Alors l'icône porte une pastille indiquant « 3 »
    Et au-delà de neuf événements, la pastille indique « 9+ »
    Et ouvrir la fenêtre d'activité récente, ou choisir « tout marquer comme lu », remet le compteur à zéro

  @SPEC-LINK-001
  Scénario: Adresse d'une pull request
    Etant donné la pull request 1234 du dépôt « backoffice-api » dans le projet « Backoffice »
    Quand l'adresse de la pull request est construite
    Alors elle vaut « https://dev.azure.com/mon-organisation/Backoffice/_git/backoffice-api/pullrequest/1234 »
    Et un espace dans le nom du projet, comme « Backoffice Mobile », est encodé en « Backoffice%20Mobile »
    Et une adresse d'organisation terminée par une barre oblique ne produit pas de double barre

  @SPEC-LINK-002
  Scénario: Adresse d'une discussion
    Etant donné la discussion 99 de la pull request 1234
    Quand l'adresse de la discussion est construite
    Alors elle vaut « https://dev.azure.com/mon-organisation/Backoffice/_git/backoffice-api/pullrequest/1234?discussionId=99 »
    Et l'ouverture déroule cette discussion précise plutôt que le haut de la pull request

  @SPEC-LINK-003
  Scénario: Adresse de la liste des pull requests d'un dépôt
    Etant donné le menu de la zone de notification
    Quand Camille choisit « backoffice-api »
    Alors son navigateur s'ouvre sur « https://dev.azure.com/mon-organisation/Backoffice/_git/backoffice-api/pullrequests »

  @SPEC-LINK-004
  Scénario: Une adresse fournie par la forge est préférée à une adresse reconstruite
    Etant donné un commentaire dont l'API a livré l'ancre exacte
    Quand la notification correspondante est construite
    Alors c'est cette ancre qui est ouverte au clic, sans reconstruction
    Et le cas général reste la reconstruction, seule façon d'ouvrir un élément connu du seul état mémorisé
    Et cela évite de deviner laquelle des trois formes d'ancre de GitHub s'applique au message

  @SPEC-FORGE-003
  Plan du Scénario: Les adresses suivent les usages de chaque forge
    Etant donné que le fournisseur configuré est « <forge> »
    Quand l'adresse d'une pull request est construite
    Alors elle a la forme « <forme> »

    Exemples:
      | forge        | forme                                                       |
      | Azure DevOps | organisation, projet, dépôt, puis pullrequest et son numéro |
      | GitHub       | hôte, propriétaire, dépôt, puis pull et son numéro          |
      | GitLab       | hôte, groupe, projet, puis merge_requests et son numéro     |

  @SPEC-FORGE-005
  Scénario: Les libellés parlent le même langage quelle que soit la forge
    Etant donné une forge qui nomme « merge request » ce qu'une autre nomme « pull request »
    Quand Camille reçoit une notification issue de cette forge
    Alors le libellé affiché parle de pull request, de discussion, de vote et de pipeline
    Et Camille n'a pas à apprendre le vocabulaire de chaque forge pour lire ses notifications
