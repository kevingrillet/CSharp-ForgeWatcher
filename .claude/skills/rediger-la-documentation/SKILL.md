---
name: rediger-la-documentation
description: "Choisir quel document du dépôt Forge Watcher porte un changement — spec SPEC-*, scénario Gherkin, SDD, ADR, README, CHANGELOG, traçabilité — et le rédiger dans le style du dépôt. À utiliser dès qu'il faut écrire ou modifier un fichier de docs/, un .feature, le README ou le CHANGELOG, dès qu'une décision de conception mérite d'être consignée, et quand la question « où est-ce que ça s'écrit ? » se pose."
---

# Rédiger la documentation

Ce dépôt a **huit** endroits où écrire, et la faute la plus fréquente n'est pas de mal
rédiger : c'est d'écrire au mauvais endroit, ou aux deux à la fois. Une information dupliquée
dérive, et une documentation qui dérive devient un piège — elle a plus de crédit qu'un
commentaire, donc elle égare plus longtemps.

La démarche générale (spec → Gherkin → test → code) et les conventions de forme sont dans
[`docs/CONTRIBUER.md`](../../../docs/CONTRIBUER.md). **Ne pas les recopier ici** : cette fiche
répond à une autre question, celle de l'aiguillage et du style de chaque document.

## 1. Où écrit-on ? — le tableau d'aiguillage

| Ce que tu as changé | Où ça s'écrit | Où ça ne s'écrit **pas** |
|---|---|---|
| Un comportement observable par l'utilisateur | `docs/specs/SPEC-*.md`, une section `## SPEC-XXX-0NN` | pas dans le SDD : il décrit l'architecture, pas les cas |
| Le même comportement, raconté | `docs/features/*.feature`, un scénario taggé | — |
| Le lien spec → test | `docs/TRACEABILITE.md`, une ligne | — |
| Ce que l'utilisateur y gagne | `CHANGELOG.md`, sous `[Non publié]` | pas le détail d'implémentation |
| Comment on s'en sert | `README.md` (démarrage, réglages, dépannage, limites) | pas de justification de conception |
| **Pourquoi** une option a été retenue contre une autre | `docs/adr/000N-titre.md` | pas dans le README, pas dans un commentaire de 30 lignes |
| Comment le système est assemblé, ce qu'un cycle coûte | `docs/SDD.md`, section numérotée | pas dans un ADR : l'ADR tranche, le SDD décrit |
| Une procédure répétable pour la suite | `.claude/skills/<nom>/SKILL.md` | pas dans `CONTRIBUER.md`, qui reste une vue d'ensemble |

Trois arbitrages qui reviennent :

* **Spec ou SDD ?** La spec dit *ce qui doit arriver* et se vérifie par un test. Le SDD dit
  *comment c'est construit* et ne se teste pas. « Une PR fusionnée est notifiée » est une spec ;
  « les comptes sont sondés séquentiellement » est du SDD.
* **ADR ou paragraphe ?** ADR si une **alternative crédible** a été écartée, et que la
  connaître évitera à quelqu'un de refaire le débat. Sinon, un `<remarks>` dans le code suffit.
  Un ADR contient toujours le tableau des options rejetées : sans lui, ce n'est pas un ADR,
  c'est une note.
* **CHANGELOG ou README ?** Le CHANGELOG raconte un **changement** (« ajouté », « corrigé »),
  le README décrit l'**état actuel**. Un changement visible touche presque toujours les deux ;
  une correction interne, seulement le premier.

## 2. Le style de chaque document

**Spec** — *Étant donné / Quand / Alors*, puis une liste numérotée « Règles » pour les cas
limites. Vocabulaire fixe : *l'observateur* est l'utilisateur de l'application, *instantané*
l'état mémorisé au cycle précédent, *espace* le niveau au-dessus du dépôt (projet,
propriétaire ou groupe selon la forge). Un identifiant ne se renumérote jamais : il est cité
dans les tests, les scénarios et la traçabilité.

**Gherkin** — français (`# language: fr`), un tag `@SPEC-…` par scénario, personas et données
d'exemple imposés (voir [`docs/features/README.md`](../../../docs/features/README.md)). Les
`.feature` ne sont **pas** exécutés : ce sont la formulation lisible de la spec, la preuve
étant le test NUnit de même catégorie. Le tag et la catégorie doivent être **strictement**
identiques, sinon les garde-fous de `FeatureCoverageTests` font échouer `dotnet test` — dans
un sens comme dans l'autre.

