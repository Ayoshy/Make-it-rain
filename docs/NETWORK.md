# Dock Réseau

Le dock affiche les débits descendant et montant de l’interface choisie, deux courbes sur la dernière minute et la latence ICMP vers une cible. La cible initiale est `1.1.1.1` ; elle figure dans l’infobulle et se modifie par le bouton `⋯`. Cette latence concerne la cible indiquée, pas un serveur de jeu.

L’interface automatique suit la route Windows par défaut, avec la métrique de route et celle de l’interface. Le sélecteur permet de choisir une interface configurée explicitement. Les compteurs sont lus chaque seconde sur un worker ; l’affichage est interpolé. Une première mesure, une interface indisponible ou une absence de réponse ne deviennent pas des valeurs nulles fictives.

## Détail par application

Le bouton lance `Battlestation.NetworkHelper.exe` avec la demande Windows. Le bureau reste non privilégié. Un refus conserve les courbes et la latence ; le détail reste inactif et peut être redemandé.

Le helper utilise Microsoft TraceEvent 3.2.6, une session temps réel `Battlestation.Network` et un canal local limité à l’utilisateur. Il compte uniquement les événements d’envoi/réception TCP et UDP, IPv4 et IPv6. Le PID est lu dans le payload décodé de l’événement, jamais déduit aveuglément de l’en-tête.

Les processus du même chemin d’exécutable sont regroupés ; les cinq premiers sont classés par débit total. Ce détail couvre le trafic du PC, toutes interfaces confondues. Aucun contenu réseau, destination ou fichier ETL n’est conservé. Seuls les noms et compteurs agrégés sont transmis au bureau.

Masquer ou occulter le dock suspend les mesures et arrête la session ETW. Le helper demeure inactif pour éviter une autre demande Windows au retour. Fermer le bureau ferme son canal ; le helper arrête sa propre trace et quitte. Aucun service ni lancement privilégié automatique n’est installé, et les autres sessions de diagnostic ne sont pas touchées.

## Vérifications

- Build natif/.NET et contrôles des docks : réussis.
- Route par défaut réelle sur ce PC : Ethernet, interface 9.
- Compteurs réels et latence vers `1.1.1.1` observés sur le bureau chargé.
- Test de cible sans réponse : indisponible, pas zéro milliseconde.
- Suspension/reprise des mesures au masquage : vérifiées.
- Agrégation de deux PID du même exécutable, sens réception/envoi et consommation unique des compteurs : testés.
- Activation/refus Windows, attribution ETW pendant un téléchargement réel et fermeture effective de sa trace : en cours de validation utilisateur.

```powershell
dotnet run --project tests/Battlestation.Network.Tests.csproj -- build/battlestation-network-scenes-05
dotnet run --project tests/Battlestation.DockReview.Tests.csproj -- .
```

Les dépendances du helper sont restaurées sous `vendor/network-nuget/` : le cache NuGet global présente une métadonnée corrompue et reste inchangé pour préserver les autres projets.

Références : [événements TCP/IP et PID](https://learn.microsoft.com/en-us/windows/win32/etw/tcpip), [sessions et privilèges ETW](https://learn.microsoft.com/en-us/windows/win32/api/evntrace/nf-evntrace-starttracew), [routes Windows](https://learn.microsoft.com/en-us/windows/win32/api/netioapi/ns-netioapi-mib_ipforward_row2).

Candidat chargé : `build/battlestation-network-scenes-05`. Le pointeur de démarrage conserve `build/battlestation-dualsense-touch-05` jusqu’à acceptation de cette tranche. Le sélecteur présente les cinq interfaces configurées de ce PC et retire les couches WFP/QoS sans adresse. Les courbes ne sont plus redessinées pendant le détail par application. La refonte de toutes les scènes est appliquée sans copie des anciens plans, conformément à la dernière consigne d’Ayo. **Coordination terminée : l’Aquarium et LoL sont livrés avec leurs modèles, voir [Aquarium et LoL](AQUARIUM_LOL.md).**
