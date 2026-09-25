# Contrat du projet Battlestation

## Périmètre

Battlestation est l'application personnelle d'Ayo : un bureau Windows modulaire
pour son PC (deux dalles 2560 × 1440, la gauche est l'écran principal et de jeu)
et ses périphériques. Elle n'est ni destinée ni adaptée à une distribution
publique : pas de matrice de compatibilité, pas de généralisation à d'autres
utilisateurs, pas de mécanismes pour besoins hypothétiques. Le caractère
personnel ne diminue pas l'exigence de rendu et de fluidité.

Tout développement se fait dans ce dépôt. Lire README.md puis les documents
utiles à la tâche. Les sources et l'état live décrivent l'implémentation ; les
instructions d'Ayo définissent le résultat attendu ; les comptes rendus datés,
sauvegardes et captures sont des preuves historiques, pas des consignes actives.

## État du travail (25 septembre 2026)

- `main` propre, poussée sur GitHub. Version conservée : `build/current.txt` →
  `battlestation-wallpapers-fable-01`. Le chemin des processus identifie la
  version réellement chargée ; les PID sont périssables, revérifier en début de
  session.
- 21 docks livrés, dont les récents Disques et Gmail. Le compteur Vice City
  reste indépendant du monitoring et retirable ; sa date est un réglage
  utilisateur, pas une assertion vérifiée. Journal, sessions de suivi et
  minuteur sont retirés et ne reviennent pas.
- Scènes : quatre modèles intégrés (Bureau, Jeu, Multimédia, Mono écran) au
  format 7 de `profiles.json` (refonte du 25/09 : Disques/Terminal/Projets/Vidéo
  dans chaque scène ; en Jeu, rien de lié au jeu sur l'écran principal).
  Voir docs/THEMES_SCENES.md.
- Chantiers ouverts : la revue du 25/09 (docs/REVIEW_2026-09-25.md) liste les
  lots restants et les questions pour Ayo ; le lecteur ARAM Mayhem
  (`handover.txt`, `tools/MayhemReader`, `build/mayhem-reader-*`) est un projet
  concurrent actif à préserver.
- Préserver aussi les projets Conrad Sensor, Codex Meter et le fond sous
  steamapps, y compris leurs états non committés. Vérifier Git, worktrees,
  changements présents et processus concernés avant intervention.

## Bureau, rendu et interactions

Application .NET autonome dockée au niveau du bureau, derrière les fenêtres
normales, avec widgets cliquables. Préserver Win+D, le focus et la disposition
physique 2 × 2560 × 1440. Un export ou une fenêtre de comparaison ne valide pas
le bureau réel.

Chaque bloc se déplace et se redimensionne sur la grille, se retire puis se
réajoute ; la disposition est sauvegardée par scène. Réutiliser les composants
et matériaux communs (`Surface`, `DockAppearance`, styles partagés) et conserver
les interactions existantes. Tout réglage visuel doit agir de façon perceptible
sur le composant affiché, pas seulement sur une valeur sauvegardée. Le rendu
glass reste commun ; couleurs et ambiance évoluent avec les scènes, sans
réimplémenter les docks ni créer un moteur générique par anticipation.

L'interface ne se décrit pas elle-même : libellés fonctionnels, sans slogans ni
sous-titres décoratifs. Effets visibles et soignés, icônes nacrées, boutons
liquid glass.

La fluidité fait partie du résultat. Aucune opération lente sur le fil
d'interface. Réutiliser les ressources de rendu ; suspendre animations et
traitements inutiles quand un dock est masqué, sans pénaliser l'écran visible.

## Sessions et données personnelles

Le terminal utilise ConPTY et le rendu natif de Windows Terminal ; son hôte
.NET vit séparément. Recharger le bureau pendant une implémentation est permis ;
fermer des hôtes ou onglets actifs ne l'est jamais. Vérifier chemins et rôles
des processus, cibler seulement le bureau ; ne jamais tuer tous les
`Battlestation.exe`. Retirer le bloc terminal masque ses sessions sans les
fermer ; les anciens hôtes restent en place jusqu'à fermeture explicite par
Ayo ; une session en cours ne se transfère pas vers ConPTY.

Les réglages actifs sont dans `%LOCALAPPDATA%\Battlestation`. Préserver
dispositions, profils, applications et préférences ; les valeurs initiales du
repo ne les écrasent pas. Une nouvelle fonctionnalité ou migration conserve les
choix existants (et sauvegarde avant, cf. `scene-backups/`). Vérifier le retour
à la disposition personnelle quand une modification touche aux profils.

## Méthode de travail et QA

Respecter la portée demandée : une correction d'icône reste locale. Choisir
l'implémentation directe adaptée à cet usage personnel ; pas de couches,
dépendances ou options sans besoin concret. Préserver le travail concurrent.
En cas de délégation, transmettre un contrat court et autonome (objectif,
fichiers, contraintes, preuves attendues) ; le principal relit le diff, vérifie
les preuves et reste responsable. La délégation ne vaut pas validation.

Limiter le contexte au nécessaire : chercher avec `rg` avant de lire les plages
utiles ; éviter fichiers entiers, inventaires et journaux volumineux quand
quelques résultats suffisent. Borner les sorties d'outils sans masquer erreurs
ni preuves. Réutiliser les lectures et validations encore valables. Pour un
sujet indépendant, préférer une nouvelle conversation avec un bref état des
décisions et points ouverts.

Vérifier les comportements touchés et les régressions plausibles sur ce PC,
sans revalidation intégrale pour une modification locale ni tests de
configurations étrangères à cet usage. Maintenir des tests utiles pour les
comportements importants et incidents réels ; éviter ceux qui recopient
l'implémentation. Ne pas supprimer `tests/` en bloc.

Pour un changement d'interaction ou de rendu, vérifier sur le bureau les gestes
concernés : clics, survol, déplacements, redimensionnements, transitions. Les
commandes internes et rendus WPF isolés ne remplacent ni les clics réels ni la
fluidité perçue. Distinguer compilation, tests automatisés, rendu isolé, essai
sur le bureau et confirmation utilisateur ; dire ce qui reste non vérifié. Si
un contrôle exige Ayo, ne demander que l'observation manquante ; une
confirmation déjà obtenue pour la même version et le même comportement suffit.

Pour les changements de rendu ou de sondes, mesurer les processus concernés,
helpers compris, dans des conditions comparables ; ne pas déduire un gain du
nombre d'icônes ni imposer un benchmark à un changement sans impact
performance. Mettre à jour la documentation utile et rapporter l'état Git réel,
la version chargée et les limites de validation. AGENTS.md n'est ni un journal
de livraison ni une roadmap : sa section d'état reste courte et datée.

## Livraison et retrait des anciens builds

Compiler chaque candidat dans un dossier distinct sous `build/`, sans écraser
un build existant. Le script de build ne change pas `build/current.txt` ;
`-NoActivate` reste accepté. Charger explicitement le candidat pour les essais
en préservant les sessions. Une compilation réussie seule ne promeut rien.

L'acceptation ou le rejet d'une version passe par le dock **Atelier** : lire
son état, ne solliciter aucun verdict parallèle dans le chat, ne jamais
fabriquer un verdict ni cliquer à la place d'Ayo. **Garder cette version** vaut
acceptation et aligne lui-même le prochain lancement ; ne pas redemander une
validation du même build. Une disposition ou un réglage ne nécessite pas
d'essai Atelier. **Nettoyer les builds** est la demande explicite de
suppression des builds inutilisés ; utiliser son contrôle des références, sans
fermer de session.

Après livraison, aligner `build/current.txt`, vérifier raccourci et démarrage
Windows, puis retirer le build remplacé : archive sous
`backups/retired-builds/<date>/` par défaut, suppression sur demande explicite.
Avant tout déplacement, vérifier chemins absolus, processus, modules chargés et
références persistantes (dont le pont vidéo) ; un build encore utilisé reste en
place. Le lanceur refuse les archives et ne choisit pas de build par défaut si
le pointeur manque. Aucun nettoyage de l'historique Git, des autres builds ou
des dossiers de référence sans demande explicite. Ne pas choisir un build par
son nom ou sa date seuls.

## Confidentialité et matériel

Ne jamais lire, copier ou afficher d'identifiants Codex. `conrad-connection.js`,
`codex-connection.js` et `wallpaper-token.txt` restent hors des sources et de
Git. Les caches d'usage ne conservent que des compteurs ; les sorties du
terminal ne sont pas journalisées. Aucun appel consommant du crédit ; ne jamais
envoyer automatiquement un prompt ou une image pour tester le terminal.
Absence ≠ zéro ; modèle inconnu ≠ tarif de substitution.

Avant de modifier les réglages GPU ou de fermer Conrad ou un helper GPU,
relever les consignes GPU et l'état de Canicule. Pour tout essai matériel,
relever puis restaurer et vérifier les seuls réglages concernés ; les tests de
bornes ne remplacent pas l'essai sur le matériel réel et sa restauration
vérifiée.
