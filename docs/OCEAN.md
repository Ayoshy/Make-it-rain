# Diorama Océan

Bloc décoratif indépendant : un morceau d'océan coupé net, posé sur le bureau.
L'eau est un **objet 3D calculé image par image sur le GPU**, pas une image, pas
une vidéo, pas un effet de vagues plaqué. Le bloc s'ajoute, se retire, se
déplace, se redimensionne et se mémorise comme les autres.

| Élément | Valeur |
| --- | --- |
| Identifiant | `ocean` |
| Titre | Diorama Océan |
| Verre natif | slot 17 |
| Taille par défaut | 720 × 480, minimum 420 × 300 |
| Arrivée | **masqué**, comme l'Aquarium et LoL ; aucun modèle ne le liste |

## Ce que l'eau est réellement

`src/Battlestation/Shaders/Ocean.fx` (compilé en `ps_3_0` par
`scripts/Build-Battlestation.ps1`, embarqué en ressource WPF) trace une scène
analytique par pixel :

- le rayon primaire cherche la **surface supérieure** (champ de hauteur
  implicite, quelques itérations), les **deux faces latérales** visibles et le
  socle ;
- le rayon **réfracté** (indice 1,333) traverse le volume, atteint le relief
  immergé et revient : la couleur est l'approximation à une diffusion
  (Beer-Lambert par canal pour le fond, diffusion propre de l'eau selon
  l'épaisseur optique) ;
- les **normales** viennent du gradient analytique du champ d'ondes, pas d'une
  normal map ;
- le **reflet** est un ciel procédural (dégradé + soleil), et l'angle de vue
  module la réflexion par Fresnel ;
- l'**écume** apparaît sur les crêtes et là où le relief est proche de la
  surface.

Approximations assumées, à ne pas présenter comme une simulation : pas de
solveur volumétrique, pas de réfraction du bureau Windows (l'environnement
refleté est procédural), caustiques approchées par la hauteur de surface, relief
en champ de hauteur (pas de surplomb ni de vague déferlante).

## Pourquoi pas Direct3D 11 dans le fond Direct2D

La piste « texture D3D11 composée par le moteur Direct2D existant » a été
**mesurée avant d'être écartée** : le fond natif dessine avec
`ID2D1HwndRenderTarget` (Direct2D 1.0), et `CreateSharedBitmap` y refuse une
surface venue d'un autre appareil — `D2DERR_UNSUPPORTED_OPERATION`
(`0x88990003`), pour toutes les combinaisons testées (texture D3D11 ou D3D10.1,
modes alpha prémultiplié / ignoré / inconnu, cible cachée ou visible). La sonde
est conservée sous `artifacts/validation/ocean-diorama/probe/ocean-probe.cpp`.
Passer par une seconde couche Direct3D n'aurait donc demandé ni moins de code ni
moins de risque, et aurait imposé de réécrire le cœur de composition. Le rendu
3D tourne donc **dans la fenêtre du bloc**, sur le chemin GPU déjà utilisé par
Battlestation (tier 2, pixel shader 3.0 vérifiés sur ce PC), sans copie
GPU → CPU → bitmap.

## Statique mais réactif

Au repos, la caméra, le socle et le décor sont fixes et l'eau garde **une forme
de référence immobile** : houle figée, ondulations de vent gelées, aucune normal
map qui défile, aucune écume qui circule. Le shader n'a aucune animation cachée :
sans énergie injectée, il rend exactement la même image.

Les perturbations sont des **paquets d'ondes** bornés :

| Interaction | Effet |
| --- | --- |
| Curseur sur l'eau | le rayon projette le pixel sur la surface ; la vitesse et la direction du geste donnent l'amplitude (0,016 → 0,062), un paquet au plus toutes les 40 ms, jamais d'injection au repos |
| Curseur immobile | aucun paquet : rien ne s'accumule |
| Sortie du bloc | plus d'injection, les paquets terminent leur amortissement |
| Ciel, socle, bord du bac | hors empreinte : aucune onde |
| Basses | houle large qui parcourt le bac, tant qu'elle est nourrie |
| Aigus | rides fines, uniquement pendant qu'elles sont entendues |
| Arrêt du son | les enveloppes retombent, les paquets s'éteignent, l'eau redevient la forme de référence |

La caméra est fixe (trois quarts plongeante). Au redimensionnement, la distance
et le cadrage sont recalculés pour que l'objet garde ses proportions : il est
re-cadré, jamais étiré.

## Son

Le bloc ne capture rien lui-même : il lit `Station.MusicBands`, le spectre déjà
publié par le dock Lecteur (donc la sortie Windows suivie par Battlestation,
jamais le microphone). Aucune seconde capture n'est ouverte, aucun volume, mute
ou périphérique n'est touché, rien n'est écrit sur disque. Les enveloppes ont une
attaque rapide et une retombée lente, un seuil de silence, et les impacts sont
détectés en comparant l'enveloppe des basses à sa propre moyenne lente : une
musique normale est visible, une notification isolée ne devient pas un tsunami.
Si le dock Lecteur est masqué et que la réactivité musicale est désactivée, le
diorama voit le silence et reste immobile.

## Cycle de vie et repos

Masqué, occulté, en cours de réorganisation ou hors écran, le bloc ne calcule
rien : la boucle de rendu rapide n'existe que pendant qu'un paquet vit ou qu'un
son est entendu, sinon un simple relevé lent lit le spectre sans redessiner. Pas
de travail sur le fil d'interface : le pas de simulation est borné et déterministe
(`OceanSurface.Advance`).

## Pipeline Blender

`assets/Ocean/build_bed.py` construit le fond immergé et ses textures à partir
d'une **unique** fonction de hauteur, si bien que la scène et l'asset temps réel
ne peuvent pas diverger :

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.1\blender.exe' --background --python assets/Ocean/build_bed.py
```

| Sortie | Rôle |
| --- | --- |
| `assets/Ocean/ocean-bed.blend` | scène éditable (maillage, matériau, couleurs) |
| `assets/Ocean/ocean-bed.glb` | même géométrie pour tout autre outil |
| `assets/Ocean/ocean-bed.png` | **asset du widget** : RGB = albédo, A = hauteur |

Correspondance explicite avec le shader : `x,z ∈ [-1,1]` est l'empreinte du bac,
`y ∈ [-0.70,-0.10]` devient `A = (y + 0.70) / 0.60`, et le shader relit
`bedHeight = -0.40 + (A·2 − 1)·0.30`, `bedColor = RGB`. Sans le PNG, le shader
garde son relief procédural : le bloc ne dépend jamais de l'asset. Blender ne
sert qu'à la fabrication ; le widget fonctionne Blender fermé.

## Vérifications

Ce qui a été **exécuté** :

```powershell
dotnet run --project tests/Battlestation.Scenes.Tests.csproj
dotnet run --project tests/Battlestation.DockReview.Tests.csproj -- .
```

Les contrôles couvrent le bloc masqué par défaut, sa taille et son minimum, son
absence des modèles, le retour d'une scène existante, le verre 17, le repos
strict sans son ni souris (deux images identiques), la réaction des basses, le
retour au repos après le silence, et l'arrêt quand le bloc est masqué.

Sur le bureau réellement chargé (`build/battlestation-ocean-diorama-02`), avec le
fond Blender en service : rendu de l'eau dans le dock, absence de rectangle noir
ou de frange opaque, et projection curseur → surface mesurée indépendamment —
une perturbation injectée au point d'eau (0,0) donne un centroïde de différence
d'image à (400,5 ; 150,7) px pour une position attendue de (400 ; 152) px, soit
≈ 1 px. Le shader tient ≈ 95 images/s en boucle de rendu à 720 × 480 dans le banc
d'essai (`artifacts/validation/ocean-diorama/`).

**Reste à observer par Ayo** : la sensation au vrai curseur (naissance et
propagation du sillage sous la souris), la réaction sur une musique réelle et le
retour au silence, le redimensionnement à la poignée, et le masquage puis
réaffichage. Ces points ne peuvent pas être validés par une image fixe ni par une
compilation.
