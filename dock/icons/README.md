# Vice City Neon

Bibliothèque locale : Steam, Brave, Discord, Stremio, League of Legends, Codex
(marque OpenAI), Claude Code (marque Claude), Edge, Chrome, qBittorrent et Battle.net.
Chaque icône existe en SVG éditable et PNG transparent de 192 × 192 dans `neon/`.
Les silhouettes restent reconnaissables et partagent les mêmes proportions,
ton nacré légèrement mauve/cyan, liseré clair et halo très réduit. Cette révision
remplace le premier dégradé rose jugé trop appuyé. Le dock utilise directement les PNG.

La sélection se fait par le nom de l'application normalisé : « League of Legends »
→ `leagueoflegends.png`.
Une application sans variante néon utilise son icône Windows habituelle.

Pour étendre la famille, ajouter une silhouette SVG de viewBox 0 0 24 24 dans
`source/`, puis exécuter depuis la racine (resvg-py doit être installé) :

```powershell
python scripts/Build-DockIcons.py chrome edge battlenet qbittorrent claudecode codex
```

Le générateur centralise la palette et le style. Aucun générateur ni téléchargement
ne tourne avec le dock. SVG et PNG sont conservés dans le dépôt.

Silhouettes : [Simple Icons](https://github.com/simple-icons/simple-icons), fichiers
`icons/steam.svg`, `brave.svg`, `discord.svg`, `stremio.svg`, `leagueoflegends.svg`,
récupérés le 13 septembre 2026. Licence du projet conservée dans `source/LICENSE.md`.
Les six nouvelles silhouettes proviennent de Simple Icons 13.21.0, sauf Edge
(11.15.0). Elles ont été récupérées le 15 septembre 2026. Les fichiers source
restent inchangés ; `scripts/Build-DockIcons.py` produit leur déclinaison nacrée.
Les cinq premières icônes conservent leur rendu existant.

Spotify utilise également la silhouette Simple Icons 13.21.0, récupérée le
15 septembre 2026, et les mêmes générateurs nacré et Vice City.
`python scripts/Build-RunningDockIcons.py spotify` produit uniquement sa variante active.

Le menu Projets réutilise Codex et ajoute DeepSeek (Simple Icons v16,
`https://cdn.jsdelivr.net/npm/simple-icons@16/icons/deepseek.svg`, récupéré le
24 septembre 2026) et un monogramme K dessiné pour le lanceur Kilo, sans prétendre
reproduire sa marque. Ils suivent le même générateur nacré et les palettes des scènes.
