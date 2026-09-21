# Aquarium et LoL

Les deux blocs arrivent **masqués** : ils ne prennent la place d'aucun dock
existant et une scène déjà enregistrée les retrouve masqués. **Aquarium** est
intégré au modèle **Cinéma** (sous la vidéo, elle-même ramenée à 1888 × 760) et
**LoL** au modèle **Jeu** (sous la DualSense, ramenée à 1696 × 520). Aucun autre
modèle ne les liste ; **Rétablir le modèle** les fait apparaître dans ces deux
scènes, pas dans les scènes personnelles.

## Aquarium

Eau teintée par le thème, trois bandes de caustiques, deux massifs de plantes,
bulles et cinq à sept poissons. Chaque poisson a un corps, une queue et un œil ;
il dérive verticalement, s'enroule aux bords et suit légèrement le groupe. Le
bloc n'a ni réglage, ni interaction, ni texte.

L'animation est pilotée par `CompositionTarget.Rendering`, limitée à environ
trente images par seconde, et **uniquement quand le bloc est exposé et non
recouvert** — la même porte que les autres docks animés. Masqué, il ne calcule
rien et ne redessine rien. La réaction musicale ne crée pas de seconde capture
audio : le dock Lecteur publie son spectre existant dans `Station.MusicBands`,
qui règle l'amplitude des caustiques et la vivacité des poissons, sans
stroboscope.

### Art des poissons et des plantes

Les poissons et les massifs sont des **sprites générés** (imagegen, fond
transparent) posés sous `assets/Aquarium/` : `fish-a.png`, `fish-b.png`,
`fish-c.png`, `plant-a.png`, `plant-b.png`. Ils sont recadrés sur leur contenu,
conservent leurs proportions (le code les ajuste dans leur boîte, il ne les
étire pas) et tournent vers la droite ; le code les retourne pour la nage vers la
gauche, les fait légèrement pivoter selon leur montée et respirer d'un battement
de nageoire. Les plantes sont ancrées sur le bord bas du cadre et oscillent
autour de ce point. Les fichiers sont lus **une fois par processus** : après un
ajout ou un remplacement d'image, recharger le bureau.

Si un sprite manque, le même poisson et la même plante sont **dessinés** par le
code : le bloc ne dépend jamais des fichiers, et un asset absent ne casse rien.
Les sources 1024 × 1024 livrées par le générateur sont conservées comme preuve
dans `artifacts/validation/aquarium-art/source/` ; les versions du dépôt sont
recadrées et réduites à 512 px sur le plus grand côté.

### Eau (shader)

L'eau n'est pas dessinée : elle est calculée par un shader de pixels
(`src/Battlestation/Shaders/Water.fx`, compilé en `ps_3_0` par `fxc` au moment
du build et embarqué comme ressource WPF). Il produit des caustiques par bruit
fractal déformé, une réfraction de surface, des rais de lumière descendants et
le sillage du curseur, sur la couche d'eau placée **sous** les poissons et les
plantes. Réglages actuels : fréquences fines (cellules de quelques dizaines de
pixels, jamais des volutes), amplitude `0.10 + basses × 0.30`, atténuation avec
la profondeur.

- **Musique** : basses et aigus modulent l'amplitude et la vitesse des
  caustiques ; c'est le même spectre que les poissons (`Station.MusicBands`).
- **Curseur** : les cinq dernières positions forment un sillage ; chaque point
  pousse un anneau qui voyage et s'éteint dans le shader, et les poissons à
  moins de 170 px s'écartent. Rien n'est capturé ni écrit.
- **Repli** : si le shader ne se charge pas ou si la machine ne sait pas faire
  du shader model 3, le bloc redessine son ancienne eau (colonne, sol, ligne de
  surface, rubans) — un GPU ancien ne laisse jamais un panneau vide.
- **Portée** : le temps du shader est celui du dock, pas l'horloge murale : il
  s'arrête net quand le bloc est masqué, comme le reste de l'animation.

Le shader se recompile avec `scripts/Build-Battlestation.ps1` (étape `fxc`) ; le
bytecode `Water.ps` est versionné pour qu'un simple `dotnet build` fonctionne
sans le SDK Windows. `RenderTargetBitmap` (donc le test de rendu) ignore les
shaders de pixels : l'image de test ne montre que l'eau de base et les sprites,
la preuve du shader se fait sur le bureau.

Quand `assets/Aquarium/island.png` existe (rendu Blender de l'essai, vue de côté
en 1280 × 720), il devient l'image du bloc et le shader la réfracte sous la
ligne d'eau : l'eau ondule par défaut, suit le spectre du bloc Lecteur et le
sillage du curseur, et `inspect` publie `water: "island"`. Les poissons, bulles
et plantes sont ramenés à la bande d'eau du rendu. Sans ce fichier, l'eau
reprend le shader, ou son dessin si le GPU ne le peut pas. L'image est lue une
fois par processus : la remplacer demande de recharger le bureau. Voir
[l'essai île low-poly](AQUARIUM_ISLAND.md).

## LoL

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
.\tests\Check-Themes.ps1 -BaselineRef 9a0c43f -Layout <copie-de-layout.json>
```

Les contrôles couvrent les blocs masqués par défaut, les modèles Jeu et Cinéma,
l'aquarium à deux instants et à l'arrêt quand il est masqué, les quatre états de
fenêtre des applications, la bande horaire et la pluie imminente, la liste Nudge
paginée, la carte Projet avec écart et âge, et le relevé LoL synthétique
(champion, niveau, K/D/A, CS, or, objectifs, minuteurs, refus d'une réponse
incomplète, prédicat du certificat, aucune requête sans partie).

**Reste non vérifié ici : la télémétrie pendant une vraie partie.** Aucune partie
n'était en cours pendant cette tranche ; le relevé réel se contrôle ainsi :

1. lancer une partie et laisser le bloc **LoL** affiché ;
2. vérifier le champion, le niveau, le temps, le K/D/A, le CS et l'or face à la
   fenêtre de jeu, puis mourir une fois pour lire **Respawn** ;
3. après le premier dragon puis le premier baron, vérifier **Dragon** et
   **Baron** à 5:00 et 6:00 du kill, et les compteurs par équipe ;
4. quitter la partie et vérifier le retour à « Aucune partie » (et
   « En attente de la partie » tant que le processus tourne sans partie).

Le rendu de l'aquarium relève du verdict visuel d'Ayo : les preuves de cette
tranche sont des images (`artifacts/validation/aquarium-lol/`) et un relevé
`inspect`, pas une appréciation.
