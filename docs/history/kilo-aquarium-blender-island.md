# Aquarium — essai Blender : île low-poly flottante

## Objectif

Nouvelle approche pour l'Aquarium : reproduire en Blender 5.1 la scène de la
référence d'Ayo (diorama low-poly : montagne enneigée, arbres, rochers, plage,
eau translucide dans un socle cuivre/verre) avec le graphe d'eau du tutoriel
Dunking Dog Games (image du node setup jointe).

**Périmètre** : produire la scène Blender et ses rendus de preuve.
**Hors périmètre** : intégration WPF/dock Aquarium, animation en boucle, export
vidéo, remplacement du shader `Water.fx`. Aucun changement .NET, aucun commit.

## Décisions

- **Pas de MCP.** Blender est piloté en script `bpy` en `--background` ; les
  rendus PNG sont relus directement pour itérer (déjà validé : rendu Cycles GPU
  OptiX OK sur ce PC, ~9 s pour un smoke test 128 px). Reproductible, sans
  dépendance supplémentaire ; le `.blend` reste ouvrable dans l'interface.
- Blender 5.1.2, Cycles, `OPTIX` GPU. Écarts d'API avec le tutoriel 4.0 (sockets
  Principled BSDF renommés) tolérés par recherche de noms.

## Emplacements

| Fichier | Rôle |
| --- | --- |
| `assets/Aquarium/island/build_island.py` | Générateur unique (scène + .blend + rendu) |
| `assets/Aquarium/island/island.blend` | Scène générée, ouvrable et modifiable à la main |
| `artifacts/validation/aquarium-island/` | Rendus de preuve et comparaisons (ignoré par Git) |
| `docs/AQUARIUM_ISLAND.md` | Intention, commande de régénération, état de vérification |

## Construction de la scène (script déterministe, graines fixes)

1. Style low-poly : flat shading, triangulation/facettes, aucun lissage.
2. **Socle** : boîte carrée — plaque cuivre/orange en bas (biseau léger), volume
   d'eau vert profond translucide, parois de verre minces teintées bleu pâle,
   liseré clair en haut. Le terrain se prolonge sous la ligne d'eau pour rester
   visible à travers l'eau (rochers, sable immergés).
3. **Île** : montagne facettée (icosphère bruitée + Planar Decimate), calotte
   neigeuse, facettes grises sur les pentes, prairie verte.
4. **Plage** rose/sable en anneau + pierriers gris, petit îlot rocheux avant
   avec deux plants.
5. **Arbres** : 6 à 8 conifères low-poly (cônes empilés, deux verts), posés sur
   la prairie et l'îlot.
6. **Matériau eau** (relecture exacte de la capture au moment de l'implémentation) :
   Principled BSDF base cyan clair, Metallic 0.2, Roughness ~0.0, IOR 1.33,
   Alpha 0.706, Transmission Weight 1.0, Subsurface Radius (1.0, 0.2, 0.1) poids 0,
   Specular IOR Level 0.5, Coat roughness 0.03 ; Noise Texture 3D (scale 10,
   détail 2, roughness 0.5, lacunarity 2) → Vector de la Wave Texture (bands,
   scale 1, distortion 2, détail 2, detail scale 0.5) ; Dot Product
   (Normal · Incoming) × Wave → Bump (strength 1.0, distance 1.6) → Normal.
   Les câblages ambigus de la capture sont tranchés au rendu, pas devinés.
7. **Lumière/fond** : grande source douce au-dessus/avant, fill faible, monde
   bleu clair, léger dégradé de fond ; ombre douce portée sous le socle.
8. **Caméra/rendu** : objectif ~50 mm, vue 3/4 plongeante, cadrage carré,
   1024 × 1024, Cycles ~512 échantillons + denoise, vue AgX.

## Itération

Boucle : exécuter le script → lire `render-1024.png` → comparer à la référence
(cadrage, teintes, eau, lumière) → ajuster. Prévoir 3 à 5 passes. Rien ne tourne
en dehors des rendus ; aucune opération sur le fil du bureau.

## Vérification

- `blender --background --python assets/Aquarium/island/build_island.py`
  régénère le `.blend` et le rendu depuis un état propre (rejouabilité).
- Rendu final + éventuelle planche côte à côte sous
  `artifacts/validation/aquarium-island/`.
- `docs/AQUARIUM_ISLAND.md` précisé : ce qui correspond à la référence, ce qui
  en diffère, ce qui reste non vérifié.
- Verdict visuel d'Ayo, comme pour l'aquarium actuel ; ne pas présenter le rendu
  seul comme validation.

## Risques et limites

- Différences d'API Blender 4.0 → 5.1 : recherche des sockets par nom, test par
  rendu.
- Sémantique Alpha 0.706 + Transmission 1.0 sous Cycles : à ajuster visuellement
  si le rendu est trop transparent ou pas assez.
- ~1 min par rendu GPU ; la silhouette low-poly demande plusieurs passes mais
  aucune validation sur le bureau n'est concernée.

## Suite (hors tranche)

Si la scène est validée, décider séparément du mode d'intégration à l'Aquarium
(image fixe, boucle pré-rendue, autre) — décision d'Ayo, pas de travail anticipé.
