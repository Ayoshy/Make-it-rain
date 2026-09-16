# Contrat du projet Battlestation

## Application personnelle et périmètre

Battlestation est une application personnelle pour Ayo, sur son PC Windows et
ses périphériques. Elle n'est pas destinée à une utilisation ou distribution
publique. Développer pour cet environnement et ses usages réels : pas de matrice
de compatibilité, de généralisation à d'autres utilisateurs ou de mécanismes
prévus pour des besoins hypothétiques. Ne pas développer ni tester les cas limites
d'un produit public. Traiter les incidents rencontrés et les risques concrets pour
ses données, ses sessions et son matériel ; le caractère personnel ne diminue pas
l'exigence de rendu et de fluidité.

Tout développement se fait ici. Préserver les projets Conrad Sensor, Codex Meter
et le fond sous steamapps, y compris leurs états non committés.
Lire README.md puis les documents utiles à la tâche. Vérifier Git, les worktrees,
les changements présents et les processus concernés avant intervention.
Les sources et l'état live décrivent l'implémentation actuelle ; les instructions
utilisateur définissent le résultat attendu. Les comptes rendus datés, sauvegardes
et captures sont des preuves historiques, pas des consignes de chantier actives.
Marquer les anciennes consignes de coordination comme terminées lorsqu'elles le sont.

## Bureau, rendu et interactions

Application .NET autonome dockée au niveau du bureau, derrière les fenêtres
normales, avec widgets cliquables. Préserver Win+D, le focus et la disposition
physique 2 × 2560 × 1440. Un export ou une fenêtre de comparaison ne valide pas
le bureau réel.

Chaque bloc peut être déplacé et redimensionné sur la grille, retiré puis réajouté
dans un espace libre ; la disposition est sauvegardée. Réutiliser les composants
et matériaux communs (`Surface`, `DockAppearance`, styles partagés) et conserver
les interactions existantes. Tout réglage visuel doit agir de façon perceptible
sur le composant affiché, pas seulement sur une valeur sauvegardée.
Le rendu glass reste commun ; couleurs et ambiance peuvent évoluer avec les
thèmes, sans réimplémenter les docks ni créer un moteur générique par anticipation.

L'interface ne se décrit pas elle-même : libellés fonctionnels, données et aide
nécessaire, sans slogans ni sous-titres décoratifs. Effets visibles et soignés,
icônes nacrées et boutons liquid glass. Le compteur Vice City reste indépendant
du monitoring et retirable après la sortie du jeu ; sa date est un réglage
utilisateur conservé, pas une assertion vérifiée. Journal, sessions de suivi et
minuteur restent supprimés.

La fluidité fait partie du résultat. Aucune opération lente sur le fil d'interface.
Réutiliser les ressources de rendu et suspendre les animations et traitements
devenus inutiles quand un dock est masqué, sans pénaliser l'écran encore visible.

## Sessions et données personnelles

Le terminal utilise ConPTY et le contrôle de rendu natif de Windows Terminal.
Son hôte .NET vit séparément. Le bureau peut être rechargé pendant une
implémentation pour activer et vérifier les changements ; cela n'autorise jamais
la fermeture des hôtes ou onglets actifs. Vérifier les chemins et rôles des
processus, puis cibler seulement le bureau ; ne jamais tuer tous les processus
`Battlestation.exe`. Retirer le bloc terminal masque ses sessions sans les fermer.
Les anciens hôtes restent en place jusqu'à fermeture explicite par l'utilisateur ;
une session déjà en cours ne se transfère pas vers ConPTY.

Les réglages actifs sont dans `%LOCALAPPDATA%\Battlestation`. Préserver dispositions,
profils personnels, applications et préférences ; les valeurs initiales du repo
ne doivent pas les écraser. Une nouvelle fonctionnalité ou migration conserve les
choix existants. Vérifier la sauvegarde et le retour à la disposition personnelle
quand une modification touche aux profils.

## Méthode de travail et QA

