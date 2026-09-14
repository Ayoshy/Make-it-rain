# Pages internes de Conrad et Codex

Comportement et implémentation des transitions internes des deux docks.
Consulter VALIDATION.md pour le build réellement chargé et les limites des essais.

## Utilisation

Conrad possède les pages Résumé, 6 cœurs et Refroidissement ; Codex les pages
Résumé, Quotas et Modèles. Clic sur un bouton : sa page remplace le contenu central.
Reclic sur le bouton actif : retour au résumé. Le contenu sort à droite pendant
que son remplaçant arrive de gauche pour le glissement ; les variantes utilisent
un déplacement plus court ou restent sur place. Glissement et fondu durent 300 ms ;
défocalisation et pixels durent 380 ms pour rendre leurs phases distinctes.

Le cadre, le titre, le statut et les trois boutons restent fixes. Le bouton actif
est nacré. Canicule et Actualiser conservent leurs actions propres. Les listes
Quotas et Modèles défilent à la molette dans la zone centrale ; une jauge discrète
indique leur position. La position de chaque liste est conservée entre les pages
et bornée à la quantité de données disponible.

Les six cœurs tiennent en grille 3 × 2 dès la taille minimale. Le refroidissement
présente les deux curseurs côte à côte ; Appliquer reste visible. Ses modifications
locales survivent aux changements de page. Les mesures reçues ne remplacent pas
un brouillon modifié. Seul Appliquer envoie ses consignes ; la navigation n’écrit
pas sur le GPU. Les données absentes restent indisponibles.

Quatre effets passent chacun une fois par cycle mélangé, indépendamment pour
chaque dock. Il n’y a pas de répétition immédiate à la jonction de deux cycles :

- Glissement de référence.
- Fondu accompagné d’un déplacement de 18 pixels.
- Défocalisation sur place jusqu’à 10 pixels : l’ancien contenu se brouille avant
  de disparaître, puis le nouveau retrouve sa netteté.
- Dissolution sur place en petits fragments dispersés : chaque groupe disparaît,
  puis se recompose avec les informations de la nouvelle page.

Le choix est fait une fois au départ. Un clic rapide conserve l’effet engagé
jusqu’à sa fin accélérée. Aucun réglage ou menu supplémentaire n’est nécessaire.

## Rendu et intégration

- `DashboardTransition.cs` contient deux `DrawingVisual`, un conteneur découpé et
  deux transformations animées. Les dessins restent vectoriels en mémoire.
- Fondu et flou utilisent des masques d’opacité animés ; le flou moderne WPF
  s’applique uniquement au contenu. Les pixels utilisent au maximum 224 cellules
  réparties en 12 groupes, soit seulement 24 horloges d’opacité pour les deux
  masques vectoriels, sans capture bitmap par image. Les masques, effets
  et leurs horloges sont retirés à la fin ou à l’annulation.
- Les animations WPF déplacent les dessins sans invalider le dashboard à chaque
  image. Les mesures suivent toujours les révisions introduites par l’optimisation.
- Un clic pendant le mouvement remplace la destination en attente et raccourcit
  une fois le mouvement courant à 90 ms depuis sa position actuelle. Les callbacks
  sont versionnés ; les anciennes complétions ne peuvent pas changer la page.
- Les boutons fixes restent actifs. Les actions du contenu mobile sont neutralisées,
  y compris les callbacks d’anciennes zones cliquables et les captures de curseur.
- Fin, retrait, occultation, déchargement et redimensionnement libèrent les horloges.
  Un masquage retire aussi les dessins enfants et les zones de verre de l’affichage.
- `DesktopWorkspace` n’agrandit plus les fenêtres de monitoring. La commande interne
  `drawer:1..4` reste compatible et sélectionne maintenant une page.
- Le seul raccordement au dessin partagé rend `Surface.SetDisplayed` redéfinissable
  pour masquer aussi les deux dessins conservés. Les autres surfaces sont inchangées
  par ce raccordement.

## Vérification

`DashboardPageTests.cs` est exécuté dans la suite Responsive avec les vraies
surfaces WPF et un backend factice incapable de lancer une application, un terminal
ou une écriture GPU. Il vérifie les déplacements des deux dessins, l’absence de
  redessin par image, les clics rapprochés, le retour, les dimensions, les contrôles
neutralisés, le brouillon, les listes et l’arrêt des animations. Les PNG sont des
rendus isolés ; ils ne constituent pas une preuve du bureau docké ou de clics réels.
Les quatre variantes sont aussi exercées de façon déterministe sur un hôte WPF :
pixels intermédiaires distincts sur des cartes avec du texte, image finale complète,
quatre effets par cycle sans répétition à la jonction, flou visible et borné,
cellules partageant leurs horloges, suppression des effets et masques temporaires.

```powershell
dotnet run --project tests/Battlestation.Responsive.Tests.csproj -c Release
.\scripts\Build-Battlestation.ps1 -OutputDirectory build/battlestation-dock-variants-distinct -NoActivate
```

Le build ci-dessus ne change pas le pointeur de démarrage et ne recharge pas le
bureau. Une activation doit suivre les vérifications des sessions terminal et des
consignes GPU/Canicule du contrat projet.
