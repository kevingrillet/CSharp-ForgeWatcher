---
name: cadrer-un-comportement
description: "Transformer une demande floue en spécification Forge Watcher rédigeable : choisir la famille et le numéro d'identifiant, dérouler l'interrogatoire qui produit la liste « Règles », et rendre un squelette *Étant donné / Quand / Alors* prêt à coller — ou le constat que la demande n'est pas cadrable en l'état. À utiliser **avant** `docs/specs/`, dès qu'une demande arrive sous la forme « il faudrait aussi notifier quand… », « ça devrait faire… », « et si… », et avant d'ouvrir les skills ajouter-notification, ajouter-une-forge ou etendre-le-port-de-forge."
---

# Cadrer un comportement

Les autres fiches de ce dossier commencent une fois le comportement décidé : elles disent
**comment** l'écrire, le tester, l'implémenter. Celle-ci produit la décision.

Sa sortie est une section `## SPEC-XXX-0NN` prête à coller dans `docs/specs/`, avec sa liste
« Règles » complète — ou le constat argumenté que la demande n'est pas cadrable en l'état, et
la question précise à poser. Rien d'autre : on n'écrit ici ni scénario, ni test, ni code.

Le style de rédaction et le choix du document sont l'affaire du skill
[`rediger-la-documentation`](../rediger-la-documentation/SKILL.md) ; la démarche d'ensemble est
dans [`docs/CONTRIBUER.md`](../../../docs/CONTRIBUER.md) §1. **Ne pas les recopier.**

## 0. Est-ce bien une spec ?

Le test d'entrée tient en une phrase : **si on ne sait pas énoncer l'assertion qui échouerait,
ce n'est pas une spec.** Trois cas de renvoi immédiat :

| La demande porte sur | Ce n'est pas une spec, c'est | Où ça va |
|---|---|---|
| la façon dont c'est assemblé, ce que coûte un cycle | de la conception | `docs/SDD.md`, une section numérotée |
| un choix entre deux options crédibles | une décision | `docs/adr/000N-titre.md` |
| l'usage, un réglage à documenter | de l'aide | `README.md` |

Et deux renvois de périmètre, à trancher **avant** de rédiger quoi que ce soit :

* **écrire dans la forge** (voter, répondre, compléter) est hors périmètre v1 — `docs/SDD.md`
  §2.3, et l'application ne fait aujourd'hui que des `GET` (§6). Y toucher demande un ADR, pas
  une spec ;
* **un comportement fondé sur l'heure** est interdit par [ADR-0003](../../../docs/adr/0003-detection-par-diff-instantane.md) :
  la détection compare des instantanés. Si la demande dit « au bout de 2 jours », elle est à
  reformuler en différence entre deux observations, ou elle tombe.

## 1. Choisir la famille et le numéro

| Famille | Ce qu'elle couvre | Fichier |
|---|---|---|
| `SPEC-EVT` | ce qui déclenche une notification, et ce qui n'en déclenche pas | `docs/specs/SPEC-EVENEMENTS.md` |
| `SPEC-POLL` | le cycle de sondage : amorçage, pannes, portée de lecture, réessais | `docs/specs/SPEC-EVENEMENTS.md` |
| `SPEC-CFG` | ce qui se règle, se valide, se persiste | `docs/specs/SPEC-CONFIGURATION.md` |
| `SPEC-NOTIF` | l'affichage : seuil de synthèse, filtres, repli, compteur | `docs/specs/SPEC-NOTIFICATIONS.md` |
| `SPEC-LINK` | la construction des URL | `docs/specs/SPEC-NOTIFICATIONS.md` |
| `SPEC-FORGE` | le contrat que toute forge doit tenir | `docs/specs/SPEC-FORGES.md` |
| `SPEC-PIPE` | la surveillance des pipelines | `docs/specs/SPEC-PIPELINES.md` |
| `SPEC-UI` | apparence, icône, pastille | `docs/specs/SPEC-INTERFACE.md` |

Un identifiant **ne se renumérote jamais** et ne se réutilise pas : il est cité dans les tests,
les scénarios, la traçabilité et le code. Le prochain libre, par famille :

```powershell
Select-String -Path docs\specs\*.md -Pattern '^#+\s+(SPEC-[A-Z-]+?)-(\d+)' |
    ForEach-Object { $_.Matches[0] } |
    Group-Object { $_.Groups[1].Value } |
    ForEach-Object { '{0}-{1:000}' -f $_.Name, (1 + ($_.Group | ForEach-Object { [int]$_.Groups[2].Value } | Measure-Object -Maximum).Maximum) }
```

