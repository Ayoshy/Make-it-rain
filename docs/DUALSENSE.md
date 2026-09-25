# Dock DualSense

## Utilisation

Le dock suit les boutons, sticks et gâchettes de la DualSense d’Ayo en Bluetooth. La coque nacrée reprend ses proportions, avec des réactions lumineuses à la pression. Le modèle grandit avec le dock, garde ses proportions et réserve la place des axes détaillés. La vibration USB a été retirée à la demande d’Ayo.

**Axes bruts** affiche les valeurs SDL signées et leur écart au centre, sans calibration ni correction. Bouger chaque stick une fois permet à SDL de transmettre ensuite les petites variations. Le dessin est légèrement lissé ; les valeurs détaillées restent brutes.

**Traînée tactile** active le suivi de deux doigts sur le pavé : traces aux couleurs du thème, points lumineux et disparition progressive en 0,85 seconde. Les contacts restent uniquement en mémoire et sont effacés lorsque le dock est suspendu.

Ce suivi nécessite les rapports Bluetooth améliorés de SDL. Ayo a explicitement autorisé leur essai le 15 septembre 2026, après information sur leur incompatibilité avec DirectInput et le redémarrage nécessaire pour revenir au mode simple. Le choix global `DualSenseTouchTrail` est conservé dans les préférences, indépendamment des scènes ; il est désactivé par défaut pour les données qui ne le contiennent pas.

Désactiver **Traînée tactile** remet SDL en mode compatible pour la prochaine ouverture. Il faut ensuite **éteindre et rallumer la manette** pour que le périphérique quitte réellement son mode amélioré. Le dock affiche ce rappel. Aucun contrôle de vibration, LED ou gâchette adaptative n’est proposé ; l’activation du protocole amélioré passe par l’initialisation SDL.

## Lecture et rendu

- SDL3 3.4.16 utilise un worker dédié, sans vidéo SDL ni fenêtre de jeu.
- Le dock masqué, occulté ou en réorganisation suspend lectures et animations.
- Batterie SDL si disponible, sinon mesure Windows du lecteur Bluetooth déjà présent ; une absence reste « — ».
- Images haute définition du châssis et des textures préparées sur un thread STA en arrière-plan, puis figées et partagées avec WPF. Les contrôles, halos et traînées restent animés.
- Rendu plafonné à 60 Hz, seulement quand un bouton, un stick, le pavé ou une onde bouge : une manette connectée au repos ne redessine plus ; aucun réglage graphique ou matériel externe modifié.
- Le retrait du dock et les scènes ne ferment aucun terminal.

Le dock reste initialement masqué dans les dispositions personnelles. Le format de scènes 3 subdivise l’ancien grand compteur du modèle Jeu sauvegardé s’il n’est pas actif, avec la manette à gauche et le compteur à droite. Les autres docks restent intacts. Si Jeu est actif à la migration, l’ajout passe par le menu habituel.

## Validation du candidat tactile

Candidat chargé : `build/battlestation-dualsense-touch-05`.
Version acceptée par Ayo ; le lanceur, le raccourci et le démarrage Windows ciblent cette version.

- Ayo a confirmé les commandes simultanées dans GTA V et le dock, la reconnexion, le focus et Win+D en mode simple, puis le rendu et la fluidité du skin 03.
- Candidat 05 : compilation complète native et .NET réussie, 46 contrôles de scènes/préférences réussis et tests des docks réussis.
- Rendus contrôlés à 480 × 380, 654 × 382, 720 × 440, 936 × 480 et 1692 × 994, avec caches haute définition préparés hors du fil d’interface.
- Deux traînées indépendantes testées : aucun raccord entre contacts séparés, expiration complète, absence de coordonnées inventées en mode compatible.
- Lecture réelle en mode amélioré confirmée : deux doigts, coordonnées distinctes, batterie SDL 45 % lors du relevé.
- Dispositions et redimensionnements personnels conservés au rechargement ; hôte terminal 12876 et identités des sessions préservés.
- Ayo a confirmé le tactile et l’absence de différence en jeu sur **Brotato**, utilisé à la place de GTA V pour cet essai en mode amélioré. Computer Use est indisponible (`native pipe unavailable / os error 2`).

Les mesures de processus concernent des scènes et tailles successivement modifiées par l’utilisateur ; elles ne permettent pas d’attribuer un gain CPU au seul changement de rendu. Les relevés sont conservés sous `backups/dualsense-refresh-20260915-204118/` et `backups/dualsense-touch-20260915-212400/`. Les rendus des tests sont sous `artifacts/validation/dock-review/`.

Aucun commit ni publication Git automatique.

## Vérification native

```powershell
# Lecture en mode simple, lorsque le dock SDL n’est pas déjà actif.
python tests/Read-DualSense.py build/battlestation-dualsense-touch-05
# --enhanced est réservé à un essai explicitement autorisé.
```

SDL3 est téléchargé pour le build par `scripts/Get-Sdl3.ps1`, version et SHA-256 fixés. Le runtime et sa licence sont copiés dans chaque candidat ; aucun service ni installation globale.

Références : [rapports améliorés SDL](https://wiki.libsdl.org/SDL3/SDL_HINT_JOYSTICK_ENHANCED_REPORTS), [pavés tactiles SDL](https://wiki.libsdl.org/SDL3/SDL_GetNumGamepadTouchpads), [pilote PS5](https://github.com/libsdl-org/SDL/blob/release-3.4.16/src/joystick/hidapi/SDL_hidapi_ps5.c), [référence visuelle Sony](https://www.playstation.com/fr-fr/accessories/dualsense-wireless-controller/).

La tranche est acceptée, rapports améliorés et traînées conservés selon le choix explicite d’Ayo. Le build thèmes/scènes précédemment sélectionné a été archivé après contrôle des processus, modules et pont vidéo. L’ancien build `battlestation-app-restore-final` reste nécessaire aux sessions terminal existantes.
