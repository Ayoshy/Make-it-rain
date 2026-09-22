# Historique des scènes avant le tri du 22 septembre 2026

Ces notes décrivent des livraisons antérieures. La coordination de ces travaux est terminée ; les anciens modèles et consignes de livraison ne sont plus actifs. Voir [le fonctionnement actuel](../THEMES_SCENES.md).

# Scènes et ambiances

## Utilisation

Il n’y a plus qu’un seul concept : la **scène**. Elle porte son ambiance — palette
WPF et fond natif des deux écrans — en plus de ses positions, tailles, docks
visibles, transparence, animation et réactivité musicale. Réglages → Apparence ne
propose plus de thème ; l’ambiance change avec la scène : Personnel et Jeu en
**Vice City**, Cinéma et Focus en **Obsidienne**, Création, Multimédia et Double
écran en **Aurore**, Mono écran reprend l’ambiance courante à sa création. Le fondu
dure 240 ms. L’aperçu de la page Apparence porte sur transparence, animation et
réactivité ; Enregistrer le conserve, fermer l’annule.

Scènes remplace les anciens libellés des sélecteurs dans la palette, les réglages, le menu de notification et Réorganiser. Personnel, Jeu, Création, Cinéma, Focus, Multimédia, Double écran et les scènes personnelles restent disponibles. Les gestes validés sont sauvegardés automatiquement. Les scènes ne changent aucune sortie audio, aucun volume ou mute et ne lancent aucune application.

Sauver sous conserve la scène d’origine et crée une copie personnelle. Rétablir le modèle remet le plan et l’ambiance du modèle d’origine de la scène courante. Les copies gardent leur modèle d’origine ; les anciens profils personnels sans modèle connu sont rattachés à Personnel.

## Fonds par ambiance

Le poster animé Vice City reste `assets/Images/jason-lucia.jpg`, inchangé au pixel
près. Les deux autres ambiances attendent un poster du même genre, chargé au
démarrage du rendu natif : déposer les fichiers suffit, sans recompilation ni
réglage.

| Fichier | Ambiance | Scènes |
| --- | --- | --- |
| `assets/Images/obsidienne.jpg` | Obsidienne | Cinéma, Focus |
| `assets/Images/aurore.jpg` | Aurore | Création, Multimédia, Double écran |

Spécifications : JPEG 3840×2160 (16:9), qualité ~85-90, ≤ 2 Mo ; image très sombre
peinte façon key-art/poster, sujet centré horizontalement (la colonne affichée est
l’écran principal), bords haut et bas sans détail important, aucun texte, logo,
filigrane ni personnage protégé, pas de grandes zones claires uniformes pour garder
le verre lisible. L’app applique elle-même le fondu latéral, le voile sombre et la
teinte de palette.

Tant qu’un fichier manque, la scène garde le fond abstrait actuel : le rendu reste
strictement inchangé et `Check-Themes.ps1 -BaselineRef 9a0c43f` reste valide. Le
poster est dessiné dans la colonne de l’écran principal avec parallaxe et fondu
latéral ; halos, bokeh et rides audio suivent la palette de l’ambiance et ne
réutilisent pas les images Vice City.

## Changement de scène

Une scène ne déplace pas les docks vers leurs nouvelles places : l'ancienne scène
se dissout, la nouvelle disposition est appliquée pendant que plus rien n'est
dessiné, puis les docks reviennent en cascade depuis le haut du bureau, avec le
verre natif qui se dissout et revient avec eux. `SceneTransition` porte ce temps
de scène : sortie de 80 ms plus 50 ms de décalage entre les docks, entrée de
150 ms plus 110 ms de décalage, décalage vertical de 14 px à l'apparition. Le
verre reste un seul calque Direct2D (`BackgroundSceneFade`) : son alpha suit la
dissolution sans retoucher le fond animé, qui continue de tourner.

La scène choisie pendant la sortie remplace la précédente, sans effet intermédiaire.
L'hôte terminal est masqué le temps de l'application puis replacé par la nouvelle
scène ; ses sessions ne sont pas fermées et aucune commande n'est envoyée à un
hôte qui ne les comprend pas. Une erreur pendant l'application rend d'abord le
bureau visible et saisissable, puis remonte par le dispatcher.

`inspect` expose `scene.phase` (`repos`, `sortie`, `entree`), `scene.alpha` et
`scene.applyMs` ; `native-renderer-state.json` publie `sceneAlpha`. Relevé du
16 septembre 2026 sur le bureau chargé, thème compris dans le changement :
338 ms pour Double écran, 381 ms pour Création, 454 ms pour Cinéma et 670 ms pour
Personnel. Ce coût est celui du repositionnement des fenêtres en couches et de
l'activation des docks, pas de la transition, qui ne fait que le rendre invisible.

