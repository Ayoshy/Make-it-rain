# Battlestation

Application personnelle pour Ayo, son PC et ses périphériques ; aucune utilisation
publique prévue. Le développement et la QA ciblent cet environnement et ses usages
réels, sans compatibilité générale ni couverture de cas limites hypothétiques.

Bureau Windows modulable en .NET 10 / WPF, avec fond animé Direct2D, panneaux
liquid glass et terminal natif ConPTY. Les widgets restent au niveau du bureau,
derrière les applications ordinaires.

Blocs indépendants : horloge, météo, applications, lecteur, projets, terminal,
compteur Vice City, matériel, compteurs Codex, solde DeepSeek, Nudge, vidéo, audio, Bluetooth,
DualSense, Réseau, Achats, Montagne, LoL, Bloc-notes et Atelier. Le compteur Vice City peut être retiré sans
toucher au reste.

- **Applications** : cliquer une app déjà lancée réaffiche sa fenêtre existante,
  en la restaurant si elle est réduite. Les clics rapprochés pendant le démarrage
  ne lancent pas plusieurs instances. Une icône en cours précise aussi la fenêtre :
  néon vif et liseré clair au premier plan, néon actuel en arrière-plan, néon
  atténué quand la fenêtre est réduite, nacré avec un point discret pour une
  application en zone de notification. Le titre **APPLICATIONS** suit la police
  Art déco commune. La colonne peut descendre à 144 px de large ; ses icônes
  restent centrées et son bouton **+** se place en bas lorsqu'elle est haute.
- Clic droit sur un bloc → **Réorganiser**. Glisser le bloc pour le déplacer,
  ou ses bords/coins pour le redimensionner. **Lier les docks**, dans la barre,
  active le redimensionnement partagé et la poussée des voisins. Cette liaison
  est désactivée par défaut ; sans elle, seul le bloc saisi change.
- Maintenir **Maj au début du geste** inverse temporairement la liaison.
  Le choix permanent est également disponible dans **Réglages → Bureau**.
- **Réglages → Bureau → Grille** : activation et pas de 4 à 64, 8 par défaut.
  Changer le pas ne déplace pas les blocs déjà placés.
- **Échap** annule le geste ; **↶ / ↷** et **Ctrl+Z / Ctrl+Y** annulent/rétablissent
  les gestes validés. Les tailles sont conservées dans chaque disposition.
- **×** retire un bloc. **+ Ajouter** le replace dans un espace libre.
- L'icône Battlestation dans la zone de notification permet de réorganiser ou
  réafficher les blocs, même lorsque tous sont masqués.
- **Terminer** quitte l’édition. La disposition est sauvegardée automatiquement.
- **Scènes**, dans la palette, les réglages, le menu de notification et Réorganiser :
  **Bureau**, **Jeu**, **Multimédia** et **Mono écran**, plus les copies personnelles.
  Chaque scène conserve ses positions, tailles, docks visibles, transparence,
  animation et réactivité musicale. Bureau présente New York depuis une baie vitrée ;
  Multimédia un fjord au crépuscule ; Jeu alterne les deux illustrations Jason/Lucia
  toutes les cinq minutes visibles, avec deux secondes de fondu. La progression
  survit au rechargement du bureau ; les animations désactivées la suspendent.
  Terminal, Projets et Vidéo sont présents dans les quatre modèles. En Jeu, le
  secondaire porte Vidéo, Terminal, Réseau, LoL et DualSense ; les autres blocs
  sont sur le principal, où la fenêtre du jeu peut les recouvrir.
  Changer de scène dissout les docks et leur verre puis les fait revenir en cascade.
- **Rétablir le modèle** réinitialise la scène courante ; **Sauver sous…** crée
  une copie personnelle en conservant la scène d’origine. Les scènes ne changent
  pas la sortie audio, le volume ou le mute et ne lancent aucune application.
- Débrancher le secondaire active automatiquement **Mono écran** après une seconde
  de stabilisation : horloge, applications, terminal, vidéo, projets, lecteur, audio
  et Bluetooth sur le principal. Cette disposition conserve ses propres modifications.
  Rebrancher le secondaire restaure exactement la scène précédente, y compris après
  un redémarrage. **Mono écran** est aussi disponible dans **Scènes** pour préparer
  son agencement. Les blocs retirés restent masqués et les sessions terminal restent
  ouvertes.
