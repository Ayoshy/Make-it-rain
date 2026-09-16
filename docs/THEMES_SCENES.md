# Thèmes et scènes

## Utilisation

Réglages → Apparence propose Vice City, Obsidienne et Aurore. L’aperçu agit sur le fond natif des deux écrans, le verre, les textes, les boutons, les menus WPF et les icônes. Le fondu dure 240 ms. Enregistrer conserve le choix ; fermer annule les changements depuis le dernier enregistrement.

Scènes remplace les anciens libellés des sélecteurs dans la palette, les réglages, le menu de notification et Réorganiser. Personnel, Jeu, Création, Cinéma, Focus, Multimédia, Double écran et les scènes personnelles restent disponibles. Chaque scène conserve automatiquement ses positions, tailles, docks visibles, thème, opacité, animation et réactivité musicale. Les scènes ne changent aucune sortie audio, aucun volume ou mute et ne lancent aucune application.

Sauver sous conserve la scène d’origine et crée une copie personnelle. Rétablir le modèle remet le plan et l’ambiance du modèle d’origine de la scène courante. Les copies gardent leur modèle d’origine ; les anciens profils personnels sans modèle connu sont rattachés à Personnel.

## Bascule mono-écran

Quand Windows ne voit plus que le principal, la scène **Mono écran** s’active après
une seconde sans nouvelle notification de changement d’affichage. Elle contient
initialement horloge, applications, lecteur, audio, Bluetooth et un grand terminal.
Elle reprend l’apparence courante à sa création, puis mémorise ses réglages et sa
disposition indépendamment. Réorganiser, retirer, ajouter et rétablir le modèle
fonctionnent comme dans les autres scènes ; l’ajout et les gestes restent sur le
principal. La barre Réorganiser se place aussi sur cet écran.

Le retour du secondaire restaure la scène qui précédait le passage mono, sans
déplacer ni fusionner ses docks. Ce retour est mémorisé dans `profiles.json`
(`ReturnScene`) et fonctionne aussi si le branchement change pendant l’arrêt de
Battlestation. Les scènes enregistrées restent intactes lors de la mise à niveau ;
aucune refonte générale n’est exécutée au démarrage. La scène de retour ne peut pas
être supprimée pendant son absence. Une scène qui demande le secondaire ne peut
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

- `preferences.json` : `ThemeId`, Vice City par défaut.
- `profiles.json` : le format 2 ajoute `ThemeId` et `TemplateId` au même dictionnaire ; le format 3 introduit ensuite l’ajout DualSense dans Jeu (voir DUALSENSE.md).
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

## Refonte des agencements avec Réseau

Ayo a demandé de refaire **toutes** les dispositions, y compris Personnel et ses copies, **sans sauvegarder les anciens agencements**. La migration de format 4 applique cette refonte une fois, conserve les noms existants et n’ajoute aucune copie d’archive. Les modifications suivantes restent automatiquement conservées.

| Scène | Thème | Agencement |
| --- | --- | --- |
| Personnel | Vice City | Terminal, projets et médias à gauche ; réseau, monitoring, audio et Nudge à droite du second écran. |
| Jeu | Vice City | Grande manette, réseau et compteur ; premier écran libre pour le jeu. |
| Création | Aurore | Terminal dominant, projets, compteurs et réseau. |
| Cinéma | Obsidienne | Grande vidéo, lecteur, audio et Bluetooth. |
| Focus | Obsidienne | Terminal et projets, réseau et compteurs ; fond immobile, réactivité musicale coupée. |
| Multimédia | Aurore | Vidéo, lecteur, audio, Bluetooth et réseau. |
| Double écran | Aurore | Terminal/projets/réseau sur le premier écran ; manette, vidéo et cockpit sur le second. |

Les dispositions personnalisées reprennent leur modèle d’origine remanié ; Personnel est utilisé si aucun modèle n’est connu. La sortie audio, les volumes, le mute, les applications et le choix tactile de la manette restent inchangés. Aquarium reste ajoutable manuellement lors de sa tranche.

Les 59 contrôles de scènes/préférences couvrent la validité des rectangles, les tailles minimales, le réseau dans toutes les scènes, la conservation des noms, l’absence de copies de sauvegarde et l’absence de nouvelle refonte au relancement.
