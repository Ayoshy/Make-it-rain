# Cockpit — notes historiques d'intégration

Ce document conserve le périmètre et les preuves du chantier initial
`build/battlestation-cockpit`. La coordination avec le chantier Vidéo est terminée ;
l'ancienne restriction de rechargement est levée. Les états, cadences et validations
ci-dessous décrivent cette étape, pas nécessairement la version actuelle. Consulter
README.md et ARCHITECTURE.md pour le fonctionnement actuel, AGENTS.md pour les
règles de livraison, puis vérifier les sources et processus concernés.

## Audio et fond

- Bloc **Audio**, ajouté via Réorganiser → Ajouter ou la palette. Il reste masqué
  à la migration pour ne pas prendre la place du bloc Vidéo en cours d'essai.
- Curseurs glissables : volume général et applications, regroupées par nom de
  processus. Trois applications par page. Mute par application, sortie générale
  et microphone de communication Windows. Aucun changement audio au démarrage.
- Choix de la sortie Windows pour les trois rôles, vérification après écriture,
  tentative de restauration des rôles précédents si le changement échoue.
  Certaines applications peuvent conserver une sortie explicitement fixée.
- L'analyse suit la sortie multimédia Windows active ; elle porte sur le mix de
  sortie (musique, vidéo et jeux), pas uniquement Spotify. Basses, médiums et aigus
  alimentent les halos et les ondes Direct2D. Réglages → Apparence règle l'effet
  et son intensité, avec aperçu et restauration si la fenêtre est fermée.
- Le microphone n'est jamais capturé. Les échantillons de sortie restent dans
  une courte fenêtre mémoire. Aucune piste audio ni sortie terminal enregistrée.
- Le plein écran sur le principal laisse le second animé ; un plein écran
  couvrant le secondaire met le fond en pause. Géométrie inchangée : 5120 × 1440.

## Dispositions

**Personnel**, **Jeu**, **Création**, **Cinéma** : palette, menu de notification
et Réglages → Bureau. Personnel permet de revenir au bureau existant.
La première visite d'un mode masque les blocs sans rapport avec ce mode et
adapte l'intensité musicale, en conservant les coordonnées. Ensuite chaque mode
retrouve ses propres déplacements et visibilités, y compris les ajustements faits
après la première sélection. Les hôtes terminal ne sont ni fermés ni relancés.
Les dispositions sont dans `profiles.json`. Une disposition qui ne tient plus
est refusée sans déplacer partiellement les blocs.

## Projets

Les six cartes lisent la branche et le nombre d'entrées modifiées/non suivies via
`git status --porcelain=v2 --branch`, sans verrouillage facultatif ni fsmonitor.
Lecture asynchrone bornée à quatre secondes par projet, espacée de trente secondes
après chaque cycle. Aucun fetch, checkout, commit ou lecture de compte.
Une absence de dépôt ou un échec ne devient pas un état « propre ».

Le sous-texte est vert pour un dépôt propre, ambre pour des modifications et gris
si l’état Git est inconnu. Les cartes et leur fenêtre d’actions utilisent les mêmes
couleurs. Les actions du projet sont Explorateur, Codex CLI et Kilo CLI ; ce
dernier ouvre un nouvel onglet du terminal dans le projet sélectionné et y
démarre `kilo`.

## Liens médias

Les nouvelles copies de liens YouTube et Spotify sont conservées automatiquement,
même si la réserve est fermée. Aucun historique antérieur du presse-papiers lu.
Les copies provenant des terminaux et gestionnaires de mots de passe reconnus
sont ignorées. La taille et le domaine sont contrôlés avant de copier le texte
complet ; seul un lien média reconnu entre dans `media-reserve.json`.

- YouTube : watch, youtu.be, shorts, live, embed et playlists.
- Spotify : pistes, albums, playlists, épisodes, podcasts, artistes, liens avec
  préfixe de langue et liens courts spotify.link.
- Paramètres de partage retirés, variantes YouTube et Spotify normalisées,
  doublons réordonnés et capacité limitée aux 100 liens les plus récents.
  Un lien court Spotify et sa version longue peuvent rester deux entrées :
  aucune résolution de redirections arbitraires n'est faite.
- Les titres et miniatures viennent des endpoints oEmbed publics. Pas de compte,
  clé API, modèle IA ni crédit. En cas d'échec, le lien reste disponible.
  Les miniatures restent en mémoire, 32 images maximum décodées à 128 px.
- La réserve s'ouvre depuis **À garder** dans le lecteur, la palette ou le menu.
  Enregistrement transparent, ouverture d'un média uniquement sur clic.
  La surveillance est désactivable dans Réglages → Liens médias.

## État de livraison au moment du compte rendu

Le cockpit est intégré au build courant. Les réglages audio ne sont pas modifiés
au démarrage et le microphone n'est jamais capturé.

Audio : bandes séparées, silence, échantillons invalides, préférences et migration.
Le test WASAPI live ne modifie que sa propre session silencieuse, puis vérifie
que sortie, volume général et microphone utilisateur sont inchangés.
Le rendu natif est exercé dans sa propre fenêtre invisible avec le manifeste
de l'application, sans capture du bureau ni du terminal. Les PNG
`audio-widget-preview.png` et `audio-background-{silent,reactive}.png` sont
des aperçus isolés, pas des preuves de la version installée.

Experiences : limites de domaines, URLs trompeuses, normalisation, doublons,
capacité et persistance ; métadonnées avec transport factice ; profils indépendants
et rejet atomique des collisions ; dépôt Git temporaire réel, état propre,
modifications et HEAD détachée. Aucun accès au presse-papiers utilisateur.

À cette étape, restaient à vérifier après la fin du chantier Vidéo : chargement sur le bureau,
clics/drag des curseurs, bascule physique de sortie et du micro, réaction à la
musique en usage normal, changement de périphérique/veille, copies réelles de
liens et métadonnées réseau, transitions de dispositions avec survie des onglets.
Computer Use ne permettait alors pas les clics : native pipe, erreur 2.

Les requêtes oEmbed publiques ont répondu pour YouTube et Spotify depuis ce PC.
Un exemple Spotify indisponible a renvoyé HTTP 500 ; un autre a renvoyé une
miniature sur `image-cdn-ak.spotifycdn.com`, désormais pris en charge et testé.
Ce contrôle HTTP ne remplace pas la copie réelle suivie par le bureau chargé.
`cockpit-live-footprint.json` mesure les processus techniques et leurs descendants
pendant les essais concurrents ; ce n'est pas une mesure de performance du nouveau
build, qui n'a pas été lancé comme bureau.

Références : [NAudio 2.2.1](https://github.com/naudio/NAudio/tree/v2.2.1),
[ABI PolicyConfig consultée dans EarTrumpet](https://github.com/File-New-Project/EarTrumpet/blob/master/EarTrumpet/Interop/MMDeviceAPI/IPolicyConfig.cs),
[notifications de presse-papiers Windows](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-addclipboardformatlistener),
[oEmbed Spotify](https://developer.spotify.com/documentation/embeds/tutorials/using-the-oembed-api),
[format porcelain Git](https://git-scm.com/docs/git-status).
