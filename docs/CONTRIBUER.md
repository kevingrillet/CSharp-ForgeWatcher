# Contribuer à Forge Watcher

Ce guide décrit **comment on travaille dans ce dépôt**. Il tient en une règle : un
comportement se spécifie, puis se raconte, puis se teste, puis s'implémente — jamais
l'inverse.

Pour l'architecture et le *pourquoi* des choix, voir [`SDD.md`](SDD.md) et
[`adr/`](adr/). Pour l'usage de l'application, le [`README`](../README.md).

---

## 1. La démarche : spec → Gherkin → test → code

| Étape | Où | Ce qu'on y écrit |
|---|---|---|
| 1. **Spécifier** | `docs/specs/SPEC-*.md` | une section `## SPEC-XXX-0NN` en *Étant donné / Quand / Alors*, suivie d'une liste « Règles » numérotée pour les cas limites |
| 2. **Raconter** | `docs/features/*.feature` | le même comportement en Gherkin français, chaque scénario taggé `@SPEC-XXX-0NN` |
| 3. **Tester** | `tests/CSharpForgeWatcher.Tests/` | un test NUnit qui échoue, portant `[Category("SPEC-XXX-0NN")]` |
| 4. **Implémenter** | `src/…` | le minimum pour rendre le test vert |
| 5. **Tracer** | `docs/TRACEABILITE.md`, `CHANGELOG.md` | la ligne spec → test, et l'entrée utilisateur |

Cette démarche suppose l'étape 1 rédigeable — donc le comportement déjà tranché. Quand ce
n'est pas le cas (« il faudrait aussi notifier quand… »), le skill
[`cadrer-un-comportement`](../.claude/skills/cadrer-un-comportement/SKILL.md) déroule
l'interrogatoire qui produit la liste « Règles » et rend le squelette à coller.

L'identifiant de spec est le fil qui relie les cinq étapes. Il permet de rejouer un
comportement précis :

```powershell
dotnet test CSharp-ForgeWatcher.slnx --filter TestCategory=SPEC-EVT-005
```

Familles d'identifiants : `SPEC-EVT` (détection), `SPEC-POLL` (cycle de sondage),
`SPEC-CFG` (configuration), `SPEC-NOTIF` (notifications), `SPEC-LINK` (URL),
`SPEC-FORGE` (abstraction de la forge), `SPEC-PIPE` (pipelines), `SPEC-UI` (apparence).

Les fichiers `.feature` ne sont pas exécutés par un moteur : ils sont la formulation
lisible de la spec, et la preuve exécutable est le test NUnit qui porte le même
identifiant. C'est pour cela que le tag et la catégorie doivent être **strictement**
identiques.

Un choix structurant (dépendance nouvelle, mécanisme de stockage, rupture de
compatibilité) se consigne dans un ADR : `docs/adr/000N-titre-court.md`, numérotation
continue.

---

## 2. Architecture — la règle en une ligne

Les dépendances pointent **vers l'intérieur**, sans exception.

| Couche | Peut référencer | Contient |
|---|---|---|
| `src/CSharpForgeWatcher.Domain/` | **rien** | entités, objets-valeur, énumérations, instantanés |
| `src/CSharpForgeWatcher.Application/` | `Domain` + `Microsoft.Extensions.*.Abstractions` | cas d'usage, détection, notifications, **ports** (`Abstractions/`) |
| `src/CSharpForgeWatcher.Infrastructure/` | `Application` | REST, JSON, DPAPI, registre, journal |
| `src/CSharpForgeWatcher.Ui/` | `Application` + `Infrastructure` | WinForms, zone de notification, racine de composition |
| `tests/CSharpForgeWatcher.Tests/` | `Domain` + `Application` | NUnit, doubles dans `Doubles/` |

Ce que cette contrainte achète : le cœur métier se teste sans Windows, sans réseau et sans
disque, et la suite complète s'exécute en une fraction de seconde.

Interdits, avec leur remplacement :

