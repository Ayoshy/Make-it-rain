# Validation — migration ouverte

## Acquis

- Rainmeter 4.5.26.3894 x64 existant ; deux écrans confirmés : (0,0,2560,1440) et
  (2560,0,2560,1440), primaire à gauche.
- Sauvegarde SHA-256 de 113 fichiers, y compris changements non committés et
  intégrations non suivies. Aucun reset, clean ou écrasement des sources.
- Overrides effectifs récupérés du config.json Wallpaper Engine.
- Build natif et .NET ; vrais modules chargés dans Rainmeter et vrai enfant Codex
  app-server. Ni ConradSensor.exe ni CodexMeter.exe lancés par le plugin.
- CPU, six cœurs, GPU, charges, ventilateur et watts réels. Lecture du contrôle GPU
  réelle dans le volet refroidissement, mais aucune écriture matérielle essayée.
- Quota réel, fenêtre principale hebdomadaire et absence de secondaire respectée ;
  crédits visibles seulement en lecture. Détails de 29 couples modèle/effort lus.
- Rendu WebView2 hors écran 5120 × 1440, DPR 1. Animation démontrée par deux captures
  et matrices de transformation différentes. Les images sont des preuves ponctuelles,
  pas un remplacement du fond animé.
- Comparaison des captures dashboard, refroidissement et modèles avec les références.
  Les différences de position/échelle dans les volets suivent le rétrécissement
  adaptatif déjà présent dans les sources pour garder tous les panneaux sur l'écran.
- Quatre volets exercés par DOM : un seul ouvert à la fois, x >= 2560, x + largeur
  <= 5120, haut >= 0, bas <= 1440, Conrad au-dessus de Codex sans chevauchement.
  **Ces tests ne sont pas des clics réels dans Rainmeter.**
- 13 tests de contrats : absences, limites matérielles, tarif inconnu, conservation
  des modèles non tarifés, absence d'extrapolation, session traversant minuit.
- Relance de Rainmeter et nouveau chargement effectif du plugin vérifiés.

## Blocages et travail encore requis

| Élément | État |
|---|---|
| Contrôle des applications Windows par l'agent | Échec persistant après réinitialisation : Computer Use native pipe is unavailable, os error 2 |
| Hébergement réel derrière les icônes | À implémenter et valider ; le test actuel est une fenêtre technique hors écran |
| Clics réels, curseurs, focus, Afficher le bureau | Non validés ; ne pas remplacer ce contrôle par les tests DOM |
| Commandes GPU / UAC / Canicule / restauration | Sources portées, tests de limites seulement ; essais réels requis, après capture du réglage courant |
| Arrêt des anciens contrôleurs pendant un essai indépendant | Non effectué ; ils restent actifs pour préserver le bureau |
| Mode jeu | Détection et politique de masquage/pause non implémentées ; la prévisualisation tourne tant qu'elle reste ouverte |
| Performances de la solution complète | Non acquises ; prévisualisation avec ancien bureau simultané seulement |
| Démarrage Windows et reprise de session | Non configurés avant parité ; les anciens démarrages sont conservés |
| Bascule / suppression de la dépendance aux trois applications | Non effectuée |

L'indisponibilité de Computer Use ne signifie pas que Rainmeter exige une correction
de permissions. Un message Rainmeter « Rainmeter.ini n'est pas modifiable » a été
causé par une invocation groupée incorrecte de l'agent ; l'instance correspondante a
été identifiée par sa ligne de commande et fermée seule. Le vrai INI est resté intact.

## Ressources : observation provisoire, pas benchmark garanti

Échantillon de 20 s du rendu animé hors écran avec ancien bureau simultané : environ
4,21 % CPU normalisé sur tous les processeurs logiques, 1182 MiB de working sets
additionnés, 1446 MiB privés. Cela inclut Rainmeter, son app-server et les descendants
WebView2. Les pages partagées peuvent être comptées dans plusieurs working sets.
Le premier scan des sessions a aussi entraîné un pic mémoire du processus .NET.
Le scan initial est réutilisé via un cache d'agrégats lors des lectures suivantes.

Ne pas comparer directement cette mesure aux repères historiques. Il faut encore
des échantillons reproductibles au repos, en actualisation et en jeu, avec inventaire
complet des processus/services et mesures GPU. Le script de mesure conserve les
instances GPU par processus et moteur séparément, sans fabriquer un pourcentage
global à partir d'une addition de moteurs différents.

## Dernière passe, cache chaud

Après relance et réutilisation du cache, 20 s de rendu animé hors écran : 4,27 % CPU,
692 MiB de working sets cumulés, 863 MiB privés. Durant une actualisation demandée
par une commande Rainmeter : 4,39 % CPU, 701 MiB de working sets, 878 MiB privés.
L'ancien bureau tournait toujours en parallèle. Les instantanés GPU par moteur et
mémoire dédiée sont dans les JSON de mesures ; ce ne sont pas des moyennes de GPU.
La commande a avancé la date de lecture des quotas sans changer le nombre de crédits.
Le contrôle par bouton avec un clic réel reste à vérifier.

## GPU initial et vérification finale

Lecture du 13 septembre 11:58 : Auto, consigne thermique 83 °C, Canicule false,
bornes ventilateur 38–100 %, cible 65–88 °C. Le pourcentage observé varie normalement
en Auto. Le fichier conrad-live.json de la sauvegarde conserve cet état.
Aucune commande de réglage n'a été envoyée durant les validations ci-dessus.

## État laissé en fin de séance

Le plugin final et la sonde sont installés, mais la sonde est déchargée. Rainmeter a
été relancé pour libérer .NET et tous les processus de prévisualisation. Aucun
lecteur Codex enfant du nouveau Rainmeter et aucun module ViceCity/coreclr ne reste
chargé. L'ancien bureau (Wallpaper Engine, Conrad et son helper, Codex Meter) est
toujours actif. `handoff-state.json` conserve la vérification. Les fichiers installés
du plugin et de sa bibliothèque correspondent aux empreintes du dernier build.
