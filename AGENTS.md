# Contrat du projet Battlestation

Tout développement se fait ici. Préserver les projets Conrad Sensor, Codex Meter
et le fond sous steamapps, y compris leurs états non committés.
Lire README.md puis docs/VALIDATION.md ; vérifier les processus, les fichiers
chargés et les preuves actuelles sans confondre snapshot et état live.

Battlestation est une application .NET autonome dockée au niveau du bureau,
derrière les fenêtres normales et avec les widgets cliquables. Un export ou une
fenêtre de comparaison ne valide pas cette cible. Préserver Win+D, le focus, les
effets animés et la disposition physique 2 × 2560 × 1440.

Le bureau est modulaire : chaque bloc peut être déplacé sur la grille, retiré puis
réajouté dans un espace libre. La disposition est sauvegardée. Le compteur Vice
City est indépendant du monitoring et peut être supprimé après la sortie du jeu.
La date du compteur est un réglage utilisateur conservé, pas une assertion vérifiée.
Journal, sessions de suivi et minuteur restent supprimés. Interface sobre : effets
visibles, peu de texte d'aide, icônes nacrées et boutons liquid glass.

Le terminal utilise ConPTY et le contrôle de rendu natif de Windows Terminal.
Conserver les sessions utilisateur lors d'un rechargement du bureau : l'hôte .NET
du terminal vit séparément. Ne jamais fermer un onglet actif pour une mise à jour.
Les anciens terminaux encore ouverts sont à préserver jusqu'à fermeture explicite
par l'utilisateur. Une session déjà en cours ne se transfère pas vers ConPTY.

Ne jamais lire, copier ou afficher d'identifiants Codex. Les associations privées
conrad-connection.js, codex-connection.js et wallpaper-token.txt restent hors des
sources et de Git. Les caches d'usage ne conservent que des compteurs. Les sorties
du terminal ne sont pas journalisées. Aucun appel consommant du crédit ; ne jamais
envoyer automatiquement un prompt ou une image pour tester le terminal.
Absence != zéro ; modèle inconnu != tarif de substitution.

Avant de fermer Conrad ou un helper GPU, capturer les consignes et Canicule.
Les tests de bornes ne remplacent pas un essai matériel et sa restauration vérifiée.
Les commandes internes et tests WPF ne remplacent pas les clics réels.

Pas de bascule au démarrage Windows avant validation complète. Mesurer tous les
processus techniques ; ne pas déduire un gain du nombre d'icônes. Les sauvegardes
historiques et les preuves locales ne sont pas les sources actives. Aucun nettoyage
de l'historique Git ni des dossiers de référence sans demande explicite.
