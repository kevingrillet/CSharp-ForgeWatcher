---
name: ajouter-notification
description: "Procédure complète pour ajouter un type d'activité notifiée dans Forge Watcher (nouvelle règle de détection, nouvelle valeur de NotificationKind, nouvelle préférence). À utiliser dès qu'une demande revient à « notifier aussi quand … », « ajouter un événement », « nouvelle règle de détection », « nouveau type de notification », ou dès qu'il faut toucher à Detection/Rules, NotificationKind ou NotificationPreferences."
---

# Ajouter un type d'activité notifiée

Ordre **impératif** : spec → scénario → test → règle → enregistrement → préférence →
traçabilité. On n'écrit jamais la règle avant le test, ni le test avant la spec : c'est
la spec qui fixe le comportement attendu, et son identifiant qui relie tout le reste.

Fil rouge de cette fiche : « la branche cible de ma pull request a changé »
(`SPEC-EVT-010`, `NotificationKind.TargetBranchChanged`). Remplacer par le cas réel.

## Avant de commencer — lire ces trois fichiers

| Fichier | Ce qu'on y prend |
|---|---|
| `docs/specs/SPEC-EVENEMENTS.md` | le style de rédaction et le prochain numéro libre |
| `src/CSharpForgeWatcher.Application/Detection/Rules/ThreadStatusChangedRule.cs` | la règle la plus représentative (gardes, dedupKey, message) |
| `src/CSharpForgeWatcher.Application/Detection/DetectionContext.cs` | les propriétés calculées disponibles — ne jamais recalculer ce qui existe |

## 1. Spécifier — `docs/specs/SPEC-EVENEMENTS.md`

Ajouter une section `## SPEC-EVT-0NN` à la suite des existantes, dans le style du fichier
(*Étant donné / Quand / Alors*, puis une liste numérotée « Règles » pour les cas limites) :

```markdown
## SPEC-EVT-010 — La branche cible d'une pull request de l'observateur change

*Étant donné* une pull request dont l'observateur est l'auteur, présente dans l'instantané
*Quand* sa branche cible diffère de celle mémorisée
*Alors* un événement `TargetBranchChanged` est émis, indiquant l'ancienne et la nouvelle
branche ; le clic ouvre la pull request.

Règles :
1. Aucun événement sur les PR dont l'observateur n'est pas l'auteur : information sans action.
2. Aucun événement pendant le cycle d'amorçage (`SPEC-POLL-001`).
3. Une branche cible absente de la réponse de la forge est ignorée (pas de fausse détection).
```

Vocabulaire du fichier : **l'observateur** = l'utilisateur de l'application ;
**instantané** = état mémorisé au cycle précédent. S'y tenir.

## 2. Écrire le scénario — `docs/features/detection-evenements.feature`

Gherkin **en français**, un tag par identifiant de spec. Le fichier est la formulation
lisible de la spec ; la preuve exécutable, c'est le test NUnit portant le même
`[Category]`. Créer le fichier s'il n'existe pas encore.

```gherkin
# language: fr
Fonctionnalité: Détection des événements de pull request

  @SPEC-EVT-010
  Scénario: La branche cible de ma pull request change
    Étant donné une pull request dont je suis l'auteur, ciblant « main », déjà mémorisée
    Quand sa branche cible devient « release/2026.1 »
    Alors une notification « Branche cible modifiée » est émise
    Et le clic sur la notification ouvre la pull request

  @SPEC-EVT-010
  Scénario: Une branche cible inchangée ne notifie rien
    Étant donné une pull request déjà mémorisée, ciblant « main »
    Quand un cycle l'observe encore ciblant « main »
    Alors aucune notification n'est émise
```

## 3. Écrire le test **avant** la règle — `tests/CSharpForgeWatcher.Tests/Detection/`

Un fichier `<NomDeLaRegle>Tests.cs`. Conventions non négociables :

* `[TestFixture]` + `sealed class`, un fichier par règle ;
* `[Category("SPEC-EVT-0NN")]` sur la classe si elle ne couvre **qu'une** spec, sur chaque
  `[Test]` si le fichier couvre plusieurs specs (cf. `NewCommentRuleTests.cs`) ;
* noms de tests **en français, avec des underscores**, décrivant le comportement attendu ;
* `Assert.That(...)` uniquement (style contraint NUnit 4) ; `Assert.Multiple` pour grouper
  plusieurs vérifications d'un même cas ;
* tout objet vient de `tests/CSharpForgeWatcher.Tests/Doubles/Build.cs` — ne jamais construire
  un `PullRequest` à la main dans un test.