Respecter la portée demandée : une correction d'icône reste locale. Choisir une
implémentation directe adaptée à cet usage personnel ; ne pas ajouter de couches,
dépendances ou options sans besoin concret. Préserver le travail concurrent.
En cas de délégation, borner la tâche ; le principal relit le diff, vérifie les
preuves et reste responsable de l'intégration. La délégation ne vaut pas validation.

Vérifier les comportements touchés et les régressions plausibles sur ce PC.
Pas de revalidation intégrale pour une modification locale, ni de tests exhaustifs
de configurations étrangères à cet usage. Ajouter ou maintenir des tests utiles
pour les comportements importants et incidents réels ; éviter les tests qui ne
font que recopier l'implémentation. Distinguer tests de régression maintenus,
outils temporaires et preuves avant tout nettoyage ; ne pas supprimer `tests/`
en bloc au titre des fichiers temporaires.

Pour un changement d'interaction ou de rendu, vérifier sur le bureau les gestes
concernés : clics, survol, déplacements, redimensionnements ou transitions.
Les commandes internes et rendus WPF isolés ne remplacent pas les clics réels ni
la fluidité perçue. Distinguer compilation, tests automatisés, rendu isolé, essai
sur le bureau et confirmation utilisateur ; préciser ce qui reste non vérifié.
Si un contrôle exige l'utilisateur, ne lui demander que l'observation manquante.
Une confirmation déjà obtenue pour la même version et le même comportement suffit.

Pour les changements de rendu ou de sondes, mesurer les processus techniques
concernés, helpers compris, dans des conditions comparables. Ne pas déduire un
gain du nombre d'icônes ; ne pas imposer un benchmark aux changements sans impact
sur les performances. Mettre à jour la documentation utile et rapporter l'état Git
réel, la version chargée et les limites de validation. Ne pas transformer
AGENTS.md en journal de livraison ou en roadmap des futurs docks.

## Livraison et retrait des anciens builds

Compiler chaque candidat dans un dossier distinct sous `build/`, sans écraser un
build existant. Le script de build ne change pas `build/current.txt` ; `-NoActivate`
reste accepté pour les commandes existantes. Charger explicitement le candidat
pour les essais en préservant les sessions.

Après validation des comportements concernés et acceptation de la version,
aligner `build/current.txt` sur le build livré et vérifier les cibles du raccourci
et du démarrage Windows. Une compilation réussie seule ne promeut pas un build.
Ne pas laisser le lanceur sur une ancienne version après livraison. Le pointeur
désigne le prochain lancement ; le chemin des processus identifie la version
chargée. Ne pas choisir un build par son nom ou sa date seuls.

Retirer le build remplacé de `build/` : l'archiver sous
`backups/retired-builds/<date>/` par défaut, ou le supprimer sur demande explicite.
Avant déplacement ou suppression, vérifier les chemins absolus, processus,
modules chargés et références persistantes, notamment le pont vidéo. Un build
encore utilisé reste en place jusqu'à libération ; noter le report sans fermer
les sessions. Le lanceur refuse les archives et ne choisit pas silencieusement
un build par défaut si le pointeur manque. Aucun nettoyage de l'historique Git,
des autres anciens builds ou des dossiers de référence sans demande explicite.

## Confidentialité et matériel

Ne jamais lire, copier ou afficher d'identifiants Codex. Les associations privées
`conrad-connection.js`, `codex-connection.js` et `wallpaper-token.txt` restent hors
des sources et de Git. Les caches d'usage ne conservent que des compteurs. Les
sorties du terminal ne sont pas journalisées. Aucun appel consommant du crédit ;
ne jamais envoyer automatiquement un prompt ou une image pour tester le terminal.
Absence != zéro ; modèle inconnu != tarif de substitution.

Avant de modifier les réglages GPU ou de fermer Conrad ou un helper GPU, relever
les consignes GPU et l'état de Canicule. Pour tout essai matériel, relever puis
restaurer et vérifier les seuls réglages concernés. Les tests de bornes ne
remplacent pas l'essai sur le matériel concerné et sa restauration vérifiée.
