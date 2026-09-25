# Lecteur Mayhem : options et prochain essai

Étude du 25 septembre 2026, à la reprise de `handover.txt`.
L'utilisateur a ensuite confirmé jouer en anglais et choisi la capture + OCR.
Le dispositif est décrit dans [tools/MayhemReader](../tools/MayhemReader/README.md).
La comparaison ci-dessous conserve le cadrage de l'étude initiale.
La [première preuve en partie](LOL_READER_VALIDATION.md) confirme désormais
sept trios et cinq changements de carte, avec une couverture limitée par le stockage.
Les preuves précédentes restent dans [LOL_AUGMENT_RESEARCH.md](LOL_AUGMENT_RESEARCH.md).

## Résultat de la reprise

- La capture API Brand n'a pas établi les offres ou leurs rerolls. Les quatre
  améliorations prises viennent de la fin de partie. Ne pas recommencer le même
  polling sans hypothèse nouvelle.
- Overwolf Electron a effectivement bloqué GEP faute d'authentification valide.
  Aucun essai avec clé valide ni événement Mayhem reçu.
- Aucun processus de jeu ou de client LoL trouvé lors de cette reprise.
- Bureau chargé : `battlestation-ai-meter-01`, PID 16740 ; prochain lancement
  identique. Hôte terminal : `terminal-bg-01`, PID 19224. Rien rechargé.
- Git : `main`, un worktree, nombreux changements préexistants conservés.
  La présente reprise ajoute uniquement ce document.

## Comparaison

Appréciations d'effort et de maintenance qualitatives, non mesurées. Aucune
approche non validée ici ne bénéficie d'une garantie de risque de ban nul.

| Piste | Faisabilité et fiabilité | Confidentialité / compte | Effort et maintenance | Décision proposée |
| --- | --- | --- | --- | --- |
| Capture du jeu + OCR Windows local | Prototype de vitesse existant ; trois noms et rerolls non démontrés. Langue, animations et cadrage à éprouver. | Images locales limitées aux cartes ; aucune lecture mémoire ni entrée de jeu dans le dispositif proposé. Pas de garantie d'approbation Riot. | Petit essai possible ; entretien du catalogue et du cadrage. | Première piste à tester. |
| Reconnaissance des icônes | Non testée ; ambiguïtés et effets visuels inconnus. | Même périmètre visuel local que l'OCR. | Préparer les références et mesurer les confusions. | Complément si les titres échouent, sur le même corpus. |
| Overwolf Electron / Native | Données adaptées documentées ; accès effectif bloqué dans notre essai Electron. Native non testé. | Composants tiers ; réduction de télémétrie différente de sa suppression. | Intégration simple seulement une fois l'accès réel obtenu ; dépendance fournisseur. | Garder ouverte, sans nouveau test identique sans clé. |
| Application existante avec export | Aucun export des trois offres établi. Un overlay fonctionnel ne prouve pas une interface pour Battlestation. | À examiner application par application. | Recherche bornée d'une interface documentée avant installation. | Secondaire. |
| API / journaux / accessibilité | Résultats précédents négatifs et bornés ; accessibilité non confirmée pendant les cartes. | Lectures locales ; couverture utile toujours inconnue. | Petit test d'accessibilité synchronisé envisageable, pas une nouvelle partie de polling identique. | Seulement avec hypothèse précise. |
| Semi-manuel | Peut aider à lire une image à la demande ; ne remplit pas l'autonomie voulue. | Local dans la variante proposée. | Faible effort, interruptions répétées. | Repli seulement. |
| Mémoire du jeu | Aucun prototype ni preuve ; mécanisme et compatibilité inconnus. | Incertitude Vanguard/compte non résolue. | Maintenance potentiellement élevée par patch. | Pas retenue pour ce prochain essai. |

## Vérification de Mayhem Overlay

