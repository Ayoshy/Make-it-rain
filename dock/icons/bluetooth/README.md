# Icônes Bluetooth

Silhouettes SVG originales dans `source/` : boombox horizontale à poignée,
Buds3 Pro (embouts, tiges plates et bandes lumineuses), DualSense (façade,
sticks et touches), soundbar droite de la famille Samsung J-Series.
Le modèle exact de la soundbar n'est pas déterminé à partir de son nom Bluetooth.

`pearl/` est le gris nacré déconnecté ; `connected/` est le néon Vice City.
Le générateur lit directement les dégradés de `../neon/discord.svg` et
`../running/discord.svg`, ainsi que son filtre de halo. La palette des applications
reste la source de vérité. Les SVG et PNG 192 × 192 sont conservés ensemble.

```powershell
python scripts/Build-BluetoothIcons.py
```

Aucune icône d'application n'est réécrite par ce générateur. La conversion utilise
resvg-py comme les autres générateurs du dock et ne s'exécute pas au runtime.

Références de forme : [Buds3 Pro](https://www.dolby.com/ko/experience/headphones/galaxy-buds3-pro/)
et [JBL Boombox](https://fr.jbl.com/JBL%2BBOOMBOX.html). Les photos ne sont pas
intégrées au produit ; les silhouettes sont dessinées dans les SVG du dépôt.
