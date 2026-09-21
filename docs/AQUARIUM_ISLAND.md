# Aquarium — essai Blender de l'île low-poly

Essai demandé par Ayo : changer d'angle d'attaque pour l'Aquarium en
reproduisant en Blender la scène de référence (diorama low-poly : montagne
enneigée, conifères, plage, eau translucide dans un socle verre et cuivre) avec
le shader d'eau du tutoriel
[Dunking Dog Games](https://dunkingdoggames.com/easy-water-shader-in-blender-4-0/).

Le 17 septembre 2026, la vue de 3/4 a été abandonnée à sa demande : la scène est
rendue **de côté, en orthographique**, et son eau est **animée dans le dock** par
le shader de pixels existant (mouvement par défaut, spectre du bloc Lecteur,
sillage du curseur). Retirer `assets/Aquarium/island.png` suffit à revenir à
l'eau shader/dessinée d'avant.

## Fichiers

| Fichier | Rôle |
| --- | --- |
| `assets/Aquarium/island/build_island.py` | Générateur unique de la scène (Blender 5.1, `bpy`) |
| `assets/Aquarium/island/island.blend` | Scène générée, ouvrable dans l'interface Blender |
| `assets/Aquarium/island.png` | Rendu de côté (1280 × 720) chargé par le dock |
| `artifacts/validation/aquarium-island/` | Rendus de preuve : `render-1280x720.png`, passes, fixtures |

Régénération (script déterministe, graine fixe `20260917`) :

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" `
  --background --factory-startup `
  --python "assets\Aquarium\island\build_island.py"
```

Variables d'environnement optionnelles : `ISLAND_SAMPLES` (défaut `512`),
`ISLAND_W` et `ISLAND_H` (défaut `1280 × 720`). Le script recrée la scène,
enregistre `island.blend`, puis écrit
`artifacts/validation/aquarium-island/render-1280x720.png`.

## Contenu de la scène

- Caméra orthographique de face (largeur 4,4 unités pour 1280 × 720) : la ligne
  d'eau tombe à 66,97 % de la hauteur de l'image, valeur reprise par le dock.
- Bloc carré : cuve de verre ouverte (transmission, IOR 1.45), volume d'eau,
  plaque de cuivre inférieure, fond en `shadow catcher` sur monde bleu clair.
- Île facettée (plage, prairie, jupe immergée), montagne bruitée avec calotte
  neigeuse, 11 conifères low-poly, rochers, îlot à deux arbres.
- Matériau d'eau : Principled BSDF (Transmission 1.0, IOR 1.33, Metallic 0.2),
  `Noise Texture 3D` → `Wave Texture (Bands)` → `Multiply` avec
  `Dot Product(Normal, Incoming)` → `Bump` → Normal, absorption volumique verte.
- Rendu : Cycles GPU (OptiX), 512 échantillons + denoise, vue AgX « Punchy ».

## Dans le dock

L'image est la couche d'eau du bloc (`WaterLayer`) ; le shader de pixels la
reçoit comme entrée (`implicitInput`, constante `Scene = 1`) et **réfracte
l'image sous la ligne d'eau**. La musique agit comme sur l'eau peinte : les
basses et le chop déplacent l'image elle-même (swell vertical, pente
horizontale amplifiée par basses et aigus), les caustiques s'allument sous les
basses (teinte jade du rendu, plus la teinte pâle choisie pour l'eau peinte),
les rais et la lumière de surface suivent, et le déplacement s'atténue avec la
profondeur pour que le socle cuivre reste stable. Le sillage du curseur passe
par le même déplacement — c'est ce qui le rend convaincant.

- **par défaut**, la surface ondule doucement (swells, chop, bruit) ;
- **avec la musique**, hauteur, vitesse et éclat suivent les basses/mid/aigus du
  bloc Lecteur (`Station.MusicBands`), sans stroboscope ;
- **avec le curseur**, les cinq dernières positions forment un sillage qui
  déforme l'image et s'éteint ; les poissons proches s'écartent.

Poissons, bulles et plantes restent dessinés par-dessus, mais sont ramenés à la
bande d'eau du rendu : la ligne d'eau du dock est déduite de l'image (et du
recadrage quand le bloc est plus large que 16:9), les bulles éclosent à cette
ligne et les plantes s'arrêtent dessous.

Pour revenir à l'eau d'avant : retirer ou renommer `assets/Aquarium/island.png`
puis recharger le bureau. `build/current.txt` n'a pas été modifié.

## Vérification

- Rejouabilité : la scène est régénérée depuis zéro par le même script ; le
  `.blend` correspond au dernier rendu.
- Compilation : `scripts/Build-Battlestation.ps1` compile `Water.fx` (`fxc`) et
  publie le candidat `build/battlestation-aquarium-island-04`.
- Tests : `Battlestation.Scenes.Tests` et `Battlestation.DockReview.Tests`
  passent ; la fixture `artifacts/validation/dock-review/aquarium.png` montre le
  cadrage, les poissons et les plantes dans l'eau (les shaders de pixels
  n'apparaissent pas dans une fixture `RenderTargetBitmap`).
- Bureau : candidat 04 chargé (seul le bureau ; les hôtes terminal sont
  restés) ; `inspect` publie `water: "island"`. Dans la scène « Double écran »
  du moment, le bloc **Diorama Océan** recouvre l'Aquarium : il est donc gelé,
  comme prévu pour un bloc recouvert ; il suffit de le dégager pour le voir
  bouger.
- Aperçu CPU : `artifacts/validation/aquarium-island/preview-*.png` reproduit
  les formules du shader en numpy (silence, basses fortes à deux instants,
  curseur). C'est un miroir de mise au point, **pas** la sortie GPU ; le shader
  lui-même n'est pas capturé (la session Windows n'était pas capturable et
  `RenderTargetBitmap` ignore les shaders de pixels).
- **Non vérifié ici** : le rendu pixel du shader sur le bureau et le verdict
  visuel d'Ayo, ainsi que les clics et le redimensionnement du bloc.

## Suite

Si l'essai est retenu : juger sur écran l'eau animée (défaut, musique, curseur),
le cadrage de la vue de côté et la place des poissons et des plantes. Sinon,
retirer `assets/Aquarium/island.png` ramène l'eau d'avant.