Contrôles reproductibles : `dotnet run --project tests/Battlestation.Scenes.Tests.csproj`
(90 contrôles, dont l'ordre sortie → application → entrée, la cascade, le
remplacement d'une scène demandée pendant la sortie et le retour à un bureau
visible après une erreur) et `.\tests\Check-Themes.ps1 -BaselineRef 9a0c43f`
(Vice City identique au pixel près, fondu de scène confiné aux panneaux et à leur
ombre). Sur le bureau, l'aller-retour par commande interne a rendu les mêmes
`layout.json`, `preferences.json` et la même session terminal ; la perception du
fondu et de la cascade demande encore l'accord d'Ayo.

## Bascule mono-écran

Quand Windows ne voit plus que le principal, la scène **Mono écran** s’active après
une seconde sans nouvelle notification de changement d’affichage. Elle contient
initialement horloge, applications, terminal, vidéo, projets, lecteur, audio et
Bluetooth sur le principal. Elle reprend l’apparence courante à sa création, puis
mémorise ses réglages et sa disposition indépendamment. Réorganiser, retirer, ajouter et rétablir le modèle
fonctionnent comme dans les autres scènes ; l’ajout et les gestes restent sur le
principal. La barre Réorganiser se place aussi sur cet écran.

Le retour du secondaire restaure la scène qui précédait le passage mono, sans
déplacer ni fusionner ses docks. Ce retour est mémorisé dans `profiles.json`
(`ReturnScene`) et fonctionne aussi si le branchement change pendant l’arrêt de
Battlestation. Le passage mono ne modifie aucune autre scène ; la refonte de format 5
(voir Agencements) s’exécute une seule fois, indépendamment. La scène de retour ne
peut pas être supprimée pendant son absence. Une scène qui demande le secondaire ne peut
pas être appliquée tant qu’il est débranché.

La détection utilise `SystemEvents.DisplaySettingsChanged` et `Screen.AllScreens`,
sans sondage périodique. Les notifications rapprochées sont regroupées sur le
dispatcher ; les gestes inachevés et aperçus sont annulés avant la bascule. Le
masque de rendu exclut l’écran absent. Le terminal est replacé ou masqué par le
protocole existant, sans fermer l’hôte ni les onglets. L’inspection expose le nombre
d’écrans, la contrainte mono, la scène de retour et une éventuelle erreur.

Les régressions de débranchement, rebranchement, relancement, personnalisation,
ajout sur le principal et conservation des scènes sont dans `SingleScreenTests.cs`,
exécutés par `Battlestation.Scenes.Tests.csproj`. Les notifications physiques,
clics et la fluidité se vérifient séparément sur le bureau.

Validation du 16 septembre 2026 : `build/battlestation-mono-screen-01`, accepté
par Ayo après débranchement/rebranchement réel et vérification du terminal.
Compilation native/.NET et 88 contrôles scènes réussis, avec une copie des données
personnelles. L’aller-retour sur le bureau par commande interne a également rendu
les 14 blocs préexistants sans différence de position, taille ou visibilité ; les
autres scènes sauvegardées sont inchangées. L’hôte terminal PID 5976 et son onglet
(même identifiant) ont été conservés. L’inspection physique finale voit un seul
écran, la scène Mono écran, le masque de rendu 1 et Try 1 comme scène de retour.

Le pointeur de démarrage vise ce candidat ; la tâche Windows utilise le lanceur
qui lit ce pointeur. Aucun raccourci Battlestation n’était présent sur les bureaux
utilisateur/public ni dans le menu Démarrer utilisateur. Le build remplacé
`battlestation-dualsense-touch-05` reste utilisé par l’hôte terminal et son archivage
est donc reporté jusqu’à la fermeture de cette session par l’utilisateur.

Les relevés CPU/mémoire des processus bureau, terminal, métadonnées et pont vidéo
sont conservés dans `backups/mono-screen-20260916-090837/`. Le relevé après lancement
inclut l’échauffement et le suivant coïncide avec les manipulations physiques : ils
ne permettent pas de conclure à un gain ou une régression de performance.
Le service computer-use étant indisponible, les clics physiques reposent sur la
confirmation utilisateur ; aucune capture isolée n’est présentée comme preuve.

## Données et architecture

