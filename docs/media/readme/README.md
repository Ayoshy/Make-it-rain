# Visuels de la page d'accueil

La galerie couvre les **19 docks présents**, le 24 septembre 2026. Les vignettes
ajoutent seulement une marge sombre pour aligner les formats ; un clic ouvre
l'image source à sa résolution complète.

## Captures du bureau

- `applications.png`, `clock-weather.png`, `audio-bluetooth.png` : scène Bureau,
  palette Aurore, build `battlestation-apps-column-01`.
- `gallery/clock.png`, `weather.png`, `audio.png`, `bluetooth.png`, `video.png`
  et `atelier.png` : même scène, build `battlestation-no-mountain-01`.
  La vidéo est capturée miroir arrêté ; Atelier montre la version conservée.

Ce sont des captures de régions de l'écran, sans retouche du contenu des docks.
Aucune sortie de terminal, note personnelle ou donnée de compte n'est publiée.

## Rendus de démonstration

Les autres images sont des rendus WPF des composants réels, avec des données
simulées. Elles illustrent les commandes et dispositions, sans valider les
clics sur le bureau ni la lecture de sondes réelles.

| Images | Source |
|:--|:--|
| Applications, Projets, Nudge, Lecteur, Réseau, Achats, DualSense, LoL | `tests/DockReviewTests.cs` et fixtures associées |
| Conrad Sensor, Codex Meter, Vice City | Option `--gallery-only` des mêmes fixtures |
| Bloc-notes | `tests/NotesTests.cs` |
| Terminal | `TerminalTabsPreview` : vrais onglets, zone de console vide, aucun shell lancé |

Les températures, quotas, prix et compteurs sont des exemples. La date affichée
par Vice City est celle du réglage de démonstration, sans assertion sur la sortie
du jeu. Le visuel Projets en grand utilise le même rendu que sa vignette.

Régénération depuis la racine du dépôt :

```powershell
dotnet run --project tests/Battlestation.DockReview.Tests.csproj -- $PWD
dotnet run --project tests/Battlestation.DockReview.Tests.csproj -- $PWD --projects-only
dotnet run --project tests/Battlestation.DockReview.Tests.csproj -- $PWD --apps-only
dotnet run --project tests/Battlestation.DockReview.Tests.csproj -- $PWD --glass-only
dotnet run --project tests/Battlestation.DockReview.Tests.csproj -- $PWD --gallery-only
dotnet run --project tests/Battlestation.Notes.Tests.csproj
```

Les rendus sources se trouvent dans `artifacts/validation/`. Le mode
`Battlestation.exe --preview-terminal-tabs <fichier.png>` produit le bandeau
terminal sans démarrer de session. Les captures du bureau se refont sur les docks
affichés, sans exposer les autres fenêtres.

Les icônes et arrière-plans gardent leurs [crédits existants](../../PROVENANCE.md).
