# Journal des modifications

Toutes les évolutions notables de Forge Watcher sont consignées ici.

Le format suit [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) et le versionnage
suit [SemVer](https://semver.org/lang/fr/). Les catégories utilisées sont *Ajouté*,
*Modifié*, *Corrigé*, *Supprimé*, *Sécurité*.

> **Convention du dépôt** : toute modification de comportement visible par l'utilisateur
> ajoute une ligne dans `[Non publié]`, en même temps que la spec et le scénario Gherkin
> correspondants. La checklist de `.github/pull_request_template.md` le rappelle.

## [Non publié]

### Ajouté

- **Surveillance simultanée de plusieurs comptes et de plusieurs forges**
  (`SPEC-CFG-008`, ADR-0005) : la configuration porte désormais une **liste de comptes**,
  chacun avec sa forge, son adresse, son jeton et sa propre sélection de dépôts et de
  pipelines. Tous sont interrogés au cours du **même cycle** — Azure DevOps au travail,
  GitHub en dehors, GitLab pour l'équipe, sans arbitrer.
  - nouvel onglet *Comptes* (qui remplace *Connexion*), avec une fenêtre d'édition par
    compte ; l'arborescence de sélection gagne un niveau, chargé à la demande ;
  - l'état mémorisé est **cloisonné par compte** : ajouter un compte n'amorce que celui-là,
    et un compte en panne conserve sa mémoire pendant que les autres avancent ;
  - un compte peut être **désactivé** sans perdre sa sélection, le temps de renouveler un
    jeton ;
  - les notifications et les vues indiquent leur compte d'origine dès qu'il y en a plusieurs,
    et se taisent sur ce point quand il n'y en a qu'un ;
  - **migration automatique** : une configuration écrite par une version antérieure devient
    un compte nommé `principal`, sans ressaisie. L'état de surveillance, lui, est réamorcé en
    silence — c'est un cache.
- **Support de GitLab** (`SPEC-FORGE-002` à `SPEC-FORGE-007`) : gitlab.com comme instance
  auto-hébergée. C'est la forge dont le modèle colle le mieux au domaine — discussions déjà
  regroupées et portant leur état de résolution (`SPEC-EVT-008` y fonctionne pleinement, à la
  différence de GitHub), un projet qui est à la fois un dépôt et son pipeline, et une portée
  de jeton `read_api` réellement limitée à la lecture.
- **Support de GitHub** (`SPEC-FORGE-002` à `SPEC-FORGE-007`, ADR-0004) : github.com comme
  GitHub Enterprise Server. Le libellé du champ d'adresse, l'exemple, les portées de jeton
  conseillées et l'intitulé de l'arborescence de sélection suivent le fournisseur. L'adresse
  de l'API se déduit de celle du serveur : il n'y a pas de second champ à saisir.
  - les relectures GitHub deviennent des votes, les trois surfaces de commentaires (onglet
    *Conversation*, corps de relecture, commentaires de ligne) deviennent des discussions,
    les workflows Actions deviennent des pipelines. Aucune règle de détection, aucune
    notification et aucun écran ne connaît GitHub ;
  - basculer de forge propose de vider la sélection de dépôts et de pipelines, qui appartient
    à l'ancienne ;
  - les limites assumées sont consignées dans `docs/specs/SPEC-FORGES.md` — notamment
    l'absence de l'état « discussion résolue » dans l'API REST de GitHub, qui rend
    `SPEC-EVT-008` muette sur cette forge.
- **Adresse fournie par la forge préférée à l'adresse reconstruite** (`SPEC-LINK-004`) :
  quand l'API livre l'ancre exacte d'un message — GitHub le fait sous trois formes, GitLab
  pour chaque note —, elle est ouverte telle quelle.
- **Scripts de compilation et de nettoyage** : `scripts/publier.ps1` enchaîne restauration,
  compilation de toute la solution et publication dans `publish/` (ignoré par Git), avec le
  dossier de sortie vidé au préalable — options `-Autonome`, `-Version` et `-Sortie` ;
  `scripts/nettoyer.ps1` ramène le dépôt à l'état « sorti de clone » (`dotnet clean`, puis
  suppression effective des `bin/`, `obj/`, `publish/` et `TestResults/`), avec `-WhatIf`
  pour simuler. Les tâches VS Code *publier (win-x64)*, *publier (autonome)* et *nettoyer*
  les appellent.
- **Étape de cadrage avant la spécification** : le skill `cadrer-un-comportement` couvre le
  moment où le comportement n'est pas encore tranché — choix de la famille et du numéro
  d'identifiant, questions qui produisent la liste « Règles » (actions propres, amorçage,
  priorité face aux specs voisines, capacité absente d'une forge, coût réseau, cible du clic),
  et squelette *Étant donné / Quand / Alors* à coller. Les autres fiches commençaient toutes
  après cette décision.
