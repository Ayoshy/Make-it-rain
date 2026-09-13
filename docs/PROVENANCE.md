# Provenance et droits

Sauvegarde de l'état de travail : `backups/20260913-115800/`.
113 fichiers copiés et vérifiés par SHA-256 ; `manifest.json` donne leur chemin,
taille et empreinte. `provenance.json` conserve les trois emplacements d'origine.
Les deux fichiers `*-git-status.txt` et `*-head.txt` consignent les modifications
et les commits de référence. Les originaux n'ont pas été nettoyés ni réinitialisés.

Les sources, intégrations non suivies et captures ont été récupérées depuis les
répertoires réels. `.git`, les sorties bin/obj, distributions compilées et dépendances
reproductibles sont exclus. Les fichiers privés conrad-connection.js,
codex-connection.js et wallpaper-token.txt sont exclus de toute copie et ignorés par
Git. Aucun fichier d'authentification Codex n'a été récupéré. La configuration globale
Wallpaper Engine n'est pas copiée intégralement : seuls les réglages effectifs du
fond et sa disposition sont extraits.

La cible `2026-11-19T00:00:00+01:00` est un réglage utilisateur conservé, pas une
nouvelle vérification de date de sortie. Les overrides effectifs sont night, dual,
positionx=85 et scale=104 ; les autres valeurs viennent du config.js récupéré.

| Source | Droits / crédits |
|---|---|
| Codex Meter | MIT, copyright 2026 Ayoshy ; texte conservé dans LICENSE-Codex-Meter.txt |
| Conrad Sensor | Aucun fichier de licence trouvé dans les sources récupérées ; réutilisation locale demandée, aucune publication effectuée |
| Illustration Jason and Lucia, logo, police Art Deco | Rockstar Games / Take-Two Interactive ; assets de fan, droits réservés aux ayants droit, crédits du LIRE-MOI original conservés dans la sauvegarde |
| HTML/CSS/JS du fond | Sources locales de personnalisation ; provenance exacte conservée, aucune licence libre déduite |
| LibreHardwareMonitorLib 0.9.6 | MPL-2.0, paquet NuGet officiel ; sources upstream référencées dans le manifeste NuGet |
| WebView2 SDK 1.0.3800.47 | Microsoft ; LICENSE-WebView2.txt conservé |
| .NET / nethost | Runtime et SDK Microsoft locaux, MIT pour dotnet/runtime ; aucune distribution du runtime |
| Rainmeter | Installation existante, GPL-2.0 ; ABI utilisée par un plugin natif, pas de copie du code du chargeur |
| MSI / NVIDIA | DLL et pilote déjà installés, non copiés dans les assets |

Les sorties ne sont pas publiées. Une distribution devra inclure et vérifier les
notices des dépendances transitives et les droits sur les assets propriétaires.
