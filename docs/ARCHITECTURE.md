# Architecture de Battlestation

`Battlestation.exe` compose le bureau WPF et appelle directement
`Battlestation.Core.dll`, `Battlestation.Graphics.dll` et `Battlestation.Desk.dll`.
Le fond Direct2D est attaché au bureau Windows et dessine le verre aux coordonnées
des blocs. Aucun navigateur ni serveur HTTP local ne sert de façade.

`Surface` possède le rendu commun des cadres et leur inscription native ; les
widgets ne dessinent que leur contenu. `DockAppearance` partage polices,
boutons et arrondis. Le dictionnaire `GlassMenus.xaml` habille les composants
WPF de menus et sous-menus au niveau de l'application, sans remplacer leur
logique de navigation. Voir [l'apparence commune](HARMONIZATION.md).

`DesktopLayout` gère les identifiants de blocs, leurs positions, les collisions,
la recherche d'un emplacement libre et la persistance atomique de `layout.json`.
`DesktopWorkspace` relie ce modèle aux fenêtres, aux gestes de déplacement et au
menu de notification. Horloge, météo, lecteur, projets, compteur, matériel et
compteurs Codex ne partagent plus une fenêtre indissociable.

Les positions sont écrites pour un plan de référence de deux écrans 2560 × 1440,
en pixels logiques. `DesktopScreens` relève les moniteurs réellement présents :
un agencement écrit pour un autre ensemble de dalles est ramené dans celles-ci —
décalé, réduit à sa taille minimale, puis masqué si la place manque —, les
grilles et la barre suivent les écrans réels, et le canevas natif couvre la boîte
réelle du bureau virtuel en pixels physiques.

`DashboardTransition` compose deux dessins WPF conservés sous un découpage fixe.
Des transformations animées remplacent les informations à l’intérieur des docks
Conrad/Codex, sans modifier leurs fenêtres ni le verre natif. Une seule destination
en attente regroupe les clics rapides. Les données conservent leur cadence par
révision ; aucun timer de dessin ne pilote le glissement. Masquage, occultation
et changement de taille arrêtent les horloges. Voir [les pages internes](DOCK_PAGE_TRANSITIONS.md).

`PaletteHotkey` possède son propre HWND de messages et enregistre Ctrl+Espace
avec `RegisterHotKey` et `MOD_NOREPEAT`. Un conflit est exposé dans les réglages
et l’inspection ; le raccourci d’une autre application n’est pas retiré.
`CommandPaletteWindow` est une fenêtre temporaire activable, hors du placement
des widgets. Elle se ferme sur perte de focus ; Échap rend le focus précédent.
`PaletteSearch` normalise les accents et classe les correspondances par nom,
type et sous-séquence. Le mode `>` filtre les commandes. Les projets supplémentaires
viennent d’une énumération asynchrone des noms de sous-dossiers, sans lecture de
contenus. Aucun historique de recherche ni saisie de palette n’est enregistré.

`SettingsWindow` est une fenêtre normale, unique. `DesktopSettings` conserve
`preferences.json` dans les données utilisateur ; `desk/settings.json` reste
la valeur initiale du projet. La date garde son fichier personnel `target.txt`.
L’aperçu d’apparence appelle directement le moteur natif et se restaure à la
fermeture. Modifier le lieu météo ou le dossier réinitialise uniquement le lecteur
Desk ; ni le backend matériel ni les hôtes terminal ne sont redémarrés.

`DesktopPlacement` compare l'hôte des icônes à une référence basse invisible pour
suivre Win+D. Le terminal autorise l'activation et la saisie ; les autres widgets
restent utilisables sans voler le focus. Aucun parentage interprocessus du terminal.

Le terminal utilise un second processus `Battlestation.exe --terminal-host` :

- `ConPtySession` crée les pipes, la pseudo-console Windows et les processus shell.
  Les lectures, écritures et fermetures sont hors du fil d'interface.
- `TerminalView` utilise le moteur natif de Windows Terminal pour l'ANSI/VT,
  le défilement, la sélection, les couleurs et le rendu accéléré.
- `NativeTerminalWindow` possède les onglets et leurs surfaces. Masquer ou
  réorganiser le bloc ne détruit pas les sessions.
- Le pipe `Battlestation.NativeTerminal.v1`, limité à l'utilisateur courant,
  transporte position et actions. Il ne transporte pas les transcriptions.
- Une relance du bureau détache puis retrouve le même hôte. Aucun onglet n'est
  fermé pour mettre à jour le rendu du bureau.

L'ancien hôte sans protocole glass peut subsister dans une fenêtre détachée avec
ses sessions, sur le pipe historique `Battlestation.NativeTerminal`. Son mutex
ne bloque pas le nouvel hôte. Le bureau ne s'y rattache plus. Fermer explicitement
le dernier onglet du nouvel hôte termine celui-ci ; une ouverture ultérieure
utilise l'exécutable du bureau courant. Masquer le bloc ne termine jamais l'hôte.

Les données multimédias viennent de GSMTC, la météo d'Open-Meteo, le spectre audio
de NAudio. La pochette reste en mémoire et n'est recopiée que lorsqu'elle change.
Tous les sous-dossiers de projets sont classés par modifications de sources, en
excluant les dossiers générés. Le dock affiche les cartes qui tiennent dans sa
taille et permet de faire défiler le reste. Un FileSystemWatcher regroupe les
changements de sources ; le scanner ne réanalyse que les projets concernés,
avec une réconciliation complète espacée. Retirer ou couvrir le bloc suspend
ce scanner. Les états Git ne sont demandés que pour les cartes présentées.

Le bureau invalide les surfaces selon la révision de leurs données. Horloge et
compteur suivent la seconde ; météo, applications et projets statiques ne sont
plus redessinés quatre fois par seconde. Les ressources de dessin stables sont
réutilisées. Une politique d'occultation conserve les effets sur les écrans
exposés, suspend les producteurs inutilisés et reprend après retour au bureau.
L'analyse audio et la capture WGC ont leurs workers propriétaires ; aucune
attente de périphérique ou de transfert GPU ne se fait dans leurs ticks WPF.
La Montagne suit la même règle : sa scène est tracée par pixel dans le shader du
bloc (`Shaders/Montagne.fx`, `ps_3_0`), composée dans la fenêtre du dock
au-dessus du verre natif, et sa boucle de rendu n'existe que pendant que le bloc
est exposé. Voir [la montagne](MONTAGNE.md).

Le radar d'achat sépare le dock de son moteur : `ShoppingSurface` et
`ShoppingRadar` ne connaissent que `IShoppingEngine`, tandis que
`Battlestation.Shopping` porte l'analyse de la demande, les adaptateurs de
boutiques, le verdict et la veille. Les relevés datés et les veilles vivent dans
`shopping.db` (SQLite, connexions non regroupées) ; `shopping.json` garde les
seuils et le fournisseur de modèle. Les requêtes passent par un adaptateur par
enseigne, espacées et mises en cache sur disque ; une boutique qui refuse la
lecture est signalée comme telle. Le dock ne prend pas le focus clavier : la
saisie se fait dans une fenêtre ancrée, comme l'édition d'un rappel. Voir [le
radar d'achat](SHOPPING.md).

Les diagnostics sont écrits en arrière-plan et tolèrent les erreurs disque.
Le pipe de contrôle borne les messages, impose un délai par requête et évite
la capture du contexte WPF dans son client synchrone. La perte d'une cible
Direct2D provoque une reconstruction des ressources plutôt que l'arrêt du fond.

Les lecteurs matériels et de compteurs sont issus des projets locaux Conrad et
Codex Meter. Les écritures GPU restent protégées contre un autre Conrad actif.
`Battlestation.GpuHelper.exe` est lancé à la demande, pas au démarrage de Windows.
Les lectures Codex utilisent l'app-server existant sans demander de génération.
Le solde DeepSeek est lu directement sur l'API du compte (`/user/balance`) avec
la clé `DEEPSEEK_API_KEY` de l'environnement utilisateur ; la clé n'est ni
journalisée ni conservée dans les données.

Les sources et assets utilisent des chemins relatifs au projet. Les réglages
utilisateur sont dans `%LOCALAPPDATA%\Battlestation` ; la date cible déjà choisie
reste prioritaire sur la valeur initiale de `desk/settings.json`.

Les palettes d'ambiance des scènes, le protocole terminal de thème et la persistance des scènes sont décrits dans [THEMES_SCENES.md](THEMES_SCENES.md).
