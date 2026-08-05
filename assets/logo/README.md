# Logo de Forge Watcher

| Fichier | Rôle |
|---|---|
| `forge-watcher.svg` | **Maître**. Toute retouche commence ici. |
| `forge-watcher-mono.svg` | Variante à une encre (`currentColor`), glyphe détouré — documentation, impression. |
| `forge-watcher.ico` | **Généré**, ne pas éditer à la main. 7 images : 16, 24, 32, 48, 64, 128, 256. |
| `generator/` | Outil de fabrication du `.ico`. Hors solution : ce n'est pas du code applicatif. |

## Le dessin

Deux branches qui convergent vers une flèche de fusion, dans un disque : le geste d'une pull
request. Contraintes assumées pour rester lisible à 16 x 16, taille réelle dans la zone de
notification :

- **trois éléments seulement** — disque, « Y » fléché, nœud de la branche entrante ;
- **traits épais** — 4,5 unités sur une grille de 32, soit 2,25 px à 16 x 16 ;
- **pointe large** — c'est la seule forme encore identifiable quand tout le reste se brouille ;
- **fort contraste** — blanc sur bleu, aucun détail sous 3 unités ;
- **1 unité de marge** — les icônes Windows sont rognées de près.

| Rôle | Valeur |
|---|---|
| Bleu de marque (disque) | `#006ABE` |
| Glyphe | `#FFFFFF` |
| Accent (nœud de la branche entrante) | `#FFB300` |

Les liserés d'état et la pastille de non-lus **ne font pas partie du logo** : ils sont composés
à l'exécution par `Ui/Tray/TrayIconFactory` (SPEC-UI-ICON-001).

## Régénérer `forge-watcher.ico`

```
dotnet run --project assets/logo/generator/LogoGenerator.csproj
```

À lancer depuis la racine du dépôt, après toute modification de `forge-watcher.svg` — la
géométrie est transcrite en GDI+ dans `generator/LogoPainter.cs`, et **c'est cette
transcription qui produit le `.ico`**. Les deux doivent donc rester synchronisées ; la même
géométrie est également reprise dans `TrayIconFactory` comme tracé de repli.

Le générateur dessine chaque taille en 4x puis réduit (GDI+ est médiocre en anticrénelage sur
de très petites géométries), assemble le conteneur ICO à la main — en-tête `ICONDIR`, une
`ICONDIRENTRY` par image, charges utiles PNG — puis **relit le fichier produit** et échoue si
quelque chose ne colle pas : nombre d'images, dimensions réellement décodées, sélection par
`new Icon(chemin, new Size(n, n))`, transparence préservée.

Une limite à connaître : la norme ICO code la dimension 256 sur un octet valant `0`, et le
sélecteur de `System.Drawing.Icon` lit cet octet littéralement — demander 256 px rend donc
l'image 128. Le fichier est correct pour autant : l'explorateur Windows et les ressources Win32
exploitent bien l'image 256 px. La vérification l'attend explicitement.

## Où le logo est utilisé

`src/CSharpForgeWatcher.Ui/CSharpForgeWatcher.Ui.csproj` embarque `forge-watcher.ico` via
`<ApplicationIcon>`. Tout le reste en découle : `TrayIconFactory` relit l'icône de
`ForgeWatcher.exe` au lieu d'embarquer une seconde copie de l'image, si bien que l'exécutable, la
barre des tâches, les fenêtres et la zone de notification ne peuvent pas diverger.
