# ADR-0001 — WinForms pour l'application résidente

* **Statut** : accepté
* **Contexte** : il faut une icône dans la zone de notification Windows, un menu
  contextuel, deux petites fenêtres de configuration et de consultation, une empreinte
  mémoire faible et un démarrage instantané avec la session.

## Options considérées

| Option | Pour | Contre |
|---|---|---|
| **WinForms (.NET 9)** | `NotifyIcon` natif, démarrage < 200 ms, faible empreinte, API stable, code d'UI très court | Rendu daté, pas de binding déclaratif |
| WPF | Rendu moderne, MVVM, binding | Pas de `NotifyIcon` natif (dépendance tierce), démarrage plus lent, beaucoup de XAML pour deux boîtes de dialogue |
| WinUI 3 / MAUI | Rendu Fluent | Empaquetage MSIX ou dépendances lourdes, `NotifyIcon` non pris en charge nativement |
| Service Windows + toasts | Aucune UI | Configuration impossible sans UI, activation des toasts complexe depuis un service |

## Décision

**WinForms**, en construisant l'UI en code (pas de fichiers `.Designer.cs`) pour que
tout soit lisible et modifiable dans un éditeur de texte.

## Conséquences

* Le tray, les toasts et les fenêtres tiennent dans un seul projet léger.
* L'apparence reste sobre ; c'est un outil d'arrière-plan, ce compromis est assumé.
* La couche `Ui` est isolée : passer à WPF ou WinUI plus tard ne touche ni
  `Application` ni `Domain` (cf. SDD §8).
