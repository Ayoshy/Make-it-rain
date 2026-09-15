# Dock Vidéo — miroir à la demande

Le bloc Vidéo est redimensionnable. Il se déplace sur les deux écrans,
se retire et se réajoute comme les autres blocs. Les coordonnées et la visibilité
restent dans `layout.json`, le choix Auto/YouTube/Twitch/Stremio dans `video.json`.
Une marge exactement égale aux 12 pixels de la grille est maintenant acceptée.

## Utilisation

- Choisir Auto, YouTube, Twitch ou Stremio, puis cliquer **Activer le miroir**.
- Cliquer l'image pour lecture/pause du lecteur sélectionné.
- **■** arrête le miroir et laisse la lecture d'origine ouverte.
- **↗** revient au lecteur d'origine.
- Auto utilise le type réel de l'onglet navigateur actif prêt, puis conserve la
  source courante disponible. Un choix manuel verrouille le type de source.
- Une bascule YouTube/Stremio garde la fenêtre de support Stremio derrière le
  dock. Elle ne ramène pas le lecteur normal au premier plan.
- Retirer le bloc arrête la capture. La réorganisation suspend le miroir pendant
  le déplacement. Aucun terminal n'est fermé.

## YouTube / Brave

L'extension locale `browser/video-dock` capture uniquement l'élément vidéo de
YouTube avec `captureStream` et `MediaStreamTrackProcessor`. Aucune fenêtre PiP
n'est nécessaire ; le PiP du même élément est quitté au démarrage du miroir.
Les commandes lecture/pause ciblent cet élément, sans frappe clavier globale.

Permissions : pages `https://www.youtube.com/*`, `https://www.twitch.tv/*` et
`https://clips.twitch.tv/*`, injection sur ces mêmes pages
et native messaging. Aucun cookie, identifiant, historique, titre ou URL de vidéo
n'est envoyé au bureau. Aucune donnée ne part vers un service distant. Les images
JPEG passent en mémoire par le pont .NET et un pipe limité à l'utilisateur courant.
Aucune piste audio n'est transmise ou enregistrée ; le son reste dans Brave.

Version de l'extension : 0.4.0. La résolution suit le cadre du dock,
jusqu'à 1920 × 1080, sans agrandir la source à la capture. Encodage JPEG dans un
worker chargé depuis une page interne de l'extension, cadence plafonnée à
30 images/s et deux images au maximum en attente
d'acquittement. L'affichage WPF est notifié dès l'arrivée d'une image. Les
compteurs reçus/dessinés ne prouvent pas à eux seuls une vidéo en mouvement.
La redécouverte couvre la reconnexion, les onglets déjà ouverts et l'arrivée des
données après les métadonnées. Le diagnostic n'expose que des étapes et noms
d'erreurs bornés ; aucun texte de page ou message d'exception complet n'est transmis.
Une capture synthétique ne remplace pas un essai utilisateur de YouTube réel.

Le content script transfère des ImageBitmap redimensionnés par un MessageChannel
privé vers la page interne et son worker. Il ne crée plus de worker `blob:` dans
la page YouTube : ses règles CSP restent intactes. Les VideoFrame ne franchissent
pas la frontière entre origines, et chaque image est fermée après utilisation.

L'extension chargée vient de `browser/video-dock`.
Son identifiant public est `bbbkiomcecimmpndgliccmeagfhbednp`.

`scripts/Install-VideoBridge.ps1 -Build <dossier-build>` enregistre l'hôte local.
Le Brave installé consulte la clé compatible Chrome sous
`HKCU/Software/Google/Chrome/NativeMessagingHosts/com.battlestation.video` ; la clé
Brave équivalente est aussi enregistrée. Le manifeste est dans les données
personnelles Battlestation, jamais dans Git. Aucun démarrage Windows n'est ajouté.

## Twitch / Brave

L'extension locale reconnaît explicitement les onglets `https://www.twitch.tv/*`
et `https://clips.twitch.tv/*`. Elle capture l'élément `video` du lecteur avec
`captureStream`, comme pour YouTube ; elle ne capture jamais la fenêtre Brave,
le PiP ou un autre onglet. Le son reste dans Brave : les pistes audio du flux
local sont arrêtées avant le traitement des images.

Twitch remplace parfois son élément vidéo pendant une navigation SPA. Tant que
le miroir a été activé explicitement, le content script reprend la capture après
le remplacement de l'élément, la fin de la piste ou le retour à un état prêt.
La simple découverte d'un onglet prêt n'active jamais la capture. Les commandes
lecture/pause sont envoyées à l'élément ciblé ; retour remet cet onglet au premier
plan. Une navigation hors des deux domaines Twitch purge immédiatement sa
disponibilité du bridge.

Le bridge accepte les types `youtube` et `twitch` strictement. Les messages d'une
ancienne extension sans champ `kind` restent interprétés comme YouTube ; un type
inconnu est rejeté. La capture Twitch réelle n'a pas encore été validée sur un
lecteur utilisateur dans cet environnement.

## Stremio Windows

Windows Graphics Capture fournit les images du client natif. Pour continuer
après réduction, la fenêtre du lecteur reste dessinable derrière le bloc ; elle
peut temporairement prendre la taille de la miniature. Sa taille, sa position,
son état réduit et son état topmost sont restitués à la libération, sauf retour
explicite de l'utilisateur au lecteur. Aucun parentage ni fermeture de Stremio.

Un worker possède les ressources WGC/Direct3D. Un shader réduit l'image sur GPU
à la taille utile, au maximum 1920 × 1080, avant le transfert CPU. Deux tampons
réutilisés transmettent la dernière image à WPF. L'interface ne patiente plus
sur le transfert GPU et la réduction ne se fait plus pixel par pixel sur CPU.

Le fond vidéo est opaque et la fenêtre de support est en retrait, ombre comprise.
L'image remplit le cadre sans déformation, avec un léger rognage possible suivant
son rapport d'aspect. Les contrôles utilisent l'action Invoke du groupe Play/Pause
exposé par Stremio WebView2, pas une touche envoyée à l'application au premier plan.
Une absence de commande reste indisponible ; elle n'est pas remplacée par une
commande multimédia globale.

## État actif

Le build courant est indiqué par `build/current.txt`. Le bridge vidéo est enregistré
pour le build actif par `scripts/Install-VideoBridge.ps1` ; aucun démarrage Windows
supplémentaire n'est ajouté par cette association.

Références : [capture Windows](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture),
[capture depuis un élément média](https://www.w3.org/TR/mediacapture-fromelement/),
[native messaging](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging),
[recherche du manifeste Windows](https://chromium.googlesource.com/chromium/src/+/main/chrome/browser/extensions/api/messaging/launch_context_win.cc).
