# Fonds du bureau — 25 septembre 2026

Images produites avec l'outil imagegen intégré, sans CLI ni appel API facturé par le terminal.
Les originaux fournis et les premières variantes sont conservés. Les fichiers référencés
par le rendu sont tous dans le projet. Les générations sont des images de style
photographique, pas des photographies documentaires de lieux réels.

## Fichiers retenus

| Scène | Fichier | Origine |
| --- | --- | --- |
| Bureau | assets/Images/aurore-photo.png | Nouvelle passe sur aurore.png, direction Manhattan demandée par Ayo |
| Bureau | assets/Images/manhattan-blue-hour.png | Imagegen intégré, prompt Fable fourni par Ayo ; 2184 × 720 pixels |
| Jeu | assets/Images/vice-city-boulevard.png | Imagegen intégré, prompt Fable fourni par Ayo ; 1983 × 793 pixels |
| Multimédia | assets/Images/obsidienne-photo.png | Nouvelle passe plus naturelle sur obsidienne.png |
| Jeu | assets/Images/jason-lucia.jpg | Illustration préexistante, inchangée |
| Jeu | assets/Images/jason-lucia-03-painted.png | Retouche de Jason_and_Lucia_03.jpg, style de jason-lucia.jpg ; acceptée par Ayo |

Le panorama demandé était 5120 × 1440 ; le service a produit une résolution inférieure.
Le rendu adapte le ratio sans déformer les immeubles. Les dimensions réelles sont
celles des fichiers, sans prétendre à une génération 5K. Les reflets, flares et
lueurs sont ajoutés dans Direct2D, indépendamment des images.

## Prompt final — Bureau

Rework this wallpaper into an utterly believable professional architectural PHOTOGRAPH of real Manhattan, viewed from inside a high-floor office through a single large window. Retain the user's basic direction: almost entirely the view out of the window, neighboring skyscrapers directly opposite with visible streets and small cars/pedestrians far below; barely a narrow dark walnut sill and window edge at the frame. Deliver an ultra-wide single 32:9 panorama, requested output 5120x1440, no collage. REMOVE the AI-looking features of the reference: no impossible repeated skyline landmarks, no rigid mirror symmetry, no every-window-amber-light pattern, no uniformly detailed facades, no glossy overprocessed HDR, no epic orange sunset, no fanciful futuristic architecture. Reconstruct plausible Manhattan architectural geometry, a few restrained nearby mid-century curtain-wall towers and older limestone buildings, varied facade weathering, small reflections warped naturally between glass panels, patches of ordinary offices with blinds up/down and many dark windows, subtle differences in interior white balance. Real late afternoon light after a cloudy day, warm daylight softly breaking through haze, neutral slate-blue and grey city with occasional restrained warm reflections. Tiny irregular street traffic, crosswalks and shop awnings below, no cloned taxis. Natural photographic dynamic range, subtle film grain, imperfect glass, authentic prestige TV establishing shot atmosphere like Suits or Mad Men, not a synthetic illustration. Street life and depth visible but not tack-sharp everywhere. Most of frame is exterior view; NO desk, computers, chairs or interior people. Center unobstructed by mullions. No text or overlays. This is a new sibling version; do not modify the original.

## Prompt final — Multimédia

Rework this wallpaper into a restrained, convincing real landscape PHOTOGRAPH from the shore of a Norwegian fjord in late blue hour. Ultra-wide continuous panorama target 32:9, requested output 5120x1440. Keep the calm dark water, layered coastal mountains and nighttime atmosphere, but eliminate everything that screams AI fantasy matte painting. NO observatory, NO dramatic volcano spires, NO giant theatrical full moon, NO thousands of identical stars, NO implausible rim lighting, NO glittering oversharp water, NO symmetrical mountain framing, NO idealized HDR. Low realistic worn Scandinavian mountain ridges with uneven sparse snow patches, a nearer rocky shoreline partly visible at one edge, occasional tiny ordinary cabin lights far across the water. Clouded indigo dusk sky with a soft break in the cloud layer, just a faint small natural moon if any. Authentic photographic exposure and lens behavior, muted blue-grey palette, some deep shadows with no invented microdetail, atmospheric haze veiling the distant slopes. Water shows subtle irregular wind ripples and a few small believable distant light reflections. Calm and beautiful, like a photographer waiting after sunset, not an epic movie poster. Natural asymmetry, sparse details, understated light. Single continuous view, no text, no logo, no UI. Deliver a new sibling version; preserve the original.

