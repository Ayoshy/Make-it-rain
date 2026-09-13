# Validation native — ouverte

## Vérifié

- 113 fichiers récupérés depuis les états de travail réels, contrôlés par SHA-256.
  Sources et états Git des deux anciens dépôts préservés.
- Topologie réelle : principal (0,0) 2560 × 1440, secondaire (2560,0) 2560 × 1440.
- ABI native et .NET 10 effectivement chargés dans Rainmeter ; dépendance WebView2
  retirée de la version active.
- Deux skins natifs chargés. Le panneau est à droite et les volets utilisent un
  conteneur qui limite physiquement leurs dimensions. État compact mesuré :
  (3962,145)–(4741,1294). État six cœurs : (3965,57)–(4739,1382).
- Fond Direct2D de 5120 × 1440 dans le processus Rainmeter, thread séparé ; son temps
  d'animation avance. Capture native du rendu dans `native-direct2d-frame.png`.
- Données matérielles réelles, quotas réels et détails par modèle. Actualisation des
  quotas après l'arrêt des anciennes applications confirmée à 13:19:16.
- Essai autonome : aucun ConradSensor, CodexMeter, wallpaper64 ou webwallpaper64 ne
  restait actif. Le helper Conrad s'est fermé après la fin du processus parent ; il
  n'a pas été tué directement. Avant/après : Auto et cible 83 °C conservés.
- Les anciens ports HTTP ne répondaient plus avant cet arrêt. Le drapeau logiciel
  Canicule de l'ancien Conrad n'était donc plus lisible ; le matériel a été lu
  directement et n'était pas dans le profil Canicule (100 %, 65 °C).
- 13 tests de contrats passent : absences, bornes GPU, modèles non tarifés,
  absence d'extrapolation et calcul quotidien traversant minuit.

## Incident de réactivité et correction

L'utilisateur a signalé une forte latence et un possible crash sur Canicule.
La première boucle du fond natif par meters atteignait 36 % CPU ; des commandes
restaient en attente. Aucun événement Windows 1000/1026 correspondant à Rainmeter
n'a été trouvé dans la fenêtre examinée. Une instance bloquée a été arrêtée après
vérification qu'aucun helper GPU natif n'était actif.

Le fond a été remplacé par Direct2D sur un thread séparé. Un nouvel essai de la
commande Canicule pendant la coexistence a été refusé proprement, sans changer le
PID de Rainmeter. Cela vérifie la protection de coexistence, **pas encore une
activation matérielle suivie de sa restauration**.

## Mesures natives

Sur 15 secondes avec Direct2D actif : environ 0,85 % CPU normalisé sur six processeurs
logiques et 331,6 MiB de working sets cumulés pour Rainmeter, app-server et conhost.
Les mesures par processus et les instantanés GPU sont dans
`resources-native-direct2d.json`. Ce n'est pas une garantie de benchmark universel.
Les anciens résultats WebView2 de 692–1182 MiB concernent une architecture abandonnée.

## Dernière validation du 13 septembre

- L'utilisateur a confirmé les clics réels, les volets et Canicule ; l'état actif
  100 % / 65 °C a été visible dans ses captures. Le retour Auto / 83 °C a été observé.
- Le fond invisible a été corrigé par la composition de fenêtre native layered.
  Deux captures du véritable écran montrent les particules à des positions différentes.
- Le comportement avec Afficher le bureau, les fenêtres et les icônes a été confirmé
  par l'utilisateur sur les deux écrans.
- Les quatre volets passent le contrôle de coordonnées : logo et cellules du
  compteur fixes, panneaux contenus dans (3962,851)–(4741,1294).
- Les libellés et caractères provenant de Lua ont été corrigés, les modèles séparés
  en colonnes, les montants affichés en vert et les quotas en cartes par fenêtre.
- La relance complète a été effectuée avec capture et réapplication du profil manuel
  choisi par l'utilisateur (77 %, 65 °C). Le nouveau helper a été confirmé dans
  Windows, et les valeurs ont été relues sans erreur.
- Les anciennes entrées de démarrage ConradSensor, CodexMeter et WallpaperEngine
  ont été sauvegardées puis retirées. Le raccourci Rainmeter existant pointe vers
  l'installation vérifiée ; aucune tâche planifiée correspondante supplémentaire
  n'a été trouvée.

Avec l'interface verre révisée et son helper inclus, un échantillon de 15 secondes
a donné environ 1,87 % CPU et 409,6 MiB de working sets cumulés. Les autres applications
de l'utilisateur étaient actives : ce relevé ne constitue pas un benchmark isolé.

## Limites de validation

- Les variations de consommation selon chaque jeu ne sont pas caractérisées.
- Le script de retour arrière protège et sauvegarde un profil GPU actif, mais son
  scénario complet de retour aux anciennes applications n'a pas été exécuté après
  les dernières retouches visuelles.

Le contrôle automatique Windows demeure indisponible (`native pipe`, os error 2).
Les commandes Rainmeter et les captures de meters ne sont pas présentées comme des
clics réels. Les validations interactives ci-dessus reposent sur les essais et
captures de l'utilisateur, complétés par les lectures matérielles et diagnostics.

Les anciens projets restent disponibles pour référence et retour arrière. Aucune
phase 2 (applications ou raccourcis) n'est engagée.
