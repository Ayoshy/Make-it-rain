# Scènes seules, fonds par ambiance, nouveaux agencements

## Objectif

Trois volets demandés par Ayo :

1. **Simplifier scènes/thème** : il n'y a plus qu'un seul concept, la **scène**.
   Le sélecteur de thème disparaît de Réglages → Apparence ; chaque scène porte
   son ambiance (palette + fond) de façon fixe. « Rétablir le modèle » réapplique
   l'ambiance du modèle.
2. **Fonds des deux autres ambiances** : les rendus Obsidienne et Aurore sont
   pauvres à côté du poster animé Vice City. Support natif d'un poster par
   ambiance (même traitement que `jason-lucia.jpg`), repli sur l'abstrait actuel
   si l'image manque ; Ayo génère les 2 images avec un autre LLM (specs et
   prompts ci-dessous). Aucune génération d'image par l'agent.
3. **Refonte de toutes les dispositions** avec les docks récents. **Terminal,
   projets et vidéo visibles dans chaque scène** (y compris Mono écran).
   **Aquarium et Diorama Océan ignorés** (masqués partout) pour l'instant.
   En Jeu, Ayo veut sur le 2ᵉ écran : DualSense, LoL, vidéo, terminal, matériel
   (sensor), audio, média, Bluetooth ; ça tient, vérifié géométriquement.
   Projets reste ajouté (règle des trois obligatoires), écran 1 libre pour le jeu.

Décisions utilisateur validées : retrait du sélecteur de thème ; 2 posters par
ambiance ; Mono écran suit la règle des trois ; refonte appliquée automatiquement
au premier lancement avec sauvegarde de l'ancien `profiles.json` (format 5).

Point tranché pendant la préparation : en Cinéma, l'écran 1 affiche **hardware
seul** (Matériel en 2016,888 ; Codex/`usage` masqué), conformément à la table et
aux visibilités ci-dessous.

## 1. Scènes seules

- `src/Battlestation/SettingsWindow.cs` : supprimer le champ `themeId` (l. 28), le
  titre « Thème » et la boucle de boutons `DesktopTheme.Definitions` (l. 68-74) ;
  `Save()` passe `station.Settings.ThemeId` (l. 117). La page Apparence garde
  transparence, animation du fond, réactivité musicale et intensité. Déplacer la
  note sur les anciens hôtes terminal (l. 75) près du choix de scène, page
  Bureau (le changement d'ambiance n'arrive plus que par un changement de scène).
  Mettre à jour `Preview()` (l. 80) et `Closed` (l. 102) pour ne plus lire
  `themeId` : `station.PreviewAppearance(station.Settings.ThemeId,…)`.
- Aucune migration de données : `DesktopProfile.ThemeId` et
  `DesktopSettings.ThemeId` restent la source d'ambiance, validés comme avant
  (`vice-city`, `obsidienne`, `aurore`). Mapping inchangé, porté par `Create()` :
  Personnel/Jeu = Vice City ; Cinéma/Focus = Obsidienne ;
  Création/Multimédia/Double écran = Aurore ; Mono = ambiance courante.
- `TerminalHost`, `DesktopTheme`, `Themes.json`, les icônes `themes/<id>` et le
  protocole `theme:<id>` ne changent pas. `ApplyAppearance` reste la voie unique
  d'application (déjà branchée sur `SwitchScene` via `SceneTransition`).

## 2. Fond par ambiance (support natif + 2 images à générer)

**Fichiers attendus** (déposés par Ayo, chargés au démarrage du rendu natif,
aucune recompilation nécessaire ensuite) :

| Fichier | Ambiance | Scènes |
| --- | --- | --- |
| `assets/Images/obsidienne.jpg` | Obsidienne | Cinéma, Focus |
| `assets/Images/aurore.jpg` | Aurore | Création, Multimédia, Double écran |

`jason-lucia.jpg` reste le poster Vice City, inchangé au pixel près.

