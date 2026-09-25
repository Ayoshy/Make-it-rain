# Scènes et ambiances

## Modèles actuels

Chaque scène montre au minimum Disques, Terminal, Projets et Vidéo. En Jeu,
le principal (écran gauche, où se joue la partie) ne porte que des blocs sans
rapport avec le jeu ; tout ce qui sert pendant une partie vit sur le secondaire.

| Scène | Fond | Agencement |
| --- | --- | --- |
| Bureau | Manhattan Aurore / heure bleue en alternance, palette Aurore | Principal : Terminal et Projets, colonne Bloc-notes / Nudge / Gmail / Atelier, bande libre avant la couture. Secondaire : Vidéo 1440 × 864 avec Applications et Réseau compact dessous, colonne de suivi à droite (Horloge, Météo, Disques, Conrad, AI Meter). Plus de 30 % du fond reste libre. |
| Jeu | Deux illustrations Jason/Lucia et boulevard Vice City, palette Vice City | Secondaire : Vidéo 1560 × 876, Audio, Conrad, Horloge et Lecteur dessous ; colonne Vice City / DualSense / LoL / Réseau à droite. Principal (recouvert par le jeu) : Applications, Terminal, Projets, Disques, AI Meter, Bluetooth. |
| Multimédia | Fjord au crépuscule, palette Obsidienne | Principal : Terminal et Projets, colonne Lecteur / Audio / Bluetooth. Secondaire : Vidéo 1896 × 1128, Applications dessous, Horloge, Météo et Disques à droite. |
| Mono écran | Ambiance conservée | Horloge et Applications en bandeau, Terminal et Vidéo côte à côte, Projets, Disques, Audio et Lecteur dessous. |

Les modèles visent les deux dalles 2560 × 1440 de ce PC. Ces dimensions sont des choix initiaux, pas de nouveaux minimums. Chaque scène sauvegarde les gestes validés, blocs masqués, tailles, verre et réglages d'animation. Sauver sous conserve la scène d'origine ; Rétablir le modèle reprend son plan. Les blocs retirés restent ajoutables. Le contenu vidéo conserve son ratio.

**Rétablir le modèle** retrouve ces plans, y compris les nouveaux docks. Charger
une nouvelle version ne réinitialise pas les scènes personnelles ni les autres
modèles.

Débrancher le secondaire active Mono écran après stabilisation. Le rebranchement restitue la scène précédente avec ses modifications. Les scènes ne lancent pas d'application et ne modifient ni la sortie audio, ni le volume.

## Fonds et effets

- `assets/Images/aurore-photo.png` : Manhattan, reflets verticaux et flares chauds sur les façades.
- `assets/Images/manhattan-blue-hour.png` : Manhattan à l'heure bleue, en alternance avec Aurore.
- `assets/Images/vice-city-boulevard.png` : troisième visuel Jeu, panorama du boulevard sur les deux écrans.
- `assets/Images/obsidienne-photo.png` : fjord plus naturel, lueurs et petits reflets mouvants sur l'eau.
- `assets/Images/jason-lucia.jpg` et `jason-lucia-03-painted.png` : illustration originale puis variante peinte du fichier fourni `Jason_and_Lucia_03.jpg`. La variante Lucia a été acceptée par Ayo ; ses pixels restent inchangés.

Les images et brosses sont chargées une fois sur le fil du rendu natif, puis réutilisées, y compris dans le verre. Les panoramas couvrent le bureau sans étirer leur géométrie ; un recadrage adapte leur ratio. Les effets photo sont distincts des particules et traits lumineux GTA. La réaction musicale module leur intensité. Un écran occulté cesse d'être dessiné sans suspendre celui qui reste visible.

Bureau alterne deux images et Jeu trois images après 300 secondes visibles, avec un fondu de 2 secondes. Chaque scène a son compteur, suspendu hors de la scène, lorsque tout le bureau est occulté ou lorsque l'animation est désactivée. `wallpaper-progress.txt` conserve les secondes Jeu puis Bureau sur deux lignes (l'ancien fichier à une ligne reste lisible). Sauvegarde toutes les cinq secondes de progression et à l'arrêt propre. Recharger le bureau ne redémarre pas les délais. L'état natif expose `wallpaperSeconds`, `wallpaperIndex`, `wallpaperBlend` pour Jeu et `bureauSeconds`, `bureauIndex`, `bureauBlend` pour Bureau. Le nouveau boulevard couvre les deux écrans ; les illustrations Jason/Lucia conservent leur cadrage. Les flares des façades Aurore s'effacent sur la photo heure bleue.

## Données et migration

Le format 7 de `profiles.json` applique la refonte du 25 septembre aux quatre
modèles intégrés (le format 6 avait déjà regroupé les anciennes scènes dans
Bureau et Multimédia ; Try 1 reste supprimée). Une copie horodatée de
`profiles.json`, `layout.json` et `preferences.json` est créée dans
`scene-backups/` avant migration. Les scènes personnelles restent intactes et
leurs références de modèle sont conservées.

La migration ne se répète pas au lancement. Elle conserve les choix de verre, d'animation et de réactivité.

Les trois palettes restent partagées par WPF et Direct2D. Les hôtes terminal préexistants restent ouverts et peuvent conserver leur habillage jusqu'à fermeture explicite par l'utilisateur. Aucun changement du projet Wallpaper Engine.

Pendant une transition de scène, les couleurs WPF et images thématiques passent
directement à leur état final lorsque les docks sont dissous. Le fondu de la
scène reste actif ; l'aperçu des réglages garde son animation de couleur. Ce
changement cible le [crash natif observé le 25 septembre](SCENE_CRASH_20260925.md)
et reste à valider sur le bureau dans le candidat `scene-brush-01`.

## Vérification

- `dotnet run --project tests/Battlestation.Scenes.Tests.csproj -c Release` : scènes, migration, sauvegarde, modèles, Mono écran et transitions. Un dossier de copies des trois fichiers personnels peut être passé après `--`.
- `tests/Check-Themes.ps1` : rendus natifs, alternance complète, suspension, persistance du compteur et effets photo vérifiés séparément de la parallaxe.
- Clics, gestes et fluidité du bureau restent distincts de ces tests. Une capture du fond natif n'est pas une preuve de clic physique.

Les anciens relevés et leur coordination terminée restent dans [l'historique](history/THEMES_SCENES_BEFORE_TRI.md). Les prompts et la provenance sont dans [les fonds](WALLPAPERS.md).
