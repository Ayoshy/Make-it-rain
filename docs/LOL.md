# LoL

Le bloc arrive **masqué** : il ne prend la place d'aucun dock existant et une
scène déjà enregistrée le retrouve masqué. **LoL** est intégré au modèle **Jeu**
(sous la DualSense). Aucun autre modèle ne le liste ; **Rétablir le modèle** le
fait apparaître dans cette scène, pas dans les scènes personnelles.

## Source

Source unique : l'API locale du client de jeu,
`https://127.0.0.1:2999/liveclientdata/allgamedata`, sans authentification, sans
jeton et sans lecture du lockfile ou d'un fichier du jeu. Le certificat
auto-signé du client n'est accepté que si l'hôte est exactement `127.0.0.1` et
le port `2999` (`LolTelemetry.IsLiveClientEndpoint`, testé). Aucune donnée n'est
écrite, aucun identifiant n'est conservé, rien n'est envoyé au client.

Le worker est hors du fil d'interface : la présence de `League of Legends.exe`
est vérifiée toutes les 5 secondes, la requête part une fois par seconde avec un
timeout de 3 secondes, et seulement quand le bloc est exposé (`SetActive`).
**Sans processus de partie, l'API n'est jamais interrogée** : le bloc reste sur
« Aucune partie ». Un dernier relevé reste affiché au maximum 10 secondes, puis
la ligne d'état en style erreur apparaît.

| État | Signification |
| --- | --- |
| Aucune partie | aucun processus `League of Legends.exe` |
| En attente de la partie | processus présent, API injoignable |
| Télémétrie indisponible | échec HTTP ou analyse impossible après un relevé |
| Live | champion, niveau, temps, K/D/A, CS, or, respawn et objectifs |

Affichage : portrait officiel du champion et anneau de niveau,
nom, niveau, or, temps de partie, quatre compteurs dessinés
— éliminations, morts, aides et CS — puis bandeau d'objectifs par équipe
(dragons, barons, tourelles, inhibiteurs), où le côté du joueur porte sa couleur.
Chaque compteur d'objectif a son icône : l'élément du dernier dragon pris par
l'équipe pour les dragons, puis baron, tourelle et inhibiteur. Les seuls
minuteurs sont **Dragon +5:00** et **Baron +6:00** après le kill correspondant,
dérivés des événements ; les autres objectifs restent des compteurs. Aucune
entrée clavier, aucune automatisation de jeu. Les délais de 5 et 6 minutes restent
ceux du dock existant ; leur adéquation aux variantes de jeu n'est pas établie.

Les 173 portraits Data Dragon 16.19.1 sont inclus dans `assets/LoL/champions`
([provenance](../assets/LoL/SOURCE.md)). Décodage hors du fil d'interface puis
réutilisation des images gelées : aucun téléchargement pendant la partie.
Le nom interne `KIWI` sur la carte 12 est affiché comme **ARAM MAYHEM**.

## Indicateurs dérivés

- **Or/s estimé** : hausse du portefeuille sur les 60 dernières secondes
  observées, avec au moins 10 secondes de relevés. Une baisse d'or, un changement
  d'inventaire (achat, vente, consommation), une coupure de plus de 4 secondes ou
  une nouvelle partie réinitialisent la fenêtre. Ce n'est ni l'or total gagné ni
  le revenu passif ; le détail figure au survol. Les transactions entièrement
  réalisées entre deux relevés peuvent échapper à cette estimation.
- **Participation** : `(éliminations + aides) / éliminations de l'équipe` ;
  un dénominateur nul ou inconnu affiche un tiret.
- **KDA** : `(éliminations + aides) / max(1, morts)`.
- **CS/min** : CS divisés par la durée de partie en minutes.

À partir de 380 px de haut, la carte montre un grand portrait avec fond illustré,
les compteurs, trois indicateurs et une courbe lumineuse des gains d'or entre
relevés (échelle verticale automatique). Au-delà de 1050 px de large, les dix
portraits d'équipe et le score collectif occupent le bandeau inférieur ; les
champions morts sont atténués. Le format par défaut garde le portrait et des
indicateurs textuels. Le format minimal conserve les compteurs essentiels.

