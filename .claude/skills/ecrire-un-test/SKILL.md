---
name: ecrire-un-test
description: "Catalogue des patrons de test du dépôt Forge Watcher : quel patron pour quel sujet (règle de détection, générateur de liens, validation de configuration, cycle complet, multi-comptes), quels doubles réutiliser, et ce qu'un test ne doit jamais faire. À utiliser avant d'écrire un test dans tests/CSharpForgeWatcher.Tests, et quand un comportement semble « pas testable »."
---

# Écrire un test

La démarche — test **avant** code, catégorie `[Category("SPEC-…")]`, nom français — est dans
[`docs/CONTRIBUER.md`](../../../docs/CONTRIBUER.md) §1 et §3. Cette fiche répond à la question
suivante : **quel patron** employer, et avec quels doubles.

Deux propriétés du dépôt à préserver, plus importantes que n'importe quel test isolé :

* la suite complète s'exécute en **quelques centaines de millisecondes** — aucun accès réseau,
  disque ou horloge ;
* les tests ne référencent que `Domain` et `Application`. Ce qui n'est pas testable de là est
  **assumé** dans le tableau « Zones non couvertes » de `docs/TRACEABILITE.md`, avec sa raison
  et son mode de vérification manuelle. Ce tableau est un contrat, pas un aveu.

## Choisir le patron

| Sujet | Patron | Exemple à copier |
|---|---|---|
| Règle de détection | `Build.Context(…)` + `Build.Snapshot(…)`, appel direct de `Detect` | `Detection/NewCommentRuleTests.cs` |
| Règle de pipeline | `Build.PipelineContext(run, previous)` | `Detection/PipelineRulesTests.cs` |
| Générateur de liens | URL **littérale complète** attendue | `Links/GitHubLinkBuilderTests.cs` |
| Validation de configuration | `Validate(Func<WatchedAccount, string?>)` | `Configuration/WatcherConfigurationTests.cs` |
| Jetons, migration, application à chaud | `InMemoryConfigurationStore` + `ReversibleSecretProtector` | `Configuration/ConfigurationServiceTests.cs` |
| Cycle complet (un compte) | `MonitorHarness` | `Monitoring/PullRequestMonitorTests.cs` |
| Cycle multi-comptes | `MonitorHarness` + `GatewayFactory.With(provider, gateway)` | `Monitoring/MultiAccountMonitoringTests.cs` |
| Politique de notification | `NotificationDispatcher` + `RecordingNotificationPresenter` | `Notifications/NotificationDispatcherTests.cs` |
| Réessai d'erreur transitoire | `FlakyGateway` + `ImmediateDelayScheduler` | `Monitoring/ResilientSourceControlGatewayTests.cs` |
| Fonction pure du domaine | appel direct, aucun double | `Theming/ThemeResolverTests.cs` |

## Les doubles, et ce qu'ils garantissent

Tout objet de test vient de `tests/CSharpForgeWatcher.Tests/Doubles/Build.cs` — jamais un
`PullRequest` construit à la main. Les personas (`Viewer`/Camille, `Alice`, `Bob`) et les
données d'exemple y sont fixés une fois : les réutiliser garde le dépôt neutre.