- `preferences.json` : `ThemeId`, Vice City par défaut ; il suit la scène active, une scène portant elle-même son ambiance.
- `profiles.json` : le format 2 ajoute `ThemeId` et `TemplateId` au même dictionnaire ; le format 3 introduit ensuite l’ajout DualSense dans Jeu (voir DUALSENSE.md) ; le format 5 refond une fois tous les agencements (voir Agencements).
- La migration conserve d’abord le plan et les préférences live sous le nom courant ; les autres instantanés restent intacts avec Vice City par défaut.
- Les événements de sauvegarde des gestes validés et des réglages enregistrés sauvegardent la scène ; un déplacement intermédiaire et un aperçu n’y entrent pas.
- `src/Battlestation/Themes.json` embarque les trois palettes. `DesktopTheme` partage les brosses WPF liées et transmet les couleurs au worker natif sans attendre son rendu.
- Obsidienne et Aurore réutilisent les ressources Direct2D et le matériau glass existant. Le code de rendu Vice City et ses assets sont conservés. Le projet Wallpaper Engine n’est pas modifié.
- `scripts/Build-ThemeIcons.py` dérive les variantes depuis les SVG existants avec les palettes partagées ; aucun changement de silhouettes ni réécriture des assets Vice City.
- Les nouveaux hôtes terminal annoncent `themeVersion: 1` et acceptent `theme:<id>`. Le bureau n’envoie rien aux anciens hôtes, dont les sessions et l’habillage restent en place. Les couleurs personnelles d’onglets sont conservées.

## Validation de la tranche thèmes du 15 septembre 2026

Version acceptée par Ayo : `build/battlestation-scenes-themes-candidate-02`.

- Compilation complète native et .NET réussie.
- 43 contrôles ciblés : migration d’une copie des données réelles, conservation de Focus, Personnel et Try 1, scènes intégrées persistantes, copie, rétablissement, retour à Personnel, aperçu/annulation/enregistrement de la fenêtre Réglages avec backend de test sans matériel.
- Tests des docks existants et rendus WPF des apps/Bluetooth dans les trois thèmes réussis.
- Comparaison Direct2D avec `9a0c43f`, à temps, audio et géométrie identiques : zéro pixel différent pour Vice City sur 5120 × 1440.
- Animation d’Obsidienne et Aurore constatée dans les rendus des deux écrans.
- Sur le bureau chargé : migration conservant Focus, aller-retour Personnel → Focus et relancement avec les coordonnées initiales inchangées. Sortie audio, volume, mute et mute microphone identiques avant/après.
- Hôte terminal PID 12876 et deux identités de sessions/PID shell conservés. L’ancien build reste utilisé par cet hôte ; son archivage attend sa fermeture explicite par l’utilisateur.
- Ayo a confirmé « all good » après la demande de vérification des trois thèmes, du fondu et de l’annulation, du déplacement et retour entre scènes, de Win+D, du focus et de la fluidité.
- Les clics directs de l’agent étaient indisponibles (`native pipe unavailable / os error 2`) ; la validation des gestes et de la perception est la confirmation utilisateur ci-dessus.

Aucun commit ni publication Git automatique. La copie de sécurité et les relevés sont dans `backups/themes-scenes-20260915-192418/`. Les rendus déterministes retenus sont dans `artifacts/themes-20260915-194352-772/`.

## Vérifications reproductibles

```powershell
dotnet run --project tests/Battlestation.Scenes.Tests.csproj -- <copie-des-preferences>
dotnet run --project tests/Battlestation.DockReview.Tests.csproj -- .
.\tests\Check-Themes.ps1 -BaselineRef 9a0c43f -Layout <copie-de-layout.json>
```

Les tests natifs rendent dans un dossier distinct sous `artifacts/`, sans bureau, sondes ou terminal ni modification des données actives.