| Interdit | À la place |
|---|---|
| appel HTTP hors du port de forge | `ISourceControlGateway`, implémenté dans `Infrastructure` |
| `System.Windows.Forms` / `System.Drawing` hors de `Ui` | remonter la décision dans `Application`, notifier via `INotificationPresenter` |
| accès disque ou registre hors de `Infrastructure` | `IConfigurationStore`, `IMonitorStateStore`, `IAutoStartService` |
| `DateTime.Now` dans `Domain` ou `Application` | `IClock`, ou `context.ObservedOn` dans une règle de détection (ADR-0003) |
| secret en clair, où que ce soit | `ISecretProtector` ; en test, `https://dev.azure.com/contoso` et des GUID factices |
| vocabulaire propre à une forge dans `Domain` | traduire dans le mappeur d'infrastructure (SPEC-FORGE-005) |

---

## 3. Conventions de code

* **Français** : commentaires, documentation XML, messages d'erreur, libellés, noms de
  tests, entrées de journal des modifications. Aucun message utilisateur en anglais.
* **Documentation XML sur tout membre `public`** : `<summary>` qui dit *pourquoi*,
  `<remarks>` pour la décision de conception et la référence à la spec ou à l'ADR,
  `<inheritdoc />` sur une implémentation d'interface. `CS1591` étant neutralisé, le
  compilateur ne rappelle rien : c'est une discipline.
* **Commentaires** : ils expliquent une décision ou un piège, jamais ce que le code dit
  déjà.
* **Style** : `.editorconfig` fait loi — 4 espaces, **fins de ligne CRLF**, saut de ligne
  final, `namespace` de portée fichier, accolade sur une nouvelle ligne, champs privés en
  `_camelCase`. Un fichier écrit en LF fait échouer `dotnet format` sur `ENDOFLINE`.
* **Tests** : `[TestFixture]` sur une classe `sealed` ; `[Category("SPEC-…")]` sur la
  classe si elle ne couvre qu'une spec, sur chaque `[Test]` sinon ; noms de méthodes en
  français avec underscores décrivant le comportement attendu ; `Assert.That(...)`
  exclusivement, `Assert.Multiple` pour grouper ; tout objet vient de `Doubles/Build.cs`.

```csharp
[Test]
[Category("SPEC-EVT-005")]
public void Une_reponse_dans_une_discussion_ou_jai_ecrit_est_signalee_comme_reponse()
```

Pour une règle de détection, le jeu de tests minimal est : cas nominal, cas inchangé,
premier regard (`previous` absent, SPEC-POLL-001), et au moins un cas exclu par les gardes.
Le catalogue des patrons de test par sujet — règle, générateur de liens, cycle complet,
multi-comptes — est dans le skill
[`ecrire-un-test`](../.claude/skills/ecrire-un-test/SKILL.md).

Deux garde-fous automatiques complètent cette discipline, dans
`tests/CSharpForgeWatcher.Tests/Features/FeatureCoverageTests.cs` : ils font échouer
`dotnet test` si un scénario Gherkin cite une spec que plus aucun test ne vérifie, **ou** si
une spec testée n'est racontée par aucun scénario. Ajouter un `[Category("SPEC-…")]` oblige
donc à écrire le scénario — ce n'est pas une négligence possible.

---

## 4. Checklist avant de committer

```powershell
dotnet restore CSharp-ForgeWatcher.slnx
dotnet build CSharp-ForgeWatcher.slnx -c Release
dotnet format CSharp-ForgeWatcher.slnx --verify-no-changes
dotnet test CSharp-ForgeWatcher.slnx
```

`TreatWarningsAsErrors` est **déjà** dans `Directory.Build.props` : tout `dotnet build` échoue
au premier avertissement, l'objectif de **0 avertissement** est donc tenu par la compilation
elle-même. Pour appliquer les corrections de format : `dotnet format CSharp-ForgeWatcher.slnx`.

Attention, `dotnet format` **ne voit que le C#** : un `.md` ou un `.feature` écrit en LF passe
sa vérification et n'apparaîtra qu'au premier `git diff` d'un collègue. La commande de
normalisation est dans le skill `verifier-avant-commit`.

Puis :

- [ ] spec, scénario Gherkin, test et ligne de traçabilité en place pour chaque
      comportement nouveau ou modifié
- [ ] `CHANGELOG.md` complété, formulé côté utilisateur
- [ ] `README.md` à jour si le changement est visible
- [ ] règles de dépendance respectées (§2)
- [ ] aucun secret, aucune donnée réelle, aucun chemin absolu de poste de travail
- [ ] aucun `bin/`, `obj/`, `config.json` ni `state.json` ajouté au dépôt

Le détail, avec les scripts de contrôle de cohérence, est dans le skill
[`verifier-avant-commit`](../.claude/skills/verifier-avant-commit/SKILL.md).