## Prompt retenu — Lucia

Edit the FIRST reference image (Jason and Lucia in the convertible) into the hand-painted illustrated key-art style of the SECOND reference. The second reference is STYLE AND PALETTE ONLY; do not use its composition. Preserve the first image's exact scene composition, faces and identities, poses, hairstyle, clothes, tattoos, car, cup, city background, camera angle and 16:9 framing. Replace photorealistic 3D surface shading with crisp painterly brush shapes, subtly textured pastel/gouache color planes, confidently drawn edges and warm peach/pink highlights with muted lavender and indigo shadows, matching the illustrated GTA VI poster treatment in the second reference. Beautiful premium illustration, detailed faces, restrained stylization, not cartoon, not anime, not washed out. Do not add text, objects or logos. Deliver one complete edited wallpaper.

## Prompt Fable — Manhattan heure bleue (25 septembre)

Ultra-wide 32:9 panorama, 5120x1440, single continuous view. A believable professional architectural PHOTOGRAPH of Manhattan at early blue hour from a high-floor office window: varied curtain-wall and limestone facades, scattered lit offices among many dark windows, restrained warm interior glows against slate-blue dusk, light haze, natural film grain and imperfect glass reflections. No mirror symmetry, no cloned landmarks, no HDR gloss, no orange epic sunset, no desk or interior, no text. Keep the right third of the frame calmer and less detailed than the center.

## Prompt Fable — Boulevard Vice City (25 septembre)

Hand-painted key-art illustration in the GTA VI poster style: an empty Vice City boulevard at dusk, palm silhouettes, neon signs reflecting on wet asphalt, a lone parked convertible, warm peach and pink highlights with muted lavender and indigo shadows, crisp painterly brush shapes, premium illustration, not cartoon, not anime. Ultra-wide 32:9, 5120x1440, no characters, no text, no logos.

Prompts transmis tels quels à l'outil intégré. Les fichiers produits sont conservés
sans redimensionnement ; le rendu recadre les panoramas sur les deux écrans sans
les déformer. Bureau alterne Aurore et Manhattan heure bleue ; Jeu alterne les
deux illustrations existantes et le boulevard. Ces ajouts restent soumis à
l'acceptation du candidat dans Atelier.

## Vérification du candidat wallpapers-fable-01

- Compilation complète réussie ; `tests/Check-Themes.ps1` valide les cycles Jeu
  (trois images) et Bureau (deux images), les fondus aller/retour, les pauses,
  les compteurs indépendants et la lecture de l'ancien fichier de progression.
- Rendus natifs des deux ajouts examinés à 5120 × 1440 dans
  `artifacts/themes-20260925-204026-577/` ; il s'agit de rendus isolés.
- Candidat chargé ; même hôte terminal conservé. La scène Multimédia active
  juste avant le rechargement et son fichier de disposition sont inchangés.
  Atelier indique une version à l'essai ; le pointeur de lancement est inchangé.
- Mesures des processus, helpers compris, dans `live-before.json` et
  `live-after.json` du même dossier d'artefacts. Les conditions de scène ont
  changé pendant le travail : ces relevés ne démontrent aucun gain ni absence
  de régression de performance.
- Computer Use échoue avec `Computer Use native pipe is unavailable` : clics,
  fluidité perçue et rotations sur le bureau réel restent à observer.