| Double | Ce qu'il apporte |
|---|---|
| `FixedClock` | horloge figée, `Advance(…)` pour tester une fenêtre de rafraîchissement |
| `ImmediateDelayScheduler` | l'attente de réessai n'est plus du temps réel, et `RequestedDelays` prouve l'attente exponentielle |
| `ReversibleSecretProtector` | « chiffre » de façon lisible : un test peut vérifier que le clair **n'apparaît pas** dans ce qui est stocké |
| `FakeSourceControlGateway` | données déclarées par `With…`, pannes injectables, et `Calls` — le journal d'appels |
| `StubGatewayFactory` | une passerelle par fournisseur, pour les scénarios multi-comptes |
| `RecordingNotificationPresenter` | ce qui a été affiché ; `ThrowOnShow` simule un canal défaillant |
| `MonitorHarness` | un moniteur complet, plus `State` (l'`AccountSnapshot` du compte de test) et `Reconfigure(…)` |

**`Calls` n'est pas décoratif** : c'est ce qui permet de vérifier qu'on n'appelle **pas trop**.
Une garde de coût — « les discussions ne sont lues que si c'est utile », « une seule lecture par
espace » — ne se démontre que par un compteur (`ThreadCallCount`, `PipelineRunCallCount`), pas
en relisant le code.

## Le jeu de cas minimal

**Règle de détection** — quatre cas, sans exception :

1. le **cas nominal** ;
2. le **cas inchangé** : rien ne doit être émis ;
3. le **premier regard** (`previous` absent) : silence imposé par SPEC-POLL-001 ;
4. au moins un **cas exclu** par les gardes de la règle.

**Générateur de liens** — c'est le seul endroit du dépôt où comparer une chaîne littérale
complète est la bonne façon de tester : ces URL sont un contrat avec un service externe.
Y ajouter le cas de la donnée inattendue (identifiant négatif, espace absent) plutôt que le
seul cas heureux.

**Cycle** — ce que les scénarios de cycle doivent couvrir et qu'on oublie : l'amorçage
silencieux, la panne isolée d'un dépôt ou d'un compte, la purge après retrait d'un élément
surveillé, et **ce qui n'est pas écrit** quand tout échoue (`StateStore.SaveCount`).

## Ce qu'un test ne fait jamais

* lire l'horloge, le réseau ou le disque — `IClock`, un double de passerelle, un store en
  mémoire ;
* dépendre de l'ordre d'exécution d'un autre test, ou d'un état statique mutable ;
* n'affirmer que `Kind` alors que la spec promet un message, une URL ou un compteur : le test
  passerait alors qu'un libellé faux est livré ;
* employer `Assert.AreEqual`, `Assert.IsTrue` ou `Assert.Fail` — NUnit 4 impose
  `Assert.That(…)`, et `Assert.Multiple` pour grouper les vérifications d'un même cas ;
* se contenter d'un message d'échec muet : quand l'assertion n'est pas parlante seule, lui
  donner une phrase en français qui dit **pourquoi** cela compte.

## « Ce comportement n'est pas testable »

Presque toujours faux. Trois recours, dans cet ordre :

1. **Extraire la décision.** Le calcul pur remonte dans `Application` ou `Domain`, il ne reste
   dans `Ui` ou `Infrastructure` que ce qui s'affiche ou parle au réseau. `ThemeResolver` est
   né ainsi : la résolution du thème est testée, sa peinture ne l'est pas.
2. **Rendre la dépendance explicite** par un port déclaré dans `Application/Abstractions/`,
   puis un double. C'est ce qui rend testables l'horloge, le chiffrement, le navigateur et le
   registre.
3. **Assumer**, en dernier recours : une ligne dans le tableau « Zones non couvertes » de
   `docs/TRACEABILITE.md`, avec la raison et le mode de vérification manuelle. Sans cette
   ligne, ce n'est pas une exception, c'est un oubli — et le subagent
   `relecteur-couverture-spec` le signalera.

## Pièges déjà rencontrés

* **Un double trop complaisant rend un test vide de sens.** Cas réel : le protecteur de secret
  se contentait de préfixer le clair, si bien que « le jeton n'est jamais stocké tel quel »
  passait sans rien vérifier. Un double doit reproduire la **propriété** testée, pas seulement
  la signature.
* **Un test qui suit le code au lieu de le précéder** valide l'implémentation, pas la spec. Le
  symptôme : il ne serait jamais passé au rouge avant. Le vrai bénéfice du test-d'abord s'est
  vu deux fois cette session — un état persisté à tort et une validation devenue obsolète ont
  été rattrapés par des tests existants, pas par une relecture.
* **Ajouter un `[Category("SPEC-…")]` sans scénario Gherkin** fait échouer
  `FeatureCoverageTests` : le garde-fou compare les deux dans les deux sens. Écrire le scénario
  en même temps, ou retirer la ligne devenue inutile de `VerificationManuelleOuAVenir`.
* **Étendre une signature du port sans les doubles** : les tests ne compilent plus, et la
  tentation est de retourner `[]` partout. Un double muet rend verts des tests qui ne vérifient
  plus rien (skill `etendre-le-port-de-forge`).
