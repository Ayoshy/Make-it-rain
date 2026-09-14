# Revue de Battlestation — 14 septembre 2026

Cet audit décrit l’état antérieur aux corrections. Leur livraison et les limites de validation sont documentées dans [VALIDATION.md](VALIDATION.md).

La base dispose de bonnes protections fonctionnelles, mais la consommation minimale et la résistance aux pannes ne sont pas encore acquises. Les corrections ci-dessous sont à traiter avant de multiplier les docks et les effets. Revue des sources présentes, y compris les changements non committés ; aucune correction applicative ni bascule du bureau réalisée.

**Constats classés.** P1 : correction prioritaire ; P2 : défaut à corriger dans la prochaine tranche de consolidation. Les conséquences déduites du code sont distinguées des mesures réellement effectuées.

1. **[P1] Le démarrage Windows donne durablement la priorité High au bureau.** [Start-Battlestation.ps1:40](../scripts/Start-Battlestation.ps1#L40), [Install-Startup.ps1:24](../scripts/Install-Startup.ps1#L24).

   Avec `-AtLogon`, le lanceur élève tout le processus, sans retour ultérieur à Normal. La tâche installée a effectivement `Priority=1`. Sous contention CPU, les tâches de rendu et de collecte peuvent donc passer devant les applications ordinaires. C'est contraire à l'objectif d'un bureau qui laisse des ressources au travail et aux jeux. Le processus live 9788 est actuellement Normal : ce défaut n'explique pas sa mesure actuelle, mais s'applique au prochain lancement par ce chemin. Conserver Normal pour l'interface et abaisser les travaux de fond appropriés ; ne pas modifier la priorité des shells utilisateur. Microsoft recommande de réserver High aux travaux critiques brefs : [Scheduling Priorities](https://learn.microsoft.com/en-us/windows/win32/procthread/scheduling-priorities).

2. **[P1] Une perte de cible Direct2D arrête définitivement le fond et le verre.** [NativeBackground.cpp:171](../src/Battlestation.Native/NativeBackground.cpp#L171), [NativeBackground.cpp:223](../src/Battlestation.Native/NativeBackground.cpp#L223).

   `EndDraw` passe par `Check`, puis le catch extérieur termine le thread et détruit sa fenêtre. Il n'existe aucune reconstruction des ressources ni reprise. En cas de `D2DERR_RECREATE_TARGET`, le processus WPF peut rester vivant avec des widgets privés de leur fond. Le handle de thread reste également non nul, empêchant `Start` de relancer seul le moteur. Traiter cette erreur dans la boucle, libérer les ressources dépendantes du périphérique et les recréer avec une reprise bornée. C'est le traitement documenté par Microsoft : [EndDraw](https://learn.microsoft.com/en-us/windows/win32/api/d2d1/nf-d2d1-id2d1rendertarget-enddraw). Chemin de panne confirmé dans le code, sans provoquer de reset GPU sur la machine.

3. **[P1] Une erreur d'écriture d'un diagnostic peut interrompre une fonction essentielle.** [DesktopWorkspace.cs:210](../src/Battlestation/DesktopWorkspace.cs#L210), [Backend.cs:46](../src/Battlestation.Core/Backend.cs#L46).

   Toutes les cinq secondes, `host-state.json` est écrit synchroniquement sur le thread WPF, sans gestion locale des erreurs. Une IOException remonte au gestionnaire global qui journalise sans marquer l'exception comme traitée. Côté capteurs, l'échec d'écriture ou de remplacement de `probe.json` sort de toute la boucle `ReadSensors` ; celle-ci ne redémarre jamais, même lorsque le disque redevient disponible. Découpler les diagnostics des fonctions applicatives, regrouper leurs écritures sur un worker et tolérer les erreurs avec reprise espacée. Tester une écriture refusée, puis le retour à la normale. Ce sont des conséquences de contrôle de flux, pas des pannes injectées dans les données utilisateur.

4. **[P2] Tous les docks visibles sont redessinés quatre fois par seconde, même inchangés.** [DesktopWorkspace.cs:209](../src/Battlestation/DesktopWorkspace.cs#L209), [Surface.cs:42](../src/Battlestation/Surface.cs#L42).

   La boucle commune invalide météo, applications, projets, compteurs et autres surfaces sans comparer leur état. À chaque passage, `Paint` reconstitue les commandes de dessin et les zones cliquables. Les brosses et stylos sont recréés ; le Typeface est construit avant même de vérifier le cache de texte ; les images déjà en cache passent encore par `File.Exists`. Le coût permanent croît avec le nombre de docks et la complexité de chacun. Passer aux mises à jour sur changement, avec une cadence propre aux éléments temporels, conserver les animations actives, et réutiliser les ressources graphiques stables. Les animations de survol existantes savent déjà s'arrêter : conserver ce comportement.

5. **[P2] Le spectre audio appelle encore les périphériques depuis le thread de rendu WPF.** [DesktopWorkspace.cs:61](../src/Battlestation/DesktopWorkspace.cs#L61), [DeskSurface.cs:24](../src/Battlestation/DeskSurface.cs#L24), [Spectrum.cs:25](../src/Battlestation/Spectrum.cs#L25).

   Le timer Render de 33 ms appelle `Spectrum.Start`. Toutes les deux secondes, cette méthode construit un `MMDeviceEnumerator` et demande la sortie par défaut ; lors d'un changement, elle arrête et recrée la capture sur le même thread. `StopRecording` et `Dispose` y passent aussi. Le déplacement d'AudioMixer sur son worker ne couvre donc pas cette voie : un pilote lent ou un changement de sortie peut encore figer les interactions. Confier le cycle de vie de cette capture à un worker propriétaire, publier uniquement les bandes et réagir aux notifications de périphériques. Aucun temps de blocage particulier n'est attribué à cette voie sans profilage dédié.

6. **[P2] Le miroir Stremio attend le GPU et réduit les images sur le thread de l'interface.** [VideoCapture.cpp:82](../src/Battlestation.Native/VideoCapture.cpp#L82), [VideoCapture.cs:25](../src/Battlestation/VideoCapture.cs#L25).

   `VideoSurface.Update` appelle `VideoRead`, qui exécute `CopyResource`, puis `Map(READ, 0)`, puis une réduction pixel par pixel sur CPU avant de recopier vers WPF. La texture source complète est transférée avant la réduction à 720 pixels. Le plafond de sortie ne plafonne donc pas le transfert GPU/CPU. Sous charge graphique ou avec une source agrandie, cette attente se répercute sur tous les docks. Réduire sur GPU avant lecture, déplacer la capture hors du dispatcher et publier la dernière image disponible dans un tampon borné ; réévaluer une texture partagée si les mesures le justifient. [Contrat de Map](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-map). Ce chemin n'était pas actif dans le relevé CPU ; son surcoût n'y a pas été mesuré.

7. **[P2] Retirer Projets laisse tourner son balayage récursif.** [Desk.cpp:107](../src/Battlestation.Native/Desk.cpp#L107), [Desk.cpp:133](../src/Battlestation.Native/Desk.cpp#L133), [DesktopWorkspace.cs:147](../src/Battlestation/DesktopWorkspace.cs#L147).

   Le thread natif parcourt les sous-dossiers de tous les projets, jusqu'à un million d'entrées, puis recommence après une minute. Il ignore la visibilité du dock. Le contrôle de visibilité côté WPF arrête seulement le polling `ProjectSignals`, pas ce scanner. Avec des dépôts volumineux, des accès disque et des réveils persistent même après retrait du bloc. Ajouter un état d'activation au scanner, une attente interruptible et une actualisation incrémentale/coalescée ; garder une réconciliation complète espacée pour les événements manqués. Le thread a déjà une priorité de fond, ce qui ne supprime pas son travail.

8. **[P2] Le rendu couvert n'a pas de politique cohérente pour les deux écrans.** [NativeBackground.cpp:174](../src/Battlestation.Native/NativeBackground.cpp#L174), [DesktopWorkspace.cs:209](../src/Battlestation/DesktopWorkspace.cs#L209).

   La seule pause automatique du fond repose sur la fenêtre au premier plan couvrant le second écran. Si les deux écrans sont couverts et que le premier possède le focus, le fond complet 5120 × 1440 continue à être dessiné. À l'inverse, une fenêtre plein écran sur le second suspend aussi les effets encore visibles sur le premier. Les rafraîchissements WPF et l'analyse audio ne suivent pas cette pause. Déterminer les zones réellement utiles par écran et coordonner les producteurs d'effets avec cet état ; conserver les effets sur l'écran découvert. Ajouter aussi une suspension explicite de l'animation lors du verrouillage de session. Les scénarios d'occultation n'ont pas été exercés en modifiant le bureau utilisateur pendant cette revue.

9. **[P2] Un client connecté mais silencieux monopolise le pipe de contrôle.** [ControlPipe.cs:20](../src/Battlestation/ControlPipe.cs#L20).

   Le serveur traite une connexion à la fois et attend une ligne avec seulement le token d'arrêt global. Un client bloqué avant son saut de ligne empêche les commandes suivantes du bureau ou du terminal d'être servies. Le contrôle de 256 caractères intervient après la lecture complète et ne borne pas son accumulation. Utiliser un délai par connexion, une lecture réellement bornée et une écriture annulable, puis accepter le client suivant. Le contrôle `CurrentUserOnly` est utile et doit rester ; il ne résout pas le blocage d'un client légitime défaillant. À vérifier avec un pipe isolé et un client silencieux, sans toucher aux sessions utilisateur.

10. **[P2] La validation de l'extension vidéo dépend d'un environnement local non reproductible.** [browser-video.test.cjs:1](../tests/browser-video.test.cjs#L1), [Validate.ps1:3](../scripts/Validate.ps1#L3).

    Le test importe Playwright depuis `artifacts/browser-tests/node_modules`, sans manifeste de dépendances versionné pour ce test, et n'est pas exécuté par `Validate.ps1`. Son lancement dans l'état actuel échoue avec `MODULE_NOT_FOUND`. La suite .NET BrowserVideo vérifie le transport, pas l'exécution du content script. Déclarer et verrouiller cette dépendance, prévoir une commande d'installation reproductible, puis intégrer le test ou annoncer explicitement son exclusion dans le résultat global.

**Mesures actuelles et périmètre.** Le relevé de 20 secondes précède les tests de cette revue. Les métadonnées et compteurs de tous les hôtes/helpers et descendants ont été inventoriés, ainsi que Brave et Stremio. Les descendants incluent aussi des applications utilisateur et des outils de développement concurrents : leur consommation ne doit pas être attribuée entièrement aux docks.

| Processus Battlestation | CPU en % d'un cœur logique | Mémoire privée, Mio |
| --- | ---: | ---: |
| Bureau 9788, fluid-presets-final | 45,13 | 531,5 |
| Hôte terminal 22176, palette-settings | 0,39 | 226,5 |
| Hôte historique 6312, single-header | 0,00 | 144,9 |
| Helper de métadonnées 19772 | 0,00 | 24,5 |
| Pont vidéo 24184, video-smooth | 0,00 | 11,1 |
| Total de ces cinq processus | 45,52 | 938,5 |

Windows expose six processeurs logiques dans ce relevé : 45,52 % d'un cœur correspond à environ **7,59 % du CPU total**. Ces nombres ne constituent ni un benchmark au repos contrôlé, ni une mesure de fuite mémoire, ni une prévision de gain. Les compteurs GPU WMI n'ont pas répondu dans un délai raisonnable ; la requête a été interrompue. Aucun chiffre GPU, wattage ou FPS applicatif n'est revendiqué.

Les modules chargés du bureau proviennent de `build/battlestation-fluid-presets-final`. `build/current.txt` cible toujours `build/battlestation-palette-settings`. Les hôtes terminal et le pont conservent leurs autres builds : leurs comportements ne doivent pas être assimilés aux nouvelles sources. Aucun processus utilisateur arrêté, aucune mise à jour de terminal, aucun changement de consigne GPU, aucun appel de génération.

**Vérification et qualités à conserver.** Build Release managé : zéro erreur, zéro avertissement. Les douze suites de `Validate.ps1` et les contrats C++ passent. Le test JavaScript échoue au chargement de sa dépendance, avant d'exercer le navigateur. Les essais ConPTY utilisent leurs propres shells ; les tests WPF ne constituent pas des clics physiques. La capture vidéo native, le reset GPU, les pannes disque et les blocages IPC ne sont pas validés par ce passage.

Le code préserve correctement plusieurs invariants importants : hôte terminal séparé, maintien des sessions au masquage, données absentes distinctes du zéro, tarifs inconnus conservés comme inconnus, buffers vidéo bornés, absence de transcription persistée, sauvegardes principales par remplacement, tests de disposition et worker audio avec regroupement des mouvements de curseur. L'application compile avec les références nullables activées. Une refonte générale n'est pas justifiée par cette revue.

Pour l'extension future, centraliser les informations minimales de chaque dock — identité, surface, emplacements de verre, activation de ses services et besoin de rafraîchissement. Elles sont aujourd'hui réparties entre plusieurs dictionnaires/switches et le tableau natif de 16 panneaux. Ce point constitue une préparation architecturale, pas un défaut de capacité déjà rencontré avec les onze blocs actuels.

L'ordre proposé est de corriger les P1, supprimer le travail inutile à état inchangé, sortir la capture audio/vidéo du dispatcher, puis mesurer à disposition et activité identiques. Comparer bureau visible, écrans couverts, blocs retirés et médias actifs, avec CPU de chaque processus, mémoire privée, allocations, GPU et latence des interactions. Ne fixer un objectif chiffré de gain qu'après ce relevé contrôlé.

Preuves locales de cette revue : [compteurs de processus](../artifacts/validation/code-review/process-metrics.json), [fenêtres et géométrie](../artifacts/validation/dotnet-code-review-windows.json), [validation](../artifacts/validation/code-review/validation.log), [build](../artifacts/validation/code-review/build.log).