Le candidat `battlestation-lol-polish-01` centre le fond illustré sur le même axe
que le portrait et l'or. Les statistiques et libellés grandissent avec la place
disponible ; les portraits d'équipe atteignent 46 px sur le dock large.
La courbe utilise une interpolation cubique monotone passant par les relevés,
sans ajouter de pics, avec remplissage doré et halo.
Le fondu de 380 ms de `lol-polish-01` a été rejeté visuellement : il faisait
clignoter la courbe entre relevés. `battlestation-lol-flow-01` le remplace par
une seule courbe opaque défilant à 30 Hz, sur une fenêtre fixe de 60 secondes
avec les horodatages réels, comme le dock Réseau. Un retard d'affichage de 1,5 s
permet de faire entrer les points déjà mesurés par la droite sans inventer de
valeurs futures ; l'échelle verticale rejoint progressivement sa nouvelle cible.
Seul un visuel enfant redessine le graphique entre relevés, sans redessiner le
portrait et les textes à 30 Hz. Le masquage arrête les mises à jour.
Les tests couvrent l'opacité constante, l'absence de saut à l'arrivée d'un relevé,
le mouvement entre relevés, les bornes de la courbe et l'arrêt masqué.

`lol-flow-01` : compilation et suite ciblée réussies. Chargé sur la partie réelle
de Miss Fortune : 275 mises à jour du graphique sur un relevé d'environ 10 s,
horloge du graphique avancée de 10,39 s, opacité constante à 1. Ces compteurs
mesurent les mises à jour de dessin, pas les images effectivement présentées par
le compositeur. Disposition identique et hôte terminal 19224 conservé ; capture
dans `artifacts/validation/lol-flow/desktop.png`. Ressenti utilisateur encore ouvert.
Cette capture est partiellement masquée par une fenêtre de navigateur ; elle ne
valide pas visuellement le dock entier. Les rendus isolés restent distincts.
Coût isolé de préparation du visuel : 0,039 ms par mise à jour sur 300 appels,
hors rasterisation. Sur 10 s en partie : bureau 3,83 s CPU / 365 Mo,
helper métadonnées 0,02 s / 34 Mo, terminal 0,02 s / 145 Mo,
pont vidéo 0 s / 26 Mo. L'ancien candidat était à 3,44 s CPU / 390 Mo dans
une autre phase de partie : aucun gain CPU n'est établi.

Compilation et suite `--lol-only` réussies ; capture sur le bureau réel avec
Viktor dans `artifacts/validation/lol-polish/desktop.png`. Disposition identique,
hôte terminal 19224 conservé. Les survols, le déplacement/redimensionnement réel
et la fluidité ressentie ne sont pas validés par ces captures.
Sur 10 s en scène Jeu : bureau précédent 3,14 s CPU / 317 Mo, nouveau bureau
après chargement 4,20 s CPU / 376 Mo ; helper de métadonnées 0,48 s CPU / 47 Mo,
terminal et pont vidéo 0 s CPU sur ce relevé. Les phases de combat et le démarrage
diffèrent : aucun gain de performances n'est revendiqué. Le choix reste dans Atelier.

### Faisabilité des builds et améliorations

Suite de l'exploration d'accès direct du 25 septembre :
[sources locales, sonde et limites](LOL_AUGMENT_RESEARCH.md).

Étude du 24 septembre 2026, pas de recommandation intégrée au candidat :

- Les relevés réels `allgamedata` Mayhem ne contiennent pas de champ de sélection
  d'amélioration. Les builds peuvent utiliser le champion et l'inventaire déjà lus.