Code examiné au commit `57c23850cc67c7b3394666ea62f5a1ebe02d0641` :
[ocr.js](https://github.com/zp96-cmd/mayhem-overlay/blob/57c23850cc67c7b3394666ea62f5a1ebe02d0641/src/main/ocr.js),
[ocr-match.js](https://github.com/zp96-cmd/mayhem-overlay/blob/57c23850cc67c7b3394666ea62f5a1ebe02d0641/src/main/ocr-match.js),
[main.js](https://github.com/zp96-cmd/mayhem-overlay/blob/57c23850cc67c7b3394666ea62f5a1ebe02d0641/src/main/main.js).

- OCR Tesseract configuré en anglais ; capture de l'écran principal puis découpe
  centrale. Ce n'est pas une capture limitée à la fenêtre du jeu.
- Lecture périodique lorsque l'offre est attendue ou active : intervalles
  configurés de 2,2 s / 1,3 s, avec boucle de 300 ms et exclusion des scans
  concurrents. Ce ne sont pas des latences mesurées sur notre PC.
- Le curseur quittant la zone de reroll accélère aussi la prochaine lecture.
  La description historique d'un fonctionnement uniquement lié au curseur
  ne décrit donc plus tout le code examiné.
- Une offre peut être acceptée avec deux correspondances de score >= 0,7.
  Le choix est inféré après deux lectures négatives et un survol récent.
  Ces heuristiques ne prouvent ni trois identifiants exacts ni le choix effectué.
- `package.json` déclare MIT ; aucun fichier LICENSE/LICENCE trouvé dans
  l'arbre examiné. Aucun code repris et aucune dépendance installée ici.

L'ancien `OcrProbe.cpp` mesure durée, mémoire et nombre de lignes ; il
n'exporte pas les textes reconnus. Le catalogue local contient 554 entrées,
avec notamment `All For You` et `FireFox`. L'utilisateur joue en anglais :
ce catalogue convient à la langue recherchée, avec vérification des noms en jeu.

## Plus petit essai discriminant proposé

Objectif : une ouverture des trois cartes et un vrai reroll, enregistrés sans
signalement dans le chat. Cela démontre un cas ; la fiabilité générale demandera
ensuite plusieurs séquences réelles, dont une modification d'une seule carte.

Avant toute demande de partie :

1. Vérifier le catalogue anglais associé aux identifiants et le moteur OCR EN
   installé. Faire sortir texte, position et rapprochements, en conservant les
   ambiguïtés comme inconnues. Plusieurs identifiants peuvent partager un nom.
2. Préparer un processus autonome capturant uniquement la fenêtre de jeu et
   sauvegardant la zone des cartes, avec identifiant de frame, UTC et horloge
   monotone. Viser 4 images/s pour ce bref essai ; cadence à mesurer.
3. Enregistrer aussi les images sans détection OCR : autrement un faux négatif
   ferait disparaître sa propre preuve. Comparer les trois positions séparément.
   Une variation visuelle déclenche une relecture sans dépendre du curseur.
4. Fournir un statut actualisé : PID, heartbeat, dernière capture réussie,
   captures tentées/réussies, erreurs, octets écrits et raison d'arrêt. Un
   heartbeat seul ne signifie pas que le jeu est effectivement capturé.
5. Borner l'attente et la collecte : au maximum 30 minutes depuis le lancement
   et 256 Mio de fichiers par essai, arrêt au premier plafond, à la fermeture du
   jeu après détection ou par signal local explicite. Conserver les preuves,
   sans suppression circulaire. Journaliser minimisation/masquage et les trous.
6. Vérifier hors partie le lancement indépendant, la capture d'une fenêtre de
   test identifiée, l'arrêt, les plafonds et la persistance du statut. Ces essais
   techniques ne valent pas capture réussie de LoL.

Pendant l'essai réel, conserver le temps de capture et celui de disponibilité
du résultat OCR. Comparer visuellement les trois noms avant/après reroll aux
résultats, mesurer délai depuis la première frame où les cartes sont lisibles,
lectures manquées, faux positifs et intervalles sans image. Cible initiale :
trois offres exactes et changement publié en moins d'une seconde une fois les
titres lisibles ; cible proposée, pas performance acquise. Mesurer CPU/mémoire
du collecteur ; tout effet FPS exige une comparaison du jeu, pas ces seuls compteurs.

Le choix final reste inconnu si seule la fermeture des cartes est observée.
L'historique peut confirmer les choix ultérieurement, pas leur instant exact.
Ne pas intégrer au dock avant cette preuve minimale. Si la capture est noire
ou les noms ambigus, arrêter et exploiter les images conservées hors partie.

## Overwolf et limites des conclusions

La [documentation GEP](https://dev.overwolf.com/ow-electron/live-game-data-gep/supported-games/league-of-legends/#augments)
décrit bien `augment_1/2/3` et `picked_augment` pour Mayhem.
Le [Dev Mode](https://dev.overwolf.com/ow-electron/guides/dev-tools/dev-mode/)
distingue deux moyens d'authentification : compte Console avec API key, ou
`OW_DEV_KEY` obtenu après statut Approved. Le statut Pending rapporté dans le
handover concerne cette seconde voie ; il n'établit pas à lui seul l'accès ou
l'absence d'accès au premier moyen. Aucun accès au compte privé vérifié ici.
Un contrôle de la rubrique Console / Profile / API Keys serait le petit essai
pertinent pour cette autre voie, sans publier ni soumettre de projet.

La [politique d'onboarding](https://dev.overwolf.com/ow-electron/getting-started/project-roadmap/)
continue de refuser l'approbation des applications privées et exige une idée
approuvée pour utiliser les API. Authentification technique, approbation de
l'application et publication restent trois questions distinctes.

La [politique Riot](https://developer.riotgames.com/docs/lol#game-policy)
consultée mentionne toujours les taux de victoire des améliorations parmi les
usages non approuvés. Les statistiques et recommandations restent hors de cet essai.
