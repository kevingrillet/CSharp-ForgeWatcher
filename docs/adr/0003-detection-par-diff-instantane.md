# ADR-0003 — Détection par diff d'instantanés (et non par horodatage)

* **Statut** : accepté
* **Contexte** : il faut décider si un commentaire, un vote ou un changement d'état est
  « nouveau ». Azure DevOps ne fournit pas de flux d'événements consommable côté client
  sans abonnement de service hook (nécessite des droits d'administration de projet et un
  point d'entrée HTTP joignable).

## Options considérées

| Option | Pour | Contre |
|---|---|---|
| **Diff d'instantané persistant** | Robuste aux redémarrages, aux coupures réseau et aux décalages d'horloge ; idempotent | Un fichier d'état à maintenir ; sondage nécessaire |
| Filtre « depuis la dernière date vue » | Simple à écrire | Doublons ou trous selon le fuseau/la dérive d'horloge ; rejoue tout après une longue coupure ; les changements de vote n'ont pas de date exploitable |
| Service hooks Azure DevOps | Temps réel, pas de sondage | Droits d'administration, serveur d'écoute exposé, inadapté à un poste de travail |

## Décision

Mémoriser un **instantané** par PR (état, votes par relecteur, identifiants de
commentaires par discussion, participation de l'observateur) dans
`%APPDATA%\ForgeWatcher\state.json`, et **comparer** l'observation courante à cet
instantané. Aucune règle de détection ne lit l'horloge.

## Conséquences

* Redémarrer l'application ne rejoue pas les notifications déjà vues.
* Une coupure de plusieurs heures est rattrapée d'un coup au cycle suivant, sans rafale
  grâce au toast de synthèse (SPEC-NOTIF-002).
* Le premier cycle doit être **silencieux** (SPEC-POLL-001), sinon l'utilisateur reçoit
  l'intégralité de l'historique actif.
* Un dépôt en erreur ne doit **pas** être interprété comme « ses PR ont disparu »
  (SPEC-POLL-002) : l'état n'est purgé que pour les dépôts effectivement lus.
* Les tests de détection sont de simples fonctions pures : instantané + observation →
  liste d'événements attendus.
