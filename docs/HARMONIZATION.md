# Apparence commune des docks

La base WPF `Surface` dessine une seule fois le cadre et enregistre son rectangle
auprès du moteur natif. Chaque widget fournit son contenu et ses interactions.
Le cadre suit le même cycle de redimensionnement, masquage et réaffichage.
Le compteur Vice City reste une surface sans cadre, avec son habillage propre.

`DockAppearance` conserve les choix partagés :

- Segoe UI Variable Text pour l'interface ; en-têtes à 10 points, couleur atténuée.
- Bahnschrift pour les mesures, températures et compteurs techniques.
- Art déco pour les grands chiffres de l'horloge et du compteur Vice City.
- Boutons nacrés : mêmes dégradés normal/survol et même rayon de 15 px.
- Cadres de 24 px, également fixes dans `NativeBackground.cpp`, quelle que soit
  la taille du dock. L'opacité du verre reste le réglage utilisateur existant.

Les onglets conservent leur couleur de bordure et leurs indicateurs d'activité.
Leur fond partage les dégradés des autres boutons. Leurs titres fixent
explicitement leur famille de police, sans dépendre d'un héritage WPF implicite.

`GlassMenus.xaml`, enregistré dans les ressources de l'application, fournit
les styles WPF ContextMenu, MenuItem et Separator. Audio, clic droit des docks,
onglets, ajout de blocs et dispositions les héritent sans redessiner chaque menu.
Le même panneau nacré translucide sert aux sous-menus ; survol, coches, entrées
désactivées et séparateurs n'utilisent plus le fond blanc du thème Windows.
Ouverture, navigation et activation restent celles des composants WPF.
Il s'agit d'un matériau dessiné, pas d'un effet système de réfraction du bureau.

La popup Projets conserve sa réfraction du contenu des cartes : c'est une couche
superposée, distincte du cadre commun. Le menu de notification WinForms et les
applications externes ne sont pas des menus WPF et ne reçoivent pas ce template.
Les anciens hôtes terminal en cours ne sont jamais fermés pour mettre à jour leur
habillage interne.

Les rendus isolés et les preuves du bureau chargé sont distingués dans
`artifacts/validation/harmonization/` et dans `VALIDATION.md`.
