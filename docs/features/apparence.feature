# language: fr
Fonctionnalité: Apparence de l'application

  L'application vit dans la zone de notification et n'ouvre ses fenêtres que
  ponctuellement. Deux choses doivent donc être irréprochables : son icône, qui doit
  renseigner d'un coup d'œil, et son thème, qui doit s'accorder à celui de Windows
  sans que Camille ait à y penser.

  Contexte:
    Etant donné que l'application est en fonctionnement dans la zone de notification

  @SPEC-UI-THEME-001
  Scénario: Le réglage d'apparence a exactement trois positions
    Etant donné que Camille ouvre les réglages d'apparence
    Quand elle déroule le choix du thème
    Alors elle a le choix entre « clair », « sombre » et « comme Windows »
    Et « comme Windows » est la position par défaut

  @SPEC-UI-THEME-002
  Plan du Scénario: Le thème effectif se déduit du réglage et de Windows
    Etant donné que le réglage d'apparence de Camille est « <réglage> »
    Quand Windows est configuré en « <windows> »
    Alors l'application s'affiche en « <effectif> »

    Exemples:
      | réglage       | windows | effectif |
      | clair         | clair   | clair    |
      | clair         | sombre  | clair    |
      | sombre        | clair   | sombre   |
      | sombre        | sombre  | sombre   |
      | comme Windows | clair   | clair    |
      | comme Windows | sombre  | sombre   |

  @SPEC-UI-THEME-002
  Scénario: Une apparence Windows illisible retombe sur le thème clair
    Etant donné que le réglage d'apparence de Camille est « comme Windows »
    Quand l'apparence configurée dans Windows ne peut pas être lue
    Alors l'application s'affiche en clair, le défaut historique de Windows et le rendu le plus sûr

  @SPEC-UI-THEME-003
  Scénario: Changer de thème ne demande aucun redémarrage
    Etant donné la fenêtre d'activité récente ouverte en thème clair
    Quand Camille choisit le thème sombre et enregistre
    Alors les fenêtres ouvertes sont repeintes immédiatement
    Et les fonds, les textes, les champs de saisie, les arborescences, les listes et les boutons restent cohérents entre eux
    Et les libellés secondaires, affichés en gris, restent lisibles sur fond sombre
    Et le réglage est retrouvé au démarrage suivant

  @SPEC-UI-THEME-004
  Scénario: Le menu de la zone de notification suit le thème
    Etant donné que le thème effectif est sombre
    Quand Camille fait un clic droit sur l'icône de la zone de notification
    Alors le menu s'affiche en sombre lui aussi
    Et il ne détonne pas au milieu des fenêtres de l'application

  @SPEC-UI-ICON-001
  Scénario: L'icône renseigne d'un coup d'œil
    Etant donné quatre événements non lus et un dépôt inaccessible au dernier cycle
    Quand Camille regarde l'icône de la zone de notification
    Alors l'icône est celle de l'application, surmontée d'une pastille indiquant « 4 »
    Et un liseré signale l'état de la surveillance : normal, avertissement, erreur ou non configuré
    Mais l'icône du fichier exécutable et celle des fenêtres restent le logo seul, sans pastille ni liseré

  @SPEC-UI-LANG-001
  Scénario: La langue suit Windows par défaut, et peut être imposée
    Etant donné un poste dont la langue d'affichage de Windows est l'allemand
    Quand Camille lance l'application sans avoir touché au réglage de langue
    Alors l'interface s'affiche en anglais, repli des langues que l'application ne connaît pas
    Quand Camille choisit « Français » et enregistre
    Alors le menu de la zone de notification est en français dès son ouverture suivante
    Et les fenêtres le seront à leur prochaine ouverture, WinForms ne relisant pas ses textes
    Mais ses dates restent au format de ses paramètres régionaux, que la langue ne touche pas

  @SPEC-UI-LANG-002
  Scénario: Un événement mémorisé se relit dans la langue courante
    Etant donné un commentaire détecté alors que l'interface était en français
    Quand Camille passe l'interface en anglais et rouvre la fenêtre d'activité récente
    Alors l'événement s'affiche en anglais
    Car il a été mémorisé sous forme de clé et d'arguments, jamais de phrase
