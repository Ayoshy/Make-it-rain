# ViceCity — Rainmeter natif

Phase 1 installée localement : **Rainmeter assure le bureau natif et son démarrage
Windows.** Les validations réalisées et les limites restantes sont documentées.

Le rendu actif est natif : fond Direct2D dans le plugin C++, panneaux en meters
Rainmeter et Lua, lecteurs matériels et Codex compilés dans une bibliothèque .NET 10
chargée par Rainmeter. Aucun WebView2, navigateur, ConradSensor.exe, CodexMeter.exe
ou Wallpaper Engine n'est nécessaire à ce rendu.

Depuis l'essai d'indépendance, les trois anciennes applications sont arrêtées.
Les données matérielles et quotas continuent d'être lus. L'utilisateur a validé les
clics, Canicule et le comportement du bureau avec les fenêtres et les icônes. La
relance avec réapplication du profil GPU a été vérifiée. Les anciennes entrées de
démarrage ont été sauvegardées puis désactivées ; le raccourci Rainmeter existant
est conservé. Aucun projet d'origine n'a été supprimé.

```powershell
python .\scripts\Generate-Native-Skins.py
.\scripts\Build.ps1
.\scripts\Validate.ps1
.\scripts\Install-Native.ps1
```

Le build utilise Visual Studio C++ x64 et .NET 10. Rainmeter 4.5.26 x64 est installé
localement. La police TTF est issue du WOFF original, convertie avec fontTools.
Les assets d'origine restent dans les sauvegardes ; les masques et lumières natifs
sont générés par le script. Le développement reste entièrement dans ce dossier.

Skins actifs : `ViceCity\Background` et `ViceCity\Dashboard`.
Le verre et le fond sont dans `ViceCityGlass.dll`, les données et commandes dans
`ViceCityNative.dll`, tous deux chargés par Rainmeter. `Update-Visuals.ps1 -DashboardOnly`
met à jour les panneaux sans interrompre un profil GPU actif.
`ViceCity\Probe` sert uniquement de maintien temporaire de la session matérielle
pendant une mise à jour visuelle ; il est normalement inactif. L'ancien diagnostic
et le prototype `ViceCity\Desktop` sont archivés.
Le code WebView2 est archivé sous `experiments/webview`, exclu du build actif.

- [Architecture et processus nécessaires](docs/ARCHITECTURE.md)
- [Vérifications et limites restantes](docs/VALIDATION.md)
- [Provenance et crédits](docs/PROVENANCE.md)
- Captures, diagnostics et mesures : `artifacts/validation`

Ne pas interpréter les captures de l'ancien prototype WebView2 comme une validation
du bureau natif. Les preuves natives ont un nom commençant par `native-`.

Dernière interface : positions fixes, Conrad se déplie vers le bas et masque Codex ;
Codex se déplie vers le haut et masque Conrad. Les quatre volets restent dans la même
zone de 779 × 443 pixels. Corps en Segoe UI Variable, chiffres en Bahnschrift,
modèles séparés en colonnes, estimations vertes, quotas en cartes par fenêtre.
La date cible est un réglage `TargetDate` de la section Variables du Dashboard.ini.

Retour arrière : `scripts/Rollback.ps1`. Le script sauvegarde et protège un réglage
GPU actif ; `-RestoreInitialGpu` signifie explicitement revenir au profil d'avant
la session avant de relancer les anciennes applications. Le test complet de ce
retour arrière avec un profil manuel reste à effectuer.

Les sauvegardes, caches, mesures personnelles et captures de validation restent
locales (`backups/`, `artifacts/`) et sont exclus de Git, comme les associations
privées. Le dépôt contient les sources, assets nécessaires, scripts et notices.
