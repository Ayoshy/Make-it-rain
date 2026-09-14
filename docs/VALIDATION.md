# Validation courante — Battlestation

## Palette, réglages et volets ancrés — activés le 14 septembre 2026

Version active : `build/battlestation-palette-settings`, désignée par
`build/current.txt`. Bureau PID 20052 au dernier relevé ; chemins des modules
managés et natifs vérifiés dans ce dossier. Les versions des sections suivantes
sont historiques. Aucun changement de configuration du démarrage Windows.

- Ctrl+Espace est réellement enregistré par Windows (`hotkeyRegistered=true`,
  erreur 0). Palette activable hors de la couche des widgets, recherche sans accents
  et par sous-séquence, sélection clavier, mode `>`, projets avec choix explicite
  Explorateur/Codex CLI. Le service utilise MOD_NOREPEAT et libère son propre
  raccourci à l’arrêt. Un conflit reste visible dans Réglages et permet une nouvelle
  tentative ; aucune application concurrente n’est fermée.
- Réglages accessibles depuis la palette, le menu de notification et le menu d’un
  bloc : visibilité, applications du dock, dossier des projets, lieu météo,
  date/heure du compteur, transparence des panneaux et animation du fond. Aperçu
  d’apparence immédiat, enregistrement dans `preferences.json`, restauration de
  l’apparence enregistrée à la fermeture. Blocs et éditeur du dock s’appliquent
  séparément. La date existante et son offset sont conservés si non modifiés.
- Le défaut Conrad était causé par `DesktopLayout.Resize` : l’agrandissement à
  443 px cherchait un emplacement libre sur les deux écrans, puis sauvegardait ce
  déplacement. Les détails sont maintenant des volets temporaires : résumé fixe,
  même écran, ouverture vers le haut au besoin, aucun changement du layout.
  Leur matériau plus dense évite que les libellés du voisin transparaissent.

Build final sans avertissement. `scripts/Validate.ps1` passe les cinq suites
managées et les contrats natifs. Après les derniers ajustements visuels, les
contrôles Commands/Desktop/Layout ont aussi passé ; le dernier ajustement de clip
et survol des volets a été compilé et le volet Codex contrôlé sur le bureau réel.
La nouvelle suite Commands vérifie recherche accentuée et multi-termes, commande
inconnue sans action de substitution, Entrée sur la ligne sélectionnée, les deux
choix projet, persistance/validation des préférences et conflit/libération du
raccourci. Les essais WPF utilisent uniquement des fenêtres et actions isolées.

Vérifications live : les ouvertures/fermetures internes du volet Conrad gardent
X=4272, Y=720, hauteur 218 → 443 → 218 ; fichier de disposition inchangé.
Le volet Codex garde son résumé à (4272,960), étend sa fenêtre à Y=726, puis
revient à 209 px sans sauvegarder de déplacement. Les rapports et captures
`commands-final-conrad-expanded.*`, `palette-settings-codex-expanded.json` et
`commands-settings-live.png` sont dans `artifacts/validation`. La capture des
réglages provient de sa vraie fenêtre WPF via PrintWindow. Les captures ne
conservent pas de contenu de terminal. `commands-preview-final.png` est seulement
un aperçu isolé de palette.

Dernier rechargement : bureau 3884 → 20052, neuf blocs identiques avant/après.
Hôte terminal 14608 / HWND 1114672 : deux sessions avec identifiants et PID
conservés. Hôte historique 6312 / HWND 132134 : une session conservée. Preuves
`palette-settings-{desktop,terminal,legacy}-{before,after}.json`,
`palette-settings-loaded-modules.json` et `dotnet-palette-settings-windows.json`.
Le fond reste attaché en 5120 × 1440, les widgets derrière les applications
ordinaires et le terminal au-dessus de son cadre. Aucun onglet utilisateur fermé,
aucune saisie/prompt de test, aucun réglage GPU appliqué.