- **Outillage d'assistance versionné** : `.mcp.json` (CodeGraph, Context7) et
  `.claude/settings.json` (serveur de langage C#, outils Chrome DevTools, permissions
  usuelles) font partie du dépôt ; les préférences personnelles restent dans des fichiers
  `*.local.json` ignorés.

- **Interface en français ou en anglais** (`SPEC-UI-LANG-001`, `SPEC-UI-LANG-002`, ADR-0007) :
  réglage à trois positions dans *Préférences*, la position automatique suivant la langue
  d'affichage de Windows — toute langue autre que le français donne l'anglais.
  - le menu de la zone de notification suit **immédiatement** ; les fenêtres à leur prochaine
    ouverture, WinForms ne relisant pas les textes d'une fenêtre déjà construite ;
  - **aucune couche sous l'interface ne produit plus de phrase** : un événement, une erreur de
    forge, une erreur de validation portent une clé et ses arguments (`TextRef`). Un événement
    mémorisé se relit donc dans la langue courante, même si elle a changé depuis ;
  - les formulations vivent dans `Text/Strings.resx` (français, langue neutre) et
    `Text/Strings.en.resx` (anglais) — 255 entrées par langue ;
  - **deux tests de garde** : les deux langues portent exactement les mêmes clés, et chaque clé
    employée par le code existe dans les deux. Ajouter une valeur à une énumération affichée
    sans la formuler dans les deux langues casse le build ;
  - le format des dates et des nombres n'est **pas** touché — il reste celui des paramètres
    régionaux du poste —, et le journal reste en français : `log.txt` est un outil de
    diagnostic, pas une surface produit.

### Modifié

- **Le projet s'appelle Forge Watcher** (ADR-0006) : dépôt `CSharp-ForgeWatcher`, exécutable
  `ForgeWatcher.exe`, données dans `%APPDATA%\ForgeWatcher`. « PR Watcher » ne décrivait plus
  que la moitié du travail : l'application surveille aussi les pipelines, sur trois forges, et
  *forge* est déjà le mot employé partout dans le code et les specs.
  - **reprise automatique au premier lancement** : le dossier de données est déplacé depuis
    `%APPDATA%\PrWatcher`, le démarrage automatique est réinscrit sous le nouveau nom, et le
    raccourci du menu Démarrer de l'ancien nom est supprimé. Rien à ressaisir : le jeton
    chiffré reste lisible, DPAPI étant lié au compte Windows et non au nom de l'application
    (ADR-0002). Aucune notification d'historique n'est rejouée, l'état surveillé suivant le
    dossier ;
  - la reprise est idempotente et sans effet sur une installation neuve ; un échec est
    journalisé en avertissement et n'empêche jamais le démarrage.
- **Enregistrer les paramètres ne relance un cycle que si la surveillance change.**
  Ajouter un dépôt ou renouveler un jeton déclenche un cycle immédiat, comme avant ; changer
  de thème, couper le son ou décocher le démarrage avec Windows n'interroge plus les forges
  pour rien. La distinction repose sur une empreinte de configuration
  (`WatcherConfiguration.MonitoringSignature`) qui ignore l'apparence, et le décompte du
  prochain cycle n'est plus remis à zéro par un enregistrement qui ne touche pas à
  l'intervalle.
- **Le filtre des onglets *Dépôts* et *Pipelines* porte aussi sur les éléments**, et plus
  seulement sur les espaces : c'est un dépôt qu'on cherche le plus souvent, pas le projet qui
  le contient. Un espace dont le nom correspond montre tout son contenu ; un espace dont le
  nom ne correspond pas reste visible s'il contient un élément qui correspond.
- **Un jeton inchangé n'est plus rechiffré à chaque enregistrement** : DPAPI produit un
  chiffré différent à chaque appel, ce qui faisait passer un compte intact pour modifié.
- **La fenêtre de configuration est découpée par onglet** (`SettingsForm.Accounts.cs`,
  `.Selection.cs`, `.Preferences.cs`, `.Advanced.cs`) : un fichier de 958 lignes en cinq
  fichiers, même classe partielle, aucun changement de comportement.
- **Les identifiants de forge sont des entiers 64 bits** (`SPEC-FORGE-006`) : commentaires,
  discussions, définitions et exécutions de pipeline. Les identifiants d'exécution de GitHub
  Actions dépassent largement la capacité d'un entier 32 bits, et ceux des commentaires en
  approchent la limite ; un débordement aurait produit un lien mort ou, plus grave, un
  commentaire tenu pour « déjà vu » donc jamais notifié. Les fichiers `config.json` et
  `state.json` existants restent lisibles.