**Specs de génération** : JPEG 3840×2160 (16:9), qualité ~85-90, ≤ 2 Mo.
Image très sombre, peinte façon key-art/poster, sujet centré horizontalement
(la colonne affichée est l'écran principal), bords haut/bas sans détail
important, pas de texte, logo, filigrane ni personnage protégé, pas de grandes
zones claires uniformes (lisibilité du verre). L'app applique elle-même fondu
latéral, voile sombre et teinte de palette.

**Prompts suggérés** :

- Obsidienne (noir #07090D, acier froid #E5EDF7/#8492A5) :
  « Cinematic key art painting, monolithic obsidian cliffs under cold moonlight,
  wet glossy black stone, thin fog, steel-blue and silver rim light, deep
  shadows, mostly near-black composition with a luminous focal point slightly
  right of center, painted poster style, subtle brush texture, smooth dark
  gradients, no text, no logo, no watermark, no people, 16:9, very dark and
  moody ».
- Aurore (nuit #030D20, cyan #36CDE7, violet #A896EC) :
  « Cinematic key art painting, aurora borealis ribbons in cyan, violet and
  magenta over a calm black arctic sea, snowy ridge silhouette, faint
  reflections, stars, luminous sky above a mostly dark foreground, focal glow
  near the center, painted poster style, smooth gradients, no text, no logo,
  no watermark, no people, 16:9, very dark and moody ».

**Implémentation native** (`src/Battlestation.Native/NativeBackground.cpp`) :

- `Paint` charge les posters d'ambiance : `art` reste `jason-lucia.jpg` avec le
  virage violet actuel, un tableau `posters[2]` porte
  `obsidienne.jpg`/`aurore.jpg` sans virage GTA (dessinés via un helper commun
  `Blit(bitmap,weight)`). Un helper `optional` tolérant
  (`filesystem::exists` + `try`/`catch`) : fichier absent → `nullptr`, aucune
  erreur ; les assets obligatoires (vignette, glows, bokeh) gardent leur
  `Check` actuel.
- Masque de fondu dédié `posterFade`, construit comme `fade` (arrêt .88) sur la
  largeur du poster (2560 + 154).
- Nouvelle fonction `Poster(int theme,float weight)` calquée sur `ViceCity()` :
  fond `palettes[theme][0]`, poster en colonne écran principal (0..2560) avec
  parallaxe (`easedX`/`easedY`) et masque `posterFade`, voile de palette
  (`palettes[theme][2]`/`[3]`), halos et `bokeh` teintés par la palette du thème,
  rides audio (0..2560, teintées), vignette. Aucun réemploi des `glows[0..2]`
  (leurs couleurs Vice City sont cuites) ; le poids de fondu multiplie chaque
  opacité, comme `Draw()` le fait déjà par calque.
- `Draw()` : `theme==0 → ViceCity()`, sinon `posters[theme]` chargé →
  `Poster(theme,weights[theme])`, sinon `Abstract(theme)` (chemin actuel
  bit-à-bit conservé, donc `Check-Themes.ps1 -BaselineRef 9a0c43f` reste vert
  tant que les images sont absentes).
- Le fondu entre ambiances reste géré par les poids de thème existants : chaque
  poster est dessiné dans la couche de son thème. `ViceCity()` n'est pas
  retouché.

## 3. Nouveaux agencements (`DesktopProfiles.Preset`)

Règles : écrans 2560×1440, marges 24 px, écarts 24 px, pas de grille 12 ;
tailles minimales de `DesktopLayout.Minimum` respectées (vérifié par script) ;
**terminal, projets et vidéo visibles partout** ; `aquarium` et `ocean` masqués
partout ; blocs non listés = masqués (géométrie précédente conservée, comme
aujourd'hui).

```
"Personnel"=>[
    ("clock",2584,24,512,160),("apps",3120,24,1080,160),
    ("terminal",2584,208,936,1208),
    ("video",3544,208,656,369),("projects",3544,601,656,392),("music",3544,1017,656,192),
    ("weather",3544,1233,376,183),("bluetooth",3944,1233,256,183),
    ("network",4224,208,872,280),("hardware",4224,512,872,218),("usage",4224,754,872,218),
    ("audio",4224,996,872,284),("reminders",4224,1304,872,112)]

"Jeu"=>[
    ("clock",2584,24,808,160),("dualsense",2584,208,808,560),("lol",2584,792,808,220),("audio",2584,1036,808,380),
    ("apps",3416,24,808,160),("terminal",3416,208,808,1208),
    ("video",4248,24,848,450),("projects",4248,498,848,300),("hardware",4248,822,848,218),
    ("music",4248,1064,848,168),("bluetooth",4248,1256,848,160)]

"Création"=>[
    ("apps",2584,24,1696,144),("clock",4304,24,792,144),
    ("terminal",2584,192,1696,912),
    ("music",2584,1128,480,288),("usage",3088,1128,440,288),("reminders",3552,1128,728,288),
    ("video",4304,192,792,445),("projects",4304,661,792,336),("network",4304,1021,792,395)]

"Cinéma"=>[
    ("terminal",24,24,1424,1392),
    ("projects",1472,24,1064,648),("reminders",1472,696,1064,168),
    ("weather",1472,888,520,218),("hardware",2016,888,520,218),
    ("video",2584,24,1888,1062),("clock",4496,24,600,184),
    ("network",4496,232,600,300),("audio",4496,556,600,416),("bluetooth",4496,996,600,420),
    ("music",2584,1110,1104,306),("apps",3712,1110,760,306)]

"Focus"=>[
    ("clock",2584,24,600,168),("reminders",3208,24,1216,168),
    ("terminal",2584,216,1096,1200),
    ("video",3704,216,720,405),("music",3704,645,720,192),
    ("weather",3704,861,376,193),("bluetooth",4104,861,320,193),("hardware",3704,1078,720,338),
    ("projects",4448,24,648,504),("network",4448,552,648,320),
    ("usage",4448,896,648,224),("apps",4448,1144,648,272)]

"Multimédia"=>[
    ("terminal",24,24,1424,1104),("projects",24,1152,1424,264),("reminders",1472,24,1064,168),
    ("video",2584,24,1568,1128),("apps",2584,1176,1568,240),
    ("clock",4176,24,440,168),("weather",4640,24,456,168),
    ("music",4176,216,920,264),("audio",4176,504,920,420),
    ("bluetooth",4176,948,440,468),("network",4640,948,456,468)]

"Double écran"=>[
    ("clock",24,24,560,168),("apps",608,24,1928,168),
    ("projects",24,216,832,456),("reminders",24,696,832,216),
    ("network",24,936,832,480),("terminal",880,216,1656,1200),
    ("dualsense",2584,24,1104,720),("video",3712,24,1384,720),
    ("music",2584,768,1104,168),("audio",2584,960,1104,456),
    ("hardware",3712,768,680,260),("usage",4416,768,680,260),
    ("countdown",3712,1052,760,364),("weather",4496,1052,600,164),
    ("bluetooth",4496,1240,600,176)]

"Mono écran"=>[
    ("clock",24,24,512,160),("apps",560,24,1976,160),
    ("terminal",24,208,1536,700),("video",1584,208,952,536),
    ("projects",24,932,1536,484),("music",1584,932,464,232),
    ("audio",1584,1188,464,228),("bluetooth",2072,932,464,484)]
```

Visibilités résultantes :

| Scène | Visibles | Masqués (inchangés) |
| --- | --- | --- |
| Personnel | clock, apps, terminal, video, projects, music, weather, bluetooth, network, hardware, usage, audio, reminders | countdown, dualsense, lol, aquarium, ocean |
| Jeu | clock, apps, dualsense, lol, audio, terminal, video, projects, hardware, music, bluetooth | network, usage, countdown, weather, reminders, aquarium, ocean |
| Création | apps, clock, terminal, music, usage, reminders, video, projects, network | weather, bluetooth, hardware, countdown, dualsense, lol, aquarium, ocean |
| Cinéma | terminal, projects, reminders, weather, hardware, video, clock, network, audio, bluetooth, music, apps | usage, countdown, dualsense, lol, aquarium, ocean |
| Focus | clock, reminders, terminal, video, music, weather, bluetooth, hardware, projects, network, usage, apps | countdown, dualsense, lol, aquarium, ocean |
| Multimédia | terminal, projects, reminders, video, apps, clock, weather, music, audio, bluetooth, network | hardware, usage, countdown, dualsense, lol, aquarium, ocean |
| Double écran | clock, apps, projects, reminders, network, terminal, dualsense, video, music, audio, hardware, usage, countdown, weather, bluetooth | lol, aquarium, ocean |
| Mono écran | clock, apps, terminal, video, projects, music, audio, bluetooth | weather, network, hardware, usage, reminders, countdown, dualsense, lol, aquarium, ocean |

Notes : en Cinéma, l'écran 1 (terminal, projets, Nudge, météo, Matériel) est le
poste de travail et l'écran 2 le mur d'image ; en Multimédia, écran 1 =
terminal/projets/Nudge, écran 2 = média. En Jeu, l'écran 1 reste libre pour le
jeu, tout est sur l'écran 2. En Mono, terminal 1536×700 + vidéo 952×536 en haut,
projets 1536×484 + lecteur/audio/Bluetooth en bas.

`Preset()` n'est pas restructuré : il garde l'ordre du layout d'origine et ne
change la visibilité que des ids absents de la table. Chaque table passe le
script `C:\Users\Ayo\AppData\Local\Temp\kilo\validate-layouts.ps1` (bornes, écart
12 px minimum, tailles minimales, pas de doublon, terminal/projets/vidéo requis).

## 4. Migration format 5

- `DesktopProfiles` : `NeedsRedesign = saved.Version < 5` (l. 28) ; `Persist`
  écrit la version 5 (l. 98).
- `DesktopWorkspace.InitializeCommands` : après `SaveCurrent` (l. 17), si
  `profiles.NeedsRedesign`, copier `%LOCALAPPDATA%\Battlestation\profiles.json`
  vers `profiles-v4-<yyyyMMdd-HHmmss>.json` dans le même dossier (seulement si le
  fichier existe), puis `RedesignAll(layout,settings,apps)` →
  `station.ApplyAppearance(next)` → `ApplyAll()` → `station.Layout.Save()`.
  Envelopper dans un `try` tolérant `IOException`/`UnauthorizedAccessException` :
  en cas d'échec, le bureau démarre normalement et la refonte retentera ;
  `RedesignAll` restaure l'ancien layout avant de propager, donc l'ancien
  `profiles.json` reste intact (pas d'état à moitié migré).
- La refonte reprend les noms existants et les copies personnelles via leur
  `TemplateId`, sans archive des anciens agencements (même règle qu'en format 4).
  Elle réapplique aussi les réglages d'apparence du modèle à chaque scène
  (comportement actuel de `Create`).
- `NeedsRedesign` n'est plus du code mort ; le chemin normal (fichier déjà en 5)
  garde le snapshot live sans refonte.

## 5. Tests

`tests/ScenesTests.cs` :
- Remplacer les contrôles de boutons de thème (l. 110-116) : la page Apparence ne
  contient plus Vice City/Obsidienne/Aurore ; Enregistrer conserve transparence,
  animation, réactivité et le choix tactile ; fermer annule l'aperçu. Les clics
  passent par les libellés encore présents (aucun clic « Aurore »/« Obsidienne »).
- Nouveaux contrôles de refonte : toutes les scènes ont terminal, projets et
  vidéo visibles ; `aquarium` et `ocean` masqués partout ; `lol` visible
  seulement en Jeu ; `dualsense` visible en Jeu et Double écran ; tailles
  minimales et validité conservées.
- Adapter la version attendue (l. 50) à 5 et retirer les contrôles de placement
  Aquarium/LoL de Jeu/Cinéma devenus faux (l. 77-84) ; garder les contrôles de
  minimums des nouveaux blocs (l. 73-76).
- Le changement de scène reste la source du changement d'ambiance
  (`DesktopTheme.Current.Id` après `Switch("Cinéma")` = obsidienne).

`tests/SingleScreenTests.cs` : jeu visible Mono = horloge, apps, terminal,
vidéo, projets, lecteur, audio, Bluetooth (8 blocs) ; mettre à jour les contrôles
« six docks » (l. 20 et l. 48).

`tests/DockReviewTests.cs` : inchangé (les trois palettes restent rendues).

## 6. Documentation

- `README.md` : remplacer le paragraphe « Réglages → Apparence → Thème » (l. 53-57)
  par « les scènes portent leur ambiance » ; ajuster la description des scènes
  (l. 36-46) et de Mono écran (l. 47-52 : 8 blocs, terminal/projets/vidéo
  toujours présents).
- `docs/THEMES_SCENES.md` : réorienter sur les scènes et ambiances (garder le nom
  de fichier, liens existants), nouveaux tableaux d'agencement, migration format 5
  avec sauvegarde, section fonds par ambiance (fichiers attendus, specs, repli).
- `docs/ARCHITECTURE.md` l. 116 : formulation « palettes d'ambiance des scènes ».
- `AGENTS.md` l. 35-36 : « couleurs et ambiance évoluent avec les scènes ».

## 7. Validation

1. Compilation native + .NET dans un nouveau dossier `build/` (sans toucher
   `build/current.txt`).
2. `dotnet run --project tests/Battlestation.Scenes.Tests.csproj -- <copie>` et
   `dotnet run --project tests/Battlestation.DockReview.Tests.csproj -- .`.
3. `.\tests\Check-Themes.ps1 -BaselineRef 9a0c43f` : Vice City identique ;
   Obsidienne/Aurore inchangés tant que les images sont absentes.
4. Sur le bureau chargé : vérifier chaque scène (terminal/projets/vidéo visibles,
   pas de chevauchement, transition), Mono écran par commande interne si
   possible, et la fluidité. Vérifier `inspect` (`scene`, `theme`, `blocks`).
5. Ayo génère les 2 posters, les dépose dans `assets/Images/`, puis rechargement
   du bureau pour vérifier le rendu (pas de recompilation).
6. Après acceptation : aligner `build/current.txt`, rapporter l'état Git réel et
   les limites de validation (perception du fondu et des posters = confirmation
   utilisateur).

## Hors périmètre

- Aquarium et Diorama Océan restent masqués dans les nouveaux agencements.
- Pas de per-scène poster (2 images par ambiance seulement), pas de modification
  du projet Wallpaper Engine, pas de refonte des docks ni du protocole terminal.
- Pas de restauration automatique des anciens agencements : la migration format 5
  les remplace, seule la copie `profiles-v4-*.json` les conserve.
