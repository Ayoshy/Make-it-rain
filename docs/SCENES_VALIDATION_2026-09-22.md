# Validation des scènes — 22 septembre 2026

## Version retenue et coordination

Ayo a accepté les fonds, effets et l'agencement Jeu de scènes-tri-03 le 22 septembre.
Pendant cette acceptation, le chantier Shopping/Network a chargé et sélectionné
build/battlestation-shopping-glass-01. Cette version est conservée, sans retour
à scènes-tri-03 : dernier bureau observé PID 25216, scène Bureau, terminal PID 2196.
Le pointeur de démarrage et le processus sont alignés sur shopping-glass-01.

La comparaison des sections .text, .rdata et .data de Battlestation.Graphics.dll
entre les deux builds donne des SHA256 identiques : code du rendu, constantes et
données initiales sont inchangés. Le fichier personnel contient les quatre scènes ;
Jeu conserve exactement les cinq docks et dimensions du secondaire validé.
La nouvelle version parallèle reprend donc le rendu accepté et sa persistance.
Les comportements Shopping/Network ajoutés par l'autre chantier ne sont pas
revalidés dans cette livraison des scènes.

La tâche Windows Battlestation appelle Start-Battlestation.ps1 -AtLogon et lit
ce pointeur. Aucun raccourci Battlestation trouvé dans les bureaux et menus
Démarrer utilisateur/public. Le pont vidéo PID 17976 et son manifeste pointent
toujours sur battlestation-cache-tabs-08 ; aucune intervention dessus.
Aucun hôte terminal ou onglet fermé. Aucun build déplacé : le retrait est différé
pour éviter une collision avec le chantier parallèle qui pilote le build actif.

Git : `main`, HEAD `6594f69`, un seul worktree ; changements préexistants
nombreux et conservés, notamment Achats et Montagne. Pas de commit ni push.
Le candidat inclut les corrections Achats présentes lors de sa compilation ;
des modifications concurrentes ultérieures de ShoppingSurface/ShoppingService
ne sont pas présentées comme faisant partie de ce binaire.

## Preuves

- Compilation native/.NET réussie dans un dossier neuf.
- 167 contrôles scènes réussis, dont une migration sur copie des préférences réelles.
- Rotation GTA : 300 secondes visibles, fondu 2 secondes, boucle complète et pause.
- Persistance du compteur vérifiée dans les tests natifs et sur le bureau :
  53,634668 s à l'arrêt propre, puis 54,3205 s après relancement.
  Fichier de preuve : `artifacts/scenes-live-wallpaper-reload.json`.
- Effets photo : comparaison de pixels avec/sans effets au même instant et au même
  cadrage ; différences présentes sur chacun des deux écrans.
- Rendus et preuves natifs : `artifacts/themes-20260922-171317-101/`.
- Jeu inspecté en fonctionnement : secondaire = Terminal, Vidéo, DualSense,
  Réseau, LoL, et aucun autre bloc visible.
- Après passage par Jeu et Multimédia, Bureau, Multimédia et Mono écran sont
  strictement égaux à leurs instantanés précédant le réagencement de Jeu :
  `artifacts/scenes-other-layouts-preserved.json`.
- La variante Lucia est identique à sa génération acceptée, SHA256 :
  `68F7933716789CF324E1F640739C60BCF5B03DEE07FB01620B12D245E3F68AE3`.
- `git diff --check` réussi.

Le réagencement de Jeu a été appliqué avec le bureau fermé après une vérification
sur copie. Sauvegarde live :
`%LOCALAPPDATA%/Battlestation/scene-backups/jeu-secondary-20260922-172204-252708/`.
L'outil ponctuel et sa copie d'essai restent dans `artifacts/Apply-GameScene.py`
et `artifacts/game-layout-dry-run/` ; ce ne sont pas des mécanismes de migration
supplémentaires de l'application.

## Mesures et limites

Relevés de 15 secondes sur Bureau, hôtes/helpers inclus :
`artifacts/scenes-game-before-performance.json` (nom de fichier historique,
la scène réellement active était Bureau) et
`artifacts/scenes-bureau-warm-after-performance.json`.

| Mesure | Référence shopping-max-01 | Candidat tri-03 stabilisé |
| --- | --- | --- |
| CPU du bureau, pourcentage d'un cœur | 52,76 % | 36,67 % |
| Mémoire de travail du bureau | 493,2 Mo | 412,6 Mo |
| Temps moyen de dessin natif | 1,923 ms | 1,312 ms |
| GPU global, trois échantillons | 38 / 30 / 30 % | 29 / 28 / 22 % |

Ces relevés ne constituent pas une mesure de gain attribuable : des travaux
Achats, applications au premier plan et rechargements étaient concurrents.
Le premier relevé du candidat était encore en échauffement ; le tableau utilise
le second, stabilisé. Le GPU provient de nvidia-smi et concerne tout le PC,
pas uniquement Battlestation. Les compteurs GPU WMI étant trop lents, leur lecture
a été interrompue sans toucher aux applications ni aux réglages matériels.

Captures du bureau : `%LOCALAPPDATA%/Battlestation/inspection/dotnet-scenes-*-03-screen.png`.
Le navigateur recouvrait le secondaire sur la capture Jeu : celle-ci ne prouve
donc pas la lisibilité de ses cinq docks. Les placements sont confirmés par
l'inspection live et les tests, pas par des clics physiques.
Ayo a confirmé le rendu et l’usage de cette version. Aucune automatisation physique
n’a été reprise après l’interruption Computer Use du tour précédent : les gestes
et la fluidité perçue reposent sur cette confirmation utilisateur, distincte des
tests et de l’inspection. Lucia et les deux nouvelles passes sont acceptées.
