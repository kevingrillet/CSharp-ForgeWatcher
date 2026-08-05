# Intégration continue

Ce document décrit les pipelines du dépôt, les runners qu'ils exigent, et comment
rejouer exactement les mêmes contrôles sur un poste de développement.

La solution est utilisable sur **GitHub** comme sur **GitLab** : les deux
configurations lancent les mêmes quatre commandes, dans le même ordre.

---

## 1. Vue d'ensemble

| Fichier | Plateforme | Déclencheur | Rôle |
| --- | --- | --- | --- |
| `.github/workflows/ci.yml` | GitHub Actions | `push` et `pull_request` sur `main` / `master` | Compilation Release, tests unitaires, contrôle de mise en forme |
| `.github/workflows/codeql-analysis.yml` | GitHub Actions | `push` et `pull_request` sur `main`, plus le dimanche | Analyse statique de sécurité (CodeQL, C#) |
| `.github/workflows/links.yml` | GitHub Actions | modification d'un `*.md`, plus le dimanche | Liens **internes** de la documentation (mode hors ligne) |
| `.github/workflows/release.yml` | GitHub Actions | poussée d'un tag `v*` | Publication `win-x64`, archive ZIP, release GitHub |
| `.github/workflows/dependabot-auto-merge.yml` | GitHub Actions | pull request ouverte par Dependabot | Fusion automatique des montées **patch** et **mineures** uniquement |
| `.github/dependabot.yml` | GitHub | hebdomadaire (lundi 06:00, Europe/Paris) | Mises à jour des paquets NuGet et des actions |
| `.gitlab-ci.yml` | GitLab CI | `push` et merge request | Mêmes contrôles, en trois étapes (`verification`, `build`, `test`) |

CodeQL compile la solution **explicitement** (`build-mode: manual`) au lieu de s'en
remettre à `autobuild` : le format `.slnx` exige un SDK ≥ 9.0.200, qu'il faut donc
installer soi-même plutôt que d'espérer celui du runner.

---

## 2. Ce que la CI vérifie

Quatre contrôles, ni plus ni moins :

1. **`dotnet restore`** — la restauration passe avec la seule source déclarée dans
   `NuGet.Config` (nuget.org). Ce contrôle attrape le classique « ça restaure chez
   moi » dû à un flux d'entreprise configuré uniquement en local.
2. **`dotnet build -c Release`** — la solution compile en Release. Aucun paramètre
   n'est ajouté pour neutraliser les avertissements : la solution étant configurée
   en `TreatWarningsAsErrors`, **le moindre avertissement fait échouer la CI**.
3. **`dotnet format --verify-no-changes`** — le code respecte le `.editorconfig`
   (indentation, fins de ligne, espaces, tri des `using`). Cette vérification tourne
   dans une tâche séparée : un rouge ici signifie « reformate », pas « le code est cassé ».
4. **`dotnet test -c Release`** — les tests NUnit du domaine et de la couche
   application passent. Le rapport `.trx` est publié en artefact, y compris — et
   surtout — quand les tests échouent.

Ce que la CI **ne** vérifie **pas** : le comportement de l'interface WinForms, les
toasts Windows, le chiffrement DPAPI et les accès au registre. Ces couches n'ont pas
de tests automatisés ; elles ne sont que compilées.

### Découpage en tâches

**GitHub** — deux tâches parallèles, toutes deux sur `windows-latest` :

- `build-et-tests` : restore → build → test → artefact `resultats-tests`
- `mise-en-forme` : restore → `dotnet format --verify-no-changes`

**GitLab** — trois étapes séquentielles :

- `verification` : `mise-en-forme:windows` (et la tâche manuelle Linux, voir §4)
- `build` : `compilation:windows`, qui transmet `bin/` et `obj/` en artefacts
- `test` : `tests:windows`, qui réutilise ces artefacts avec `--no-build`

---

## 3. Runners nécessaires

### GitHub : `windows-latest`, obligatoire

La solution ne compile **que** sur Windows :

| Projet | Cible | Compile sur Linux ? |
| --- | --- | --- |
| `CSharpForgeWatcher.Domain` | `net9.0` | oui |
| `CSharpForgeWatcher.Application` | `net9.0` | oui |
| `CSharpForgeWatcher.Tests` | `net9.0` | oui |
| `CSharpForgeWatcher.Infrastructure` | `net9.0-windows` | non (DPAPI, registre) |
| `CSharpForgeWatcher.Ui` | `net9.0-windows10.0.17763.0` | non (WinForms, toasts) |

Le SDK est installé par `actions/setup-dotnet@v4` avec `dotnet-version: 9.0.x`.

### GitLab : un runner Windows taggé `windows`

Les trois tâches Windows portent `tags: [windows]`. Le runner doit fournir :

- **le SDK .NET 9, version 9.0.200 minimum** — le fichier solution est au format
  `.slnx`, que les SDK antérieurs ne savent pas lire ;
- un exécuteur `shell` (PowerShell) ou `docker-windows`.

Sur GitLab.com, les runners Windows partagés utilisent d'autres tags (par exemple
`saas-windows-medium-amd64`) : remplacer alors `windows` par le tag correspondant
dans le bloc `.socle-windows` de `.gitlab-ci.yml`.