```csharp
using CSharpForgeWatcher.Application.Detection.Rules;
using CSharpForgeWatcher.Domain.Events;
using CSharpForgeWatcher.Tests.Doubles;

namespace CSharpForgeWatcher.Tests.Detection;

/// <summary>SPEC-EVT-010 — changement de branche cible.</summary>
[TestFixture]
[Category("SPEC-EVT-010")]
public sealed class TargetBranchChangedRuleTests
{
    private readonly TargetBranchChangedRule _rule = new();

    [Test]
    public void Un_changement_de_branche_cible_sur_ma_PR_est_signale()
    {
        var avant = Build.Pull(author: Build.Viewer);              // cible « main »
        var apres = avant with { TargetBranch = "release/2026.1" };

        var evenements = _rule.Detect(Build.Context(apres, previous: Build.Snapshot(avant))).ToList();

        Assert.That(evenements, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(evenements[0].Kind, Is.EqualTo(NotificationKind.TargetBranchChanged));
            Assert.That(evenements[0].Message, Does.Contain("release/2026.1"));
            Assert.That(evenements[0].Url, Does.EndWith("/pullrequest/42"));
        });
    }

    [Test]
    public void Une_branche_cible_inchangee_ne_produit_rien()
    {
        var pullRequest = Build.Pull(author: Build.Viewer);

        Assert.That(_rule.Detect(Build.Context(pullRequest, previous: Build.Snapshot(pullRequest))), Is.Empty);
    }

    [Test]
    public void Sans_etat_precedent_la_regle_se_tait()
    {
        Assert.That(_rule.Detect(Build.Context(Build.Pull(author: Build.Viewer))), Is.Empty);
    }

    [Test]
    public void Un_changement_sur_la_PR_dun_autre_est_ignore()
    {
        var avant = Build.Pull(author: Build.Alice);
        var apres = avant with { TargetBranch = "release/2026.1" };

        Assert.That(_rule.Detect(Build.Context(apres, previous: Build.Snapshot(avant))), Is.Empty);
    }
}
```

Le jeu de tests minimal attendu pour toute règle : **le cas nominal**, **le cas
inchangé**, **le premier regard** (`previous` absent, cf. SPEC-POLL-001) et **au moins un
cas exclu** par les gardes.

## 4. Déclarer le type — `src/CSharpForgeWatcher.Domain/Events/NotificationKind.cs`

Trois modifications dans ce seul fichier :

1. une valeur dans l'`enum NotificationKind` ;
2. un cas dans `ToLabel()` — c'est le titre de la notification ;
3. un cas dans `ToDescription()` — c'est la ligne affichée dans l'onglet *Notifications*.

**L'ordre de déclaration est une priorité**, pas un détail : pour un même fait, seule la
valeur numérique la plus basse est retenue (tri final du détecteur). Un événement plus
précis que « commentaire sur ma PR » se place donc *avant* lui, quitte à renuméroter les
suivants — c'est sans risque, les enums sont sérialisés **par leur nom** dans
`state.json` et `config.json`, jamais par leur valeur. Un événement générique va à la fin.

```csharp
    /// <summary>La branche cible d'une PR de l'utilisateur a changé (SPEC-EVT-010).</summary>
    TargetBranchChanged = 9,
```

Rien à faire côté interface : `SettingsForm` construit la liste des cases à cocher en
parcourant `NotificationKindExtensions.All`, qui dérive de `Enum.GetValues`.

## 5. Implémenter la règle — `src/CSharpForgeWatcher.Application/Detection/Rules/`

Un fichier `<Nom>Rule.cs`, `public sealed class … : IPullRequestEventRule`. L'exemple ci-dessous lit `precedent.TargetBranch`, qui n'est **pas** encore mémorisé : dérouler l'étape 8 avant d'espérer compiler.

```csharp
using CSharpForgeWatcher.Domain.Events;

namespace CSharpForgeWatcher.Application.Detection.Rules;

/// <summary>
/// SPEC-EVT-010 — la branche cible d'une pull request de l'utilisateur a changé.
/// </summary>
/// <remarks>
/// Restreinte aux PR dont l'utilisateur est l'auteur : sur les PR des autres, le
/// changement est une information sans action associée.
/// </remarks>
public sealed class TargetBranchChangedRule : IPullRequestEventRule
{
    /// <inheritdoc />
    public string Name => "Changement de branche cible";

    /// <inheritdoc />
    public bool RequiresThreads => false;

    /// <inheritdoc />
    public IEnumerable<PullRequestEvent> Detect(DetectionContext context)
    {
        if (context.Previous is not { } precedent || !context.ViewerIsAuthor)
        {
            yield break;
        }

        var cible = context.PullRequest.TargetBranch;
        if (string.IsNullOrEmpty(cible)
            || string.Equals(precedent.TargetBranch, cible, StringComparison.Ordinal))
        {
            yield break;
        }

        yield return context.CreateEvent(
            NotificationKind.TargetBranchChanged,
            $"Branche cible : {precedent.TargetBranch} → {cible}",
            dedupKey: $"targetbranch|{context.Key}|{cible}");
    }
}
```

Contrat d'une règle — le détecteur ne le rattrapera pas à votre place :

* **fonction pure** : même contexte, même résultat. Ni réseau, ni disque, ni
  `DateTimeOffset.Now` (ADR-0003) ; la date du cycle est `context.ObservedOn`, utilisée par
  défaut par `CreateEvent`.
