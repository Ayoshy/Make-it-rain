# Battlestation

Bureau Windows modulable en .NET 10 / WPF, avec fond animé Direct2D, panneaux
liquid glass et terminal natif ConPTY. Les widgets restent au niveau du bureau,
derrière les applications ordinaires.

Neuf blocs indépendants : horloge, météo, applications, lecteur, projets, terminal,
compteur Vice City, matériel et compteurs Codex. Le compteur Vice City peut être
retiré sans toucher au reste.

- Clic droit sur un bloc → **Réorganiser**, puis glisser les blocs sur la grille.
- **×** retire un bloc. **+ Ajouter** le replace dans un espace libre.
- L'icône Battlestation dans la zone de notification permet de réorganiser ou
  réafficher les blocs, même lorsque tous sont masqués.
- **Terminer** quitte l'édition. La disposition est sauvegardée automatiquement.
- Le terminal dispose de ses propres onglets PowerShell/Codex ; retirer son bloc
  masque son affichage et conserve ses sessions.
- **Ctrl+Espace** ouvre la palette : recherche d’applications et projets,
  flèches pour choisir, Entrée pour ouvrir, Échap pour revenir/fermer. Le préfixe
  **>** limite la recherche aux commandes. Un projet propose Explorateur ou Codex CLI.
- **Réglages** est accessible dans la palette, le menu de notification et le clic
  droit d’un bloc : blocs visibles, applications, dossier des projets, météo,
  compteur, transparence du verre et animation du fond. L’apparence est prévisualisée
  immédiatement ; **Enregistrer** la conserve. Fermer restaure l’apparence enregistrée.
  La visibilité des blocs et l’éditeur d’applications s’appliquent séparément.
- Les détails Conrad/Codex s’ouvrent au-dessus des widgets voisins, ancrés à leur
  résumé, sans déplacer les blocs ni réécrire leur disposition.
- Fermer explicitement son dernier onglet termine l'hôte terminal ; la prochaine
  ouverture utilise le build du bureau courant. Les anciennes sessions d'avant
  la migration glass peuvent rester dans une fenêtre séparée sur le premier écran.

```powershell
.\scripts\Build-Battlestation.ps1
.\scripts\Validate.ps1
.\scripts\Start-Battlestation.ps1
```

Les chemins du projet sont relatifs : le dossier parent peut porter le nom
Battlestation. Les paramètres personnels restent dans `%LOCALAPPDATA%\Battlestation`.
Le lancement Windows automatique n'est pas modifié par les scripts ci-dessus.
Depuis le 14 septembre, la tâche Windows `Battlestation` lance le build courant
à l'ouverture de session, sans délai et en priorité élevée. Le lanceur attend
la surface du bureau puis se termine ; il ne reste pas de PowerShell de lancement.
Pour réinstaller cette tâche, exécuter `scripts/Install-Startup.ps1` dans un
PowerShell administrateur du même utilisateur. Rainmeter a été désinstallé.

Lire [la validation courante](docs/VALIDATION.md) avant de reprendre le travail.
Voir aussi [l'architecture](docs/ARCHITECTURE.md) et [les crédits](docs/PROVENANCE.md).