L’utilisateur a ensuite confirmé « Tout fonctionne » après l’essai demandé :
Ctrl+Espace, recherche « réglages » et clic sur « 6 cœurs » ou « Refroidissement »
avec Conrad restant en place. Le raccourci, l’accès aux réglages et l’ancrage du
volet sont donc confirmés en usage réel. Computer Use reste indisponible (native
pipe, erreur 2). Cette confirmation ne constitue pas un essai de chaque contrôle
des réglages, ni une validation matérielle des écritures GPU.

## Popup projets transparente — activée le 14 septembre 2026

Le bureau charge `build/battlestation-project-clear`, indiqué par
`build/current.txt`. Relevé live après rechargement : PID 16080 ; modules
Battlestation managés et natifs vérifiés dans ce dossier. Les versions et PID des
sections suivantes sont des relevés antérieurs.

La liste de projets reste dessinée derrière la popup. Ses cartes sont légèrement
floutées et grossies, avec une déformation plus forte dans trois bandes de bord,
un liseré nacré et un reflet lié au pointeur. La teinte sombre native de cette
popup passe de 46 % à 8 %, et son échantillon de fond diffus est mélangé à 22 %.
Le rendu des autres panneaux ne change pas. Il s'agit d'une approximation Windows
inspirée de Liquid Glass, pas du matériau système Apple ni d'une simulation
optique complète. Le cache WPF reste en mémoire et ne contient que les dessins
des cartes projets de cette ouverture, jamais les pixels du terminal ou du bureau.

Build sans avertissement ; `scripts/Validate.ps1` passe. Le contrôle isolé
`artifacts/validation/project-glass-check/Check.csproj` compile les vraies sources
DeskSurface/Surface avec des données factices et sans backend : six cibles dans
la liste, trois dans la popup même après plusieurs rendus, fermeture et nouvelle
sélection vérifiées. Les cartes de fond ne reçoivent pas les clics de la popup.
Les PNG de ce contrôle sont des aperçus, pas des preuves du bureau installé.

L'utilisateur a ensuite ouvert la popup sur le bureau réel. Capture limitée au
bloc Projets : `artifacts/validation/project-clear-popup-live.png`, avec état
`project-clear-popup-live.json`. Les cartes restent perceptibles sous le verre et
les actions sont lisibles. Computer Use n'a pas pu se connecter au native pipe
(erreur 2) : ouverture par l'utilisateur confirmée, mais les actions Explorateur,
Codex CLI et fermeture n'ont pas été réessayées par clics réels dans cette session.

Rechargement du seul bureau : 11908 → 16080. Hôte courant 14608, HWND 1114672,
session `aff613ee-739a-4ea4-b1a4-c005fa642537`, shell 11532 inchangés. Hôte ancien
6312, HWND 132134, session `9b830497-a552-4177-8fa8-e443b8761a46`, shell 9540
inchangés. Les neuf blocs gardent leurs positions ; fond animé en 5120 × 1440,
hôte terminal au-dessus de son cadre. Aucun onglet fermé, aucune saisie de test
dans un terminal utilisateur, aucun changement GPU ni configuration de démarrage.
Preuves avant/après : `project-clear-{desktop,terminal,legacy}-{before,after}.json`
et `dotnet-project-clear-windows.json` dans `artifacts/validation`.

## Onglets et ascenseur glass — activés le 14 septembre 2026

Le bureau **et** son hôte terminal chargent maintenant
`build/battlestation-glass-live`, désigné par `build/current.txt`. Dernier relevé :
bureau PID 11908, hôte PID 2804, HWND 722054, `chromeVersion=1`,
`externalChrome=true`, `headerVisible=false`. La barre glass est dessinée dans
le cadre du bureau ; l'ascenseur nacré est chargé dans le contrôle natif.

Cause du problème persistant : le redémarrage du bureau préservait l'hôte 6312
de `battlestation-single-header`. Fermer puis recréer ses onglets ne renouvelait
pas cet hôte. Le nouvel hôte utilise le pipe et mutex
`Battlestation.NativeTerminal.v1`, séparés de l'ancien protocole. Le script
`Invoke-Battlestation.ps1 -Terminal` cible ce nouvel hôte. La fermeture explicite
du dernier onglet du nouvel hôte provoque désormais sa sortie ; le masquage du
bloc et le rechargement du bureau conservent toujours les sessions ouvertes.

