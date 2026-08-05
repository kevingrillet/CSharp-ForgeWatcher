---
name: verifier-avant-commit
description: "Checklist de vérification locale du dépôt Forge Watcher avant de proposer un commit ou une pull request (restauration, compilation sans avertissement, format, tests, cohérence spec / Gherkin / test / traçabilité / CHANGELOG). À utiliser à la fin de toute modification, et dès que la demande ressemble à « c'est bon ? », « vérifie », « prêt à committer », « lance les tests », « avant de pousser »."
---

# Vérifier avant de committer

Toutes les commandes se lancent **depuis la racine du dépôt**. Elles sont à exécuter dans
l'ordre : la suivante n'a pas de sens si la précédente échoue.

## 1. Les quatre commandes obligatoires

```powershell
dotnet restore CSharp-ForgeWatcher.slnx
dotnet build CSharp-ForgeWatcher.slnx -c Release
dotnet format CSharp-ForgeWatcher.slnx --verify-no-changes
dotnet test CSharp-ForgeWatcher.slnx
```

Précisions qui évitent de perdre du temps :

* **`TreatWarningsAsErrors` est déjà dans `Directory.Build.props`** : inutile de passer
  `-warnaserror`, tout `dotnet build` échoue au premier avertissement. Lire malgré tout la
  ligne « 0 Avertissement(s) » du compte rendu — c'est elle qu'on cite dans le rapport.
  Ne jamais faire taire un avertissement en l'ajoutant au `NoWarn` global : le corriger, ou
  le désactiver **localement** par `#pragma warning disable` avec sa justification.
* Compiler en **`Release`** au moins une fois : les analyseurs et l'élagage n'y produisent
  pas exactement les mêmes avertissements qu'en `Debug`.
* **`--verify-no-changes`** ne modifie rien, il échoue. Pour appliquer les corrections :
  `dotnet format CSharp-ForgeWatcher.slnx` (puis relire le diff, l'outil peut reformater
  au-delà de vos lignes).
* `dotnet format` **échoue le plus souvent sur `ENDOFLINE`** : `.editorconfig` impose
  `end_of_line = crlf` et `insert_final_newline = true`. Un fichier écrit en LF est refusé,
  même si le C# est parfait. C'est l'erreur numéro un après une génération de fichier.
* **`dotnet format` ne voit que le C#.** Un `.md`, un `.feature` ou un `.json` écrit en LF
  passe donc inaperçu ici, et n'apparaîtra qu'au premier `git diff` d'un collègue. Après
  toute création de fichier non-C#, normaliser :

  ```powershell
  Get-ChildItem -Recurse -Include *.md,*.feature,*.json -File |
      Where-Object { $_.FullName -notmatch '\\(bin|obj|\.codegraph)\\' } |
      ForEach-Object {
          $brut = [System.IO.File]::ReadAllText($_.FullName)
          $crlf = ($brut -replace "`r`n", "`n") -replace "`n", "`r`n"
          if ($brut -ne $crlf) { [System.IO.File]::WriteAllText($_.FullName, $crlf); $_.Name }
      }
  ```

  Aucun nom en sortie est le résultat attendu.
* La suite de tests s'exécute en quelques centaines de millisecondes (aucun accès réseau,
  disque ou horloge). Un test lent est un signal d'alarme, pas une fatalité.

Cibler pendant l'itération, sans remplacer l'exécution complète finale :

```powershell
dotnet test CSharp-ForgeWatcher.slnx --filter TestCategory=SPEC-EVT-010
dotnet test CSharp-ForgeWatcher.slnx --filter FullyQualifiedName~TargetBranch
```

## 2. Cohérence spec / scénario / test / traçabilité

Toute modification de **comportement** doit se retrouver aux quatre endroits.

**Une bonne partie est déjà automatisée** : `tests/CSharpForgeWatcher.Tests/Features/FeatureCoverageTests.cs`
échoue, dans les deux sens, si un scénario Gherkin cite une spec sans test ou si une spec
testée n'est illustrée par aucun scénario. Concrètement, deux réflexes :

* ajouter un `[Category("SPEC-…")]` **oblige** à écrire le scénario Gherkin correspondant,
  sinon `dotnet test` passe au rouge — ce n'est pas une négligence possible ;
* dès qu'une spec de la liste `VerificationManuelleOuAVenir` reçoit enfin un test, **retirer
  sa ligne** de cette liste. Elle doit rétrécir avec le temps ; si elle grossit, la
  documentation avance plus vite que les tests.

Le script ci-dessous complète le garde-fou sur ce qu'il ne voit pas : les specs **déclarées
dans `docs/specs/` mais citées nulle part**.

```powershell
$specs = Select-String -Path docs\specs\*.md -Pattern '^#+\s+(SPEC-[A-Z]+-\d+)' |
    ForEach-Object { $_.Matches[0].Groups[1].Value } | Sort-Object -Unique
$couvertes = Get-ChildItem tests -Recurse -Filter *.cs |
    Select-String -Pattern 'Category\("(SPEC-[^"]+)"\)' -AllMatches |
    ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
Compare-Object $specs $couvertes
```

Lecture du résultat :

* `<=` : spec **sans test**. Deux issues acceptables seulement — écrire le test, ou
  inscrire la zone dans le tableau « Zones non couvertes par des tests automatisés » de
  `docs/TRACEABILITE.md` avec la raison et le mode de vérification manuelle.