> **Sans runner Windows, les tâches `...:windows` restent en attente indéfiniment.**
> Le pipeline n'échoue pas, il ne démarre simplement jamais. C'est le comportement
> voulu : mieux vaut un pipeline en attente qu'un pipeline vert qui n'a rien vérifié.

---

## 4. Mode dégradé Linux (GitLab uniquement)

La tâche `compilation-partielle:linux` de `.gitlab-ci.yml` existe pour dépanner
quand aucun runner Windows n'est disponible. Elle est **manuelle**
(`when: manual`) et **tolérante à l'échec** (`allow_failure: true`).

- **Image** : `mcr.microsoft.com/dotnet/sdk:9.0`
- **Périmètre** : `Domain`, `Application` et `Tests` uniquement — compiler le projet
  de tests entraîne les deux autres par référence de projet.
- **Hors périmètre** : `Infrastructure` et `Ui` (impossibles sur Linux), ainsi que
  `dotnet format` (voir §7 : le contrôle des fins de ligne CRLF échoue sur une copie
  de travail Linux).

Un vert sur cette tâche ne dit donc **pas** que la solution compile : il dit
seulement que la logique métier et ses tests sont sains. Elle reste facultative et
signalée comme telle pour ne jamais faire passer une validation partielle pour
une validation complète.

---

## 5. Reproduire la CI en local

Prérequis : SDK .NET 9 (>= 9.0.200) sur Windows. À exécuter depuis la racine du dépôt.

```powershell
# 1. Restauration (mêmes sources que la CI)
dotnet restore CSharp-ForgeWatcher.slnx

# 2. Compilation Release — doit finir avec 0 avertissement
dotnet build CSharp-ForgeWatcher.slnx --no-restore -c Release

# 3. Contrôle de mise en forme (ne modifie rien, sort en code 2 s'il y a des écarts)
dotnet format CSharp-ForgeWatcher.slnx --verify-no-changes --no-restore

# 4. Tests unitaires + rapport TRX au même endroit que la CI
dotnet test CSharp-ForgeWatcher.slnx --no-build -c Release --logger trx --results-directory TestResults
```

Commandes utiles autour de ces quatre-là :

```powershell
# Corriger automatiquement tout ce que l'étape 3 signale
dotnet format CSharp-ForgeWatcher.slnx

# Rejouer les tests d'une seule spec (les tests sont tagués par identifiant de spec)
dotnet test CSharp-ForgeWatcher.slnx --no-build -c Release --filter "TestCategory=SPEC-EVT-001"

# Reproduire la publication de la release, tag v1.2.3 par exemple
dotnet publish src/CSharpForgeWatcher.Ui -c Release -r win-x64 --self-contained false -p:Version=1.2.3 -o publish
Compress-Archive -Path 'publish\*' -DestinationPath 'ForgeWatcher-v1.2.3-win-x64.zip' -Force

# Les mêmes étapes, restauration et compilation de la solution comprises
.\scripts\publier.ps1 -Version 1.2.3
Compress-Archive -Path 'publish\*' -DestinationPath 'ForgeWatcher-v1.2.3-win-x64.zip' -Force

# Repartir d'un dépôt propre (bin, obj, publish, TestResults) ; -WhatIf pour simuler
.\scripts\nettoyer.ps1
```

Sur Linux ou macOS, seule la partie multiplateforme est jouable :

```bash
dotnet test tests/CSharpForgeWatcher.Tests/CSharpForgeWatcher.Tests.csproj -c Release
```

---

## 6. Rapports de tests

`dotnet test --logger trx` produit un fichier `.trx` (format Visual Studio) dans
`TestResults/`.

- **GitHub** : le dossier est publié par `actions/upload-artifact@v4` sous le nom
  `resultats-tests`, conservé 14 jours, y compris quand la tâche échoue.
- **GitLab** : le dossier est publié en **artefact brut**, à télécharger depuis la
  tâche. Il n'y a **pas** de `reports: junit`, car GitLab ne sait pas lire le TRX et
  qu'aucune conversion n'est faite aujourd'hui.

Pour alimenter l'onglet « Tests » d'une merge request GitLab, deux possibilités,
détaillées en commentaire dans `.gitlab-ci.yml` :

1. ajouter le paquet `JunitXml.TestLogger` à `CSharpForgeWatcher.Tests.csproj` et
   remplacer le logger par `--logger "junit;LogFilePath=TestResults/resultats.xml"` ;
2. convertir le TRX en JUnit sans toucher au projet, via l'outil `trx2junit` installé
   dans un `after_script`.

