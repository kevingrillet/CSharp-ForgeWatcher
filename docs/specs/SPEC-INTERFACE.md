# SPEC-UI — Apparence de l'interface

## SPEC-UI-THEME-001 — Trois positions

Le réglage d'apparence a exactement trois valeurs :

| Valeur | Effet |
|---|---|
| `Light` | Thème clair, quel que soit le réglage de Windows |
| `Dark` | Thème sombre, quel que soit le réglage de Windows |
| `System` (défaut) | Suit le réglage d'apparence des applications de Windows |

## SPEC-UI-THEME-002 — Résolution du thème effectif

La résolution est une fonction pure — préférence + apparence système → thème effectif —
donc testable sans interface :

| Préférence | Windows en clair | Windows en sombre |
|---|---|---|
| `Light` | clair | clair |
| `Dark` | sombre | sombre |
| `System` | clair | sombre |

Si l'apparence de Windows ne peut pas être lue (clé de registre absente), le mode `System`
retombe sur **clair** : le défaut historique de Windows, et le rendu le plus sûr.

## SPEC-UI-THEME-003 — Application sans redémarrage

*Étant donné* l'application en fonctionnement, une fenêtre ouverte
*Quand* l'utilisateur change le thème et enregistre
*Alors* les fenêtres ouvertes sont repeintes immédiatement — aucun redémarrage n'est
nécessaire, et le réglage est persisté.

Sont mis en cohérence : fond et texte des fenêtres, champs de saisie, arborescences,
listes, boutons, séparateurs, la **bande d'onglets** de la fenêtre de configuration, et les
libellés secondaires (texte grisé) qui doivent rester lisibles sur fond sombre.

Deux contrôles ne se contentent pas d'une couleur de fond et sont **peints par
l'application** : le menu de la zone de notification (SPEC-UI-THEME-004) et le contrôle à
onglets. Tous deux délèguent normalement leur rendu à Windows, qui ignore la couleur
demandée et laisserait une zone claire au milieu d'une fenêtre sombre.

## SPEC-UI-LANG-001 — Langue de l'interface

Réglage à trois positions, dans l'onglet *Préférences*, persisté par son nom dans
`config.json` :

| Réglage | Langue de Windows en français | Toute autre langue de Windows |
|---|---|---|
| `System` (défaut) | français | anglais |
| `French` | français | français |
| `English` | anglais | anglais |

Une langue de Windows illisible ou inconnue donne l'**anglais** : c'est le repli le plus
utile pour un poste dont on ne sait rien, et le seul choix honnête tant que l'application
n'en connaît que deux.

Les noms de langue sont écrits **dans leur propre langue** (« Français », « English ») quelle
que soit la langue courante : quelqu'un coincé dans une interface qu'il ne lit pas doit
pouvoir reconnaître la sienne dans la liste.

*Étant donné* l'application en fonctionnement
*Quand* l'utilisateur change de langue et enregistre
*Alors* le menu de la zone de notification suit immédiatement, et les fenêtres à leur
prochaine ouverture — WinForms ne relit pas les textes d'une fenêtre déjà construite, ce que
la fenêtre de configuration indique.

Le **format des dates et des nombres n'est pas touché** : il reste celui des paramètres
régionaux du poste. Quelqu'un qui lit l'interface en anglais depuis un poste français attend
toujours ses dates en jour/mois.

Les **journaux restent en français** : `log.txt` est un outil de diagnostic, pas une surface
produit.

## SPEC-UI-LANG-002 — Le domaine ne choisit pas la langue

Aucune couche sous l'interface ne produit de phrase. Un événement, une erreur de forge, une
erreur de validation portent une **clé** et ses arguments (`TextRef`) ; la formulation vient
du catalogue, et la langue n'est connue qu'à l'affichage.

Conséquences vérifiables :

* un événement mémorisé se relit dans la langue **courante**, même si elle a changé depuis ;
* les deux catalogues portent exactement les mêmes clés, et chaque clé employée par le code
  existe dans les deux — deux tests de garde le vérifient ;
* ajouter une valeur à une énumération affichée (un type de notification, un état de
  discussion) sans la formuler dans les deux langues fait échouer les tests.

Les formulations vivent dans `Text/Strings.resx` (français, langue neutre) et
`Text/Strings.en.resx` (anglais), pour les raisons exposées dans
[ADR-0007](../adr/0007-multilingue.md).

## SPEC-UI-THEME-004 — Menu de la zone de notification

Le menu contextuel suit le thème effectif. C'est un rendu personnalisé : Windows ne
thématise pas automatiquement les menus WinForms.

## SPEC-UI-ICON-001 — Icône et pastille

L'icône de la zone de notification est composée à l'exécution : **le logo de
l'application** surmonté, le cas échéant, d'une **pastille** indiquant le nombre
d'événements non lus (`9+` au-delà de neuf) et d'un liseré d'état (normal, avertissement,
erreur, non configuré).

L'icône du fichier exécutable et celle des fenêtres sont le même logo, sans pastille.