L'ancien hôte reste détaché à (120,120) sur le premier écran, avec son onglet
Battlestation PID 9540, identifiant `9b830497-a552-4177-8fa8-e443b8761a46`,
inchangé avant/après bascule. Trois onglets avaient été observés au début du
diagnostic ; un seul subsistait au relevé juste avant la bascule. Aucune fermeture
d'onglet utilisateur n'a été envoyée. L'ancien hôte reste joignable explicitement
par `-PipeName Battlestation.NativeTerminal` jusqu'à sa fermeture utilisateur.

Le nouveau bureau a ensuite été rechargé une seconde fois : PID 12996 → 11908.
Hôte, HWND, identifiants et PID de ses deux sessions sont identiques avant/après ;
la barre externe est rétablie. Les neuf blocs gardent exactement leur disposition,
le fond est en 5120 × 1440 et le terminal reste au-dessus de son cadre, derrière
les applications ordinaires. Aucune modification GPU ou du démarrage Windows.

Build sans avertissement ; `scripts/Validate.ps1` passe. Le test terminal vérifie
aussi le template glass réellement appliqué au ScrollBar natif, son défilement,
et la notification de fermeture du dernier onglet, sans retraite lors du masquage.
Ces tests WPF ne remplacent pas les clics réels. Aucun prompt Codex envoyé.

Preuves dans `artifacts/validation` : `glass-live-legacy-before.json` et
`glass-live-legacy-after.json`, `glass-live-terminal-before-reload.json` et
`glass-live-terminal-after-reload.json`, `glass-live-desktop-final.json`,
`dotnet-glass-live-final-windows.json`. `terminal-glass-live-screen.png` montre
les onglets sur le bureau réel, avec une fenêtre Explorateur devant sa droite.
`terminal-glass-live-scrollbar.png` capture uniquement l'ascenseur de l'hôte réel
via PrintWindow, sans conserver son contenu terminal.

Les états ci-dessous sont historiques, notamment les mentions d'activation en attente.

## Récupération et démarrage Windows — 14 septembre 2026

Le bureau affiché au début de la session était l'ancien Rainmeter, PID 16400.
Battlestation n'était pas lancé. Le build `battlestation-single-header` et la
disposition personnelle du 13 septembre étaient toujours présents. Le lancement
du build courant a retrouvé les neuf blocs et leurs positions sauvegardées.

À la demande explicite de l'utilisateur le 14 septembre, le démarrage Windows
est maintenant activé : tâche `Battlestation`, utilisateur interactif Ayo,
sans élévation de l'application, priorité High (1), aucun délai de déclenchement,
aucune limite de durée ni restriction secteur/batterie. Le lanceur lit
`build/current.txt`, attend la surface du bureau Windows et quitte après le
lancement. Aucune session terminal n'est fermée par ces scripts.

Rainmeter a été désinstallé avec son désinstalleur. Son démarrage et ses dossiers
installés de skins, plugins et données utilisateur ont été retirés. Les projets
Conrad Sensor, Codex Meter, le fond sous Steam et l'historique Git n'ont pas été
modifiés. Rainmeter était déjà arrêté au relevé précédant la désinstallation ;
sa dernière sonde datée indiquait Canicule désactivée et aucune consigne GPU
chargée. Ce snapshot ne constitue pas une nouvelle mesure matérielle. Aucun
réglage GPU n'a été envoyé.

La tâche a été exécutée réellement après fermeture du seul bureau : PID 12408,
priorité High confirmée, retour de tâche 0, fond attaché sur 5120 × 1440 et
neuf blocs visibles. L'hôte terminal 6312 et son HWND 132134 ont survécu.
Les listes d'onglets ont changé pendant l'essai : l'utilisateur a confirmé avoir
lui-même fermé les deux anciens onglets et ouvert le nouvel onglet Battlestation.
Aucune fermeture de session n'a été envoyée par la migration. Rapports avant/après dans
`artifacts/validation/recovery-20260914`, tâche exportée dans
`artifacts/validation/battlestation-startup.xml`.