* **tolérante** : donnée manquante ⇒ `yield break`, jamais d'exception.
* **`RequiresThreads`** vaut `true` **seulement** si la règle lit `context.Threads` : cette
  propriété décide si le monitor paie un appel réseau supplémentaire (SPEC-POLL-003).
* **`dedupKey`** identifie le *fait*, pas le type. Deux règles qui décrivent le même fait
  doivent produire la **même** clé : le détecteur ne garde alors que l'intitulé le plus
  précis (c'est ainsi qu'une mention dans une réponse ne notifie qu'une fois). **Ne pas y
  mettre le compte** : `CreateEvent` préfixe déjà la clé de `AccountId`, de sorte que deux
  comptes surveillant le même dépôt notifient chacun de leur côté (SPEC-CFG-008). Idem pour
  le libellé du compte, repris automatiquement dans l'événement.
* **identifiants de commentaire et de discussion : `long`**, jamais `int` — ceux de certaines
  forges dépassent les 32 bits (SPEC-FORGE-006).
* réutiliser les gardes du contexte : `IsFirstSight`, `ViewerIsAuthor`, `ViewerIsReviewer`,
  `ViewerIsInvolved`, `ShouldIgnoreActor(acteur)`. Cette dernière porte déjà l'option
  « me notifier de mes propres actions » — ne pas la réimplémenter.

## 6. Mettre la règle en service

Une seule ligne, dans `PullRequestEventDetector.CreateDefaultRules()`
(`src/CSharpForgeWatcher.Application/Detection/PullRequestEventDetector.cs`) :

```csharp
        new TargetBranchChangedRule(),
```

L'injection de dépendances suit automatiquement :
`ApplicationServiceCollectionExtensions.AddForgeWatcherApplication` boucle sur
`CreateDefaultRules()`. **Ne rien ajouter** dans la racine de composition de l'interface.

## 7. Ajouter la préférence — `src/CSharpForgeWatcher.Application/Configuration/NotificationPreferences.cs`

Trois modifications, toutes dans ce fichier :

```csharp
    /// <summary>Notifier les changements de branche cible sur mes PR (SPEC-EVT-010).</summary>
    public bool TargetBranchChanged { get; set; } = true;
```

puis un cas dans `IsEnabled` et un dans `SetEnabled`. Le nom de la propriété **doit** être
celui de la valeur d'enum : c'est ce qui rend `config.json` lisible. Un type absent de
`IsEnabled` retombe sur `_ => false` et ne notifiera **jamais** — c'est le piège classique.

## 8. Si la règle compare une donnée non mémorisée

`PullRequestSnapshot` (`src/CSharpForgeWatcher.Domain/Monitoring/PullRequestSnapshot.cs`) est
la **seule** structure à faire évoluer. Ajouter la propriété *et* son affectation dans
`PullRequestSnapshot.From(...)` :

```csharp
    /// <summary>Branche cible au dernier cycle (SPEC-EVT-010).</summary>
    public string TargetBranch { get; set; } = string.Empty;
```

Un champ ajouté est absent des `state.json` déjà sur disque : il vaudra sa valeur par
défaut au premier cycle après mise à jour. Vérifier que ça ne produit pas une fausse
détection (ici, la garde `string.IsNullOrEmpty(precedent.TargetBranch)` n'est pas nécessaire
car la comparaison échoue avant d'émettre — mais y penser systématiquement).

## 9. Refermer la boucle documentaire

| Fichier | Modification |
|---|---|
| `docs/TRACEABILITE.md` | une ligne `SPEC-EVT-010 \| Branche cible modifiée \| Detection/TargetBranchChangedRuleTests.cs` |
| `CHANGELOG.md` | une entrée sous *Ajouté*, formulée côté utilisateur |
| `README.md`, tableau « Ce qui est notifié » | une ligne (notification, quand, ce que le clic ouvre) |
| `docs/SDD.md` §2.1 | une exigence `EF-n` si le besoin est nouveau, pas seulement une variante |

## 10. Vérifier

```powershell
dotnet test CSharp-ForgeWatcher.slnx --filter TestCategory=SPEC-EVT-010
```

puis la checklist complète du skill `verifier-avant-commit`.

## Pièges déjà rencontrés

* **Règle en itérateur `yield` non matérialisé** : le détecteur matérialise déjà chaque
  règle dans son bloc de protection, mais un `Detect` qui lève *avant* le premier `yield`
  n'est pas plus protégé. Mettre les gardes en tête, pas de calcul risqué avant.
* **Événement en doublon** : deux règles qui voient le même fait sans partager de
  `dedupKey`. Le symptôme est deux notifications pour un seul changement.
* **Type activé nulle part** : préférence oubliée dans `IsEnabled` → aucune notification,
  aucun message d'erreur, tests unitaires verts. Toujours dérouler l'étape 7.
* **Notification émise pour ses propres actions** : garde `ShouldIgnoreActor` oubliée.
