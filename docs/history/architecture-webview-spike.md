# ViceCity dans Rainmeter — état au 13 septembre 2026

## Architecture testée

Rainmeter 4.5.26.3894 x64 charge `ViceCity.dll`, un plugin C++ exportant Initialize,
Reload, Update, GetString, ExecuteBang et Finalize. `nethost`/`hostfxr` charge .NET 10
dans le processus Rainmeter. Les sources métier récupérées de Conrad (.NET 10) et
Codex Meter (.NET 8) sont recompilées en bibliothèque .NET 10. Les exécutables WPF,
leurs tray icons et leurs serveurs HTTP ne sont pas utilisés par ce plugin.

Preuve effective : les modules ViceCity.Core, coreclr 10.0.11 et LibreHardwareMonitor
sont chargés dans Rainmeter ; les six températures CPU, les données GPU et un vrai
quota Codex ont été lus. Il ne s'agit donc pas d'une hypothèse de compatibilité du
SDK C# historique avec .NET moderne. Ce SDK n'est pas utilisé : l'ABI native reçoit
des points d'entrée `UnmanagedCallersOnly` via le mécanisme Microsoft d'hébergement.

Le rendu HTML/CSS/Canvas d'origine fonctionne dans WebView2, contrôlé par une fenêtre
technique du même processus Rainmeter. Le test actuel est volontairement hors écran,
5120 × 1440, zoom 1 et DPR 1. Les panneaux passent par `postMessage`, avec méthodes
autorisées explicitement, et aucune écoute HTTP. La page ne peut naviguer ailleurs,
ouvrir une fenêtre, télécharger, utiliser des permissions ou effectuer des requêtes
réseau. Les fichiers Web sont servis par une correspondance locale de domaine.

**Ce rendu n'est pas encore attaché au bureau Windows.** Le comportement derrière
les icônes, Afficher le bureau, les fenêtres plein écran, les changements de DPI et
le retour de veille reste à implémenter et à valider. Une fenêtre simplement posée
au-dessus du bureau ne serait pas une migration terminée. Aucune concession sur les
animations ni aucune architecture de remplacement permanente n'est approuvée par
les seules captures hors écran.

## Dépendances résiduelles

| Composant | Rôle et durée |
|---|---|
| Rainmeter x64 | Seule application de personnalisation visée ; contrôle le cycle de vie du plugin |
| .NET Desktop Runtime 10 x64 | Runtime chargé dans Rainmeter ; présent localement |
| WebView2 Runtime | Processus navigateur, rendu et GPU techniques, attachés au rendu ; présent localement |
| Codex installé et déjà connecté | Un `codex.exe app-server --stdio` enfant de Rainmeter, lecture des compteurs |
| MSI Center : MSI_Center_Service et MSI_Case_Service | Dépendance actuelle pour lire les températures CPU, API MSI non redistribuée |
| Pilote NVIDIA / NVAPI | Mesures et commandes de la RTX 2060 SUPER |
| ViceCity.GpuHelper.exe | Petit exécutable technique privilégié, lancé uniquement à la demande d'une écriture GPU avec UAC ; aucun service ni autostart |

Le helper reste attaché par pipe pendant une session de réglage afin d'éviter une
confirmation UAC à chaque mouvement. À la fermeture du pipe il restaure les réglages
capturés. La version portée restaure aussi le mode et la consigne ventilateur
antérieurs, au lieu de toujours imposer Auto. Son exécution matérielle n'a pas été
testée : le bureau actuel conserve la main. Le contrôle est bloqué si Conrad tourne,
afin d'éviter deux propriétaires des réglages.

## Confidentialité et données manquantes

Le client appelle uniquement initialize, initialized, account/rateLimits/read et
account/usage/read. Aucun code ne consomme un crédit de reset. Les identifiants ne
sont ni ouverts, ni copiés par le plugin. Codex utilise sa connexion existante.
Les sessions locales sont lues pour leurs compteurs ; les prompts/réponses ne sont
ni persistés dans le cache ni transmis. Le cache contient des clés de chemins
hachées et des agrégats de modèles, efforts et tokens.

Une fenêtre de quota absente reste absente. Un usedPercent ou un nombre de crédits
absent reste nullable. Une lecture échouée conserve le dernier quota avec un état
d'erreur. Les tokens du jour utilisent le bucket du serveur quand il existe, puis
les deltas horodatés des compteurs locaux. Le calcul local a été corrigé pour une
session traversant minuit : les tokens sont rattachés au jour de l'événement dans
le fuseau local, et non au jour de création du fichier. Un test couvre ce cas.

Les prix connus proviennent du tableau de l'application récupérée, sans nouvelle
validation tarifaire. Les prix de substitution Spark et auto-review ont été retirés.
Aucun modèle inconnu ne reçoit de prix ; ses tokens restent visibles. La valeur des
modèles connus n'est plus extrapolée au total du compte contenant des modèles non
tarifés. C'est une estimation locale partielle, jamais une facture.

## Références techniques consultées

- ABI Rainmeter : https://raw.githubusercontent.com/rainmeter/rainmeter/master/Plugins/API/RainmeterAPI.h
- Chargeur Rainmeter : https://raw.githubusercontent.com/rainmeter/rainmeter/master/Library/MeasurePlugin.cpp
- Hébergement .NET : https://learn.microsoft.com/en-us/dotnet/core/tutorials/netcore-hosting
- Processus WebView2 : https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-model
- Lecture Codex : https://learn.chatgpt.com/docs/app-server

Ces références justifient les mécanismes employés, pas une certification de parité.