Si aucune famille ne convient, c'est probablement que la demande relève du §0. En créer une
nouvelle se justifie pour un **genre d'objet surveillé** inédit (`docs/SDD.md` §8), pas pour
une variante.

## 2. L'interrogatoire

Chaque question ci-dessous a déjà produit une règle numérotée dans une spec existante. Les
dérouler **toutes** : c'est leur exhaustivité qui fait la valeur de la fiche, pas leur
subtilité. Une réponse restrictive devient une règle ; une question sans réponse devient une
question à poser (§4).

### Périmètre

| Question | Ce qu'elle produit | Précédent |
|---|---|---|
| Qui est concerné — auteur, relecteur, participant, quiconque ? | une garde `ViewerIsAuthor` / `ViewerIsReviewer` / `ViewerIsInvolved` en tête de règle | `SPEC-EVT-003` §3 : les votes sur les PR qu'on relit sont du bruit sans action |
| Et si l'acteur, c'est l'observateur lui-même ? | « aucun événement, sauf option *mes propres actions* » — garde `ShouldIgnoreActor` | `SPEC-EVT-001` §1, `SPEC-EVT-003` §1 |
| Que se passe-t-il au tout premier regard ? | **systématiquement** : « aucun événement pendant le cycle d'amorçage (`SPEC-POLL-001`) » | toutes les specs `SPEC-EVT` |
| Quels cas *ressemblants* doivent rester muets ? | une règle d'exclusion par cas | commentaires système `SPEC-EVT-004` §1, commentaires supprimés §3, relecteur retiré `SPEC-EVT-003` §2 |

Ce dernier point est le plus rentable : dans ce dépôt, la moitié des règles sont des
exclusions. Formuler chacune comme un futur nom de test — `Un_commentaire_systeme_est_ignore`.

### Collision avec l'existant

