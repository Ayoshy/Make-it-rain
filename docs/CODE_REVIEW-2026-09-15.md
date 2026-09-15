# Revue ciblée du dock — 15 septembre 2026

## Défauts confirmés et corrections

- **Bluetooth : matériau absent.** Le constructeur appelait `base(station)`
  sans emplacement liquid glass et repeignait un rectangle WPF. Il utilise
  maintenant le composant commun `Surface`, emplacement 12. La suppression,
  la réapparition, le déplacement et le redimensionnement actualisent ce fond.
- **Bluetooth : faux état connecté.** Le bit `DN_STARTED` décrivait le pilote,
  pas la connexion radio. Les états Enabled et Connected sont désormais
  distincts ; Connected vient de `BluetoothFindFirstDevice` / `fConnected`.
  Une information indisponible reste inconnue.
- **Bluetooth : noms corrompus.** `SetupDiGetDeviceRegistryProperty` utilisait
  sa variante ANSI, puis le code interprétait le résultat en UTF-16.
  L'appel Unicode remplace cette erreur et les associations de noms à quatre
  adresses matérielles écrites en dur ont été retirées.
- **Bluetooth : scan bloquant et erreurs ignorées.** L'énumération de tous les
  périphériques avait lieu sur le thread WPF toutes les 2,5 secondes.
  Elle cible maintenant le bus Bluetooth et s'exécute en arrière-plan toutes
  les 5 secondes. Les échecs d'une action sont visibles.
- **Bluetooth : présentation.** Les cadres de boutons et les commandes
  rafraîchir/réglages ont été remplacés par quatre dessins cliquables.
- **Applications : silhouettes approximatives.** Six logos dessinés à la main
  sont remplacés par des silhouettes Simple Icons, avec les proportions et
  couleurs nacrées de la famille existante. Le générateur est reproductible.
  Une application sans variante reçoit son icône Windows, au lieu d'une lettre.
- **Nudge : commande sans effet et surcharge.** Le bouton « plus tard » ne
  faisait rien avec un seul rappel. Il disparaît ; le passage au rappel suivant
  existe uniquement avec plusieurs éléments. Le texte se modifie directement
  au clic. La police Art déco est utilisée à la demande de l'utilisateur.
  Le dialogue de saisie garde la police lisible des autres dialogues.
- **Tests supprimés.** Le commit `ee8501a` a retiré tout le dossier `tests/`.
  Les tests isolés du pont vidéo ont été rétablis depuis le parent de ce commit,
  sans leur mode de capture live. Des tests ciblés de rendu/interactions Nudge,
  des limites de clic et du cycle de vie du verre Bluetooth les complètent.

## Vidéo et Brave

Le processus du bureau utilisait `battlestation-nudge-bluetooth`, le pont natif
`battlestation-bluetooth`. Leurs DLL VideoBridge ont exactement le même SHA-256 :
`C3F8B4D207B46F16E4035F40E2A4D186582DFD3AF860216B518339565E20169D`.
Cette différence de dossier ne démontre donc pas une régression de protocole.

Sur une mesure live avant rechargement : 228 images reçues et dessinées en
10,02 secondes, 253 images décodées côté vidéo, page signalée `hidden`,
extension 0.3.2, connexion établie et aucune erreur. Cela prouve le passage
des images, pas leur qualité visuelle ni l'état réduit de la fenêtre Brave.
Les onze chemins d'applications enregistrés existent, dont le raccourci Brave.

Les tests du pont couvrent : limites JPEG, capture explicitement demandée,
identité de l'onglet, acquittement des images, lecture/pause ciblée, arrêt,
rejet des images tardives et déconnexion. Aucun navigateur utilisateur,
terminal ou backend n'est lancé par ces tests.

## Validation et limites

- Compilation native et .NET réussie ; compilation WPF sans avertissement.
- Tests : `dotnet run --project tests/Battlestation.BrowserVideo.Tests.csproj -c Release`.
- Tests de rendu : `dotnet run --project tests/Battlestation.DockReview.Tests.csproj -c Release -- . --bluetooth-read-only`.
- Aperçus inspectés : `artifacts/validation/dock-review/`. Ce sont des rendus
  de composants avec données de test, pas des captures de validation du bureau.
- Lecture matérielle sans mutation : Buds3 Pro et PARTYBTMS3 connectés ;
  DualSense et soundbar déconnectés au moment du contrôle.
- Le clic Bluetooth conserve la désactivation/réactivation du périphérique
  Windows. Ce n'est pas une API universelle de connexion/déconnexion radio.
  L'essai réel d'une bascule matérielle reste à effectuer.
- Le contrôle visuel Windows est indisponible : `Computer Use native pipe is
  unavailable`. Pas de validation revendiquée des vrais clics, de Win+D ou de
  la fluidité perçue. Stremio n'était pas ouvert pour un essai réel.

Build chargé : `build/battlestation-dock-review`. Le rechargement conserve les
hôtes terminal 6312 et 19552 et la session active
`283418b6-fd9a-4dac-a4d6-b393e2bacb57`. Le choix de démarrage
`build/current.txt` reste inchangé en attendant la validation visuelle.

Références : [état Bluetooth Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/bluetoothapis/ns-bluetoothapis-bluetooth_device_info_struct),
[Simple Icons](https://github.com/simple-icons/simple-icons).

## Contrôle après activation

Le bureau actif 19240 enregistre bien la surface liquid glass 12 aux coordonnées
Bluetooth : x=4192, y=936, largeur=488, hauteur=280, sur un fond 5120 × 1440.
Une seconde mesure live retrouve 228 images vidéo reçues et dessinées en
10 secondes, sans erreur. Canicule reste désactivé, sans erreur GPU remontée.

Les captures envoyées ensuite par l'utilisateur montrent les dessins Bluetooth
et les nouvelles icônes d'applications dans le bureau réel ; l'utilisateur
approuve leur rendu. Cela complète la validation visuelle de ces deux blocs,
sans valider automatiquement leurs clics ni Win+D.
