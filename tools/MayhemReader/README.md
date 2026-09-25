# Mayhem : sonde visuelle locale

Sonde indépendante de Battlestation et de la conversation. OCR Windows en
anglais (`en-US`), capture Windows Graphics Capture de la seule fenêtre
`League of Legends.exe`. Aucun accès mémoire au jeu, aucune entrée automatisée,
aucun appel réseau dans le collecteur. Le catalogue anglais vient du relevé LCU
déjà conservé dans `artifacts/validation/lol-direct/cherry-augments.json`.

## Lancer / arrêter

Depuis la racine du dépôt, après compilation et vérification du build :

```powershell
.\tools\MayhemReader\Start.ps1
.\tools\MayhemReader\Stop.ps1
```

`Start.ps1` lit le chemin exact inscrit après QA dans
`artifacts/validation/lol-reader/validated-build.txt`. Il n'utilise pas le build
le plus récent et ne modifie pas `build/current.txt`. Pour compiler un nouveau
build distinct : `tools/MayhemReader/Build.ps1`.

Une fenêtre affiche état, dernière image, heure du statut, CPU cumulé, mémoire,
stockage et bouton **Arrêter la collecte**. Elle peut être minimisée pendant la
partie ; fermer la fenêtre demande aussi l'arrêt. Les autres processus restent
en place. La collecte s'arrête à la fermeture du jeu après attachement, au bout
de 30 minutes depuis lancement, à 256 Mio ou sur demande explicite. Le statut
final reste consultable. L'arrêt par fichier ou par limite laisse la fenêtre
de statut ouverte ; la fermer termine alors le processus de sonde.
Le plafond porte sur les preuves de cette exécution,
pas sur l'ensemble des anciens essais. Aucune suppression automatique.

## Données et couverture

- Zone enregistrée : x 12 %, y 22 %, largeur 76 %, hauteur 40 % du client du jeu.
  Cadrage initial à vérifier sur les vraies cartes. Les pixels du jeu présents
  dans cette zone peuvent contenir du texte personnel ; les preuves restent locales.
- Une tentative toutes les 250 ms, OCR et encodage séquentiels. Pas de file de
  travail qui grossit. La cadence réelle dépend du coût et des frames disponibles.
- Images JPEG qualité 85 conservées même sans résultat OCR ; OCR effectué sur
  les pixels originaux. Les images uniformes sont signalées `blank-frame`.
- `observations.jsonl` contient fichier, UTC de réception, horodatage natif QPC
  en unités de 100 ns, âge de frame, intervalle, cadrage, texte OCR, candidats,
  durée OCR et heure de disponibilité. Les changements d'état sont journalisés.
- `status.json` est remplacé atomiquement chaque seconde : PID, heartbeat,
  tentatives, images effectivement enregistrées, erreurs, dernière image,
  intervalle maximum entre frames reçues et motif d'arrêt. Un statut ancien ou
  `waiting-game` ne prouve pas une capture active. Les frames de plus d'une
  seconde sont rejetées. La minimisation du jeu interrompt l'enregistrement.
- Les intervalles excluent le temps avant la première image et après la dernière ;
  utiliser aussi les horodatages début/fin et changements d'état pour la couverture.

## Reconnaissance et limites

Trois zones horizontales, noms normalisés, rapprochement conservateur, contrôle
d'alignement des titres et confirmation sur deux lectures. Les résultats partiels
restent dans les preuves ; ils ne deviennent pas une offre confirmée. Les noms
absents ou ambigus restent inconnus. Les descriptions peuvent encore produire
des faux positifs : aucune fiabilité sur LoL n'est affirmée avant preuve réelle.

Certains noms ont plusieurs identifiants dans le catalogue (FireFox : 308/1308).
La sonde conserve tous ces identifiants candidats, sans choisir arbitrairement.
Un changement de noms est une offre mise à jour, pas une preuve du clic reroll
en lui-même. La disparition des cartes n'identifie pas l'amélioration choisie.
Le champ `selection` reste `unknown`.

Les dossiers `fixture-*` contiennent des fenêtres de test générées, jamais des
preuves de partie. `--fixture --seconds 10` produit une fenêtre qui change une
carte après quatre secondes ; `--matcher-checks` vérifie six cas ciblés sans OCR.
Ces options ne sont utilisées que pour la QA du dispositif.

## Validation du dispositif — 25 septembre 2026

Build `mayhem-reader-20260925-130229-104` : compilation réussie, six contrôles
du rapprochement réussis. La fenêtre de test a fourni 68 images sans erreur,
avec deux offres confirmées : FireFox / All For You / Infinite Recursion puis
FireFox / Twin Fire / Infinite Recursion. Confirmation du nouveau trio 262 ms
après réception de la première image concernée. OCR et rapprochement moyens
6,8 ms, maximum 41,5 ms ; intervalle natif maximal 333 ms. Ces chiffres concernent
une fenêtre simple de 1280 × 720, pas une partie ni son impact FPS.

Arrêt automatique sous plafond de 1 Mio : 916 233 octets réellement présents,
motif `storage-limit`. Processus : 2,94 s CPU cumulées et 91 Mio de mémoire au
dernier statut, démarrage inclus. Essai sans jeu de cinq secondes : zéro tentative
de capture, zéro image, arrêt `duration-limit`. Commande `Stop.ps1` : arrêt de
la collecte confirmé `explicit-stop` ; fenêtre de statut ensuite fermée.
Les clics réels sur le bouton d'arrêt n'ont pas été automatisés.

Preuves locales : `artifacts/validation/lol-reader/checks-final`,
`fixture-storage-final`, `idle-final`, `run-20260925-130357-706`.
Le premier essai natif avait échoué car la fenêtre de test lancée masquée
n'était pas capturable. Le dispositif affiche désormais sa fenêtre sans prendre
le focus avant l'attachement. À cette étape, seules les images de test étaient
validées. L'essai réel suivant a confirmé sept trios et cinq changements de
carte, avec arrêt anticipé au plafond de stockage :
[résultat et limites de la première partie](../../docs/LOL_READER_VALIDATION.md).

Référence API : [Windows Graphics Capture](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.direct3d11captureframepool.createfreethreaded).
Le code du projet tiers Mayhem Overlay n'a pas été repris.
