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

Affichage : champion et niveau, temps de partie, **K/D/A**, **CS**, **or**,
**Respawn** quand le joueur est mort, et bandeau d'objectifs par équipe
(dragons, barons, tourelles, inhibiteurs). Les seuls minuteurs sont **Dragon
+5:00** et **Baron +6:00** après le kill correspondant, dérivés des événements ;
les autres objectifs restent des compteurs. Aucune constante dépendante du patch,
aucune entrée clavier, aucune automatisation de jeu.

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
```

Les contrôles couvrent le bloc masqué par défaut, le modèle Jeu, le relevé LoL
synthétique (champion, niveau, K/D/A, CS, or, objectifs, minuteurs, refus d'une
réponse incomplète, prédicat du certificat, aucune requête sans partie).

**Reste non vérifié ici : la télémétrie pendant une vraie partie.** Aucune partie
n'était en cours pendant cette tranche ; le relevé réel se contrôle ainsi :

1. lancer une partie et laisser le bloc **LoL** affiché ;
2. vérifier le champion, le niveau, le temps, le K/D/A, le CS et l'or face à la
   fenêtre de jeu, puis mourir une fois pour lire **Respawn** ;
3. après le premier dragon puis le premier baron, vérifier **Dragon** et
   **Baron** à 5:00 et 6:00 du kill, et les compteurs par équipe ;
4. quitter la partie et vérifier le retour à « Aucune partie » (et
   « En attente de la partie » tant que le processus tourne sans partie).