- Les dispositions suivent les dalles réellement présentes : remplacer le secondaire
  par une dalle plus étroite, passer sur un ultra large ou démarrer sur un portable
  ramène les blocs dans les écrans du moment — la fenêtre bouge, se réduit à sa
  taille minimale, puis reste masquée si la place manque.
- **Réglages → Apparence** : transparence du verre, animation du fond et réactivité
  musicale ; l’aperçu agit immédiatement et **Enregistrer** le conserve. L’ambiance
  — palette et fond des deux écrans — vient de la scène et change avec elle.
  Les anciens hôtes terminal conservent leur habillage jusqu’à leur fermeture.
  Voir [les scènes et ambiances](docs/THEMES_SCENES.md).
- Les titres de dock partagent la police Art déco d'Atelier. Au survol, un liseré
  coloré souligne les commandes et un reflet lumineux suit la souris sur les
  cadres liquid glass. Les contrôles désactivés ne s'illuminent pas ; aucun
  minuteur d'animation supplémentaire. Voir [le style commun](docs/LIQUID_GLASS.md).
- **Vidéo** : choisir YouTube, Twitch ou Stremio, puis cliquer **Activer le miroir**.
  Un clic sur l'image commande lecture/pause ; **■** arrête le miroir et **↗**
  revient au lecteur. Voir [l'intégration Brave et Stremio](docs/VIDEO.md).
- **Bluetooth** : quatre dessins nacrés sur le même fond liquid glass que les
  autres docks. Les appareils sont déjà appairés : le clic connecte/déconnecte
  les Buds et les enceintes. **Néon Vice City = connecté ; gris nacré = déconnecté**,
  après confirmation Windows, avec un fondu entre les deux et une animation
  temporaire pendant la commande.
  La DualSense se déconnecte au clic et se reconnecte avec son bouton **PS**.
  Aucun pilote n'est désactivé et l'appariement est conservé. Le survol précise
  l'état et l'action ; un état indisponible n'est pas présenté comme déconnecté.
  Le pourcentage de batterie apparaît sous le dessin si Windows le fournit et
  que l'appareil est connecté ; les niveaux bas (20 % ou moins) sont ambrés.
  Pour les Buds3 Pro, chaque écouteur a son pourcentage ; une mini-icône du
  boîtier porte le troisième niveau. Les mesures Samsung sont relues toutes
  les 30 secondes ; « — » signifie que le niveau n'est pas communiqué.
  Voir [le fonctionnement et les preuves Bluetooth](docs/BLUETOOTH.md).
  En mode **Réorganiser**, redimensionner le dock choisit automatiquement une
  ligne, une colonne ou une grille ; les quatre appareils restent visibles.
  Le bloc est ajoutable depuis **+ Ajouter**.
- **DualSense** : ajout depuis Réorganiser → Ajouter, ou depuis la scène Jeu. Boutons,
  sticks, gâchettes, connexion et batterie disponible ; **Axes bruts** pour le drift.
  Coque nacrée qui grandit avec le dock et réactions lumineuses à la pression.
  **Traînée tactile** suit deux doigts ; ce mode Bluetooth amélioré demande un
  redémarrage de la manette pour revenir au mode compatible. La vibration est retirée.
  Voir [les détails et vérifications](docs/DUALSENSE.md).
- **Réseau** : débits, courbes sur 60 secondes et latence vers une cible configurable.
  **Détail par application** demande une autorisation Windows et affiche les cinq
  exécutables les plus actifs. Voir [le fonctionnement du réseau](docs/NETWORK.md).
- **Achats** : décrire une demande et un budget (« frigo max 800 €, no frost,
  300 L, blanc »), comparer les modèles et les offres de plusieurs boutiques,
  séparer les **Choix** vérifiés des **Pistes** encore incomplètes, surveiller un article et être
  prévenu quand son prix descend. Aucun achat ni message automatique ; le clic
  ouvre l'annonce. Voir [le radar d'achat](docs/SHOPPING.md).
- **Météo** : conditions du moment, plage du jour et vent depuis Open-Meteo.
  À partir de 190 px de haut, une bande horaire de six cellules apparaît sous le
  bloc (heure, icône jour/nuit, température) ; en dessous, le rendu reste celui
  d'avant. La pluie imminente est annoncée près du vent (« Pluie maintenant »,
  « Pluie dans 15 min »…) d'après les pas de 15 minutes.
