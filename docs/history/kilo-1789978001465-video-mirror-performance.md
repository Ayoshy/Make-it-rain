# Performance — miroir vidéo (YouTube/Twitch en priorité)

## But

Améliorer la fluidité perçue du dock Vidéo, en priorité le chemin navigateur
(YouTube/Twitch), avec un dock utilisé en taille moyenne à large (jusqu'à la
limite 1920×1080 de la capture). Le chemin Stremio/WGC est traité en second,
sans refonte GPU tant que les gains CPU/GC ne sont pas mesurés.

## État des lieux (constats vérifiés dans le code)

Chemin navigateur, par image (~30 images/s) :
1. `video-worker.js` encode JPEG qualité .9 puis `btoa` ; l'enveloppe JSON/base64
   est imposée par le native messaging (extension ↔ `Battlestation.VideoBridge.exe`).
2. `VideoBridge.exe` (`src/Battlestation.VideoBridge/Program.cs`) est un relais
   transparent : il n'interprète pas les messages.
3. `VideoBrowserBridge.Receive` (`src/Battlestation/VideoBrowserBridge.cs:89-139`)
   alloue par image : chaîne base64 (~2,8 Mo), `Convert.FromBase64String` (~2 Mo),
   `JsonDocument.Parse`, puis un **nouveau `BitmapImage` décodé et gelé** (~8,3 Mo
   BGRA + internes du décodeur). Soit ~350 Mo/s d'allocations transitoires dans
   le processus du bureau → pauses GC sur le fil d'interface, et un nouveau
   `BitmapImage` par image = nouvelle texture GPU à chaque rendu (churn).
4. `VideoSurface.BrowserFrame` fait un `BeginInvoke(Render)` par image, coalescé ;
   `Paint()` (`VideoSurface.cs:143-176`) redessine tout le dock puis
   `D.DrawImage(picture, bounds)` ré-importe l'image dans le rendu.

