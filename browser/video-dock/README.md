# Battlestation · Vidéo pour Brave

Extension locale limitée aux pages `https://www.youtube.com/*`,
`https://www.twitch.tv/*`, `https://clips.twitch.tv/*` et à l'hôte natif
`com.battlestation.video`. Elle ne lit ni cookies, ni identifiants, ni historique.
Elle ne transmet pas les titres ou URL des vidéos. Les images vont uniquement
au dock local, en mémoire ; aucune piste audio n'est transmise ou enregistrée.

Installation unique :

1. Exécuter `scripts/Install-VideoBridge.ps1` après le build (déjà préparé par Codex).
2. Ouvrir `brave://extensions`, activer **Mode développeur**, puis **Charger
   l'extension non empaquetée** et sélectionner ce dossier.
3. L'extension rejoint les onglets YouTube et Twitch déjà ouverts. Si nécessaire, cliquer
   son icône pour reconnecter l'onglet courant, sans recharger la vidéo.
4. Dans le bloc Vidéo, choisir YouTube, Twitch ou Auto puis cliquer **Activer le miroir**.

Cliquer l'image du dock commande lecture/pause. **■** arrête uniquement le miroir ;
la lecture d'origine continue. Le PiP du même lecteur est quitté lors de
l'activation du miroir. Aucun autre onglet ou lecteur n'est fermé.

**↗** remet l'onglet source au premier plan ; les commandes restent ciblées sur
ce seul onglet.

La capture utilise `HTMLVideoElement.captureStream`, `MediaStreamTrackProcessor`
et des ImageBitmap transférés à une page interne de l'extension et son worker.
La version 0.4.0 respecte la CSP des pages autorisées, adapte la résolution au dock jusqu'à
1080p et borne la cadence à 30 Hz. Les restrictions des vidéos protégées restent
en place. Le type `kind` est strictement `youtube` ou `twitch` ; les messages
anciens sans ce champ restent compatibles avec YouTube. Un onglet Twitch peut
remplacer son élément vidéo pendant une navigation SPA : la capture armée est
reprise quand un élément prêt ou une piste vidéo est retrouvé. La découverte
seule ne démarre jamais une capture. La capture Twitch réelle n'a pas encore
été confirmée sur un lecteur utilisateur dans cet environnement. YouTube réel et les bascules Stremio/YouTube ont été confirmés par
l'utilisateur le 14 septembre 2026 ; voir `docs/VALIDATION.md` pour les mesures.

Identifiant public : `bbbkiomcecimmpndgliccmeagfhbednp`.

Pour mettre à jour une installation existante : dans `brave://extensions`, cliquer
**Recharger** sur **Battlestation · Vidéo** et vérifier la version **0.4.0**.
Autoriser les pages Twitch si Brave le demande, puis sélectionner **Twitch** dans
le dock mis à jour et cliquer **Activer le miroir**.
Pour retirer l'intégration : supprimer l'extension depuis Brave ; le pont se
termine quand Brave ferme sa connexion native. L'enregistrement local de l'hôte
est sous `HKCU/Software/BraveSoftware/Brave-Browser/NativeMessagingHosts/com.battlestation.video`.