La première option est plus propre (pas d'outil externe), la seconde ne demande
aucune modification du projet de tests.

---

## 7. Points d'attention connus

### Fins de ligne : CRLF attendu

Le `.editorconfig` impose `end_of_line = crlf`. Les fichiers `.cs` du dépôt sont
actuellement stockés en **LF**. Conséquence : `dotnet format --verify-no-changes`
signale une erreur `ENDOFLINE` sur pratiquement chaque ligne dès que la copie de
travail contient des LF.

- Sur un runner Windows où `core.autocrlf = true` (le cas des runners
  `windows-latest` de GitHub), le checkout convertit les fichiers en CRLF et le
  contrôle passe.
- Sur Linux, ou sur un poste Windows où `core.autocrlf = false`, le contrôle échoue
  massivement.

**Correctif recommandé** : ajouter à la racine un fichier `.gitattributes` contenant

```gitattributes
* text=auto eol=crlf
```

Le comportement devient alors identique partout, indépendamment de la configuration
git locale. Tant que ce fichier n'existe pas, la réussite de la tâche de mise en
forme repose sur une configuration git implicite du runner.

### Écarts de mise en forme déjà présents

Au moment de la mise en place de ces pipelines, `dotnet format --verify-no-changes`
signale trois écarts d'indentation réels (hors problème de fins de ligne) dans
`src/CSharpForgeWatcher.Application/Detection/Rules/VoteChangedRule.cs` (lignes 67 à 69).
La tâche de mise en forme sera donc rouge à sa première exécution. Correctif :

```powershell
dotnet format CSharp-ForgeWatcher.slnx
```

### Format de solution `.slnx`

`CSharp-ForgeWatcher.slnx` utilise le nouveau format XML de solution, lu par le SDK
.NET **à partir de la version 9.0.200**. `dotnet restore`, `build`, `test` et
`format` l'acceptent tous ; un SDK plus ancien échouera dès la restauration. C'est
pour cette raison que `dotnet-version: 9.0.x` est demandé côté GitHub et que le
runner GitLab doit être maintenu à jour.

### Cache NuGet

- **GitHub** : `actions/cache@v4` sur `~/.nuget/packages`, clé calculée depuis
  `**/*.csproj`, `Directory.Build.props` et `NuGet.Config`. Un changement de version
  de paquet crée une nouvelle entrée ; `restore-keys` sert de repli.
- **GitLab** : le cache ne peut contenir que des chemins situés sous
  `$CI_PROJECT_DIR`. La variable `NUGET_PACKAGES` déplace donc le dossier des
  paquets dans l'espace de travail — sans cela, le cache déclaré serait vide. La clé
  est fixe : le cache NuGet est additif, une restauration ajoute les paquets
  manquants puis republie le cache.

---

## 8. Release et secrets

### Publier une version sur GitHub

```powershell
# Le tag doit commencer par « v » ; le workflow en déduit le numéro de version
git tag v1.0.0
git push origin v1.0.0
```

Le workflow `release.yml` enchaîne alors : restore → build Release → tests →
`dotnet publish -r win-x64 --self-contained false` → archive
`ForgeWatcher-v1.0.0-win-x64.zip` → création de la release GitHub avec l'archive
attachée. Le numéro du tag est injecté dans les assemblies via `-p:Version`, pour
que le binaire livré porte le même numéro que la release.

Un tag contenant un tiret (`v1.1.0-beta.1`) crée automatiquement une **préversion**.

L'archive est **framework-dependent** : légère (~2 Mo), mais le poste cible doit
avoir le **runtime .NET 9 Desktop (x64)**. Pour un exécutable autonome, remplacer
`--self-contained false` par `--self-contained true` dans `release.yml`.

### Quels secrets faut-il configurer ?

**Aucun, par défaut.** La release utilise le jeton `GITHUB_TOKEN` fourni
automatiquement à chaque exécution ; le workflow demande simplement le droit
d'écriture correspondant :

```yaml
permissions:
  contents: write
```

Un secret n'est nécessaire que pour aller au-delà : signature du binaire,
publication sur un dépôt externe, notification vers un outil tiers.

### Ajouter un secret sur GitHub

1. Dépôt → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**.
2. Nommer le secret en majuscules, par exemple `CERTIFICAT_SIGNATURE_BASE64`.
3. L'utiliser dans le workflow, jamais en clair :

```yaml
      - name: Étape ayant besoin du secret
        env:
          CERTIFICAT: ${{ secrets.CERTIFICAT_SIGNATURE_BASE64 }}
        run: ./script-de-signature.ps1
```

### Ajouter un secret sur GitLab

1. Projet → **Settings** → **CI/CD** → **Variables** → **Add variable**.
2. Cocher **Masked** (valeur cachée dans les journaux) et **Protected** si la
   variable ne doit être exposée qu'aux branches et tags protégés.
3. La variable est disponible comme variable d'environnement dans les tâches.

> Règles à ne jamais contourner : un secret ne se met pas dans un fichier YAML ni
> dans un `.env` versionné, et `config.json` / `state.json` — qui contiennent le
> jeton chiffré et l'état de surveillance — restent exclus par `.gitignore`.
