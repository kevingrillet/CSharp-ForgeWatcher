# ADR-0002 — Stockage du jeton d'accès personnel (PAT)

* **Statut** : accepté
* **Contexte** : l'application doit s'authentifier auprès d'Azure DevOps sans
  redemander de secret à chaque démarrage. Le secret doit résister à la lecture du
  fichier de configuration par un autre utilisateur ou depuis une sauvegarde.

## Options considérées

| Option | Pour | Contre |
|---|---|---|
| **DPAPI `CurrentUser`** | Dans .NET, aucune dépendance native supplémentaire, clé liée au compte Windows, illisible ailleurs | Windows uniquement ; un code exécuté sous le même compte peut déchiffrer |
| Texte clair | Trivial | Inacceptable |
| Gestionnaire d'identifiants Windows | Stockage dédié aux secrets | API à interoper, gain marginal face à DPAPI pour ce besoin |
| OAuth / Entra ID (device code) | Pas de secret longue durée, révocable | Beaucoup plus de code, enregistrement d'application requis, hors périmètre v1 |

## Décision

**DPAPI, portée `CurrentUser`**, via `System.Security.Cryptography.ProtectedData`.
Le chiffré est stocké en Base64 dans `config.json`.

## Conséquences

* Copier `config.json` sur une autre machine ne divulgue pas le PAT ; le déchiffrement
  échoue proprement et l'application redemande le jeton (SPEC-CFG-001).
* Le port `ISecretProtector` isole ce choix : passer au gestionnaire d'identifiants ou à
  OAuth ne concerne qu'une classe d'infrastructure.
* Limite acceptée : un processus exécuté sous la même session Windows peut déchiffrer le
  jeton. Parade recommandée : PAT en **lecture seule sur le code**, durée courte.