* `=>` : test référençant une spec **inexistante** — presque toujours une faute de frappe
  dans l'identifiant. À corriger, sinon `--filter TestCategory=…` ne trouvera jamais rien.

Puis, à la main :

- [ ] chaque comportement nouveau ou modifié a une section `SPEC-…` dans `docs/specs/`
- [ ] chaque `SPEC-…` touchée a un scénario taggé dans `docs/features/*.feature`
- [ ] chaque `SPEC-…` touchée a une ligne dans le tableau de `docs/TRACEABILITE.md`
- [ ] `CHANGELOG.md` porte une entrée formulée **côté utilisateur** (ce qu'il gagne), pas
      côté implémentation
- [ ] `README.md` est à jour si le changement est visible par l'utilisateur (tableau
      « Ce qui est notifié », réglages, dépannage)
- [ ] un choix structurant a son ADR dans `docs/adr/` (numérotation continue)
- [ ] si une méthode a été ajoutée au port de forge, les **trois** adaptateurs et les
      **deux** doubles de test la portent — dérouler le skill `etendre-le-port-de-forge`

## 3. Conventions de code

- [ ] tout membre `public` a une doc XML **en français** (`<summary>`, `<param>`,
      `<returns>` quand il apporte quelque chose ; `<inheritdoc />` pour une
      implémentation d'interface)
- [ ] les commentaires expliquent **pourquoi**, pas quoi ; ils citent la spec ou l'ADR
      quand une décision n'est pas évidente
- [ ] noms de tests en français avec underscores, décrivant le comportement attendu
- [ ] `Assert.That(...)` partout (jamais `Assert.AreEqual`), `Assert.Multiple` pour
      grouper les vérifications d'un même cas
- [ ] objets de test construits via `tests/CSharpForgeWatcher.Tests/Doubles/Build.cs`
- [ ] aucun message utilisateur en anglais (l'application est francophone,
      `NeutralLanguage` vaut `fr-FR`)

## 4. Sécurité et propreté du dépôt

- [ ] aucun jeton, mot de passe, URL d'organisation réelle ni identité réelle dans le code,
      les tests, la documentation ou un fichier d'exemple — les tests utilisent
      `https://dev.azure.com/contoso` et des GUID factices, s'y tenir
- [ ] aucun chemin absolu de poste de travail ni nom de compte : les chemins sont relatifs
      à la racine du dépôt, les emplacements utilisateur passent par `%APPDATA%`
- [ ] rien de secret ni de contenu de commentaire n'est écrit dans le journal
- [ ] aucun `bin/`, `obj/`, `.corrupt`, `config.json` ou `state.json` ajouté au dépôt
- [ ] aucun flux NuGet privé introduit dans `NuGet.Config` (le `<clear />` est volontaire)

```powershell
Get-ChildItem src, tests, docs -Recurse -Include *.cs,*.md |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    Select-String -Pattern '(?i)((pat|password|secret|token)\s*=\s*"[^"]|bearer\s+[A-Za-z0-9]{8}|C:\\Users\\|@[a-z0-9._-]+\.(com|fr|net)\b)' |
    Where-Object { $_.Line -notmatch 'PasswordChar|ProtectedPersonalAccessToken' } |
    ForEach-Object { "$($_.Filename):$($_.LineNumber) -> $($_.Line.Trim())" }
```

Aucune ligne en sortie est le résultat attendu. Les deux exclusions écartent des faux
positifs connus et légitimes (`UseSystemPasswordChar` de la zone de saisie du jeton, et le
nom de la propriété qui porte le jeton **chiffré**). Toute autre ligne se lit, elle ne se
suppose pas : une chaîne peut être un exemple fictif acceptable — auquel cas la sortie doit
rester vide après vérification, sinon il faut affiner la donnée d'exemple, pas le motif.

**Neutralité du dépôt** — aucun nom d'entreprise, de produit interne ni de personne, y
compris dans les données de test, les exemples de documentation et les URL d'exemple. Le
balayage porte sur **tous** les fichiers, pas seulement le C# :

```powershell
Get-ChildItem -Recurse -File |
    Where-Object {
        $_.FullName -notmatch '\\(bin|obj|\.codegraph)\\' -and
        $_.Extension -notin '.ico', '.png', '.dll', '.exe', '.pdb', '.db'
    } |
    Select-String -Pattern 'NomDEntreprise', 'NomDeProduit', 'Prénom' -SimpleMatch |
    ForEach-Object { "$($_.Path):$($_.LineNumber)" }
```

Remplacer les trois motifs par les termes réellement à bannir avant de lancer la commande —
ils ne sont volontairement pas écrits en dur ici, ce fichier étant lui-même versionné.

## 5. Architecture

Dérouler le skill `respecter-architecture` — quatre `grep` et une lecture des
`ProjectReference`. C'est la vérification la plus vite oubliée et la plus coûteuse à
rattraper après coup.

## 6. Rapport

Ne jamais annoncer « c'est vérifié » sans donner : le résultat des quatre commandes
(avertissements et tests chiffrés), les écarts de cohérence restants avec leur
justification, et ce qui n'a **pas** pu être vérifié automatiquement (rendu WinForms,
toasts Windows, DPAPI, appels réels vers la forge — cf. `docs/TRACEABILITE.md`).
