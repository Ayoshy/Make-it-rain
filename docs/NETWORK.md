# Dock Réseau

État d'intégration au 22 septembre 2026 : rémanence, tri par débit total,
contraste des barres et couleurs réception/envoi sont présents dans le bureau
chargé `battlestation-shopping-glass-01`. Les anciens noms de candidats cités
plus bas décrivent leurs essais historiques, pas le pointeur de démarrage actuel.
Les 17 contrôles Réseau et DockReview ont été relancés avec succès avant publication.

Le dock affiche les débits descendant et montant de l’interface choisie, deux courbes sur la dernière minute et la latence ICMP vers une cible. La cible initiale est `1.1.1.1` ; elle se consulte et se modifie par le bouton `⋯`. Cette latence concerne la cible indiquée, pas un serveur de jeu.

Le titre, le nom de la connexion, la latence et le bouton de réglage partagent
une seule ligne. Les données et le détail par application remontent de 20 px,
avec la même hauteur de bandeau commune que Disques. Les courbes bénéficient
de la place récupérée ; le bouton inférieur conserve sa position.

L’interface automatique suit la route Windows par défaut, avec la métrique de route et celle de l’interface. Le sélecteur permet de choisir une interface configurée explicitement. Les compteurs sont lus chaque seconde sur un worker ; l’affichage est interpolé. Une première mesure, une interface indisponible ou une absence de réponse ne deviennent pas des valeurs nulles fictives.

## Détail par application

Le bouton lance `Battlestation.NetworkHelper.exe` avec la demande Windows. Le bureau reste non privilégié. Un refus conserve les courbes et la latence ; le détail reste inactif et peut être redemandé.

Le helper utilise Microsoft TraceEvent 3.2.6, une session temps réel `Battlestation.Network` et un canal local limité à l’utilisateur. Il compte uniquement les événements d’envoi/réception TCP et UDP, IPv4 et IPv6. Le PID est lu dans le payload décodé de l’événement, jamais déduit aveuglément de l’en-tête.

Les processus du même chemin d’exécutable sont regroupés ; les cinq applications sont sélectionnées et triées à chaque mesure par débit total décroissant (réception + envoi). Une application sans trafic attribué reste visible pendant quatre secondes avec des débits à zéro, en bas de liste, puis disparaît ; une application nouvellement active peut immédiatement remplacer une ligne inactive. Cette rémanence absorbe les courtes pauses de trafic et de livraison ETW sans rejouer les anciens compteurs. Ouvrir le détail ne déclenche pas de lecture supplémentaire entre deux mesures. Une suspension ou une erreur de collecte ne conserve pas ces lignes.

Ce détail couvre le trafic du PC, toutes interfaces confondues. Aucun contenu réseau, destination ou fichier ETL n’est conservé. Seuls les noms et compteurs agrégés sont transmis au bureau.

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

Historique de mise en place : `build/battlestation-network-scenes-05` a introduit le sélecteur des cinq interfaces configurées de ce PC, sans les couches WFP/QoS dépourvues d’adresse, et l’arrêt du dessin des courbes pendant le détail. La refonte des scènes et sa coordination sont terminées ; voir [LoL](LOL.md).

Le 21 septembre 2026, `build/battlestation-network-remanence-02` est chargé et sélectionné pour le démarrage. Ayo a confirmé la stabilité et le délai de quatre secondes sur le premier candidat ; le tri final réception + envoi décroissant a ensuite été vérifié par les tests et sur huit relevés réels. Les tests couvrent aussi la priorité du trafic montant et le maintien des zéros en bas. Voir [la revue et les limites de validation](REVIEW_2026-09-21.md).

À la suite du retour sur le contraste, le candidat `build/battlestation-network-contrast-01` est chargé pour essai : les barres seules passent de 9 % à 44 % d’opacité, avec les couleurs de chaque thème conservées. Compilation et rendus WPF vérifiés ; appréciation du contraste sur le bureau en attente. Le démarrage conserve `network-remanence-02` jusqu’à cette validation, sans archivage de ce build accepté.

Les libellés et les débits reprennent la couleur de leur courbe : « ↓ RÉCEPTION » avec l’accent descendant (`Active[3]`) et « ↑ ENVOI » avec l’accent montant (`Active[0]`), comme dans le détail par application. Candidat `build/battlestation-network-colors-01` ; compilation et rendu WPF vérifiés, essai sur le bureau en attente.