Les quatre suites managées et les contrats natifs passent. Les tests terminal
utilisent leurs propres fenêtres et shells, sans pipe de commande utilisateur.
Le démarrage réel du PC et la durée d'un éventuel aperçu du bureau Windows
restent à confirmer au prochain redémarrage ; aucun redémarrage n'a été imposé.

Les sections suivantes décrivent les relevés historiques du 13 septembre.

## Onglets et ascenseur glass — état du 14 septembre

Le build `build/battlestation-glass-chrome` réunit la barre `TerminalTabs` préparée
précédemment et le nouveau style `TerminalScrollBar`. Celui-ci habille le
ScrollBar existant du contrôle Windows Terminal : rail sombre, curseur nacré
arrondi et survol animé. Les événements du moteur et le Track WPF sont conservés.

Compilation réussie sans avertissement ; `scripts/Validate.ps1` passe entièrement.
Le shell de test produit 120 lignes pour vérifier le défilement par page et le
retour en bas avec le nouveau template. Sélection des onglets, bascule des barres,
conservation des PID, ConPTY, UTF-8 et masquage/réaffichage passent également.
Ces commandes WPF ne constituent pas une validation par clics réels.

`artifacts/validation/terminal-glass-chrome-preview.png` présente les vrais
contrôles WPF sur un fond de démonstration sans session terminal. Ce n'est pas une
preuve du rendu installé dans l'hôte utilisateur.

Le bureau seul a été rechargé : PID 12408 → 14484. Ses modules Battlestation
chargés proviennent de `build/battlestation-glass-chrome`, désormais désigné par
`build/current.txt`. La disposition des neuf blocs est strictement conservée,
avec le fond 5120 × 1440 et l'hôte terminal au-dessus de son cadre.

**Le terminal utilisateur conserve encore son ancien style.** Son hôte PID 6312,
HWND 132134, charge toujours `build/battlestation-single-header`. Il n'annonce
pas `chromeVersion` et ne peut pas changer de barre à chaud. Son unique onglet
Battlestation, session `d8ed8883-4a0c-40c2-97e0-95503a28bedc`, PowerShell PID 10868,
est resté prêt, sans erreur et avec la même identité avant/après rechargement.
L'ouverture d'un nouvel onglet dans cet hôte garderait aussi l'ancien style.
L'activation complète exige le remplacement de cet hôte ; l'onglet actif n'a pas
été fermé. Aucune saisie ni génération Codex de test n'a été envoyée.

Preuves : `glass-chrome-terminal-before.json`, `glass-chrome-terminal-after.json`,
`glass-chrome-desktop-before.json`, `glass-chrome-desktop-after.json` et
`dotnet-glass-chrome-ready-windows.json` dans `artifacts/validation`.
Les PID et chemins des sections suivantes sont des relevés historiques.

Le bureau utilise désormais des projets nommés Battlestation et une disposition
modulaire. Le dossier parent est renommé par l'utilisateur, pas par les scripts.
L'historique de travail antérieur est préservé localement dans `.git/local-history`.

## Contrôles automatisés

- 13 contrats de données : absence, zéro réel, bornes GPU, modèles sans prix,
  estimation partielle et passage de minuit. Aucun compte interrogé ni GPU modifié.
- Placement des fenêtres : remontée du shell avec ou sans topmost, retour derrière
  une application, géométrie, focus et survie d'un hôte externe. Le correctif Win+D
  précédent a été confirmé par l'utilisateur ; le nouveau bureau doit aussi être
  essayé en interaction.
- Disposition : neuf blocs, placement sans collision, deux écrans, retrait du
  compteur indépendant, expansion des deux blocs de monitoring, persistance,
  agrandissement du dock et restauration depuis un bureau entièrement masqué.
- Terminal : vraie pseudo-console Windows avec PowerShell, aller-retour de saisie,
  UTF-8, séquences truecolor et écran alternatif, redimensionnement et trois
  masquages/réaffichages. L'entrée est injectée dans un shell de test, pas dans
  Codex. Ce test ne valide pas une frappe physique ou le collage d'image utilisateur.

Les projets de tests ont des sorties séparées afin d'éviter les collisions
d'apphost quand plusieurs projets résident dans le même dossier.

