# language: fr
Fonctionnalité: Choix de la forge

  Camille travaille sur Azure DevOps au bureau, publie sur GitHub, et son équipe héberge du
  code sur une instance GitLab. Le même exécutable doit servir les trois — et en même temps —
  sans qu'aucune règle de détection, aucune notification ni aucun écran ne sache laquelle est
  en face.

  Deux points de bascule seulement s'appuient sur le fournisseur : le choix de l'adaptateur
  qui parle au serveur, et le choix du générateur d'adresses web. Tout le reste — ce qui
  compte comme un commentaire, ce qui compte comme un échec — reste écrit une seule fois.

  @SPEC-FORGE-002
  Scénario: Camille choisit sa forge à la création d'un compte
    Etant donné que Camille ajoute un compte dans l'onglet « Comptes »
    Quand elle choisit « GitHub » dans la liste des forges
    Alors le champ d'adresse s'intitule « URL du serveur GitHub » et propose « https://github.com »
    Et les portées de jeton conseillées sont celles de GitHub
    Et l'arborescence de sélection nomme ses espaces « Propriétaires », là où Azure DevOps dit « Projets » et GitLab « Groupes »

  @SPEC-FORGE-002
  Scénario: Une forge sans adaptateur est refusée avant tout appel réseau
    Etant donné un fichier de configuration désignant un fournisseur inconnu
    Quand l'application valide cette configuration
    Alors elle refuse avec un message nommant les forges disponibles
    Et aucune requête n'est émise
    Et l'énumération reste lisible : un fichier écrit par une version antérieure fonctionne toujours

  @SPEC-FORGE-002
  Scénario: Changer la forge d'un compte invalide sa sélection
    Etant donné un compte surveillant trois dépôts Azure DevOps
    Quand Camille bascule ce compte sur « GitHub »
    Alors l'application signale que ces éléments appartiennent à l'autre forge
    Et propose de vider la sélection, sans l'imposer
    Et le cycle suivant réamorce ce compte en silence, son identité ayant changé
    Et les autres comptes continuent de notifier normalement

  @SPEC-FORGE-003
  Scénario: Chaque forge a ses propres adresses
    Etant donné la pull request 1234 du dépôt « backoffice-api » appartenant à « mon-organisation »
    Quand l'adresse de la pull request est construite
    Alors sur Azure DevOps elle vaut « https://dev.azure.com/mon-organisation/mon-organisation/_git/backoffice-api/pullrequest/1234 »
    Et sur GitHub elle vaut « https://github.com/mon-organisation/backoffice-api/pull/1234 »
    Et sur GitLab elle vaut « https://gitlab.com/mon-organisation/backoffice-api/-/merge_requests/1234 »
    Et changer de fournisseur en cours d'exécution change immédiatement la forme des adresses

  @SPEC-FORGE-003
  Scénario: Un groupe GitLab imbriqué reste un chemin
    Etant donné un projet « backoffice-api » dans le groupe « equipe/backoffice »
    Quand l'adresse de sa merge request 7 est construite
    Alors elle vaut « https://gitlab.com/equipe/backoffice/backoffice-api/-/merge_requests/7 »
    Et les barres obliques du groupe restent des séparateurs, jamais encodées en « %2F »
    Et le préfixe « /-/ » sépare sans ambiguïté le chemin du projet du reste de l'adresse

  @SPEC-FORGE-003
  Scénario: Une instance auto-hébergée est utilisée telle quelle
    Etant donné un serveur GitHub Enterprise à l'adresse « https://github.mon-entreprise.fr »
    Quand Camille saisit « https://github.mon-entreprise.fr/mon-organisation »
    Alors seule l'origine est retenue, le chemin étant ignoré
    Et l'API interrogée est « https://github.mon-entreprise.fr/api/v3 »
    Et pour « https://github.com », l'API interrogée est « https://api.github.com »

  @SPEC-FORGE-006
  Scénario: Les identifiants d'une forge ne sont jamais tronqués
    Etant donné une exécution de pipeline dont l'identifiant vaut 12345678901
    Et un commentaire dont l'identifiant dépasse quatre milliards
    Quand ces identifiants sont mémorisés puis replacés dans une adresse
    Alors ils sont restitués chiffre pour chiffre
    Et le commentaire n'est notifié qu'une seule fois, au cycle où il apparaît
    Et un identifiant tronqué passerait pour « déjà vu », donc ne serait jamais notifié : c'est ce que ce scénario empêche

  @SPEC-CFG-008
  Scénario: Camille surveille trois forges en même temps
    Etant donné un compte Azure DevOps, un compte GitHub et un compte GitLab
    Quand un cycle s'exécute
    Alors les trois comptes sont interrogés au cours du même cycle
    Et chacun a son propre jeton, chiffré séparément
    Et chacun a sa propre sélection de dépôts et de pipelines
    Et les notifications des trois forges arrivent indifféremment, le seuil de synthèse s'appliquant à leur total
    Et chaque notification indique de quel compte elle vient

  @SPEC-CFG-008
  Scénario: Un compte fâché ne prive pas les autres de leur cycle
    Etant donné trois comptes surveillés dont le jeton GitHub vient d'expirer
    Quand un cycle s'exécute
    Alors le cycle se solde par un échec partiel, avec un avertissement nommant le compte GitHub
    Et les comptes Azure DevOps et GitLab notifient normalement
    Et l'état mémorisé du compte GitHub est conservé intact
    Mais si les trois comptes échouent, rien n'est écrit du tout : l'état reste celui du dernier cycle réussi

  @SPEC-CFG-008
  Scénario: Chaque compte a son propre amorçage
    Etant donné deux comptes surveillés depuis plusieurs jours
    Quand Camille ajoute un troisième compte
    Alors le premier cycle du nouveau compte est silencieux
    Et les deux comptes établis continuent de notifier pendant ce temps
    Et retirer un compte oublie sa mémoire sans toucher à celle des autres

  @SPEC-CFG-008
  Scénario: Une configuration au format précédent devient un compte
    Etant donné un config.json écrit par une version ne connaissant qu'une seule forge
    Quand l'application démarre
    Alors son fournisseur, son adresse, son jeton et sa sélection forment un compte nommé « principal »
    Et le fichier est réenregistré au format courant, une seule fois
    Et la surveillance reprend sans que Camille ait à ressaisir quoi que ce soit
    Et l'état de surveillance, lui, est simplement réamorcé en silence : c'est un cache, pas une donnée

  @SPEC-FORGE-007
  Scénario: Une capacité absente rend la règle concernée muette
    Etant donné que GitHub n'expose pas la résolution d'une discussion dans son API REST
    Quand une discussion GitHub est observée
    Alors son état reste inconnu
    Et la règle des discussions résolues ne produit aucun événement
    Et aucune erreur n'est signalée à Camille : la fonctionnalité disparaît, elle ne tombe pas en panne
    Et la limite est consignée dans SPEC-FORGES plutôt que découverte à l'usage