- **Une mention doit être délimitée** (`SPEC-EVT-006`) : l'identifiant de l'utilisateur n'est
  reconnu que précédé de `@` ou `<` et suivi d'une fin de mot. Sur GitHub l'identité est un
  `login` — un mot ordinaire —, et la comparaison par simple sous-chaîne se serait déclenchée
  sur n'importe quelle prose le contenant.
- Le vocabulaire des messages et des journaux ne nomme plus Azure DevOps là où le propos vaut
  pour toute forge, et le port parle d'« espaces » plutôt que de « projets », ceux-ci n'ayant
  pas d'équivalent sur GitHub ni sur GitLab.
- **Plomberie HTTP mutualisée** : `RestGatewayBase` porte ce que GitHub et GitLab ont en
  commun — pagination par en-tête `Link`, désérialisation `snake_case`, parallélisme borné,
  classement des erreurs. Ajouter une quatrième forge de ce type ne demande plus que ses
  points d'entrée et son mappeur.

### Corrigé

- **Deux comptes surveillant le même dépôt notifient de nouveau chacun de leur côté**
  (`SPEC-NOTIF-002`, ADR-0005). L'étiquette d'un toast était obtenue en gardant les 64
  derniers caractères de la clé de déduplication, or celle-ci commence par l'identifiant de
  compte : au-delà d'environ 96 caractères — le cas courant sur Azure DevOps, dont les
  identifiants de dépôt sont des GUID — le compte disparaissait, et Windows remplaçait le
  premier toast par le second au lieu de les empiler. L'étiquette est désormais une empreinte
  de la clé entière.
- **Modifier un compte ne fait plus réapparaître des dépôts lus avec l'ancien jeton.** Les
  arborescences de sélection chargent sans attendre ; un chargement encore en vol au moment
  d'un rechargement repeuplait le cache que celui-ci venait de vider. Les résultats périmés
  sont maintenant écartés.
- **Un échec inattendu de chargement d'arborescence est signalé** au lieu de laisser le nœud
  sur « Chargement… » indéfiniment : seules deux familles d'exceptions étaient rattrapées, et
  les autres disparaissaient sans trace, la tâche n'étant pas attendue.
- **La bande d'onglets de la fenêtre de configuration suit le thème sombre**
  (`SPEC-UI-THEME-003`). Deux causes se cumulaient : `TabControl` délègue son rendu au
  contrôle commun Win32, qui ignore `BackColor` — la bande restait claire au-dessus d'une
  fenêtre sombre —, et les pages étaient créées avec `UseVisualStyleBackColor`, qui repeint le
  fond clair de Windows par-dessus la couleur demandée. Le contrôle est désormais peint par
  l'application, comme l'est déjà le menu de la zone de notification.
- **Le message d'erreur fatale indique le vrai chemin du journal** : il désignait
  `%APPDATA%\CSharpPrWatcher\log.txt`, dossier qui n'a jamais existé. Le chemin est lu dans
  `AppPaths`, seule source de vérité des emplacements (`SPEC-CFG-005`).
- Un cycle dont **tous** les comptes échouent n'écrit plus rien : l'état mémorisé reste celui
  du dernier cycle réussi. La première version du sondage multi-comptes persistait l'état
  avant d'évaluer les échecs, si bien qu'un jeton refusé laissait une trace dans `state.json`.

## [1.1.0] — 2026-08-04

Cette version renomme le projet, ouvre la porte à d'autres forges, et ajoute la
surveillance des pipelines ainsi que le thème sombre.

### Ajouté

- **Surveillance des pipelines** (`SPEC-PIPE-001` à `SPEC-PIPE-006`) : alerte quand une
  exécution d'un pipeline surveillé échoue, et quand il repasse au vert. Sélection
  explicite des pipelines à suivre dans un nouvel onglet, indépendamment des dépôts. Une
  seule requête par projet et par cycle, quel que soit le nombre de pipelines suivis.
- **Thème clair / sombre / automatique** (`SPEC-UI-THEME-001` à `SPEC-UI-THEME-004`) :
  réglage à trois positions, la position automatique suivant l'apparence de Windows.
  Application immédiate, sans redémarrage, y compris sur le menu de la zone de
  notification.
- **Logo** : icône dessinée pour l'application (source SVG + `.ico` multi-résolutions
  reproductible via un générateur versionné). L'icône de la zone de notification compose
  ce logo avec la pastille de non-lus et un liseré d'état.
- **Sous-menu « Pipelines »** dans la zone de notification, les pipelines en échec en tête.
- **Scénarios Gherkin** (`docs/features/`, en français) comme documentation vivante, avec
  des tests de garde qui échouent si un scénario n'a plus de test associé, ou si une spec
  testée n'est plus racontée.
