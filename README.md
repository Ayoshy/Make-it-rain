# Battlestation

Application personnelle pour Ayo, son PC et ses périphériques ; aucune utilisation
publique prévue. Le développement et la QA ciblent cet environnement et ses usages
réels, sans compatibilité générale ni couverture de cas limites hypothétiques.

Bureau Windows modulable en .NET 10 / WPF, avec fond animé Direct2D, panneaux
liquid glass et terminal natif ConPTY. Les widgets restent au niveau du bureau,
derrière les applications ordinaires.

Blocs indépendants : horloge, météo, applications, lecteur, projets, terminal,
compteur Vice City, matériel, compteurs Codex, Nudge, vidéo, audio, Bluetooth et
DualSense et Réseau. Le compteur Vice City peut être retiré sans toucher au reste.

- **Applications** : cliquer une app déjà lancée réaffiche sa fenêtre existante,
  en la restaurant si elle est réduite. Les clics rapprochés pendant le démarrage
  ne lancent pas plusieurs instances.
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
- **Scènes**, dans la palette, les réglages, le menu de notification et Réorganiser,
  conserve **Personnel**, **Jeu**, **Création**, **Cinéma**, **Focus**, **Multimédia**,
  **Double écran** et les scènes personnelles déjà enregistrées. Chaque scène
  retrouve ses déplacements, tailles, docks visibles, thème, transparence,
  animation et réactivité musicale. Les gestes validés sont sauvegardés automatiquement.
- **Rétablir le modèle** réinitialise la scène courante ; **Sauver sous…** crée
  une copie personnelle en conservant la scène d’origine. Les scènes ne changent
  pas la sortie audio, le volume ou le mute et ne lancent aucune application.
- Débrancher le secondaire active automatiquement **Mono écran** après une seconde
  de stabilisation : horloge, applications, lecteur, audio, Bluetooth et terminal
  sur le principal. Cette disposition conserve ses propres modifications. Rebrancher
  le secondaire restaure exactement la scène précédente, y compris après un redémarrage.
  **Mono écran** est aussi disponible dans **Scènes** pour préparer son agencement.
  Les blocs retirés restent masqués et les sessions terminal restent ouvertes.
- **Réglages → Apparence → Thème** : **Vice City**, **Obsidienne**, **Aurore**.
  L’aperçu agit sur les docks, les menus WPF et le fond natif des deux écrans.
  **Enregistrer** conserve le choix ; fermer restaure le dernier choix enregistré.
  Les anciens hôtes terminal conservent leur habillage jusqu’à leur fermeture.
  Voir [les thèmes, scènes et vérifications](docs/THEMES_SCENES.md).
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
- **Nudge** : texte Art déco, clic sur le rappel pour le modifier, **✓** pour
  le terminer et **+** pour ajouter. Le passage au suivant apparaît seulement
  lorsqu'il existe plusieurs rappels.
- Clic droit sur un onglet terminal : couleur, nom et titre automatique. Les états
  Codex ont un indicateur animé ; voir [les onglets](docs/TERMINAL_TABS.md).
- Le terminal dispose de ses propres onglets PowerShell/Codex ; retirer son bloc
  masque son affichage et conserve ses sessions.
- **Projets** : le statut Git est vert quand le dépôt est propre, ambre avec des
  modifications, gris quand il est inconnu. Ouvrir un projet propose Explorateur,
  Codex CLI et DeepSeek CLI.
- **Ctrl+Espace** ouvre la palette : recherche d’applications et projets,
  flèches pour choisir, Entrée pour ouvrir, Échap pour revenir/fermer. Le préfixe
  **>** limite la recherche aux commandes. Un projet propose Explorateur, Codex CLI
  et DeepSeek CLI.
- **Réglages** est accessible dans la palette, le menu de notification et le clic
  droit d’un bloc : blocs visibles, applications, dossier des projets, météo,
  compteur, transparence du verre et animation du fond. L’apparence est prévisualisée
  immédiatement ; **Enregistrer** la conserve. Fermer restaure l’apparence enregistrée.
  La visibilité des blocs et l’éditeur d’applications s’appliquent séparément.
- Les boutons Conrad/Codex remplacent les informations à l’intérieur du dock par
  une transition sous verre : glissement, fondu, flou doux ou facettes, alternés
  dans un ordre mélangé, chacun une fois par cycle. Recliquez le bouton actif pour revenir au résumé.
  Le cadre et les boutons restent fixes ; quotas et modèles défilent à la molette.
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
