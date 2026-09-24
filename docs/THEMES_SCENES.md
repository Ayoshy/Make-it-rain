# Scènes et ambiances

## Modèles actuels

| Scène | Fond | Agencement |
| --- | --- | --- |
| Bureau | New York depuis une baie vitrée, palette Aurore | Terminal et Projets à gauche, Bloc-notes et Atelier à côté ; Réseau compact 592 × 336, Vidéo à droite et commandes regroupées. Plus de 30 % du fond reste libre ; Achats reste ajoutable. |
| Jeu | Deux illustrations Jason/Lucia, palette Vice City | Secondaire : Vidéo 1568 × 888 au-dessus du Terminal 1568 × 480 ; DualSense, LoL et Réseau dans la colonne de droite. Les blocs moins urgents restent sur le principal. |
| Multimédia | Fjord au crépuscule, palette Obsidienne | Terminal, Projets et commandes audio à gauche ; Vidéo 1888 × 1128 à droite. |
| Mono écran | Ambiance conservée | Terminal et Vidéo côte à côte, Projets dessous et commandes compactes. |

Les modèles visent les deux dalles 2560 × 1440 de ce PC. Ces dimensions sont des choix initiaux, pas de nouveaux minimums. Chaque scène sauvegarde les gestes validés, blocs masqués, tailles, verre et réglages d'animation. Sauver sous conserve la scène d'origine ; Rétablir le modèle reprend son plan. Les blocs retirés restent ajoutables. Le contenu vidéo conserve son ratio.

Le modèle Bureau intègre l'agencement aéré du 24 septembre : **Rétablir le modèle**
retrouve ce plan, y compris les nouveaux docks. Charger une nouvelle version ne
réinitialise pas les scènes personnelles ni les autres modèles.

Débrancher le secondaire active Mono écran après stabilisation. Le rebranchement restitue la scène précédente avec ses modifications. Les scènes ne lancent pas d'application et ne modifient ni la sortie audio, ni le volume.

## Fonds et effets

- `assets/Images/aurore-photo.png` : Manhattan, reflets verticaux et flares chauds sur les façades.
- `assets/Images/obsidienne-photo.png` : fjord plus naturel, lueurs et petits reflets mouvants sur l'eau.
- `assets/Images/jason-lucia.jpg` et `jason-lucia-03-painted.png` : illustration originale puis variante peinte du fichier fourni `Jason_and_Lucia_03.jpg`. La variante Lucia a été acceptée par Ayo ; ses pixels restent inchangés.

Les images et brosses sont chargées une fois sur le fil du rendu natif, puis réutilisées, y compris dans le verre. Les panoramas couvrent le bureau sans étirer leur géométrie ; un recadrage adapte leur ratio. Les effets photo sont distincts des particules et traits lumineux GTA. La réaction musicale module leur intensité. Un écran occulté cesse d'être dessiné sans suspendre celui qui reste visible.

Jeu alterne ses images après 300 secondes visibles avec un fondu de 2 secondes. Le compteur est suspendu hors de Jeu, lorsque tout le bureau est occulté ou lorsque l'animation est désactivée. `wallpaper-progress.txt`, dans les données personnelles, conserve uniquement ce nombre de secondes : sauvegarde toutes les cinq secondes de progression et à l'arrêt propre. Recharger le bureau ne redémarre plus le délai. L'état natif expose `wallpaperSeconds`, `wallpaperIndex` et `wallpaperBlend`.

## Données et migration

Le format 6 de `profiles.json` regroupe Personnel, Création, Focus et Double écran dans Bureau ; Cinéma rejoint Multimédia. Try 1 est supprimée du catalogue. Une copie horodatée de `profiles.json`, `layout.json` et `preferences.json` est créée dans `scene-backups/` avant migration. Les autres copies personnelles restent intactes et leurs références de modèle sont renommées.

La migration ne se répète pas au lancement. Elle conserve les choix de verre, d'animation et de réactivité. Le réagencement complémentaire de Jeu du 22 septembre est appliqué uniquement à cette scène sur les données actives après sauvegarde ; il ne réinitialise pas Bureau, Multimédia ou Mono écran. Les cinq docks secondaires prioritaires sont visibles ; les autres blocs conservent leur visibilité choisie.

Les trois palettes restent partagées par WPF et Direct2D. Les hôtes terminal préexistants restent ouverts et peuvent conserver leur habillage jusqu'à fermeture explicite par l'utilisateur. Aucun changement du projet Wallpaper Engine.

## Vérification

- `dotnet run --project tests/Battlestation.Scenes.Tests.csproj -c Release` : scènes, migration, sauvegarde, modèles, Mono écran et transitions. Un dossier de copies des trois fichiers personnels peut être passé après `--`.
- `tests/Check-Themes.ps1` : rendus natifs, alternance complète, suspension, persistance du compteur et effets photo vérifiés séparément de la parallaxe.
- Clics, gestes et fluidité du bureau restent distincts de ces tests. Une capture du fond natif n'est pas une preuve de clic physique.

Les anciens relevés et leur coordination terminée restent dans [l'historique](history/THEMES_SCENES_BEFORE_TRI.md). Les prompts et la provenance sont dans [les fonds](WALLPAPERS.md).
