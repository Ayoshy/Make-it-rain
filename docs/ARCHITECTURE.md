# Architecture active

`Rainmeter.exe` charge `ViceCityNative.dll` pour les données et `ViceCityGlass.dll`
pour le rendu Direct2D. Les plugins exposent l'ABI native
Initialize/Reload/Update/GetString/ExecuteBang/Finalize et charge .NET 10 par
`nethost`/`hostfxr`. `ViceCity.Core.dll` contient les lecteurs et commandes issus des
sources réelles Conrad et Codex Meter. Aucune interface WPF ou application de tray
n'est lancée, et aucun serveur HTTP de migration n'est ouvert.

Le fond est dessiné par Direct2D dans le processus Rainmeter, sur un thread de rendu
distinct. Sa fenêtre technique est attachée à WorkerW derrière les icônes. Son cycle
de vie appartient au skin Background : décharger ce skin arrête le thread et ferme
la fenêtre. Ce n'est ni un exécutable de fond autonome, ni un service.

Les images, particules, halos, rayons, traînées et parallaxe sont calculés nativement.
Les compteurs et panneaux utilisent des meters Rainmeter ; Lua gère les volets
exclusifs, leur transition flou/glissement, les curseurs et le défilement. Conrad
s'étend vers le bas en masquant Codex ; Codex vers le haut en masquant Conrad.
Logo, compteur et positions de base restent fixes. Le verre adoucit et décale
légèrement le fond sous les deux blocs, avec un reflet sur leurs contours.

La première tentative utilisant de nombreux meters pour repeindre tout le fond
surchargeait la file d'interface (jusqu'à 36 % CPU mesurés) et a été retirée. La
version Direct2D sépare ce travail des clics du tableau de bord. WebView2 est retiré
du projet actif et de ses dépendances ; il n'est pas une solution finale cachée.

## Processus et services résiduels

| Composant | Rôle |
|---|---|
| Rainmeter.exe | Unique application de personnalisation, avec rendu natif et logique métier |
| .NET 10 / nethost / hostfxr | Bibliothèques chargées dans Rainmeter, pas d'application supplémentaire |
| codex.exe app-server --stdio | Processus technique enfant pour les lectures de quotas et d'usage |
| conhost.exe | Peut être associé au lecteur console Codex ; inclus dans les mesures |
| ViceCity.GpuHelper.exe | Helper élevé à la demande d'une écriture GPU, après confirmation Windows, fermé avec sa session |
| MSI_Center_Service, MSI_Case_Service | Services existants nécessaires à la lecture CPU via CS_CommonAPI.dll |
| Pilote NVIDIA / NVAPI | Mesures et commandes de la RTX 2060 SUPER |

Le helper GPU n'a ni tray, ni autostart, ni service installé. Il capture les réglages
antérieurs et les restaure lors de la fermeture du pipe. Le mode Canicule conserve
aussi son état de restauration dans le service du plugin. Tant qu'un ancien
ConradSensor est détecté, une écriture native est refusée pour éviter deux contrôleurs.

## Données et garanties

Seules les méthodes Codex initialize, initialized, account/rateLimits/read et
account/usage/read sont appelées. Aucun reset de crédit n'est possible dans cette
interface. Le plugin ne lit ni ne copie les identifiants ; Codex utilise sa connexion
existante. Les sessions locales fournissent uniquement des agrégats de compteurs ;
prompts et réponses ne sont pas persistés ni envoyés à un tiers.

Les fenêtres/valeurs absentes restent absentes ou « — ». Les modèles sans prix
conservent leurs tokens et un prix inconnu. Les prix de substitution Spark et
auto-review sont retirés. Les tarifs connus proviennent du tableau récupéré, sans
extrapolation du prix aux tokens non tarifés. L'estimation n'est pas une facture.
Le calcul local du jour utilise les deltas horodatés et couvre le passage de minuit.

La cible du compte à rebours reste le réglage récupéré
`2026-11-19T00:00:00+01:00`, sans nouvelle assertion sur la sortie du jeu.

## Références techniques

- ABI : https://raw.githubusercontent.com/rainmeter/rainmeter/master/Plugins/API/RainmeterAPI.h
- Chargeur : https://raw.githubusercontent.com/rainmeter/rainmeter/master/Library/MeasurePlugin.cpp
- Hébergement .NET : https://learn.microsoft.com/en-us/dotnet/core/tutorials/netcore-hosting
- Parentage Win32 : https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent
- Lecture Codex : https://learn.chatgpt.com/docs/app-server

WorkerW utilise un comportement du shell Windows à vérifier sur cette installation ;
la présence d'une fenêtre attachée ne prouve pas à elle seule les clics et Afficher
le bureau. La pause actuelle porte sur le fond lorsqu'une fenêtre au premier plan
couvre un moniteur ; les données du tableau de bord continuent d'être actualisées.