| Question | Ce qu'elle produit | Précédent |
|---|---|---|
| Quelle spec voisine décrit **le même fait** sous un autre nom ? | une règle de priorité, une `dedupKey` partagée, et la place de la valeur dans `NotificationKind` (l'ordre de déclaration *est* la priorité) | `SPEC-EVT-002` prime sur `-001` ; `SPEC-EVT-005` prime sur `-004` et `-007` |
| Si le fait se produit vingt fois dans un cycle ? | une règle de regroupement, sinon le seuil de synthèse s'en charge | `SPEC-EVT-004` §2 (un seul événement par discussion) ; `SPEC-NOTIF-002` |

Un fait est notifié **une seule fois, sous son intitulé le plus précis**. Le doublon est la
régression la plus fréquente du projet — le subagent
[`relecteur-couverture-spec`](../../agents/relecteur-couverture-spec.md) la cherche
explicitement. Se la poser au cadrage coûte une ligne ; la découvrir en usage coûte une
notification en double chez l'utilisateur.

### Ce que ça coûte

| Question | Si la réponse est « non » |
|---|---|
| La donnée comparée est-elle déjà dans l'instantané ? | un champ à ajouter à `src/CSharpForgeWatcher.Domain/Monitoring/PullRequestSnapshot.cs` — et le premier cycle après mise à jour le lira à sa valeur par défaut : vérifier que ça ne fabrique pas une fausse détection (skill `ajouter-notification` §8) |
| La donnée est-elle déjà rapportée par le port de forge ? | une méthode de plus sur `ISourceControlGateway`, répercutée sur trois adaptateurs et deux doubles → skill [`etendre-le-port-de-forge`](../etendre-le-port-de-forge/SKILL.md) |
| Se contente-t-on des lectures du cycle actuel ? | un appel réseau par PR (`RequiresThreads`) : chiffrer le surcoût comme `docs/SDD.md` §5.4, et vérifier la portée `SPEC-POLL-003` |
| Les **trois** forges savent-elles répondre ? | la règle est muette là où la capacité manque (`SPEC-FORGE-007`), la limite se consigne dans `docs/specs/SPEC-FORGES.md` — précédent : `SPEC-EVT-008` sur GitHub |
| Le comportement est-il indépendant du compte ? | préciser le cloisonnement : l'état est par compte, la `dedupKey` est déjà préfixée de l'`AccountId` (`SPEC-CFG-008`) |

### Sortie observable

| Question | Ce qu'elle produit |
|---|---|
| Où mène le clic ? | la dernière ligne du *Alors* : PR, discussion exacte, dépôt ou pipeline (`SPEC-NOTIF-001`, `SPEC-LINK-001` à `-003`) — et l'adresse fournie par la forge prime sur l'adresse reconstruite (`SPEC-LINK-004`) |
| Que lit-on dans la notification ? | les éléments cités nommément dans le *Alors* — un test les vérifie ; « une notification est émise » ne se teste pas utilement |
| Peut-on l'affirmer sans Windows, sans réseau et sans disque ? | si non : une ligne dans le tableau « Zones sans test automatisé » de `docs/TRACEABILITE.md` **avec sa raison et son mode de vérification**, plus l'identifiant dans `VerificationManuelleOuAVenir` (`tests/CSharpForgeWatcher.Tests/Features/FeatureCoverageTests.cs`) |

Cette liste blanche est un aveu, pas une commodité : elle doit rétrécir. Y inscrire une spec
pour faire taire le garde-fou est un écart, et il se voit en relecture.

## 3. Rendre le squelette

```markdown
## SPEC-XXX-0NN — <le fait, du point de vue de l'utilisateur>

*Étant donné* <l'état de départ, y compris « présente dans l'instantané » si la règle compare>
*Quand* <le changement observé — un seul déclencheur>
*Alors* un événement `<NotificationKind>` est émis, intitulé « <titre> », avec <ce qu'on lit
dedans> ; le clic ouvre <la cible exacte>.

Règles :
1. <chaque réponse restrictive du §2, une par ligne, avec l'identifiant de la spec qui prend
   le relais quand il y en a une>
2. Aucun événement pendant le cycle d'amorçage (`SPEC-POLL-001`).
3. <donnée absente de la réponse de la forge : ignorée, pas de fausse détection>
```

Vocabulaire imposé : **l'observateur** est l'utilisateur de l'application, **l'instantané**
l'état mémorisé au cycle précédent, **l'espace** le niveau au-dessus du dépôt. Jamais de nom de
classe, de code HTTP ni de format de fichier dans une spec — sauf quand le code HTTP *est* le
sujet, comme un jeton refusé (`SPEC-POLL-004`).

Relecture à voix haute avant de coller : un *Quand* qui contient « ou » cache deux
déclencheurs, un *Alors* qui promet deux événements cache deux specs.

## 4. Quand rendre la main plutôt qu'écrire

Ces signaux disent que la demande n'est pas mûre. Poser **une** question précise vaut mieux
qu'une spec que le code contredira :

* le *Quand* n'est pas observable par différence entre deux cycles (il faudrait l'historique,
  le contenu d'un diff, ou savoir *qui a lu quoi*) ;
* deux specs existantes devraient changer de sens : c'est une refonte, elle passe par un ADR ;
* la règle de priorité face à une spec voisine dépend d'une préférence de l'utilisateur qui
  n'existe pas encore — décider d'abord si on ajoute un réglage (`SPEC-CFG`) ;
* personne ne sait dire ce que la notification devrait afficher : c'est le signe que le besoin
  est « je veux être au courant », pas un comportement.

Formuler alors l'hypothèse retenue **explicitement** et poursuivre ce qui n'en dépend pas —
famille, numéro, règles déjà tranchées.

## 5. Après le cadrage

| La spec porte sur | Enchaîner sur |
|---|---|
| un type d'activité notifiée | [`ajouter-notification`](../ajouter-notification/SKILL.md) |
| une forge de plus | [`ajouter-une-forge`](../ajouter-une-forge/SKILL.md) |
| une question nouvelle posée à la forge | [`etendre-le-port-de-forge`](../etendre-le-port-de-forge/SKILL.md) |
| autre chose | [`ecrire-un-test`](../ecrire-un-test/SKILL.md), puis le code |

L'ordre reste **spec → scénario → test → code**, et le scénario Gherkin n'est pas optionnel :
`FeatureCoverageTests` fait échouer `dotnet test` dès qu'une catégorie `[Category("SPEC-…")]`
n'a pas son tag `@SPEC-…` dans `docs/features/detection-evenements.feature` ou l'un de ses
voisins.

Pour un chantier transversal — plusieurs comportements, plusieurs couches —, écrire d'abord le
plan : un comportement par ligne, dans l'ordre d'implémentation, chacun avec son identifiant.
Ce plan est un brouillon de travail, il **ne se versionne pas** : les artefacts durables du
dépôt sont la spec, le scénario, le test et la ligne de `docs/TRACEABILITE.md`.