**ADR** — en-tête `Statut` + `Contexte`, tableau des options avec *Pour* et *Contre*, la
décision, puis les conséquences. Numérotation continue, jamais réutilisée. Un ADR est
**immuable** : on ne le corrige pas, on en écrit un nouveau qui le remplace.

**SDD** — sections numérotées, référencées ailleurs (`§5.4`, `§8`). En modifiant une section,
vérifier qui la cite : `grep -rn "SDD.md#" docs .claude` et `grep -rn "§" .claude`.

**CHANGELOG** — [Keep a Changelog], catégories *Ajouté / Modifié / Corrigé / Supprimé /
Sécurité*. Formulé côté bénéfice : « alerte quand un pipeline échoue », pas « ajout de
`PipelineFailedRule` ». Une entrée de *Corrigé* dit ce qui se passait avant, sinon le lecteur
ne sait pas s'il était concerné.

**README** — destiné à quelqu'un qui n'a jamais ouvert le projet. Chemins relatifs à la racine,
emplacements utilisateur en `%APPDATA%\ForgeWatcher\…`. La section *Limites connues* est un
engagement : ce qui y manque passe pour supporté.

## 3. Ce qui rend une documentation fausse

Par ordre de fréquence constatée dans ce dépôt :

1. **Une affirmation devenue fausse après un changement de configuration.** Cas réel : deux
   fichiers ont longtemps affirmé que le dépôt ne fixait pas `TreatWarningsAsErrors`, alors
   que `Directory.Build.props` le faisait — le lecteur en déduisait l'inverse de la réalité.
   En modifiant `Directory.Build.props`, `.editorconfig`, `.slnx`, `NuGet.Config` ou un
   workflow, chercher ce que la documentation en dit : `grep -rn "warnaserror\|NoWarn\|net9" docs .claude README.md`.
2. **Un tableau d'inventaire non mis à jour** : la liste des skills et des subagents dans
   `CONTRIBUER.md` §5, celle des fichiers `.feature` dans `docs/features/README.md`, l'arbre
   des sources du README, la liste des ADR dans le SDD §10. Ajouter un fichier, c'est ajouter
   une ligne dans son inventaire.
3. **Un nom de type qui n'existe plus.** Après un renommage :
   `grep -rn "AncienNom" docs .claude README.md CHANGELOG.md`.
4. **Une duplication qui a divergé.** Si la même règle est écrite à deux endroits, l'un des
   deux doit devenir un lien. Le dépôt s'y tient : `CONTRIBUER.md` est la vue d'ensemble, les
   skills sont les procédures, et ils se citent au lieu de se recopier.

## 4. Vérifier avant de refermer

Les chemins et identifiants cités par la documentation doivent exister :

```powershell
# Chemins de fichiers cités par la documentation et l'outillage
Get-ChildItem docs, .claude -Recurse -Include *.md | Select-String -Pattern '(?<p>(src|tests|docs)/[A-Za-z0-9_./-]+\.(cs|md|feature))' -AllMatches |
    ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups['p'].Value } | Sort-Object -Unique |
    Where-Object { -not (Test-Path $_) }

# Identifiants de spec cités mais jamais déclarés
$declares = Select-String -Path docs\specs\*.md -Pattern '^#+\s+(SPEC-[A-Z-]+\d+)' |
    ForEach-Object { $_.Matches[0].Groups[1].Value } | Sort-Object -Unique
Get-ChildItem docs, .claude, README.md, CHANGELOG.md -Recurse -Include *.md, *.feature |
    Select-String -Pattern '(SPEC-[A-Z]+(-[A-Z]+)*-\d+)' -AllMatches |
    ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique |
    Where-Object { $_ -notin $declares }
```

Aucune sortie est le résultat attendu. Les gabarits `SPEC-EVT-0NN` et `SPEC-XXX-0NN` des
fiches d'exemple sont les seules exceptions admises.

Puis la checklist du skill `verifier-avant-commit` (§2 pour la cohérence, et la normalisation
CRLF : `dotnet format` ne voit pas les `.md` ni les `.feature`), et le subagent
`relecteur-conventions` pour la relecture de forme.