Références : [brosses radiales Direct2D](https://learn.microsoft.com/en-us/windows/win32/direct2d/how-to-create-a-radial-gradient-brush), [objets WPF Freezable](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/freezable-objects-overview).

Le pointeur de lancement, le raccourci et la tâche Windows sont alignés sur la version acceptée. Mesures successives de 12 s, Focus/Vice City et deux écrans exposés : 349 images natives dans chaque relevé, environ 1,26 ms/image avant et 1,23 ms/image après. Le bureau utilise 295 Mio de mémoire résidente contre 286 Mio au relevé de référence. Les CPU observés (6,32 % avant, 2,12 % après, helpers relevés séparément) sont sensibles aux tâches de démarrage : aucun gain CPU n’est déduit de ces courts relevés. Preuves : `accepted/perf-reference.json` et `accepted/perf-candidate.json` dans la sauvegarde de tranche.

## Agencements (format 5)

Ayo a demandé de refaire **toutes** les dispositions avec les docks récents, y
compris Personnel et ses copies, **sans sauvegarder les anciens agencements**.
Règles : écrans 2560×1440, marges et écarts de 24 px, tailles minimales de
`DesktopLayout.Minimum` respectées ; **terminal, projets et vidéo visibles dans
chaque scène** ; **Montagne ignorée** partout. Les blocs absents
d’une table sont masqués et gardent leur géométrie précédente. Les tables font foi
dans `src/Battlestation/DesktopProfiles.cs` (`Preset`) ; le script de contrôle
`validate-layouts.ps1` vérifie bornes, écarts, minimums et doublons, et les tests
de scènes rejouent la validité, les minimums et les visibilités.

| Scène | Ambiance | Composition |
| --- | --- | --- |
| Personnel | Vice City | Terminal, vidéo, projets et lecteur sur l’écran 2 ; réseau, Matériel, Codex, audio et Nudge sur la colonne droite. |
| Jeu | Vice City | Écran 1 libre pour le jeu ; DualSense, LoL, terminal, vidéo, projets, Matériel, lecteur, audio et Bluetooth sur l’écran 2. |
| Création | Aurore | Terminal dominant, lecteur, Codex et Nudge en bas ; vidéo, projets et réseau sur la colonne droite. |
| Cinéma | Obsidienne | Écran 1 : terminal, projets, Nudge, météo et **Matériel seul** (Codex masqué). Écran 2 : mur d’image vidéo, lecteur, audio et Bluetooth. |
| Focus | Obsidienne | Terminal et vidéo au centre ; projets, réseau, Codex et applications à droite ; ambiance immobile, réactivité musicale coupée. |
| Multimédia | Aurore | Écran 1 : terminal, projets et Nudge. Écran 2 : vidéo, applications, lecteur, audio, Bluetooth, réseau et météo. |
| Double écran | Aurore | Écran 1 : horloge, applications, projets, Nudge, réseau et terminal. Écran 2 : DualSense, vidéo, lecteur, audio, Matériel, Codex, compteur, météo et Bluetooth. |
| Mono écran | Ambiance courante | Terminal 1536×700 et vidéo 952×536 en haut, projets 1536×484 et lecteur/audio/Bluetooth en bas du principal. |

Visibilités résultantes :

| Scène | Visibles | Masqués |
| --- | --- | --- |
| Personnel | clock, apps, terminal, video, projects, music, weather, bluetooth, network, hardware, usage, audio, reminders | countdown, dualsense, lol, montagne |
| Jeu | clock, apps, dualsense, lol, audio, terminal, video, projects, hardware, music, bluetooth | network, usage, countdown, weather, reminders, montagne |
| Création | apps, clock, terminal, music, usage, reminders, video, projects, network | weather, bluetooth, hardware, countdown, dualsense, lol, montagne |
| Cinéma | terminal, projects, reminders, weather, hardware, video, clock, network, audio, bluetooth, music, apps | usage, countdown, dualsense, lol, montagne |
| Focus | clock, reminders, terminal, video, music, weather, bluetooth, hardware, projects, network, usage, apps | countdown, dualsense, lol, montagne |
| Multimédia | terminal, projects, reminders, video, apps, clock, weather, music, audio, bluetooth, network | hardware, usage, countdown, dualsense, lol, montagne |
| Double écran | clock, apps, projects, reminders, network, terminal, dualsense, video, music, audio, hardware, usage, countdown, weather, bluetooth | lol, montagne |
| Mono écran | clock, apps, terminal, video, projects, music, audio, bluetooth | weather, network, hardware, usage, reminders, countdown, dualsense, lol, montagne |

## Migration format 5

`profiles.json` passe en version 5. Au premier lancement d’une version 5 sur un
fichier plus ancien, `DesktopWorkspace` copie
`%LOCALAPPDATA%\Battlestation\profiles.json` en `profiles-v4-<horodatage>.json`
(seulement si le fichier existe), applique la refonte une fois, puis la scène
courante (`ApplyAppearance`, `ApplyAll`, `layout.json`). La refonte reprend les noms
existants et les copies personnelles via leur `TemplateId`, sans archive des anciens
agencements. Un échec disque (`IOException`, `UnauthorizedAccessException`) laisse le
bureau démarrer normalement ; `RedesignAll` restaure l’ancien plan avant de propager,
donc `profiles.json` n’est pas laissé à moitié migré. `NeedsRedesign` n’est plus du
code mort : un fichier déjà en version 5 garde le snapshot live sans refonte.
