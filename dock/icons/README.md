# Vice City Neon

Bibliothèque locale : Steam, Brave, Discord, Stremio et League of Legends.
Chaque icône existe en SVG éditable et PNG transparent de 192 × 192 dans `neon/`.
Les silhouettes restent reconnaissables et partagent les mêmes proportions,
ton nacré légèrement mauve/cyan, liseré clair et halo très réduit. Cette révision
remplace le premier dégradé rose jugé trop appuyé. Le dock utilise directement les PNG.

La sélection se fait par le nom de l'application normalisé : « League of Legends »
→ `leagueoflegends.png`. Un champ optionnel `"icon": "steam"` dans `dock/apps.json`
permet de choisir explicitement une icône, indépendamment du libellé.
Une application sans variante néon utilise son icône Windows habituelle.

Pour étendre la famille, ajouter une silhouette SVG de viewBox 0 0 24 24 dans
`source/`, ajouter son identifiant à `NAMES` dans le générateur puis exécuter :

```powershell
python -m pip install resvg-py
Les assets PNG sont fournis dans ce dossier.
.\scripts\Install-Dock.ps1
```

Le générateur centralise la palette et le style. Aucun générateur ni téléchargement
ne tourne avec le dock. SVG et PNG sont conservés dans le dépôt.

Silhouettes : [Simple Icons](https://github.com/simple-icons/simple-icons), fichiers
`icons/steam.svg`, `brave.svg`, `discord.svg`, `stremio.svg`, `leagueoflegends.svg`,
récupérés le 13 septembre 2026. Licence du projet conservée dans `source/LICENSE.md`.
La déclinaison néon est produite localement par le générateur artistique initial, archivé avec les sources historiques.
