# Montagne

Bloc décoratif indépendant : un **pic enneigé sous une neige perpétuelle**, posé
sur le bureau. La scène n'est ni une image, ni une vidéo, ni un ciel plaqué :
chaque pixel est calculé sur le GPU (`src/Battlestation/Shaders/Montagne.fx`,
compilé en `ps_3_0` par `scripts/Build-Battlestation.ps1`, embarqué en ressource
WPF). Le bloc s'ajoute, se retire, se déplace, se redimensionne et se mémorise
comme les autres.

| Élément | Valeur |
| --- | --- |
| Identifiant | `montagne` |
| Titre | Montagne |
| Verre natif | slot 15 |
| Taille par défaut | 960 × 600, minimum 560 × 340 |
| Arrivée | **masquée**, comme LoL ; aucun modèle ne la liste |

## Ce que la scène est réellement

Le shader trace une scène analytique par pixel. Le ciel est un dégradé froid avec
une lune basse et un champ d'étoiles clairsemées qui ne montre que ce qui dépasse
de la crête ; derrière le pic principal, une chaîne lointaine se dissout dans la
brume. Le massif porte quatre sommets asymétriques : chaque face a sa largeur et
sa courbure, la pente est la **dérivée analytique** du profil — jamais une
différence finie, qui éclairerait une colonne blanche sur l'arête. La neige suit
la pente et l'altitude, creusée par des couloirs verticaux et dérivée par le
bruit ; les roches n'apparaissent que sur les faces les plus raides. Un liseré
clair souligne la silhouette, comme une arête qui accroche la lune.

## La neige perpétuelle

Les flocons sont discrets et procéduraux, sur **trois plans de profondeur** (une
grille par plan, un flocon par cellule, dérive du vent, oscillation lente). Ils
tombent en continu tant que le bloc est exposé. **Le passage du curseur les
écarte** : chaque flocon proche est repoussé du pointeur dans son propre plan, et
la vitesse du geste laisse un « stir » qui se dissipe en un peu plus d'une
seconde. Masquée, occultée, en cours de réorganisation ou hors écran, le bloc ne
calcule rien : la boucle de rendu n'existe que pendant qu'il est exposé, et le
pas de simulation est borné (`MontagneSurface.Advance`).

| Interaction | Effet |
| --- | --- |
| Curseur qui passe | les flocons proches s'écartent, proportionnellement au plan de profondeur |
| Curseur immobile | plus rien : le stir retombe et les flocons reprennent leur chute |
| Sortie du bloc | plus d'injection ; seuls les flocons déjà écartés finissent leur écart |
| Basses | le vent se lève (dérive et chute accélérées), un beat envoie une bourrasque bornée |
| Médiums | oscillation latérale renforcée |
| Aigus | éclats sur la neige et scintillement des flocons |
| Arrêt du son | les enveloppes retombent ; la neige reprend sa chute de référence |

## Son

Le bloc ne capture rien lui-même : il lit `Station.MusicBands`, le spectre déjà
publié par le dock Lecteur, jamais le microphone. Aucune seconde capture n'est
ouverte, aucun volume, mute ou périphérique n'est touché, rien n'est écrit sur
disque. Les enveloppes ont une attaque rapide et une retombée lente, un seuil de
silence calculé sur les bandes brutes (l'intensité de scène module la réaction
sans jamais l'annuler), et les impacts sont détectés en comparant l'enveloppe des
basses à sa propre moyenne lente : une musique normale lève le vent, une
notification isolée ne devient pas une tempête. Si le dock Lecteur est masqué et
que la réactivité musicale est désactivée, la montagne voit le silence.

## Géométrie

La tranche est fixe : l'étendue horizontale suit le ratio du bloc
(`WidthPerAspect`), l'étendue verticale est constante, donc un redimensionnement
élargit la vue au lieu d'étirer le massif. Les nombres vivent une seule fois dans
`MontagneCamera.cs` et sont recopiés tels quels dans le shader ; le repli dessiné
(machine sans pixel shader 3) rejoue la même crête.

## Vérifications

Ce qui a été **exécuté** :

```powershell
dotnet run --project tests/Battlestation.Scenes.Tests.csproj
dotnet run --project tests/Battlestation.DockReview.Tests.csproj -- .
```

Le shader est rendu sur le GPU réel par la sonde
`artifacts/validation/montagne/probe/probe.cpp`, qui charge exactement le
bytecode `Montagne.ps` embarqué : coins arrondis transparents, ciel statique,
flocons animés, effet du vent musical et du stir mesurés sur les pixels.
`RenderTargetBitmap` ignore les pixel shaders, donc la fixture de test ne montre
que le repli dessiné ; la preuve du shader se fait sur le bureau.

**Reste à observer par Ayo** : la sensation au vrai curseur (les flocons qui
s'écartent), la réaction sur une musique réelle, le redimensionnement à la
poignée, et le masquage puis réaffichage. Ces points ne peuvent pas être validés
par une image fixe ni par une compilation.