- **Lecteur** : la pochette du morceau remplace la teinte du verre — floutée,
  elle reste translucide et teinte le bloc de sa couleur dominante (liseré,
  progression, égaliseur). L'égaliseur occupe le bas du verre sur toute la
  largeur, étiré bord à bord, jusqu'à la moitié de la hauteur, avec des crêtes
  qui montent d'un coup puis retombent ; la barre de progression lui sert de
  socle et le liseré pulse sur les basses. Source, titre, artiste, temps,
  précédent/lecture/suivant et la recherche restent commandés par le lecteur
  Windows.
- **Montagne** : neige perpétuelle sur un pic enneigé, calculée par pixel sur le
  GPU : ciel de nuit étoilé, lune basse, crête avec ses faces éclairées et
  ombrées, couloirs de neige soufflée et éclats sur les congères. Des flocons
  discrets tombent sans fin sur trois plans de profondeur ; le passage du
  curseur les écarte doucement, et le spectre du bloc Lecteur lève le vent,
  accélère la chute et fait scintiller la neige, sans seconde capture audio.
  Masquée ou occultée, elle ne calcule rien. Voir
  [la montagne](docs/MONTAGNE.md).
- **LoL** : télémétrie de partie en cours, lue **uniquement** pendant une partie.
  Champion et niveau, temps de partie, K/D/A, CS, or, **Respawn** en cas de mort,
  et bandeau d'objectifs par équipe (dragons, barons, tourelles, inhibiteurs).
  Seuls les minuteurs dérivables des événements sont affichés : **Dragon +5:00**
  et **Baron +6:00** après le kill correspondant. Source unique, l'API locale du
  client de jeu (`127.0.0.1:2999`), sans jeton ni fichier du jeu. États :
  « Aucune partie », « En attente de la partie », « Télémétrie indisponible ».
  Voir [le dock LoL](docs/LOL.md).
- **Atelier** : **Garder cette version** conserve le build ouvert pour les prochains
  lancements ; **Nettoyer les builds** libère l'espace des builds inutilisés en
  protégeant les sessions et le pont vidéo. Ajout depuis **Réorganiser → Ajouter →
  Atelier** ; chemins techniques dans **Détails**. Voir
  [le dock Atelier](docs/ATELIER.md).
- **Bloc-notes** : texte libre éditable directement dans le dock, avec retour à la
  ligne, défilement et copier-coller. Sauvegarde automatique locale dans
  `%LOCALAPPDATA%\Battlestation\notes.txt`, commune aux scènes. Retirer le bloc
  conserve le texte ; ajout depuis **Réorganiser → Ajouter → Bloc-notes**.
- **Nudge** : texte Art déco, clic sur le rappel pour le modifier, **✓** pour
  le terminer et **+** pour ajouter. La liste suit la hauteur du bloc
  (`(hauteur - 96) / 44` rappels, borné à la liste) et pagine par **‹ / ›** avec
  le compteur de page dès que les rappels dépassent la hauteur ; chaque ligne
  reste cliquable et garde son **✓**. La molette change aussi de page.
- Clic droit sur un onglet terminal : couleur, nom et titre automatique. Les états
  Codex ont un indicateur animé ; voir [les onglets](docs/TERMINAL_TABS.md).
- Les onglets Codex portent un compte à rebours estimé du cache de prompt (« ⏳ ≈ 12 min »
  puis « cache incertain ») et leur fond se teinte tant qu'aucune couleur n'est
  choisie ; les onglets Codex (DS) affichent le taux de hit du cache.
- Le terminal dispose de ses propres onglets PowerShell/Codex ; retirer son bloc
  masque son affichage et conserve ses sessions.
