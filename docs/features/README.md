# Scénarios Gherkin — documentation vivante

Ce dossier contient la traduction des spécifications de [`../specs/`](../specs/) en
scénarios Gherkin francophones.

## À quoi ça sert

Ces fichiers sont **de la documentation, pas des tests exécutables**. Aucun runner BDD ne
les joue (voir plus bas). Ils servent à trois choses :

1. **Décrire les comportements du point de vue de l'utilisateur.** Les specs sont écrites
   pour être précises ; les scénarios sont écrits pour être lus — y compris par quelqu'un
   qui n'ouvrira jamais le code. On y parle de notifications, de dépôts et de discussions,
   jamais de classes, de fichiers JSON ou de codes HTTP (sauf quand le code HTTP *est* le
   sujet, par exemple un jeton d'accès refusé).
2. **Servir de point de départ à une évolution.** Avant de coder, on écrit le scénario :
   s'il est difficile à formuler simplement, c'est en général que le comportement demandé
   est mal cadré.
3. **Rester vérifiablement en phase avec les tests.** Un test de garde échoue si un
   scénario parle d'une spec que plus aucun test ne couvre, ou si une spec couverte par un
   test n'est illustrée par aucun scénario (voir
   [`FeatureCoverageTests.cs`](../../tests/CSharpForgeWatcher.Tests/Features/FeatureCoverageTests.cs)).

Les personas sont toujours les mêmes : **Camille** utilise l'application, **Alice** et
**Bob** sont ses collègues, le projet d'exemple est **Backoffice** et ses dépôts
**backoffice-api** et **backoffice-web**.

## Les fichiers

| Fichier | Contenu | Specs illustrées |
|---|---|---|
| [`detection-evenements.feature`](detection-evenements.feature) | Ce qui déclenche une notification, et ce qui n'en déclenche pas | `SPEC-EVT-001` à `SPEC-EVT-009` |
| [`cycle-surveillance.feature`](cycle-surveillance.feature) | Amorçage silencieux, pannes, portée de lecture, réessais | `SPEC-POLL-001` à `SPEC-POLL-005` |
| [`configuration.feature`](configuration.feature) | Jeton d'accès, sélection des dépôts, validation, application à chaud | `SPEC-CFG-*`, `SPEC-FORGE-001`, `SPEC-FORGE-002` |
| [`notifications.feature`](notifications.feature) | Affichage, seuil de synthèse, filtres, liens directs | `SPEC-NOTIF-*`, `SPEC-LINK-*`, `SPEC-FORGE-003`, `SPEC-FORGE-005` |
| [`pipelines.feature`](pipelines.feature) | Échec, retour au vert, sélection, coût d'un cycle | `SPEC-PIPE-*`, `SPEC-FORGE-004` |
| [`forges.feature`](forges.feature) | Choix de la forge, comptes multiples, adresses par forge, identifiants, capacités absentes | `SPEC-FORGE-002`, `-003`, `-006`, `-007`, `SPEC-CFG-008` |
| [`apparence.feature`](apparence.feature) | Thème clair / sombre / automatique, icône et pastille | `SPEC-UI-*` |

Chaque fichier reste volontairement court : un scénario par comportement observable, une
quinzaine au maximum par fichier. Les variantes d'un même comportement (libellés de vote,
résolution du thème, erreurs réessayées) sont regroupées dans un `Plan du Scénario` plutôt
que dupliquées.

## Convention d'étiquettes

Chaque scénario porte en étiquette l'identifiant de la spec qu'il illustre :

```gherkin
  @SPEC-EVT-005
  Scénario: Une réponse à un commentaire de Camille est signalée comme telle
```

Un scénario peut porter plusieurs étiquettes lorsqu'il illustre l'articulation de deux
specs (par exemple la priorité de `SPEC-EVT-002` sur `SPEC-EVT-001`).

C'est la même étiquette que la catégorie portée par les tests NUnit :

```csharp
[Test]
[Category("SPEC-EVT-005")]
public void Une_reponse_dans_une_discussion_ou_jai_ecrit_est_signalee_comme_reponse()
```

## Retrouver le test qui vérifie un scénario

```powershell
dotnet test --filter TestCategory=SPEC-EVT-005
```

Pour retrouver le code du test plutôt que l'exécuter, une recherche de
`SPEC-EVT-005` dans `tests/` suffit. La correspondance spec → fichier de test est aussi
récapitulée dans [`../TRACEABILITE.md`](../TRACEABILITE.md).

## Pourquoi pas Reqnroll (ou un autre runner BDD) ?

Ces scénarios ne sont pas branchés sur des *step definitions*. C'est un choix, pour
l'instant :

- **Le coût dépasse le bénéfice à cette échelle.** L'application est un utilitaire de
  bureau maintenu par une personne. Les tests NUnit existants sont déjà lisibles et
  nommés en français ; les doubler d'une couche de phrases et de définitions de pas
  ajouterait un étage d'indirection à maintenir sans rien vérifier de plus.
- **La granularité n'est pas la même.** Un scénario Gherkin décrit un comportement
  utilisateur ; les tests, eux, descendent au niveau de la règle de détection, ce qui
  permet des messages d'échec précis. Un pas Gherkin comme
  « Alors Camille est notifiée d'une réponse à son commentaire » correspond souvent à
  plusieurs assertions.
- **Une partie des scénarios n'est pas automatisable** en l'état : rendu des fenêtres,
  notifications Windows, registre, chiffrement lié au compte Windows. Les rendre
  exécutables demanderait un harnais d'interface disproportionné (voir la section
  « Zones non couvertes » de [`../TRACEABILITE.md`](../TRACEABILITE.md)).

**Évolution possible.** Si l'application gagne des contributeurs, ou si les specs
deviennent un support de discussion avec des non-développeurs, brancher
[Reqnroll](https://reqnroll.net/) (successeur de SpecFlow, compatible NUnit) est la suite
naturelle : les fichiers de ce dossier sont déjà du Gherkin valide, en français, avec
`# language: fr` en tête. Il faudrait alors ajouter le paquet au projet de tests, écrire
les définitions de pas, et remplacer le test de garde par l'exécution réelle des
scénarios.

## Ajouter ou modifier un scénario

1. Écrire ou compléter la spec dans [`../specs/`](../specs/) — c'est elle qui fait foi.
2. Écrire le scénario ici, étiqueté avec l'identifiant de la spec.
3. Écrire le test NUnit correspondant avec la catégorie de même nom.
4. Si le comportement n'est pas automatisable, ajouter son identifiant à la liste blanche
   documentée en tête de
   [`FeatureCoverageTests.cs`](../../tests/CSharpForgeWatcher.Tests/Features/FeatureCoverageTests.cs),
   en expliquant pourquoi.
