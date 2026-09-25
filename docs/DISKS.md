# Disques

Le dock **Disques** s'ajoute depuis **Réorganiser → Ajouter**. Il utilise le
cadre liquid glass commun et les palettes de la scène active.

- Accueil : volumes fixes et amovibles prêts, nom, espace libre et capacité.
  Les lignes horizontales affichent une petite jauge autour du SSD. Les grandes
  cartes affichent une jauge centrale et le pourcentage occupé. Une seule ligne
  « utilisé / capacité » indique les tailles. Elle reprend la couleur de la
  jauge : cyan/vert doux, puis ambre et corail à mesure que le disque se remplit.
  Aucun compteur de disques ni détail supplémentaire en pied de carte.
  Chaque carte indique l'analyse en cours, l'attente, ou l'heure du dernier scan
  terminé. « Scan partiel » signale les accès refusés ou chemins non suivis.
- Clic sur un volume : entrée animée dans sa mosaïque.
- Molette avant : zoom dans le dossier sous le pointeur ; arrière : niveau précédent.
- Clic gauche sur un dossier : ouverture dans l'Explorateur. Sur un fichier,
  ouverture du dossier qui le contient ; aucun fichier n'est exécuté.
- Les boutons du bandeau remontent, reviennent aux disques ou relancent l'analyse.
- Le clic droit conserve le menu du bureau existant ; aucun menu propre aux disques.

Les transformations WPF animent des dessins conservés pendant 350 ms, sous un
découpage fixe. Le cadre natif reste immobile. Une transition correspond à un
niveau ; les impulsions de molette reçues pendant celle-ci sont ignorées.
Redimensionnement et masquage arrêtent les transitions.
Les indications de navigation ne s'affichent plus en infobulle.

Le bandeau tient sur une ligne : titre, récapitulatif ou chemin, et boutons de
navigation. Le contenu commence à 50 px du haut, soit 38 px récupérés. Un chemin
trop long utilise une seconde ligne de 26 px ; le redimensionnement réévalue ce
besoin sans réserver cette hauteur aux autres vues.

## Données et activité

`DiskIndex` parcourt les métadonnées dans un worker. Aucune lecture du contenu des
fichiers, élévation ou suppression. Dès que le dock est actif, les volumes détectés
sans index commencent leur première analyse, même depuis l'accueil. Trois scans
complets peuvent avancer en parallèle ; les suivants attendent une place. Sur ce
PC, C:, D: et E: sont trois SSD physiques distincts. Les dossiers de premier niveau
apparaissent progressivement lorsqu'on entre dans un volume encore en analyse.
Chaque volume déjà ouvert garde son index et son suivi en mémoire jusqu'à la
fermeture de Battlestation. Revenir à l'accueil ou changer de disque ne déclenche
aucune analyse complète : la sélection retrouve immédiatement son dernier arbre.
Un scan commencé continue même si l'on passe à un autre volume.
Il n'y a pas de cache entre deux lancements de Battlestation.
L'heure affichée correspond à la fin du dernier scan complet, même si son résultat
est partiel ; les deltas ne changent pas cette date. Une nouvelle analyse complète
n'est pas déclenchée parce que cette date est ancienne.

Les notifications Windows sont regroupées toutes les 2,5 secondes. Seuls les
dossiers concernés et leurs ancêtres sont réévalués ; leurs sous-arbres inchangés
sont réutilisés. Ce suivi reste actif pour les volumes déjà analysés, même lorsque
l'accueil ou un autre volume est affiché. Un débordement de notifications entraîne
une nouvelle analyse du seul volume concerné ; le bouton ↻ la demande explicitement.
Le masquage suspend l'énumération, mais conserve les notifications pour la reprise.
La liste et l'espace libre sont relus toutes les cinq secondes quand le dock est
actif. Aucune promesse de délai maximal pendant une grosse copie ou l'analyse initiale.

La mosaïque représente les **tailles logiques des fichiers**, pas une mesure exacte
des blocs physiques alloués : compression, fichiers creux et liens physiques
peuvent expliquer un écart avec l'espace occupé du volume. Les points de réanalyse
ne sont pas suivis et les refus d'accès sont marqués comme analyse partielle.
Chaque dossier conserve ses sous-dossiers et ses 96 plus gros fichiers ; le reste
est regroupé dans **Autres fichiers**, sans perdre ses octets du total.

## Vérification

`dotnet run --project tests/Battlestation.Disks.Tests.csproj -c Release` vérifie
les surfaces proportionnelles, le suivi réel des fichiers d'une fixture, le
masquage/reprise, la conservation des index, les deltas hors sélection, l'absence
de lectures lors des allers-retours et les transitions internes. Les PNG sous
`artifacts/validation/disks/` sont des rendus WPF isolés : ils ne prouvent ni les
gestes réels ni la fluidité perçue sur le bureau.