- **Projets** : le statut Git est vert quand le dépôt est propre, ambre avec des
  modifications, gris quand il est inconnu. Chaque carte affiche la branche, les
  fichiers modifiés, l'écart **↑avance / ↓retard** sur le suivi et l'âge du dernier
  commit ; un point néon signale qu'un onglet terminal porte le nom du dossier du
  projet (indice, pas une preuve d'activité). Cliquer une carte ouvre un menu sous verre
  avec icônes nacrées : Explorateur, Codex CLI / ChatGPT, Codex CLI / DeepSeek et
  Kilo CLI / DeepSeek. Il se replie par son chevron ou un clic hors du menu ; ses
  quatre tuiles passent sur deux lignes dans un dock étroit. Kilo ouvre un nouvel
  onglet du terminal sur le projet sélectionné et y démarre `kilo`.
- **Ctrl+Espace** ouvre la palette : recherche d’applications et projets,
  flèches pour choisir, Entrée pour ouvrir, Échap pour revenir/fermer. Le préfixe
  **>** limite la recherche aux commandes. Un projet propose Explorateur, Codex CLI (ChatGPT),
  Codex CLI (DS) et Kilo CLI (DS) ; Kilo démarre le CLI dans le projet sélectionné.
  La fenêtre garde sa taille pendant la recherche ; les icônes nacrées du dock
  suivent le thème, sont chargées en arrière-plan et réutilisées. La colonne **Scènes** conserve
  les aperçus des dispositions et distingue la scène active du survol.
- **Réglages** est accessible dans la palette, le menu de notification et le clic
  droit d’un bloc : blocs visibles, applications, dossier des projets, météo,
  compteur, transparence du verre et animation du fond. L’apparence est prévisualisée
  immédiatement ; **Enregistrer** la conserve. Fermer restaure l’apparence enregistrée.
  La visibilité des blocs et l’éditeur d’applications s’appliquent séparément.
- Les boutons Conrad/Codex remplacent les informations à l’intérieur du dock par
  une transition sous verre : glissement, fondu, flou doux ou facettes, alternés
  dans un ordre mélangé, chacun une fois par cycle. Recliquez le bouton actif pour revenir au résumé.
  Le cadre et les boutons restent fixes ; quotas, modèles et solde DeepSeek se
  consultent par page, les listes défilant à la molette.
  **DEEPSEEK** lit le solde du compte via l’API et `DEEPSEEK_API_KEY`, gardée dans
  l’environnement utilisateur ; Actualiser relit Codex et DeepSeek.
  Voir [les transitions internes](docs/DOCK_PAGE_TRANSITIONS.md).
- Fermer explicitement son dernier onglet termine l'hôte terminal ; la prochaine
  ouverture utilise le build du bureau courant. Les anciennes sessions d'avant
  la migration glass peuvent rester dans une fenêtre séparée sur le premier écran.

```powershell
$candidateBuild = 'build/battlestation-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
.\scripts\Build-Battlestation.ps1 -OutputDirectory $candidateBuild
.\scripts\Start-Battlestation.ps1 -Build $candidateBuild
```

Chaque compilation utilise un nouveau dossier et conserve la sélection de démarrage.
Le lancement explicite ci-dessus sert aux essais du candidat. Après validation et
acceptation, inscrire son chemin relatif dans `build/current.txt` ; le lancement
habituel utilise alors `.\scripts\Start-Battlestation.ps1` sans `-Build`.

Les chemins du projet sont relatifs : le dossier parent peut porter le nom
Battlestation. Les paramètres personnels restent dans `%LOCALAPPDATA%\Battlestation`.
`build/current.txt` sélectionne le **prochain lancement**, pas nécessairement le
processus déjà ouvert. Après acceptation d'une livraison, ce pointeur doit viser
le build livré ; le build remplacé est archivé hors de `build/`, sous
`backups/retired-builds/<date>/`, après contrôle des processus et des références.
Un ancien hôte terminal conserve son build jusqu'à fermeture par l'utilisateur.
Le lanceur refuse les archives et s'arrête si le pointeur manque ou est vide.
Le 15 septembre 2026, la cible a été alignée sur `build/battlestation-dualsense-touch-05`
et `battlestation-nudge-bluetooth` retiré vers les archives.
Le lancement Windows automatique n'est pas modifié par les scripts ci-dessus.
Depuis le 14 septembre, la tâche Windows `Battlestation` lance le build courant
à l'ouverture de session, sans délai. Le lanceur place désormais l'application
en priorité normale pour laisser le CPU aux autres applications. Il attend
la surface du bureau puis se termine ; il ne reste pas de PowerShell de lancement.
Pour réinstaller cette tâche, exécuter `scripts/Install-Startup.ps1` dans un
PowerShell administrateur du même utilisateur. Rainmeter a été désinstallé.

Voir [l'architecture](docs/ARCHITECTURE.md), [l'intégration vidéo](docs/VIDEO.md)
et [les crédits](docs/PROVENANCE.md).

Le build courant réunit aussi le [cockpit audio, fond musical, dispositions,
états projets et réserve de liens](docs/COCKPIT.md). La compilation ne modifie jamais
la version ciblée par le lancement Windows ; `-NoActivate` reste accepté pour
les anciennes commandes et n'est plus nécessaire.