- Overwolf documente les choix Mayhem et Arena. Initialement écartée, cette
  dépendance est autorisée par l'utilisateur le 25 septembre, avec préférence
  pour une variante autonome. La vérification de l'accès développeur révèle
  toutefois un accès développeur requis, confirmé par une sonde locale ; voir
  [la vérification Overwolf](LOL_AUGMENT_RESEARCH.md#overwolf--autorisation-utilisateur-et-accès-développeur).
- Le schéma OpenAPI du jeu a également été lu pendant une nouvelle partie Mayhem :
  douze endpoints `liveclientdata`, aucun endpoint ni champ augment/choice.
  Overwolf expose ces choix par son GEP ; la documentation consultée ne décrit
  pas la méthode interne du lecteur LoL. Son SDK public « Events SDK for Game
  Developers » sert à publier des événements depuis le code d'un jeu, pas à lire
  les choix LoL depuis Battlestation. Un lecteur direct autonome reste une piste
  de recherche non démontrée ; l'absence dans l'API ne prouve pas son impossibilité.
- Une autre piste étudiée est l'OCR Windows local des seules cartes, déclenché
  pendant les phases de choix et après changement des cartes. Pas d'OCR continu
  pendant les combats. La détection d'ouverture, des rerolls et la reconnaissance
  des noms français restent à démontrer sur les cartes réelles.
- Preuve locale temporaire : `artifacts/validation/lol-polish/OcrProbe.cpp`.
  Sur la capture fournie de 1628 × 558, moteur `fr-FR` : initialisation et décodage
  63 ms, cinq lectures 35–56 ms, CPU 16–63 ms, mémoire de processus 23 Mo.
  Il s'agit de l'OCR sur image déjà disponible, sans coût de capture du jeu ni
  mesure d'impact sur les FPS. Aucun appel réseau dans ce programme.
- Les taux de choix et de victoire doivent rester distincts, spécifiques au
  champion et au mode, et accompagnés du patch, du nombre de parties et de la
  région de la source. L'accès et la fraîcheur d'une source automatisable restent
  à confirmer ; ne pas remplacer des statistiques Mayhem par celles d'Arena.

Références consultées :
[OCR Windows](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine),
[événements Mayhem Overwolf](https://dev.overwolf.com/ow-native/live-game-data-gep/supported-games/league-of-legends/#augments),
[événements Arena Overwolf](https://dev.overwolf.com/ow-native/live-game-data-gep/supported-games/league-of-legends-arena/#augments),
[GEP](https://dev.overwolf.com/ow-native/live-game-data-gep/live-game-data-gep-intro/),
[SDK destiné aux développeurs de jeux](https://dev.overwolf.com/ow-native/live-game-data-gep/events-sdk-for-game-developers/),
[rapport ARAMGG 26.16](https://aramgg.com/en/blog/aram-mayhem-26-16-stats-report)
(1,88 M de parties annoncées, dont 96,7 % Chine : preuve de disponibilité de
statistiques, pas un jeu de données actuel validé pour le dock).

## Effets

Les effets suivent les événements reçus, jamais autre chose : doubles ondes et
éclats radiaux autour du portrait
à chaque élimination, **SÉRIE** dès deux éliminations sans mourir, bandeau
**DOUBLE / TRIPLE / QUADRA / PENTA KILL**, **PREMIER SANG** et **ACE**, pulsation
du bandeau de l'équipe qui prend un objectif, minuteurs Dragon et Baron qui
clignotent sous 30 s, et **Respawn** en anneau pendant la mort.

**SÉRIE** et multi-kills se lisent dans les événements `ChampionKill` réellement
reçus : l'élimination du joueur et la mort qui remet la série à zéro. Deux
éliminations à 10 s d'intervalle s'enchaînent, la cinquième garde la fenêtre de
30 s du pentakill, et une série ne dépasse jamais le nombre d'éliminations du
score. Une partie dont les événements ne sont pas publiés n'affiche donc ni série
ni multi-kill. Les effets se déclenchent sur une lecture comparée à la précédente :
afficher le bloc sur une partie déjà commencée ne rejoue pas les coups manqués.

Le bloc se replie sous 620 × 200 px : quatre compteurs sur une
ligne, bandeau réduit aux icônes et minuteurs resserrés.
Les tailles par défaut restent 700 × 220 ; la scène Jeu personnelle peut être plus
grande.

Sur **ARAM** (Howling Abyss, carte 12, mode `ARAM`), il n'y a ni dragon ni baron :
le bandeau ne garde que les **tourelles** et les **inhibiteurs**, et aucun
minuteur d'objectif n'est dessiné. Les **reliques de soin** et les **plantes**
n'ont en revanche aucun événement dans l'API locale du client : leur ramassage
n'est pas observable, donc aucun minuteur ne peut en être dérivé sans l'inventer.
`inspect` publie `lol.mode`, `lol.map` et `lol.events` — la liste des noms
d'événements réellement reçus — précisément pour trancher ce point sur une vraie
partie ARAM plutôt que sur une supposition.

## Vérifications

```powershell
dotnet run --project tests/Battlestation.Scenes.Tests.csproj
dotnet run --project tests/Battlestation.DockReview.Tests.csproj -- .
dotnet run --project tests/Battlestation.DockReview.Tests.csproj -- . --lol-only
```

Les contrôles couvrent le bloc masqué par défaut, le modèle Jeu, le relevé LoL
synthétique (champion, niveau, K/D/A, CS, or, objectifs, minuteurs, refus d'une
réponse incomplète, prédicat du certificat, aucune requête sans partie), les
événements lus (équipe, premier sang, série, multi-kill, morts), les effets
dessinés (bandeau de série, anneau de respawn, pulsation de l'équipe qui prend un
objectif) et le repli compact à la taille minimale déclarée.

L'API réelle a été lue le 24 septembre 2026 sur la carte 12, mode `KIWI` :
champion, inventaire, scores, or et événements sont présents ; aucun champ de
revenu total ou `goldPerSecond` n'était exposé. Les tests ciblés couvrent aussi le
calcul du débit, les ruptures d'inventaire et de relevés, la participation et le
chargement du portrait. Rendus isolés : 440 × 180, 700 × 220, 774 × 516 et
1640 × 546. Ces preuves ne valident pas les effets ressentis en jouant.

Contrôle de la télémétrie sur le bureau :

Le candidat `build/battlestation-lol-analytics-02` a été chargé le 24 septembre :
capture du dock réel 1632 × 546 avec Karthus, les dix portraits, 12,1 or/s observés,
83 % de participation, KDA 4,00 et CS/min 3,4. La disposition avant/après est
identique ; l'hôte terminal 19224 est conservé. Compilation et suite `--lol-only`
réussies. Le survol et la fluidité ressentie en combat restent non vérifiés.
La sélection du prochain lancement reste inchangée, en attente du choix dans Atelier.

Charge de processus sur des fenêtres de 10 s, scène Jeu et partie en cours :
ancien bureau 3,09 s CPU / 440 Mo ; candidat après démarrage 3,66 s CPU / 371 Mo.
Helper de métadonnées : 0,02 s CPU / 48 Mo ; hôte terminal et pont vidéo :
0 s CPU sur ce relevé. Les combats diffèrent entre les fenêtres : cette mesure
ne permet pas de conclure à un gain, et la charge CPU du bureau est ici supérieure.
Les captures sont dans `artifacts/validation/lol-analytics/` ; les rendus synthétiques
dans `artifacts/validation/dock-review/lol-*.png`.

1. lancer une partie et laisser le bloc **LoL** affiché ;
2. vérifier le champion, le niveau, le temps, le K/D/A, le CS et l'or face à la
   fenêtre de jeu, puis mourir une fois pour lire **Respawn** ;
3. après le premier dragon puis le premier baron, vérifier **Dragon** et
   **Baron** à 5:00 et 6:00 du kill, et les compteurs par équipe ;
4. quitter la partie et vérifier le retour à « Aucune partie » (et
   « En attente de la partie » tant que le processus tourne sans partie).
