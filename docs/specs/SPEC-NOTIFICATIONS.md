# SPEC-NOTIF / SPEC-LINK — Notifications et liens profonds

## SPEC-NOTIF-001 — Un clic mène à l'endroit exact

*Étant donné* une notification affichée
*Quand* l'utilisateur clique dessus (ou clique le bouton *Ouvrir*)
*Alors* le navigateur par défaut s'ouvre sur l'URL portée par l'événement :
la discussion concernée pour un commentaire, la PR sinon.

L'activation fonctionne même si la notification est consultée plus tard depuis le centre
de notifications Windows : l'argument est transporté par le toast lui-même.

## SPEC-NOTIF-002 — Pas de rafale

*Étant donné* un cycle produisant plus de `MaxNotificationsPerPoll` événements (défaut 5)
*Quand* les notifications sont diffusées
*Alors* un **unique** toast de synthèse est affiché (« N nouvelles activités »), dont le
clic ouvre la fenêtre *Activité récente* ; tous les événements restent listés dans cette
fenêtre.

En deçà du seuil, un toast par événement est affiché.

Chaque toast porte une **étiquette** dérivée de la clé de déduplication de son événement :
un même fait ré-affiché remplace le précédent au lieu de s'empiler dans le centre de
notifications. Deux faits distincts doivent donc toujours porter des étiquettes distinctes —
y compris deux événements identiques survenus sur **deux comptes différents** surveillant le
même dépôt, qui notifient chacun de leur côté (ADR-0005). L'étiquette étant limitée en
longueur par Windows, elle est obtenue par empreinte de la clé entière et non par troncature,
qui effacerait le compte (ADR-0006).

## SPEC-NOTIF-003 — Filtres par type

*Étant donné* un type de notification désactivé dans les préférences
*Quand* un événement de ce type est détecté
*Alors* il n'est ni notifié, ni ajouté à la liste d'activité — mais l'état est tout de
même mémorisé (il ne sera pas re-détecté plus tard).

## SPEC-NOTIF-004 — Repli si les toasts sont indisponibles

*Étant donné* un environnement où les toasts Windows échouent (stratégie de groupe,
notifications désactivées, échec d'enregistrement COM)
*Quand* une notification doit être affichée
*Alors* l'application bascule automatiquement et définitivement sur les **bulles d'info**
de la zone de notification, en conservant le comportement de clic.

## SPEC-NOTIF-005 — Compteur non lu

Le nombre d'événements non lus est affiché en pastille sur l'icône de la zone de
notification (au-delà de 9 : `9+`). Ouvrir la fenêtre *Activité récente* ou choisir
*Tout marquer comme lu* remet le compteur à zéro.

---

Les formats ci-dessous sont ceux d'Azure DevOps. Chaque forge a les siens : le tableau
complet est dans [SPEC-FORGES](SPEC-FORGES.md) (SPEC-FORGE-003).

## SPEC-LINK-001 — URL d'une pull request

Pour l'organisation `https://dev.azure.com/contoso`, le projet `Mon Projet`, le dépôt
`mon-repo` et la PR `1234` :

```
https://dev.azure.com/contoso/Mon%20Projet/_git/mon-repo/pullrequest/1234
```

Les noms de projet et de dépôt sont encodés ; l'éventuel `/` final de l'URL
d'organisation est ignoré.

## SPEC-LINK-002 — URL d'une discussion

```
https://dev.azure.com/contoso/Mon%20Projet/_git/mon-repo/pullrequest/1234?discussionId=99
```

`discussionId` est l'identifiant du *thread* : Azure DevOps déroule et met en évidence la
discussion à l'ouverture.

## SPEC-LINK-003 — URL d'un dépôt

```
https://dev.azure.com/contoso/Mon%20Projet/_git/mon-repo/pullrequests
```

Utilisée par le menu de la zone de notification pour ouvrir la liste des PR d'un dépôt.

## SPEC-LINK-004 — Une adresse fournie par la forge est préférée

*Étant donné* un message dont l'API a livré l'adresse web exacte
*Quand* l'événement correspondant est construit
*Alors* cette adresse est utilisée telle quelle, sans passer par la reconstruction.

L'adresse reconstruite reste le cas général : c'est elle qui permet d'ouvrir une pull
request ou une exécution de pipeline connue seulement de l'état mémorisé, sans nouvel appel
réseau. Mais quand la forge a déjà donné l'ancre du message — GitHub le fait pour chacun
d'eux, avec trois formes différentes selon qu'il s'agit d'un message de conversation, d'un
commentaire de ligne ou d'un corps de relecture — la deviner serait à la fois inutile et
fragile.

