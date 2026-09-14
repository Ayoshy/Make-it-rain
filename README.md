# Battlestation

Bureau Windows modulable en .NET 10 / WPF, avec fond animé Direct2D, panneaux
liquid glass et terminal natif ConPTY. Les widgets restent au niveau du bureau,
derrière les applications ordinaires.

Onze blocs indépendants : horloge, météo, applications, lecteur, projets, terminal,
compteur Vice City, matériel, compteurs Codex, vidéo et audio. Le compteur Vice City peut être
retiré sans toucher au reste.

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
- **Terminer** quitte l'édition. La disposition est sauvegardée automatiquement.
- **Dispositions**, dans Réorganiser, propose six setups prêts à l’emploi :
  **Jeu**, **Création**, **Cinéma**, **Focus**, **Multimédia** et **Double écran**.
  Ils sont fixes et reviennent toujours à leur plan par défaut ; **Personnel** est
  la disposition libre, dont les ajustements sont conservés.
- **Vidéo** : choisir YouTube ou Stremio, puis cliquer **Activer le miroir**.
  Un clic sur l'image commande lecture/pause ; **■** arrête le miroir et **↗**
  revient au lecteur. Voir [l'intégration Brave et Stremio](docs/VIDEO.md).
- Clic droit sur un onglet terminal : couleur, nom et titre automatique. Les états
  Codex ont un indicateur animé ; voir [les onglets](docs/TERMINAL_TABS.md).
- Le terminal dispose de ses propres onglets PowerShell/Codex ; retirer son bloc
  masque son affichage et conserve ses sessions.
- **Projets** : le statut Git est vert quand le dépôt est propre, ambre avec des
  modifications, gris quand il est inconnu. Ouvrir un projet propose Explorateur
  et Codex CLI.
- **Ctrl+Espace** ouvre la palette : recherche d’applications et projets,
  flèches pour choisir, Entrée pour ouvrir, Échap pour revenir/fermer. Le préfixe
  **>** limite la recherche aux commandes. Un projet propose Explorateur ou Codex CLI.
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
npm ci
.\scripts\Build-Battlestation.ps1
.\scripts\Validate.ps1
.\scripts\Start-Battlestation.ps1
```

Les chemins du projet sont relatifs : le dossier parent peut porter le nom
Battlestation. Les paramètres personnels restent dans `%LOCALAPPDATA%\Battlestation`.
Le lancement Windows automatique n'est pas modifié par les scripts ci-dessus.
Depuis le 14 septembre, la tâche Windows `Battlestation` lance le build courant
à l'ouverture de session, sans délai. Le lanceur place désormais l'application
en priorité normale pour laisser le CPU aux autres applications. Il attend
la surface du bureau puis se termine ; il ne reste pas de PowerShell de lancement.
Pour réinstaller cette tâche, exécuter `scripts/Install-Startup.ps1` dans un
PowerShell administrateur du même utilisateur. Rainmeter a été désinstallé.

Lire [la validation courante](docs/VALIDATION.md) avant de reprendre le travail.
Voir aussi [l'architecture](docs/ARCHITECTURE.md) et [les crédits](docs/PROVENANCE.md).

Le build d'essai vidéo réunit aussi le [cockpit audio, fond musical, dispositions,
états projets et réserve de liens](docs/COCKPIT.md). Les validations encore à faire
sont distinguées dans `docs/VALIDATION.md`. Les builds avec `-NoActivate` ne
modifient pas la version ciblée par le lancement Windows.