Les contrats C++ de l'horloge, de la météo et de la pochette en mémoire passent
aussi. La solution `Battlestation.slnx` regroupe les trois projets applicatifs et
les quatre projets de tests managés. Les sources actives ne contiennent plus le
nom historique ; les références externes et les archives ne sont pas réécrites.

## Version chargée et preuves du 13 septembre

- Bureau `build/battlestation-desktop/Battlestation.exe`, PID 16760 lors du relevé.
  Le fichier local `build/current.txt` est utilisé par le script de lancement.
- Hôte terminal natif `build/battlestation-modular-native/Battlestation.exe`, PID
  14692, HWND 1180626. PowerShell 21176, session
  `d4edfa11-e0b6-4b1b-886d-3b63e540a5a4`, prêt et sans erreur au relevé.
- Rechargement du bureau 15076 → 16760 : hôte, HWND, identifiant de session et
  processus PowerShell identiques. Preuves `battlestation-reload-before.json` et
  `battlestation-reload-after.json` dans `artifacts/validation`.
- Mode édition exercé par le pipe : terminal masqué puis réaffiché sans changer
  son HWND ni sa session. Preuve `battlestation-edit-terminal-survival.json`.
- Horloge déplacée et compteur Vice City retiré par commandes internes : le verre
  suit l'horloge, le compteur disparaît seul, matériel et Codex restent présents.
  Capture `dotnet-independent-blocks-screen.png`. Disposition initiale restaurée.
- Capture du mode édition : `dotnet-modular-edit-mode-screen.png`. Ces captures
  sont celles du bureau réel ; les actions internes ne sont pas des clics physiques.
- Météo réelle relue : 21 °C, ciel dégagé, mesure de 21:00. Le fond reste en
  5120 × 1440 ; il se met en pause lorsqu'une application plein écran a le focus,
  comme avant, et les changements de verre sont repeints pendant cette pause.
- Les six anciens onglets sont conservés : hôte 9876, moteur 12820 et HWND
  10224726 inchangés. Leur fenêtre détachée est placée à (120,120) sur le premier
  écran pour ne pas couvrir le nouveau terminal. Aucun de ces onglets n'a été fermé.

L'hôte terminal garde volontairement le binaire déjà chargé afin de préserver la
session. Les nouvelles ouvertures d'hôte utilisent le build courant. Revérifier
toutes ces identités avant intervention : ce sont des observations datées.

## Points à confirmer en usage réel

L'utilisateur a confirmé que le déplacement des blocs fonctionne. Menus et focus
clavier du nouveau terminal, sélection,
défilement, collage texte/image et interface Codex sans consommation de crédit
automatique. Veille, changement de DPI et retour de plein écran restent distincts.
Computer Use est indisponible dans cette session (`native pipe`, erreur 2).

Les sessions existantes de l'ancien terminal sont conservées à part. Elles ne
sont pas migrées dans une autre pseudo-console et ne doivent pas être fermées
pour valider le nouveau rendu.

Pas de réglage GPU, de changement d'autostart ni de gain de ressources revendiqué.
Le paquet de distribution complet et la validation matérielle restent ouverts.

## Clic du terminal intercepté par le cadre — correctif installé

L'utilisateur a signalé que le clic ne prenait pas dans le terminal, malgré la
création possible d'onglets depuis les projets. Le test ConPTY précédent vérifiait
le transport de saisie, pas la fenêtre qui reçoit physiquement la souris.

`WindowFromPoint` a identifié le cadre WPF (HWND 1442680) aux trois points de la
zone de saisie (2704,624), (2744,624), (2784,624). Le terminal était sous ce cadre.
`DesktopPlacement` conserve maintenant l'hôte natif immédiatement au-dessus de
son cadre, tout en restant derrière les applications ordinaires. Cet ordre est
réparé même en dehors d'une transition Win+D.

Après installation, les trois points désignent le contrôle natif HWND 6423282,
racine 1180626, correspondant à l'hôte du terminal. Preuves locales :
`terminal-hit-before.json`, `terminal-hit-after.json` et les deux rapports
`terminal-hit-reload-before.json` / `terminal-hit-reload-after.json`.

