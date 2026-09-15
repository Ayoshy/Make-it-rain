# Applications et audio — 15 septembre 2026

## Comportement

- Le dock regroupe automatiquement les applications par usage : Jeux, Web,
  Social, Médias et Outils. Les groupes suivent l'ordre de la configuration,
  avec l'ordre relatif des applications conservé dans chaque groupe.
- Les groupes apparaissent seulement si tous tiennent avec leurs icônes de
  taille normale. Sinon, le dock conserve la grille et son défilement.
- Le bouton d'édition `+` mesure 28 × 28 px, en haut à droite.
- Spotify utilise le raccourci installé et les deux matériaux d'icône existants.
- Audio : hauteur par défaut 264 px, minimum 220 px ; trois sources à 264 px,
  deux à 220 px et pagination pour les suivantes.
- La session du processus courant est exclue du mélangeur : le seul flux audio
  interne est l'analyse WASAPI loopback de `Spectrum`, utilisée pour les effets
  visuels. Ce flux lit le mix de sortie et ne joue pas de son.
- Les niveaux sont lus séparément des volumes et périphériques, avec une attente
  cible de 33 ms sur le worker COM. L'interface interpole à chaque image avec une
  attaque de 45 ms et une retombée de 180 ms. La vitesse ne dépend pas du taux de
  rafraîchissement de l'écran. Les lectures et animations s'arrêtent lorsque le
  dock est masqué ; le silence finit par arrêter les redessins du vumètre.
- Les gestionnaires de sessions des sorties inchangées sont réutilisés pendant
  la redécouverte périodique, au lieu d'être détruits/recréés systématiquement.

## Validation et version active

Build chargé : `build/battlestation-app-packs-audio-v2`.
Le pointeur `build/current.txt` et le démarrage Windows restent inchangés en
attendant la validation visuelle réelle. Les bibliothèques natives de v2 sont
celles compilées dans `build/battlestation-app-packs-audio` pendant cette tâche ;
seul le code managé audio a changé entre les deux builds.

`dotnet run --project tests/Battlestation.DockReview.Tests.csproj -c Release -- .`
valide les cibles aux formats large/compact, les douze apps dans les groupes,
l'absence de chevauchement des cibles, les commandes audio aux trois tailles,
la pagination, le lissage indépendant de la fréquence et la stabilisation au
silence. Les aperçus WPF sont dans `artifacts/validation/dock-review`.

Les inspections live sont dans `artifacts/validation/app-packs-audio` :
12 apps dont Spotify, audio à 620 × 264, aucune ligne Battlestation, même hôte
terminal 13472 conservé. Sortie, volume général et états mute/micro conservés.
Sur un échantillon de 12 secondes avec Stremio actif, environ 19,4 lectures de
niveau/s et 118,7 redessins audio/s ; 0,43 ms en moyenne par lecture des niveaux.
La cadence effective dépend des attentes Windows et des appels pilotes.
Ces compteurs ne prouvent pas la fluidité perçue.

Computer Use a échoué avec `native pipe unavailable`, erreur système 2 : clics
réels, défilement et fluidité visuelle restent à confirmer sur le bureau.
Les mesures de processus avant/après incluent bureau, hôte terminal, worker
métadonnées et passerelle vidéo ; leur contexte varie, sans revendication de gain.
