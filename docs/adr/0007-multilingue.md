# ADR-0007 — Interface bilingue : des clés dans le domaine, des `.resx` dans l'application

* **Statut** : accepté
* **Contexte** : l'application ne parlait que français, et sa prose était **partout** — 78
  littéraux dans l'interface, mais aussi 55 dans les adaptateurs de forge, 43 dans la couche
  application et 33 dans le domaine. Une règle de détection fabriquait la phrase « X a
  commenté votre pull request » ; un adaptateur REST fabriquait son message d'erreur. Traduire
  en installant un catalogue dans chacune de ces couches aurait marché, mais y aurait
  installé de la présentation — exactement ce que l'architecture du dépôt proscrit — et
  dispersé les ressources dans quatre assemblys.

## Décision 1 — Les couches basses émettent des clés, pas des phrases

Un message destiné à l'utilisateur est un `TextRef` : une **clé** et ses **arguments**
(`Domain/Text/TextRef.cs`). Le domaine et les règles disent *ce qui s'est passé*, l'interface
dit *dans quelle langue*.

Ce que cela change au-delà de la traduction :

* le domaine redevient muet sur la présentation — plus un seul `ToLabel()` qui rende du
  français ;
* les tests peuvent vérifier **quel** message est produit plutôt que son libellé, ce qui les
  rend indifférents à une reformulation ;
* un événement mémorisé se relit dans la langue courante, même si elle a changé depuis :
  l'état ne contient pas de phrase.

Un argument peut être lui-même un `TextRef`. C'est ce qui permet aux fragments facultatifs —
le fichier commenté, la branche d'une exécution, « et 2 autres messages » — de se composer
sans imposer à toutes les langues la même découpe de phrase.

| Ce qui est traduit | Ce qui ne l'est pas |
|---|---|
| Interface, notifications, avertissements, erreurs de forge, erreurs de validation | `log.txt` — outil de diagnostic, le localiser complique le support sans rien apporter |
| Libellés dépendant de la forge (champ d'adresse, portées de jeton) | Noms des forges : « Azure DevOps », « GitHub », « GitLab » sont des marques |
| Les 11 types d'activité et leurs explications | Messages d'`ArgumentException` : ils visent le développeur, pas l'utilisateur |
| Documentation, specs, scénarios Gherkin, noms de tests | Ils restent en français : c'est de la documentation interne, pas une surface produit |

## Décision 2 — Les formulations vivent dans des `.resx`, dans la couche application

`Text/Strings.resx` (français, langue neutre du dépôt) et `Text/Strings.en.resx` (anglais,
assembly satellite). C'est le format que tout outil de traduction sait lire, et le repli de
culture — `fr-CA` vers `fr` vers la langue neutre — est assuré par `ResourceManager` sans code
de notre part.

Un premier jet employait des dictionnaires C#, pour deux raisons qui n'ont pas tenu : la
génération de la classe fortement typée demanderait Visual Studio (faux — MSBuild sait le
faire), et les assemblys satellites compliqueraient la publication en fichier unique (à
vérifier au moment de publier, pas rédhibitoire). Restait un argument de commodité, qui ne
valait pas de s'écarter de la convention de l'écosystème.

Le catalogue est dans **`Application`** et non dans `Ui` : les tests ne référencent que
`Domain` et `Application`, et c'est ce qui permet de le mettre sous garde-fou. Les clés, elles,
sont dans `Domain` (`TextKeys`) — le seul endroit que les quatre couches voient.

L'accès reste **par clé** plutôt que par classe fortement typée, parce qu'une partie des clés
se déduit d'une énumération (`Kind.{type}.Label`). Le filet est ailleurs : les clés sont des
constantes, et deux tests de garde vérifient la parité des deux langues et la présence de
chaque clé employée.

## Décision 3 — Le format des nombres et des dates ne suit pas la langue

Seule `CurrentUICulture` est alignée sur la langue choisie ; `CurrentCulture` reste celle du
poste. Quelqu'un qui lit l'interface en anglais depuis un poste français attend toujours ses
dates en jour/mois. Aligner les deux aurait été plus simple à écrire et plus surprenant à
l'usage.

## Conséquences

* **Le pluriel n'est pas géré.** `.resx` et `string.Format` n'ont pas d'ICU MessageFormat, d'où
  les « (+{0} autre(s) message(s)) » repris de l'existant. Acceptable en français comme en
  anglais ; si cela gêne un jour, c'est un helper à écrire, pas un changement de format.
* **Un changement de langue ne repeint pas les fenêtres ouvertes.** WinForms compose ses
  libellés à la construction ; le menu de la zone de notification, reconstruit à chaque
  ouverture, suit immédiatement, les fenêtres à leur prochaine ouverture. Même compromis que
  la barre de titre pour le thème (ADR-0001).
* **Un seul « espace » synthétique** existe — les dépôts personnels d'un compte GitHub. Comme
  l'adaptateur ne sait pas formuler, il en renvoie la clé comme nom, et l'arborescence la
  résout. Un nom d'espace réel n'est jamais une clé connue et ressort inchangé.
* **Ajouter une langue** consiste à ajouter un `Strings.<culture>.resx` et une position au
  réglage : aucun code de présentation à toucher.
