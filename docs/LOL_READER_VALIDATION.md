# Mayhem : première preuve visuelle en partie

25 septembre 2026. Build de sonde : `mayhem-reader-20260925-130229-104`.
Exécution : `artifacts/validation/lol-reader/run-20260925-132934-543`.

**Le critère minimal de lecture est atteint sur deux séquences réelles : les
trois noms correspondent aux cartes, puis changent lors de cinq modifications
d'une seule carte.** Les sept trios distincts ont été comparés visuellement aux
images enregistrées, sans dépendre de signalements dans la conversation.
Cela ne valide pas encore la fiabilité de toutes les séquences d'une partie,
l'identification du choix final ou une intégration au dock.

## Offres vérifiées

Heures locales Europe/Paris ; heure de publication du résultat confirmé.
Les numéros renvoient aux fichiers `frame-NNNNNN.jpg` de l'exécution.

| Heure | Image | Gauche | Centre | Droite |
| --- | --- | --- | --- | --- |
| 13:33:35 | 39 | Glass Cannon | Quest: Wooglet's Witchcap | High Roller |
| 13:33:50 | 96 | Glass Cannon | Quest: Wooglet's Witchcap | Mystic Punch |
| 13:33:52 | 103 | Eureka | Quest: Wooglet's Witchcap | Mystic Punch |
| 13:34:11 | 177 | Eureka | Archmage | Mystic Punch |
| 13:38:14 | 1103 | Ultimate Awakening | Transmute: Chaos | Infinite Recursion |
| 13:38:17 | 1115 | Ultimate Awakening | Transmute: Chaos | Ult Bot |
| 13:38:27 | 1154 | Back To Basics | Transmute: Chaos | Ult Bot |

Le journal contient huit émissions `offer`, dont une répétition du troisième
trio à l'image 167 après une interruption de reconnaissance. Il ne s'agit pas
d'un sixième reroll. Les cinq changements constatés concernent successivement
droite, gauche, centre, droite, gauche ; chaque fois les deux autres noms sont
préservés. Les boutons et clics de reroll ne sont pas enregistrés dans le cadrage.

Les noms sont vérifiés. Les identifiants restent parfois ambigus entre variantes
du catalogue : le collecteur conserve les candidats au lieu d'en inventer un.
Le choix final reste `unknown` ; la fermeture des cartes n'en constitue pas une
preuve. Aucun relevé d'historique de fin de partie n'a été ajouté à cet essai.

## Couverture et performance observées

- 1 485 images, de **13:33:23,683 à 13:39:54,192**, soit **6 min 30,5 s** entre
  première et dernière image. Les images comprennent le début du jeu ; cette
  durée n'est pas le temps de partie de l'API.
- Arrêt à **13:39:54**, motif **`storage-limit`** : environ 256 Mio atteints.
  La fin de partie et les choix ultérieurs ne sont pas couverts. Le plafond a
  fonctionné, mais sauvegarder toute la bande centrale à cette cadence consomme
  trop de stockage pour suivre une partie complète.
- Intervalle moyen natif entre images : environ **263 ms**. Quatre intervalles
  dépassent 500 ms au démarrage (images 2 à 5), maximum **1,236 s**. Les périodes
  avant la première image et après l'arrêt ne sont pas incluses dans ce maximum.
- Une erreur de capture `E_INVALIDARG` au démarrage, puis des images reçues.
  Aucun plantage LoL n'est établi par cette collecte ; le plafond explique l'arrêt.
- OCR et rapprochement : **74,5 ms** en moyenne, **270,9 ms** maximum sur les
  images de jeu. Ces chiffres ne se comparent pas directement aux textes simples
  de la fenêtre de test.
- Sur les sept trios vérifiés, confirmation **279 à 337 ms après réception de
  la première image correctement reconnue**, qui avait déjà **225 à 261 ms**
  d'âge à réception. Ce n'est pas une mesure depuis le clic ou la toute première
  apparition lisible : les images antérieures n'ont pas toutes été annotées.
- Dernier statut : **114,94 s CPU cumulées**, **150 Mio** de mémoire de travail,
  démarrage et attente inclus. Pas de comparaison avec/sans sonde, pas de mesure
  FPS : aucun gain ni innocuité sur les performances ne sont revendiqués.

Les sept images d'offre ont été inspectées, pas chacune des 1 485 images.
Aucun taux global de faux positifs ou faux négatifs n'est donc établi.
Les fichiers `status.json`, `observations.jsonl`, `analysis.json` et les JPEG
restent conservés localement. La sonde est arrêtée ; rien n'a été supprimé.

## Suite bornée

La recherche initiale de source visuelle est terminée pour ce critère minimal.
Le corpus permet maintenant de travailler hors partie. Avant un nouvel essai
long ou l'intégration au dock : réduire le stockage hors sélection et conserver
une courte mémoire tampon d'images pour retrouver les faux négatifs ; vérifier
la non-répétition des offres après une lecture incertaine. Ne pas demander une
nouvelle partie pour simplement reproduire les sept trios déjà disponibles.

Cette étape n'a modifié que la documentation et les preuves d'analyse. Au relevé
final, le bureau chargé était `battlestation-headers-01` (PID 17000), le prochain
lancement `battlestation-disks-05`, l'hôte terminal `terminal-bg-01` (PID 19224).
Ces états proviennent d'autres travaux ; aucune promotion ni aucun rechargement
du bureau effectué par cette investigation.