Chemin Stremio :
5. `VideoCapture.Read` (`VideoCapture.cs:65-73`) copie l'image complète
   (`WritePixels`, jusqu'à 8,3 Mo) **sur le fil d'interface**, à chaque image.
   Le worker natif (`VideoCapture.cpp`) fait déjà la réduction GPU et le
   transfert staging, puis `memcpy` dans le tampon `pending` côté worker.
6. Le même `Paint()` redessine le dock entier à chaque image.

Autres boucles du bureau vérifiées, jugées saines (pas de changement) :
`DesktopWorkspace.Tick` (250 ms), le timer audio 33 ms (gated par `listen`),
`DesktopPlacement` (100 ms), `DesktopVisibility.Update` (1 s). Le pacing est déjà
borné à 30 images/s côté sources et coalescé côté UI.

## Décisions

- **Réutiliser un `WriteableBitmap`** comme cible de décodage côté navigateur
  (au lieu d'un `BitmapImage` par image) : supprime le churn de texture GPU et
  l'essentiel des allocations.
- **Protocole binaire d'images** sur le pipe nommé entre `VideoBridge.exe` et le
  bureau : l'extension reste inchangée (base64 JSON jusqu'au pont, imposé par le
  native messaging) ; le pont décode le base64 et émet des enregistrements
  binaires (header fixe + JPEG brut). Supprime `JsonDocument.Parse` et
  `FromBase64String` par image dans le processus du bureau.
- **Compatibilité** : le pipe reste `Battlestation.Video.v1` ; le bureau accepte
  les deux encodages (octet `'{'` = JSON, octet `0x01` = trame binaire) pour
  tolérer un vieux pont encore enregistré. Les messages de contrôle restent JSON.
- **Stremio** : écrire les pixels dans le `WriteableBitmap` depuis le worker de
  capture (`WritePixels` est documenté sûr hors UI) ; le fil d'interface ne fait
  plus qu'invalider.
- **GC** : `GCSettings.LatencyMode=SustainedLowLatency` uniquement pendant qu'un
  miroir est armé, `LowLatency` sinon, restauré au repos (le bureau reste en
  mode normal le reste du temps).
- **Pas de refonte GPU (D3DImage)** maintenant : réservée à une étape 2, décidée
  seulement si les mesures après l'étape 1 restent insuffisantes à grande taille.

## Tâches ordonnées

### 1. Trame binaire côté pont
`src/Battlestation.VideoBridge/Program.cs` :
- Interpréter les messages entrants : si JSON avec `type:"frame"` et `jpeg`
  valide, décoder le base64 et émettre une trame binaire sur le pipe ; tout le
  reste est transmis tel quel (JSON).
- Trame : `0x01`, `tabId` int32, `captureId` Guid (16 octets), `kind` octet
  (1=YouTube, 2=Twitch), `sequence` int64, `width`/`height` uint16, `playing`
  octet, `flags` octet, longueur JPEG int32, octets JPEG. Bords : refuser
  `length > 4 MiB` et `jpeg.Length > 2,8 M` comme aujourd'hui.
- Conserver la limite `DropOldest(4)` et la non-journalisation.

### 2. Côté bureau : décodage sans allocation par image
`src/Battlestation/VideoBrowserBridge.cs` :
- Dans `Receive`, accepter les deux encodages (voir Décisions).
- Pour une trame binaire : valider `ValidJpeg`, puis décoder via
  `JpegBitmapDecoder` → `FormatConvertedBitmap(Bgra32)` → `CopyPixels` dans un
  `WriteableBitmap` **réutilisé** (recréé seulement si la taille change), sur le
  même thread de lecture du pipe (hors UI). `state.Image` garde la même
  instance ; la logique d'époque (`captureId`, `tabId`, `kind`) et l'acquittement
  ne changent pas.
- `BrowserVideoState` inchangé ; `Paint()` et `Picture` restent compatibles
  (`WriteableBitmap` est un `BitmapSource`).
- Le chemin JSON reste en repli (vieux pont) avec le décodage actuel.

### 3. Stremio : copie hors du fil d'interface
`src/Battlestation/VideoCapture.cs` :
- Le worker écrit les `pending` directement dans le `WriteableBitmap`
  (création à la taille demandée, hors UI) et incrémente le serial ;
  `Read()` côté UI ne fait plus de `WritePixels`, il rend juste la dernière
  image publiée. Conserver la double tamponisation et les verrous existants.
- `VideoSurface.Update`/`BrowserFrame` inchangés (ils appellent déjà `Read()`).

### 4. Latence GC bornée au miroir
- Dans `VideoSurface.StartMirror` : activer `SustainedLowLatency` ; dans
  `StopMirror`/`Release` complet/`Dispose` : restaurer. Pas d'impact quand aucun
  miroir n'est actif.

### 5. Tests
`tests/Battlestation.BrowserVideo.Tests.csproj` (console `BrowserVideoTests`) :
- Étendre le faux client pipe pour émettre une trame binaire valide et une
  invalide (taille, en-tête JPEG), et vérifier `State.Frames`/`State.Image`,
  l'acquittement et le rejet des mauvaises époques — mêmes assertions que le
  chemin JSON existant, plus : image réutilisée (même instance pour deux trames
  de même taille) et repli JSON.
- Couvrir le formateur de trame du pont si extrait en logique pure testable.

## Risques

- **Écriture concurrente du `WriteableBitmap`** (worker vs fil de rendu WPF) :
  WPF verrouille en interne ; contention brève. Si des artefacts apparaissent,
  revenir à la publication de tampons + `WritePixels` côté UI (état actuel).
- **Vieux pont enregistré** : couvert par le double encodage ; le pont est de
  toute façon réinstallé avec le build par `scripts/Install-VideoBridge.ps1`.
- **Concurrence avec le travail non commité** : les fichiers vidéo
  (`VideoCapture.cs`, `VideoBrowserBridge.cs`, `VideoSurface.cs`,
  `VideoBridge/Program.cs`, `browser/video-dock/*`) ne sont pas dans le diff
  en cours (travail écrans : `DesktopScreens.cs` etc.) ; ne pas toucher aux
  fichiers écrans, et re-vérifier `git status` avant intégration.

## Validation

1. Compiler un candidat (`scripts/Build-Battlestation.ps1`), réinstaller le
   pont (`Install-VideoBridge.ps1 -Build <dossier>`), charger le candidat
   (`Start-Battlestation.ps1 -Build <dossier>`) sans fermer de session.
2. Mesures via `scripts/Invoke-Battlestation.ps1` : `video-inspect` (frames,
   `frameAgeMs`, `encodeMs`, tailles) et `inspect` → `rendering.counts` ;
   comparer avant/après sur le même lecteur. Vérifier que le nombre de rendus
   vidéo suit les images reçues (pas de rendus surnuméraires) et que
   `frameAgeMs` reste bas.
3. Essai réel utilisateur (seul valable pour la fluidité) : vidéo YouTube en
   cours, dock redimensionné en large ; puis clic lecture/pause, bascule de
   source, changement d'onglet, reconnexion du pont (kill du processus pont).
4. Régression Stremio : activer le miroir, vérifier image, commandes et arrêt.
5. Après acceptation seulement : aligner `build/current.txt`, vérifier les
   cibles du raccourci et du démarrage Windows, archiver le build remplacé.

## Hors périmètre (suivi)

- D3DImage/zéro-copie GPU pour Stremio : étape 2, seulement si les mesures
  restent insuffisantes à grande taille.
- Réglages encodeur extension (qualité JPEG .9, `resizeQuality`) : optionnels,
  sans effet sur le processus du bureau après la tâche 2.
- Optimisations des boucles globales (`DesktopVisibility`, `DesktopPlacement`) :
  jugées saines, non modifiées.
