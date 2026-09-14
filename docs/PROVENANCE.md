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
| .NET 10 / WPF | Runtime et SDK Microsoft, MIT pour dotnet/runtime |
| MSI / NVIDIA | DLL et pilote déjà installés, non copiés dans les assets |
| Simple Icons | Silhouettes des cinq applications ; licence conservée dans `dock/icons/source/LICENSE.md`, déclinaison nacrée générée localement |
| Open-Meteo | Prévisions publiques pour Aix-en-Provence, [API et attribution](https://open-meteo.com/en/docs), marque et heure de mesure affichées dans le widget |

Les sorties ne sont pas publiées. Une distribution devra inclure et vérifier les
notices des dépendances transitives et les droits sur les assets propriétaires.

## Terminal et application autonomes

Le code de connexion ConPTY appartient au projet Battlestation. Le contrôle de
rendu vient de [Windows Terminal](https://github.com/microsoft/terminal), sous MIT.
Le binaire de contrôle est fourni par `CI.Microsoft.Terminal.Wpf` 1.25.260303002,
paquet de préversion : ce n'est pas une intégration WPF officiellement stabilisée.
La connexion utilise les API système Windows, sans dépendance à un autre terminal.

NAudio 2.2.1, sous MIT, fournit l'analyse de la sortie audio. Les crédits artistiques,
les notices Codex Meter et Simple Icons restent conservés. Le renommage des projets
ne change pas les droits sur les illustrations, logos ou dépendances.

Références : [ConPTY](https://learn.microsoft.com/windows/console/creating-a-pseudoconsole-session),
[héritage des handles standard](https://github.com/microsoft/terminal/discussions/15814).

## Palette de commandes

Référence fonctionnelle locale consultée à la demande de l’utilisateur :
`../Neon Launcher/CommandPaletteWindow.cs`,
`../Neon Launcher/ViewModels/CommandPaletteViewModel.cs` et `App.xaml.cs`.
Battlestation reprend la recherche tolérante, le raccourci global, le préfixe `>`
et la navigation clavier avec sa propre implémentation WPF. Le projet Neon Launcher
et sa copie stable n’ont pas été modifiés. Aucun service IA ou mécanisme de
transmission de prompts de Neon n’a été intégré.

Raccourci système : [RegisterHotKey](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey),
[activation du premier plan](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setforegroundwindow).
