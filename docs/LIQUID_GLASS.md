# Liquid glass commun

Référence acceptée : le menu Projets, avec **les deux effets ensemble** :
liseré coloré sur la cible interactive et reflet lumineux localisé suivant la
souris sur le cadre. Le survol est une indication d'interaction, pas d'activité.

- `Surface.Button` applique ce traitement aux commandes dessinées. Les couleurs
  fonctionnelles restent disponibles, notamment celles des lanceurs Projets.
- `Surface.HoverGlass` couvre les cibles sans bouton permanent : applications,
  Bluetooth, rappels, onglets Achats, météo et curseurs. Un contrôle indisponible
  ne reçoit pas ce traitement. Les indications de connexion et d'activité restent
  indépendantes du survol.
- `Surface.GlassReflection` porte le reflet sur les cadres de dock, le menu
  Projets et le lecteur vidéo. Le contenu vidéo reste intact.
- `DockAppearance.Reflection` conserve une seule brosse de lumière ; les dessins
  sont translatés vers le pointeur. Aucun flou supplémentaire ni minuteur : les
  événements souris déclenchent le rendu, et masquer une surface efface le pointeur.
- Les boutons WPF des onglets terminal utilisent `GlassHoverBorder` et la même
  lumière. Un ancien hôte conserve son code jusqu'à sa fermeture explicite ;
  aucune session n'est fermée pour mettre à jour cet habillage.

Les titres de dock utilisent `Surface.Header` : **GTAArtDeco Condensed** embarquée,
14 points, espacement 1,8 et couleur `Ink`, comme Atelier. Les titres du menu
Projets utilisent la même police. Les noms des cartes, données, onglets de session
et boutons gardent leur police de lecture. Les docks dépourvus de titre n'en
reçoivent pas artificiellement. Les cartes Projets restent cliquables sans chevron.

Les rendus WPF et tests de cibles vérifient le dessin et les interactions internes.
Ils ne prouvent pas les clics réels, Win+D, le focus ni la fluidité perçue sur le
bureau. Vérifier ces observations sur le candidat chargé avant de le conserver.
