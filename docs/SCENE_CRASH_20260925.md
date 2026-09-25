# Disparition pendant Jeu → Bureau, 25 septembre 2026

## Preuve du crash signalé à 14:09

Le bureau `battlestation-headers-01`, PID 17000, reçoit `Jeu -> Bureau;
palette=True` à 14:09:33.483. Le journal atteint `scene-editing-cleared` à
14:09:33.664, sans `scene-docks-applied` ni arrêt propre.

Windows enregistre une violation d'accès `0xc0000005` dans `wpfgfx_cor3.dll`
(.NET Desktop 10.0.11), décalage `0xf0212`. Le dump local
`%LOCALAPPDATA%\CrashDumps\Battlestation.exe.17000.dmp` a été analysé avec
DbgEng et le PDB correspondant téléchargé sur le serveur de symboles Microsoft.

Pile résolue du thread de rendu :

```text
CMilSlaveResource::NotifyOnChanged+0x32
CMilSlaveResource::NotifyOnChanged+0xe4
CMilSolidColorBrushDuce::ProcessUpdate+0x12c
CComposition::ProcessCommandBatch
CCrossThreadComposition::ProcessBatches
CComposition::Compose
CPartitionThread::RenderPartition
```

L'accès invalide lit l'adresse `0x48`. Le minidump ne contient pas le contenu
des objets de brosse concernés : il n'identifie pas un dock précis ni l'origine
de la référence invalide. Une erreur native du thread de rendu n'est pas captée
par `DispatcherUnhandledException`. L'ancien `error.txt` du 22 septembre ne
décrit pas cet incident.

## Essai de transition et limite de reproduction

L'essai IPC sur le bureau headers-01, PID 2092, a été interrompu lors du retour
Jeu → Bureau vers 14:18:42 ; disparition du processus confirmée par l'utilisateur,
qui a relancé le bureau. La palette n'était pas ouverte. Aucun second dump ou
événement Windows correspondant n'a été trouvé pendant cette investigation :
ne pas affirmer que cette seconde disparition a exactement la même exception.
Un premier essai avait seulement échoué sur la fermeture du client pipe, avec
processus encore vivant ; cette erreur IPC ne constituait pas un crash.

Les tests sur le bureau ont été suspendus après la relance utilisateur. Le
processus chargé ensuite est `battlestation-disks-auto-01`, PID 15024 ; le
terminal historique PID 19224 est resté présent. Le pointeur demeure headers-01
au dernier relevé. Aucun verdict Atelier ni changement de pointeur effectué ici.

## Candidat scene-brush-01

Hypothèse ciblée : les mises à jour animées des brosses de thème chevauchent le
redimensionnement et le masquage/réaffichage des fenêtres WPF de la scène.
La pile native rend cette piste pertinente ; elle ne prouve pas encore que
l'animation est la cause première.

Le candidat applique directement les couleurs finales lors de `ApplyAppearance`.
Le fondu général et la cascade de `SceneTransition` sont conservés. Les aperçus
de réglages conservent leur animation de couleur. Une sélection immédiate du
même thème termine aussi les animations déjà en cours. Les surfaces suivent ce
choix pour leur fondu d'images.

- Build complet distinct : `build/battlestation-scene-brush-01`.
- Suite scènes : **164 contrôles réussis**, dont quatre nouveaux contrôles de
  conservation des brosses liées, couleurs finales et interruption d'une animation.
- Aucun réglage GPU modifié ; aucun passage global en rendu logiciel.
- Le candidat n'a pas été chargé sur le bureau pendant cette investigation.
- Le geste réel, l'efficacité contre le crash et les performances du candidat
  restent à vérifier. Ne pas considérer l'incident résolu sur la seule compilation.

Preuves et outil d'analyse temporaire :
`artifacts/validation/scene-crash-20260925-141402/`.
Le dump reste local ; aucun contenu de dump n'a été envoyé au serveur de symboles.

## Vérification du build disks-clean-01 chargé

Après signalement utilisateur du nouveau build, le bureau chargé est
`build/battlestation-disks-clean-01`, PID 19496 ; l'hôte terminal historique
PID 19224 reste présent. Ce build contient déjà le correctif de transition.
Les empreintes SHA-256 des documents sources du PDB (`Station.cs`,
`DesktopTheme.cs`, `Surface.cs`) sont identiques à celles du candidat
`scene-brush-01` et des sources corrigées actuelles. Les méthodes attendues
sont également présentes dans l'assembly compilé. Preuve locale :
`artifacts/validation/scene-crash-20260925-141402/disks-clean-verification.jsonl`.

Aucune recompilation ni relance supplémentaire nécessaire pour intégrer ce
correctif : le build Disques ouvert l'embarque déjà. Aucun nouvel essai de
transition sur le bureau n'a été déclenché ici ; le geste utilisateur
Jeu → Bureau et l'efficacité contre le crash restent à confirmer sur ce build.
Les 164 contrôles automatisés cités plus haut ne remplacent pas cet essai.
Au relevé, `build/current.txt` désigne toujours `battlestation-headers-01`.
La conservation du build chargé reste l'action utilisateur **Garder cette
version** dans Atelier ; aucun verdict ni changement de pointeur effectué ici.