---

## 5. Outillage Claude Code

Le dépôt fournit des *skills* (procédures à suivre) et des *subagents* (relecteurs à
déléguer) dans `.claude/`.

### Skills

| Skill | Quand l'utiliser |
|---|---|
| [`cadrer-un-comportement`](../.claude/skills/cadrer-un-comportement/SKILL.md) | avant d'écrire une spec : choisir la famille et le numéro, dérouler les questions qui produisent les « Règles », ou constater que la demande n'est pas cadrable |
| [`ajouter-notification`](../.claude/skills/ajouter-notification/SKILL.md) | notifier un nouveau genre d'activité — la procédure complète, de la spec au CHANGELOG |
| [`ecrire-un-test`](../.claude/skills/ecrire-un-test/SKILL.md) | avant d'écrire un test : le patron qui correspond au sujet, et les doubles à réutiliser |
| [`rediger-la-documentation`](../.claude/skills/rediger-la-documentation/SKILL.md) | choisir **quel** document porte un changement, et le rédiger dans le style du dépôt |
| [`respecter-architecture`](../.claude/skills/respecter-architecture/SKILL.md) | avant de créer un fichier, d'ajouter un `using`, une référence ou un appel technique |
| [`verifier-avant-commit`](../.claude/skills/verifier-avant-commit/SKILL.md) | à la fin de toute modification |
| [`ajouter-une-forge`](../.claude/skills/ajouter-une-forge/SKILL.md) | brancher une forge de plus (SPEC-FORGE) — trois sont implémentées |
| [`etendre-le-port-de-forge`](../.claude/skills/etendre-le-port-de-forge/SKILL.md) | poser une nouvelle question à la forge : une méthode du port, répercutée sur trois adaptateurs et deux doubles |

### Subagents

| Subagent | Ce qu'il relit |
|---|---|
| [`relecteur-architecture`](../.claude/agents/relecteur-architecture.md) | règles de dépendance, fuites techniques, testabilité |
| [`relecteur-couverture-spec`](../.claude/agents/relecteur-couverture-spec.md) | comportement modifié sans spec, sans scénario, sans test ou sans traçabilité |
| [`relecteur-conventions`](../.claude/agents/relecteur-conventions.md) | français, documentation XML, style NUnit, cohérence de la documentation, absence de secret |
| [`relecteur-forge`](../.claude/agents/relecteur-forge.md) | contrat SPEC-FORGE d'un adaptateur : lecture seule, vocabulaire, identifiants, capacités absentes, erreurs |

Les quatre relecteurs sont en lecture seule : ils rendent un verdict, ils ne corrigent rien.
Enchaînement recommandé pour une modification de comportement : `cadrer-un-comportement` si le
comportement n'est pas encore tranché → le skill de la procédure concernée →
`verifier-avant-commit` → les relecteurs utiles (les quatre si un adaptateur de
forge est touché, les trois premiers sinon).

---

## 6. Ajouter un type de notification — la version courte

Le détail, avec le code, est dans le skill `ajouter-notification`.

1. `docs/specs/SPEC-EVENEMENTS.md` — une section `SPEC-EVT-0NN`
2. `docs/features/detection-evenements.feature` — un scénario taggé
3. `tests/CSharpForgeWatcher.Tests/Detection/<Nom>RuleTests.cs` — le test, **avant** le code
4. `src/CSharpForgeWatcher.Domain/Events/NotificationKind.cs` — la valeur, son libellé, sa
   description (l'ordre de déclaration est une priorité d'intitulé)
5. `src/CSharpForgeWatcher.Application/Detection/Rules/<Nom>Rule.cs` — la règle
   (`IPullRequestEventRule`, pure, synchrone, tolérante)
6. `PullRequestEventDetector.CreateDefaultRules()` — une ligne ; l'injection de
   dépendances suit automatiquement
7. `NotificationPreferences` — la propriété, plus un cas dans `IsEnabled` et `SetEnabled`
8. si la règle compare une donnée non mémorisée : `PullRequestSnapshot` et sa méthode
   `From(...)`
9. `docs/TRACEABILITE.md`, `CHANGELOG.md`, tableau du `README.md`

L'interface n'est pas à modifier : la fenêtre de configuration construit ses cases à cocher
en parcourant `NotificationKindExtensions.All`.