- **Intégration continue** : GitHub Actions (build, mise en forme, tests, release sur tag)
  et GitLab CI. Voir `docs/CI.md`.
- **Outillage de contribution** : `.claude/` (skills et subagents propres au projet),
  `docs/CONTRIBUER.md`, `.vscode/` versionné (extensions recommandées, tâches, débogage),
  `NuGet.Config` épinglant nuget.org, `.gitattributes` normalisant les fins de ligne.
- **Fournisseur de forge configurable** (`SPEC-FORGE-002`) : le champ existe, seul
  Azure DevOps est implémenté ; une valeur non prise en charge est refusée à la validation
  avec un message explicite.

### Modifié

- **Renommage** : le projet s'appelle désormais `CSharp-PrWatcher` ; l'exécutable est
  `PrWatcher.exe`, les données sont dans `%APPDATA%\PrWatcher`. Plus aucune référence à une
  organisation, un produit interne ou une personne.
- **Solution au format `.slnx`** (XML lisible et diffable) à la place du `.sln`. Nécessite
  le SDK .NET ≥ 9.0.200.
- **Le port de forge est neutre** : `IAzureDevOpsGateway` devient `ISourceControlGateway`,
  `AzureDevOpsException` devient `SourceControlException`. Les implémentations Azure DevOps
  gardent leur nom. Ajouter une forge ne demande plus de renommer quoi que ce soit
  (`SPEC-FORGE-001`).
- **Les événements sont génériques** : `INotifiableEvent` est implémenté par
  `PullRequestEvent` et `PipelineEvent`. La diffusion, le seuil de synthèse, la fenêtre
  d'activité et le compteur de non-lus traitent les deux sans distinction. L'énumération
  `PullRequestEventKind` devient `NotificationKind`.
- **Configuration minimale assouplie** : un dépôt **ou** un pipeline suffit désormais
  (`SPEC-PIPE-006`). Auparavant au moins un dépôt était exigé.
- **Build durcie** : `TreatWarningsAsErrors` et `EnforceCodeStyleInBuild` activés — le
  moindre avertissement casse la compilation. La mise en forme est vérifiée en CI.
- Les deux onglets de sélection (dépôts, pipelines) partagent le même pilote
  d'arborescence : leur comportement est identique et décrit une seule fois.

### Corrigé

- Un cycle ne se solde plus par un échec total quand aucun dépôt n'est surveillé mais que
  des pipelines le sont.
- Le message d'état de l'onglet *Dépôts* n'est plus superposé à l'arborescence (contrôle
  non ancré).
- Le repli sur les bulles d'info ne réutilise plus le format d'arguments des toasts : les
  URL de discussion, qui contiennent `?…=`, ne risquent plus d'être mal décodées.

## [1.0.0] — 2026-08-04

Première version.

### Ajouté

- Surveillance des pull requests Azure DevOps sur plusieurs projets et plusieurs dépôts,
  avec sélection explicite des dépôts.
- Neuf types de notifications (`SPEC-EVT-001` à `SPEC-EVT-009`) : mention, réponse à un
  commentaire, commentaire sur ma PR, commentaire sur une PR que je relis, vote, ajout
  comme relecteur, discussion résolue ou réactivée, changement d'état de PR, nouvelle PR.
  Chaque type est activable indépendamment.
- Clic sur une notification ouvrant l'endroit exact : la discussion pour un commentaire, la
  pull request sinon (`SPEC-NOTIF-001`, `SPEC-LINK-*`).
- Détection par diff d'instantané persistant, sans dépendance à l'horloge (ADR-0003) :
  redémarrer ne rejoue rien, une coupure est rattrapée d'un coup.
- Cycle d'amorçage silencieux (`SPEC-POLL-001`), isolation des erreurs par dépôt
  (`SPEC-POLL-002`), lecture des discussions à portée réglable (`SPEC-POLL-003`), réessai
  des erreurs transitoires (`SPEC-POLL-005`).
- Toast de synthèse au-delà d'un seuil configurable, avec repli automatique sur les bulles
  d'info si les toasts Windows sont indisponibles (`SPEC-NOTIF-002`, `SPEC-NOTIF-004`).
- Fenêtre de configuration (connexion, dépôts, préférences, avancé) appliquée à chaud, et
  fenêtre d'activité récente.
- Démarrage automatique avec Windows en option.

### Sécurité

- Le jeton d'accès personnel est chiffré par DPAPI, portée « utilisateur courant »
  (ADR-0002) : un fichier de configuration copié ailleurs est inutilisable.
- L'application est en lecture seule sur la forge : un jeton restreint à la lecture suffit.
- Aucun secret ni contenu de commentaire n'est écrit dans le journal.