Le bureau chargé est `build/battlestation-terminal-hit/Battlestation.exe`, PID
2804 au relevé. L'hôte 14692 et ses deux sessions (PowerShell 21176, projet Codex
Meter 1972) sont conservés ; positions et visibilité des neuf blocs sont identiques
avant/après. Aucun texte ni prompt de test n'a été envoyé à ces sessions.

La régression d'ordre cadre/terminal est couverte sur les fenêtres Win32/WPF de
test, avec et sans topmost, retour aux applications et rétrogradation de l'hôte.
Le routage réel de la souris est vérifié sans injection de clic ; la frappe
physique après correction reste à confirmer par l'utilisateur.

## Commandes

### Onglets liquid glass — préparés, activation en attente

`build/battlestation-glass-tabs` contient `TerminalTabs`, une barre WPF transparente
dessinée dans le cadre du bureau : onglets arrondis, sélection nacrée, fermeture,
collage, nouveau terminal et défilement lorsque les onglets débordent. Le fond
Direct2D du cadre reste visible derrière cette barre. L'hôte natif n'affiche que
la console lorsqu'il annonce le protocole `chromeVersion=1`.

Le protocole ajoute `chrome:external`, `chrome:internal`, `select:<guid>` et
`close-tab:<guid>`. L'identifiant est vérifié avant toute action. La barre interne
revient si le terminal est détaché. Les anciens hôtes restent compatibles : sans
`chromeVersion`, le bureau conserve leur barre existante.

Build sans avertissement. Le test terminal ouvre uniquement ses propres shells,
vérifie le passage barre interne/externe, la sélection et la conservation des PID,
rejette une fermeture avec identifiant périmé, puis ferme ses shells de test.
Il n'agit pas sur les sessions utilisateur et n'envoie rien à Codex.

`artifacts/validation/terminal-glass-tabs-preview.png` montre le vrai contrôle
sur un fond de démonstration, sans démarrer de terminal. Ce n'est pas une capture
du bureau installé. Commande de reproduction :
`Battlestation.exe --preview-terminal-tabs <chemin-png>`.

L'hôte utilisateur 14692 est toujours l'ancien binaire et ne sait pas remplacer
sa barre à chaud. Les deux sessions PowerShell 5 (6856) et PowerShell 6 (4388)
restent ouvertes. Le nouveau build n'a pas été activé : fermer ces sessions pour
redémarrer l'hôte nécessite une autorisation explicite de l'utilisateur.
`build/current.txt` reste sur `build/battlestation-single-header`.

### Barre unique du terminal

La rangée native d'onglets est désormais l'unique en-tête du bloc attaché. Le
contenu commence à 12 px du haut du cadre au lieu de 40 px ; il gagne 24 px de
hauteur, avec une marge basse de 12 px. Le « + » natif reste près des onglets.
Coller/précédent/suivant/nouvel onglet restent dans le menu contextuel du cadre.

Build chargé : `build/battlestation-single-header`, bureau 10668 au relevé.
L'hôte 14692 / HWND 1180626 et les deux sessions PowerShell 6856 et 4388 ont été
conservés, ainsi que la disposition réorganisée par l'utilisateur. Le contrôle
natif reste au-dessus du cadre. Capture réelle :
`artifacts/validation/dotnet-single-header-ready-screen.png`. Les rapports
`single-header-before.json` et `single-header-after.json` gardent les identités ;
le relevé `dotnet-single-header-ready-windows.json` confirme le placement final.

```powershell
.\scripts\Build-Battlestation.ps1 -OutputDirectory build/battlestation-next
.\scripts\Validate.ps1
.\scripts\Start-Battlestation.ps1
.\scripts\Invoke-Battlestation.ps1 -Command inspect
.\scripts\Invoke-Battlestation.ps1 -Terminal -Command inspect
.\scripts\Inspect-Battlestation.ps1 -Capture -Label current
```

Ne pas écraser un dossier contenant un binaire encore chargé, y compris l'hôte
terminal. Recharger uniquement le bureau par `close`, attendre la fin de son PID,
puis lancer le nouveau build. Vérifier les identités des sessions avant/après.
