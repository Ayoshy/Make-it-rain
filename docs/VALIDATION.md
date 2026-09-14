# Validation courante — Battlestation

## Consolidation Git — sources nettoyées et validées le 14 septembre 2026

Les sources réunissent les docks audio/vidéo, les dispositions et gestes de grille,
les optimisations des workers et du rendu, les menus/transitions communs et les
personnalisations d'onglets. Le prototype Discord abandonné et le plan de
transitions remplacé par sa documentation finale sont retirés des sources.
Les historiques de validation, sauvegardes et worktrees de référence sont conservés
localement, hors du contenu versionné.

`npm ci` réussit. Les treize suites .NET, contrats natifs et test de l'extension
réelle passent via `scripts/Validate.ps1` ; journal dans
`artifacts/validation/git-cleanup/validation.log`. La vidéo synthétique de test
donne 25 images changeantes/s en 1280 × 720 avec sa fenêtre minimisée. Ce résultat
ne mesure pas une lecture utilisateur. L'avertissement Node NO_COLOR/FORCE_COLOR
est sans effet sur le résultat de la suite.

Build complet sans avertissement dans `build/battlestation-git-clean`, construit
avec `-NoActivate` (`build.log`). Contrôle des fichiers à publier : aucun fichier
privé d'association, identifiant détecté, binaire ou fichier temporaire ajouté.
Les sept fichiers de l'extension vidéo correspondent aux sources actuellement
chargées depuis `.worktrees/video-dock/browser/video-dock`.

Le bureau reste sur `build/battlestation-terminal-tab-colors`, PID 16892, modules
relus dans `git-cleanup/loaded-modules.json`. Le bloc Discord y a été masqué.
Le pointeur Windows reste `build/battlestation-efficient-video-final`. Aucun
rechargement, fermeture de session terminal ou changement GPU dans cette passe.
Les limites des essais en clics réels restent celles des sections ci-dessous.

## Dock Discord — essai abandonné et retiré des sources

Le 14 septembre 2026, l'utilisateur a écarté le dock : pas d'accès RPC vocal
approuvé, et refus d'ajouter un navigateur permanent ou une intégration fragile.
Le prototype d'ancrage, le rendu vocal sans connexion, leurs tests et leurs
commandes ne font plus partie des sources. Les preuves historiques et une copie
du prototype restent dans les artifacts locaux ignorés par Git.

Le bloc de l'ancien build chargé a été masqué lors du nettoyage. Aucun nouveau
build n'a été activé, aucune session terminal fermée et aucune protection Windows
modifiée. Le prochain chargement des sources nettoyées revient à onze blocs.

## Couleurs des onglets restaurées — chargées le 14 septembre 2026

Build `build/battlestation-terminal-tab-colors`, bureau **16892**, modules chargés
vérifiés dans `artifacts/validation/terminal-tab-colors/loaded-modules.json`.
Les préférences de couleur arrivaient correctement jusqu’aux onglets, mais
l’harmonisation avait remplacé leur fond teinté par le verre commun. Le contour
restant était peu visible et recouvert par le halo d’activité. Une couleur choisie
teinte maintenant le corps entier, actif ou inactif ; Par défaut retrouve le verre
commun. Les indicateurs et animations d’activité sont conservés.

Compilation sans avertissement et suite TerminalTabs réussies. Le test passe par
les commandes du menu, persiste le choix et compare les pixels bleu/corail : plus
de 35 % du rectangle de l’onglet doivent changer sensiblement. Désactivation,
métadonnées d’activité et retour à Par défaut sont également vérifiés.
`header-preview.png` est inspecté : rendu du vrai contrôle sans console ni backend,
pas une capture du bureau installé. Les clics physiques restent à confirmer.

Rechargement 23144 → 16892 : disposition, édition, deux hôtes terminal, HWND et
identités/PID des sessions inchangés (`activation-result.json`). Consignes GPU
capturées puis restaurées et relues matériellement : automatique, cible 65 °C,
Canicule inactive. Aucun onglet fermé, aucune saisie ou génération de test.
Démarrage Windows inchangé ; inventaires techniques avant/après conservés sans
revendication de gain.

## Projets sans aperçu et statuts Git colorés — chargés le 14 septembre 2026

Build `build/battlestation-project-git-status`, dernier bureau relu **23144**.
Suppression des actions d’aperçu, de leur fenêtre, du stockage d’URL et des requêtes
HTTP de disponibilité ; ancien fichier personnel `project-previews.json` supprimé.
Fenêtre d’actions raccourcie à 124 px, Explorateur et Codex CLI conservés.
Sous-texte vert pour zéro modification, ambre pour des modifications, gris si
inconnu, sur les cartes et la fenêtre d’actions. Une absence reste inconnue.

Compilation sans avertissement, suites Experiences et Responsive réussies.
Les PNG projets minimum/large et popup sont inspectés : rendus WPF isolés,
pas des clics physiques. Preuves dans `artifacts/validation/project-git-status`.

Le premier rechargement a été annulé par le contrôle strict de disposition :
le chantier Discord ajoute un douzième bloc masqué. Aucun des onze blocs existants
n’avait changé. La reprise accepte uniquement cet ajout masqué et vérifie les
onze blocs, l’édition et les deux hôtes/sessions terminal inchangés (`retry/`).
Un rechargement concurrent a ensuite remplacé le PID 5008 par 23144, sur ce même
build ; modules et identités relus dans `modules-final.json` et `final-state.json`.
Après ce rechargement, cible GPU revenue à 83 °C : consigne initiale de 65 °C
restaurée puis vérifiée matériellement, ventilateur automatique, Canicule inactive
(`gpu-final.json`). Aucun prompt ni onglet fermé. Démarrage Windows inchangé.
Les clics réels restent à confirmer ; aucun gain de ressources revendiqué.

## Variantes plus distinctes et cycle complet — chargées le 14 septembre 2026

Build **`build/battlestation-dock-variants-distinct`**, bureau **23392**, modules
vérifiés dans `artifacts/validation/dock-variants-distinct/loaded-modules.json`.
L’utilisateur percevait surtout glissement et fondu. L’ancienne sélection pondérée
n’assurait pas le passage par tous les effets ; les flous de 4 px et grandes
cellules en fondu pouvaient être trop peu distincts sur le contenu des docks.

Chaque dock utilise maintenant un cycle mélangé des quatre effets, chacun une
fois, sans répétition entre deux cycles. Glissement/fondu restent à 300 ms.
La défocalisation dure 380 ms, sur place, avec flou jusqu’à 10 px et un transfert
d’opacité décalé pour laisser voir la phase floue. Les pixels durent 380 ms,
sur place : fragments plus petits et dispersés, disparition puis recomposition
avec un bref décalage. Jusqu’à 224 cellules partagent 12 phases, donc seulement
24 horloges d’opacité pour les deux masques. Les clics rapides conservent le
raccourcissement à 90 ms et la dernière destination demandée.

Build sans avertissement ; Responsive passe, avec tests renforcés sur les cycles
de quatre, leurs jonctions, une phase de flou effectivement supérieure à 4 px,
le partage des pinceaux des pixels et les annulations. Les PNG de test contiennent
désormais du texte et des valeurs de monitoring, pas seulement de grandes formes
pleines. Les aperçus intermédiaires flou/pixels sont inspectés et nettement distincts.
Journal `responsive.log`, images `variant-*.png` : rendus WPF isolés.

Rechargement **23964 → 23392** : disposition, édition, hôtes terminal 22176/6312,
HWND et identités/PID des sessions préservés. Le PID de départ avait changé pendant
le travail ; l’activation a relu l’état live juste avant sa propre intervention.
Consignes GPU capturées, restauration et lecture matérielle après rechargement :
ventilateur automatique, cible 65 °C, Canicule désactivée. Aucun onglet fermé ni
prompt automatique. Pointeur de démarrage inchangé.

Deux cycles de quatre transitions ont été exercés par commandes internes sur le
bureau chargé, sans changement de PID ou de disposition (`live-navigation.json`).
Tous les processus techniques, descendants et lecteurs sont relevés dans
`processes-transitions.json` puis `processes-idle.json`. Ces relevés ne constituent
pas une mesure de FPS. Computer Use restant indisponible dans cette conversation,
la distinction perçue et la fluidité en clics réels attendent le retour utilisateur.


## Alignement de la fenêtre Aperçu — chargé le 14 septembre 2026

`ProjectPreviewWindow` aligne désormais le bord droit d’Enregistrer sur celui du
champ, avec 6 px entre les deux boutons. Correction limitée aux marges de cette
fenêtre. Publication réussie dans `build/battlestation-preview-alignment` avec
`-NoActivate` ; journal dans `artifacts/validation/preview-alignment/build.log`.
Pas de nouveau test ajouté pour cette retouche de présentation.

Activation intégrée : d’autres chantiers ont rechargé le bureau pendant la passe,
d’abord vers `build/battlestation-toolbar-glass`, puis vers
`build/battlestation-dock-variants-distinct`. État et modules chargés relus dans
`preview-alignment/status.json` et `loaded-modules.json` : bureau PID 23392.
La DLL chargée est identique à celle du build corrigé (SHA-256 dans
`preview-alignment/binary-equivalence.json`) : la retouche est bien intégrée.
Aucun rechargement ni changement du démarrage Windows par cette passe. Rendu
et clics réels de cette correction restent à vérifier.

## Bandeau de réorganisation harmonisé — chargé le 14 septembre 2026

Première activation : `build/battlestation-toolbar-glass`, bureau **23964**.
Le chantier animations a ensuite chargé `build/battlestation-dock-variants-distinct`,
PID **23392** : modules relus dans `artifacts/validation/toolbar-glass/modules-after.json`
et bandeau harmonisé confirmé par `toolbar-current-live.png`. Le bandeau utilise
désormais `OverlayStyle.Apply` et `OverlayStyle.Frame` : mêmes boutons et police,
fond nacré et angles arrondis. La case « Lier les docks » hérite du template
CheckBox commun aux fenêtres de réglages, avec coche, survol et focus visibles.
Commandes et géométrie du bandeau conservées.

Compilation sans avertissement et suite Commands réussie. `toolbar-live.png`
montre le vrai bandeau chargé, ouvert par commande interne puis refermé en
restaurant le mode d'édition initial. Ce n'est pas un test de clic physique.
Rechargements 6752 → 23964 → 23392 : disposition, hôtes/sessions terminal et consignes GPU
préservés au relevé final (`result.json`). Ventilateur automatique, cible 65 °C, Canicule inactive
vérifiés après restauration. Inventaires techniques avant/après enregistrés,
sans revendication de gain. Le démarrage Windows reste inchangé.

## Variantes des pages Conrad/Codex — chargées le 14 septembre 2026

Build **`build/battlestation-dock-variants`**, bureau **6752**, modules contrôlés
dans `artifacts/validation/dock-variants/loaded-modules.json`. Le glissement alterne
avec fondu déplacé, flou doux et facettes translucides, sans répétition immédiate.
Durée 300 ms ; clics rapprochés regroupés et mouvement engagé raccourci à 90 ms.
Flou limité à 4 px sur les informations ; facettes limitées à 72 cellules et deux
masques vectoriels. Aucun timer permanent ni capture bitmap par image.

Compilation sans avertissement. La suite Responsive passe avec les quatre effets
exercés de façon déterministe : vrais pixels intermédiaires/finals, absence de
redessin du dashboard par image, non-répétition, flou borné, clics rapides,
annulation et retrait de toutes les horloges/masques/effets. Les tests de pages,
contrôles et brouillons passent également. Journal `responsive.log` ; PNG
`variant-*-{middle,end}.png` inspectés, sur une scène WPF synthétique. Les suites
générales de la tranche précédente n’ont pas été relancées pour ce changement
limité au contrôleur d’animation et à ses tests.

Rechargement **10420 → 6752** : disposition, édition, hôtes **22176 / 6312**, HWND,
identifiants et PID des sessions préservés (`activation-result.json`). Consignes
GPU capturées avant fermeture, restaurées si nécessaire puis vérifiées sur le
matériel : ventilateur automatique, cible **65 °C**, Canicule désactivée.
Aucun onglet fermé et aucune génération Codex de test. Le pointeur de démarrage
reste `build/battlestation-efficient-video-final`.

Seize commandes internes de navigation ont abouti sur le bureau réel, en environ
8 s, sans modifier les rectangles ni le PID. Les compteurs de dessin augmentent
de 20 pour Conrad et de 16 pour Codex, avec les mesures des capteurs indépendantes
(`live-navigation.json`). Tous les processus techniques, leurs descendants et
Brave/Stremio sont inventoriés : `processes-{before,transitions,after}.json`.
CPU du bureau sur ces fenêtres courtes : 22,16 % d’un cœur avant, 35,88 % pendant
les changements, 22,46 % après ; mémoire privée 502,9 / 563,8 / 504,9 Mio.
La charge des autres applications varie : pas de gain global revendiqué, et ces
mesures ne prouvent pas la cadence d’affichage des animations.

L’utilisateur avait confirmé le glissement puis demandé les variantes manquantes.
Computer Use reste indisponible (`native pipe`, erreur 2). Les nouveaux clics
physiques, l’appréciation des effets et leur fluidité visuelle restent à confirmer
par l’utilisateur ; les commandes internes ne remplacent pas cet essai.


## Harmonisation des docks et menus — chargée le 14 septembre 2026

**Bureau observé : `build/battlestation-harmonized-final`, PID 10420.** Ses modules
Battlestation proviennent de ce dossier (`harmonization/modules-after.json`).
`build/current.txt` conserve `build/battlestation-efficient-video-final` : cette
passe ne change pas la version lancée à l'ouverture de session Windows.

Les dix docks encadrés passent par le même rendu de base `Surface`. Polices,
titres, boutons et arrondis sont communs ; les menus WPF et sous-menus héritent
d'un seul dictionnaire de templates, sans gouttière blanche. Le compteur Vice
City reste sans panneau. Les sources partagées incluent les transitions internes
Conrad/Codex décrites ci-dessous : elles sont donc également chargées dans ce build.
Détails et exceptions : [HARMONIZATION.md](HARMONIZATION.md).

`scripts/Validate.ps1` passe (`harmonization/validation-final.log`), contrats natifs
et extension réelle sur vidéo synthétique compris. Les tests des menus exercent
leurs vrais templates, sous-menus, coches, états désactivés, activation unique et
fermeture WPF, sans changer de périphérique. Responsive a été relancé après la
suppression des anciens décalages de coordonnées dans `Surface`
(`responsive-final.log`). Publication finale sans avertissement (`publish-final.log`).

Rechargement normal du bureau 21944 → 10420. Disposition Double écran, onze blocs,
mode d'édition, hôtes terminal 22176/6312, HWND, identifiants et PID des sessions
inchangés (`harmonization/result.json`). Miroir Stremio réarmé, toujours en pause.
Consignes GPU relues avant fermeture du helper, puis restaurées et vérifiées :
ventilateur automatique, cible 65 °C, Canicule désactivée, aucune erreur GPU.
Aucun prompt envoyé et aucune sortie de terminal capturée.

`monitoring-audio-live.png`, `projects-live.png` et `terminal-header-live.png`
sont des captures ciblées du bureau réel. `menu-preview.png` et
`submenu-preview.png` sont des rendus isolés. Computer Use a échoué avec
`os error 2` ; les clics physiques des menus et une nouvelle passe Win+D/focus
restent à confirmer. `processes-after.json` mesure tous les processus techniques
et leurs descendants ; aucun gain n'est déduit de ces relevés courts.
Le fond est observé sur 5120 × 1440.

## Pages internes Conrad/Codex — implémentées, activation coordonnée en attente

Mise à jour : cette section décrit le relevé du chantier transitions avant son
intégration au build harmonisé chargé ci-dessus ; son ancien PID n'est plus live.

Le glissement est implémenté dans `DashboardTransition.cs` et raccordé à
`DashboardSurface.cs`. Les deux dessins conservés se déplacent en 300 ms dans
la zone centrale ; cadre et navigation restent fixes. Reclic : résumé ; autre
bouton : autre page. Les clics rapides se regroupent, les contrôles mobiles sont
neutralisés et les brouillons GPU conservés. Retrait, occultation, fin et changement
de taille arrêtent les animations. Aucun agrandissement de fenêtre ou du layout.
Détails : [DOCK_PAGE_TRANSITIONS.md](DOCK_PAGE_TRANSITIONS.md).

Build d’essai : `build/battlestation-dock-pages`, construit avec `-NoActivate`, puis
republié après les premières corrections du chantier menus glass concurrent.
Ce chantier continue de modifier notamment Surface/DeskSurface : le build d’essai
est une étape datée, pas une promesse de contenir ses changements ultérieurs.
Le dernier relevé de cette conversation voit encore le bureau **21944** sur
`build/battlestation-efficient-video-final`, ainsi que le pointeur `build/current.txt`.
**Aucun rechargement, fermeture de helper ou écriture GPU effectué par ce chantier.**

La compilation a réussi sans avertissement. Les treize suites .NET ont passé,
avec reprise de Commands après la correction de police effectuée par le chantier
menus. Les contrats natifs passent également. Journaux dans
`artifacts/validation/dock-pages/` : `validation.log` conserve le premier arrêt,
`test-Commands-retry.log`, `test-Responsive-final.log`, `test-{Video,BrowserVideo,
Audio,AudioWorker,Experiences,Consolidation}.log` et `test-Native.log` conservent les
vérifications complémentaires. Ne pas présenter le premier lancement de
Validate.ps1 comme une exécution intégrale réussie.

Le test de l’extension vidéo a donné 19 images changeantes/s lors d’un premier
essai lancé parallèlement aux contrats natifs, sous son seuil de 20. Rejoué seul,
il passe à 24,2 images/s en 1280 × 720 : `test-Extension-isolated.log`. Le premier
échec reste dans `test-Extension.log`. Ce relevé concerne une vidéo synthétique
dans un profil Brave isolé, pas la fluidité des transitions du bureau.

`DashboardPageTests.cs` exerce les vraies animations WPF et les surfaces aux tailles
minimales/agrandies : positions droite/gauche effectivement modifiées, compteur de
rendu inchangé entre deux images animées, retour, clics rapides, zones cliquables,
conservation du brouillon malgré des mesures tardives, défilement et bornage,
masquage/redimensionnement et libération des horloges. Backend factice : aucune
écriture GPU ou génération Codex possible. Les PNG hardware/usage copiés dans
`dock-pages/` sont des rendus isolés, inspectés visuellement.

Computer Use a été essayé deux fois : `native pipe unavailable`, **os error 2**.
Les clics physiques et la cadence sur le bureau réel restent donc à confirmer
lors de l’activation coordonnée avec le chantier visuel. Le fichier de préparation
et cette implémentation ne valident pas une bascule du démarrage Windows.


## Optimisation et vidéo HD — activées le 14 septembre 2026

**Build courant : `build/battlestation-efficient-video-final`.** Le bureau et
`build/current.txt` ciblent cette version. Identités et modules vérifiés dans
`artifacts/validation/consolidation/release-result.json` et `loaded-modules.json`.
L'utilisateur a confirmé les bascules Stremio/YouTube, puis Win+D, le niveau des
docks et le focus clavier. La dernière correction après ce contrôle invalide les
caches de données lors d'un changement de configuration ; elle ne modifie pas
le placement ou le focus.

**Consommation.** Les surfaces suivent la révision de leurs données : seconde
pour horloge/compteur, changements pour les autres. Chaînes natives, brosses,
stylos, dégradés, textes et glyphes stables sont réutilisés. Les animations
continues des onglets sont plafonnées à 30 Hz et s'arrêtent quand leur surface
est masquée. L'occultation tient compte de la bande réelle des docks et des deux
écrans, et réagit aux changements de focus/Win+D. Le fond continue sur l'écran
exposé et ne soumet plus de frames quand les deux sont couverts. Scanner de
projets, mixeur et captures inutilisés sont suspendus.

Le spectre et WGC ont chacun un worker propriétaire. Le profilage a aussi isolé
un coût dans `MMDevice.FriendlyName` : **1640,62 ms CPU sur environ cinq secondes**
sur le thread du mixeur. Les noms et l'ID stable de sortie sont maintenant lus
pendant l'inventaire des périphériques, pas à chaque mesure du volume. La fenêtre
de Hann du spectre est précalculée. Preuves : `thread-cpu.json`, `active-stacks.txt`.

Le dernier relevé de 20 s donne **11,79 % d'un cœur pour le bureau**, soit
**1,97 % du CPU total** sur six processeurs logiques. Bureau, deux hôtes terminal
conservés, helper GPU, helper de titres, pont vidéo et app-server avec son conhost :
**2,19 % du CPU total, 870,1 Mio privés cumulés**. Le premier écran était couvert
(`monitorMask=2`) et le miroir suspendu : ces valeurs ne décrivent pas une lecture
vidéo active. Tous les descendants et Brave/Stremio sont inventoriés dans
`processes-final.json`. Le travail utilisateur des shells et onglets ne peut pas
être attribué entièrement aux docks. Les scènes antérieures diffèrent ; aucun
pourcentage global de gain contrôlé n'est revendiqué. WMI n'a pas fourni de mesure
GPU exploitable. Le relevé précède la seule correction finale d'invalidation au
redémarrage du lecteur de données.

**Vidéo.** Extension **0.3.2**, chargée depuis
`.worktrees/video-dock/browser/video-dock`, chemin vérifié dans les paramètres de
cette extension. Sources de référence : `browser/video-dock`. La résolution suit
le dock jusqu'à 1920 × 1080, JPEG qualité 0,9, cadence plafonnée à 30 Hz, deux images
en attente au maximum. Les images restent en mémoire ; le son reste dans le
lecteur d'origine.

La 0.3.1 échouait avec `worker-runtime/Error`. Le test de la véritable extension
reproduit l'échec avec une CSP interdisant les workers `blob:`. La 0.3.2 charge le
worker depuis une page interne de l'extension et respecte cette même CSP. Les
VideoFrame ne traversent pas les origines : des ImageBitmap redimensionnés sont
explicitement transférés par un MessageChannel privé. Cela évite également le
DataCloneError observé sur le premier essai de page interne. Les objets images
sont fermés après utilisation. Les codes de diagnostic sont bornés et ne
transportent aucun message d'exception complet, titre ou URL de vidéo.

Sur **YouTube réel**, page `hidden`, 199 images reçues et dessinées en environ
8,07 s : **24,7 images/s en 1586 × 892**, sans erreur, avec progression du compteur
du décodeur. Un autre format de dock produit du 1102 × 620. Ce sont des compteurs
de pipeline, complétés par la confirmation utilisateur : « Le retour stremio/yt
est perfect atm, tu peux considérer que c'est ok. » Les mesures internes ont
restauré le mode initial ; l'utilisateur a ensuite poursuivi ses propres bascules.
Stremio se déclarait en pause pendant le relevé à une seule image.

WGC réduit l'image sur GPU avant le transfert CPU ; un worker publie les derniers
tampons sans bloquer WPF. Les vrais pixels, recouvrement, redimensionnement,
restauration géométrie/focus et démarrages/arrêts répétés passent sur des fenêtres
de test. L'utilisateur a validé le lecteur et les bascules réelles. Le pont déjà
lancé peut garder le chemin `battlestation-consolidation` : sa DLL est identique à
celle du build livré (`bridge-equivalence.json`). L'enregistrement natif vise le
build final pour sa prochaine ouverture ; aucun nouveau rechargement de
l'extension n'est requis.

**Projets.** Tous les sous-dossiers du répertoire configuré sont classés par
modification récente de sources. Le dock affiche autant de cartes que sa taille
le permet et défile au-delà ; plus de plafond de six. Un FileSystemWatcher
regroupe les changements et le scanner réanalyse les projets concernés, avec
réconciliation complète espacée de dix minutes. Retrait/recouvrement suspendent
le suivi. Les états Git concernent seulement les cartes présentées. Tests :
quatorze dossiers, tri, suspension/reprise, invalidation des caches au changement
de répertoire même lorsque le scanner est suspendu, et rendu large au-delà de six.

**Robustesse.** Les erreurs d'écriture des diagnostics n'arrêtent plus la boucle
des capteurs ; le snapshot du bureau est écrit en arrière-plan. Les messages et
la durée des connexions du pipe sont bornés, y compris face à un client silencieux
ou qui ne lit pas sa réponse. Une perte de cible Direct2D reconstruit les
ressources : l'injection de `D2DERR_RECREATE_TARGET` dans la DLL de test démontre
la reprise sans reset matériel du GPU.

Une régression intermédiaire avait capturé le contexte WPF dans le client de
pipe et bloquait `TerminalSession.Detach`. La pile est conservée dans
`desktop-stacks.txt`. Seul le bureau bloqué a été terminé ; sa connexion libérée
a immédiatement rendu le pipe terminal disponible. Aucun hôte terminal ni
onglet fermé. Le correctif utilise des I/O brutes annulables et
`ConfigureAwait(false)` ; un test de contexte couvre ce cas. Les rechargements
suivants sont passés par la fermeture normale.

**Préservation.** Les hôtes **22176** et **6312**, leurs HWND et les identifiants/PID
de leurs sessions sont inchangés. Chaque rechargement conserve la disposition et
le mode d'édition relevés juste avant, y compris les modifications utilisateur
pendant le travail. La cible thermique utilisateur est passée de 83 à **65 °C**
pendant la session. Les consignes ont été capturées avant fermeture des helpers,
puis restaurées et vérifiées matériellement : **ventilateur automatique, cible
65 °C, Canicule désactivée**. Ce sont des restaurations des réglages courants,
pas des tests de bornes ou une consigne choisie par l'agent. Aucun prompt Codex de test.

**Démarrage.** L'application et le lanceur se placent en priorité Normal. Windows
a refusé la modification de la tâche administrative existante (`E_ACCESSDENIED`) ;
ses métadonnées restent Priority=1. Le lanceur abaisse immédiatement sa propre
priorité puis celle de l'application, sans modifier les permissions de la tâche.
L'installateur utilise désormais Priority=6 (Normal). Le pointeur de build a été
mis à jour après validation utilisateur. Un appel `-AtLogon` avec le bureau déjà
présent a conservé son PID ; aucune ouverture de session Windows n'a été forcée.

**Vérifications.** Treize suites .NET, contrats natifs et test de la véritable
extension dans un profil Brave isolé passent via `scripts/Validate.ps1`
(`validation-release.log`). Le correctif final de cache a repassé Consolidation,
y compris les reprises natives. `npm ci` installe la dépendance verrouillée.
Les avertissements Node sur les variables de couleur ne sont pas des échecs.
Computer Use a encore échoué avec `os error 2` ; les assertions WPF ne sont pas
présentées comme des clics physiques. Les confirmations utilisateur portent sur
les lecteurs, Win+D, le niveau des docks et le focus.

Preuves principales dans `artifacts/validation/consolidation/` :
`release-result.json`, `activation-result.json`, `loaded-modules.json`,
`source-manifest.json`, `video-real-sources.json`, `extension-origin-test.log`,
`validation-release.log`, `native-video.log`, `gpu-{before,after}-activation.json`,
`resource-summary.json` et `processes-final.json`. Les étapes intermédiaires et
les diagnostics sont conservés dans ce même dossier et ses `activation-*/`.


## Onglets personnalisables et anneau d'activité — chargés le 14 septembre 2026

Build live : `build/battlestation-terminal-tabs-visible`, bureau **6564**, helper
métadonnées **1248** au relevé. Build intégré depuis les sources partagées, incluant
la correction audio et les dispositions de la section suivante. Le pointeur de
démarrage reste `build/battlestation-palette-settings`.

Clic droit, couleur, renommage et titre automatique validés par l'utilisateur.
Après son retour sur le point peu lisible, Working et Thinking utilisent un anneau
rotatif de 16 px ; Ready conserve ✓, Attention !, état inconnu ?. Les deux sessions
actuelles exposent Working avec leurs couleurs menthe et violet conservées.
L'anneau est validé en aperçu et en test WPF ; son appréciation physique reste ouverte.
Détails et réglage `/title` : [onglets terminal](TERMINAL_TABS.md).

Compilation sans avertissement et suite TerminalTabs réussies, y compris le helper
sur une console synthétique sans saisie. Les suites générales avaient passé avant
ce changement d'icône. Preuves fraîches dans `artifacts/validation/terminal-tabs-visible` :
`result.json` confirme les blocs et le mode édition inchangés, ainsi que les hôtes
22176 / 6312, HWND et identifiants/PID des sessions. Aucun onglet fermé par ce
rechargement. `modules.json` vérifie les modules chargés ; `process-metrics.json`
inventorie tous les processus techniques. Helper : 9,3 Mo privés, 0 % d'un cœur sur
le relevé de 3 s (échantillon court, pas une garantie). Le miroir vidéo était désarmé
et masqué avant le rechargement.

## Fluidité, retrait des cadres et setups — chargés le 14 septembre 2026

Build intégré depuis les sources partagées : **`build/battlestation-fluid-presets-final`**,
bureau **9788** au relevé. Il comprend les changements du chantier onglets terminal
qui a rechargé le bureau pendant les premiers essais. Modules vérifiés dans
`artifacts/validation/fluid-presets/final-loaded-modules.json`. Le lancement Windows
reste sur `build/battlestation-palette-settings` ; aucune modification d’autostart.

**Cause principale des saccades identifiée et corrigée.** Une mesure en lecture
seule du véritable `AudioMixer` montre des appels de 184–196 ms, avec des pointes
de 665–766 ms lors de l’inventaire des périphériques. Ces appels étaient exécutés
sur le thread WPF toutes les 250 ms. `AudioMixerWorker` possède désormais tous les
objets Core Audio sur un seul thread distinct. L’interface lit des états immuables
et transmet les actions explicites ; les mouvements successifs d’un curseur sont
regroupés. Le curseur conserve un retour immédiat pendant le glissement. Les lectures
ralentissent à 500 ms après la fin de la précédente, l’inventaire à 5 secondes, et
s’arrêtent lorsque le dock est masqué ou en réorganisation. Les effets musicaux
restent sur leur boucle indépendante.

Les textes du dessin WPF ont un cache borné en mémoire, la grille réutilise son motif
et des rectangles simples, les masques Direct2D sont réutilisés tant que leur forme
ne change pas. Les trois DLL natives et leurs contrats sont compilés avec `/O2`.
Les tailles minimales n’accumulent plus la tolérance de 0,001 à chaque geste.

**Cadres fantômes.** La visibilité est fixée sur la surface avant de masquer la
fenêtre ; les emplacements de verre connus sont effacés et un rendu WPF en attente
ne peut plus les réinscrire. Le test Responsive couvre explicitement ce scénario
et les zones cliquables masquées. En live, Focus ne conserve aucun panneau natif
Vidéo/Audio, et Multimédia aucun panneau Projets/popup/Terminal/monitoring : fichiers
`final-focus-native.json` et `final-multimedia-native.json`. Les panneaux observés
lors d’un premier contrôle correspondaient à des blocs réaffichés par l’utilisateur,
pas à une nouvelle régression. Retirer puis réajouter un dock réutilise désormais
ses coordonnées exactes si elles sont encore libres, sans le décaler à l’ancienne grille.

**Dispositions prêtes à l’emploi.** Focus et Multimédia utilisent le second écran ;
Double écran place projets/terminal sur le premier et médias/monitoring sur le second.
Le menu est dans Réorganiser, Réglages et la notification. Première sélection :
géométrie prédéfinie, minimums respectés, aucun chevauchement. Sélections suivantes :
ajustements personnels conservés. Le profil précédent est sauvegardé avant la
bascule ; les tests vérifient le retour exact à Personnel. Les anciennes dispositions
Jeu/Création/Cinéma sont conservées. Des bascules ont été exercées par commande
interne et l’utilisateur a aussi changé plusieurs fois de disposition pendant les essais.

Mesures détaillées dans `fluid-presets/` : `audio-profile.json`, `thread-sample.json`,
`final-thread-sample.json`, `audio-ui-{before,after}.json`. Sur deux relevés Multimédia
avec géométrie vérifiée identique, le processus bureau passe de 63,60 à 53,04 % d’un
cœur. Le thread principal consommait 2453 ms sur un relevé de 4 s avant déplacement
des appels audio ; il consomme 219 ms sur le relevé final d’environ 5 s. Les scènes
de ces deux relevés de thread diffèrent : ces chiffres documentent la disparition
du blocage audio, **pas un benchmark parfaitement contrôlé ni une garantie de FPS**.
Tous les hôtes/helpers et descendants, Brave et Stremio ont été inventoriés.

Douze suites et contrats natifs passés via `scripts/Validate.ps1` : journal
`final-validation.log`. Le test du worker bloque volontairement le faux pilote et
vérifie que lectures/commandes UI et arrêt ne bloquent pas, que les curseurs se
regroupent et que tous les appels audio restent sur le même thread. Aucune écriture
sur le son utilisateur pour cette validation. Les contrôles GPU avant/après le
premier rechargement confirment ventilateur automatique, cible 83 °C et Canicule
désactivée ; aucune commande d’application GPU n’a été envoyée.

Le dernier rechargement conserve les rectangles/visibilités du profil actif et les
hôtes terminal ; les captures de métadonnées avant/après sont préfixées `final-`.
Aucun onglet fermé et aucune saisie de test dans les sessions utilisateur. Les
preuves de rendu sont des contrôles isolés ou des états natifs. Computer Use a été
réessayé et échoue encore avec `os error 2` : les clics physiques de retrait et de
réglage audio n’ont pas été automatisés. Ne pas les confondre avec les tests WPF.

## Liaison des docks optionnelle — chargée le 14 septembre 2026

Build live vérifié : `build/battlestation-optional-link`, PID **23560**.
**Lier les docks** est accessible dans Réorganiser et Réglages → Bureau. Le choix
est sauvegardé et vaut `false` pour les nouvelles et anciennes préférences qui
ne le précisent pas. Maj, au début du geste, inverse ce choix pour ce seul geste.
La grille reste indépendante. Liaison désactivée : aucun voisin ne bouge ou ne
rétrécit ; une collision empêche le placement ou borne le redimensionnement.

Suites Layout et Commands passées : voisins inchangés lors d’un déplacement ou
redimensionnement indépendant, collisions, grille conservée, comportement lié
préservé, migration et sauvegarde de l’option. Publication isolée sans erreur,
sur la même base terminal que le build précédent pour préserver le chantier
terminal concurrent. Sources de cette publication : `build/optional-link-sources`.

Rechargement 24056 → 23560 : onze blocs identiques, hôtes 22176 et 6312 conservés
avec les mêmes HWND, identifiants de sessions et PID. Le mode Réorganiser actif
avant rechargement a été restauré. Inspection live : grille active, pas 8,
`LinkDocks=false`. Preuves dans `artifacts/validation/optional-link`, notamment
`result.json`, `loaded-modules.json`, `source-manifest.json` et `build.log`.
Le démarrage Windows reste inchangé. Clic sur la nouvelle case et combinaison
Maj non automatisés : l’accès natif Computer Use reste indisponible dans cette
session ; les tests de moteur ne constituent pas ces essais physiques.

## Docks dynamiques — build d’essai chargé le 14 septembre 2026

Build observé : `build/battlestation-dynamic-docks`, bureau PID **24056**.
Modules chargés vérifiés dans `artifacts/validation/responsive/loaded-modules.json`.
`build/current.txt` cible toujours `build/battlestation-palette-settings` :
aucune bascule du démarrage Windows. Les sections suivantes sont historiques.

Réorganiser dispose de huit poignées, du redimensionnement partagé dans les deux
sens, des minimums par bloc, de la poussée en cascade sur un même écran et de
l’annulation/rétablissement. Le pas de grille est réglable de 4 à 64, désactivable,
avec 8 par défaut. Les voisins conservent leurs gouttières ; les anciens placements
ne sont pas réalignés automatiquement. Les dimensions sont restaurées depuis
`layout.json` et les profils. Les listes Projets, Applications et Audio défilent
aux petites tailles ; les résumés Conrad/Codex et leurs volets ont des géométries
distinctes. La vidéo est contenue proportionnellement dans son cadre.

Validation automatisée : les dix suites de `scripts/Validate.ps1` et les contrats
natifs ont passé (`responsive/full-validation.log`). Après les corrections finales,
Layout, Commands, Experiences et Responsive ont été relancées avec succès. Le test
Responsive compile les véritables surfaces avec des services factices incapables
d’appeler le backend, le GPU, le terminal ou le navigateur. Il vérifie les zones
cliquables aux tailles minimales et agrandies, les listes défilantes, la popup
Projets, les volets et le montage des poignées. Les PNG dans `responsive/` sont des
**rendus isolés**, pas des captures du bureau installé. Vidéo et terminal sont
couverts par leurs suites dédiées, pas par ces neuf rendus de widgets.

Le premier démarrage a détecté un double parent logique WPF pour l’en-tête des
poignées. Le bureau précédent a été restauré immédiatement, puis la cause corrigée
et couverte par un test de montage. Le deuxième démarrage a réussi. Avant/après
17536 → 24056 : les onze blocs sont identiques ; hôtes terminal **22176** / HWND
526020 et **6312**, deux sessions et une session respectivement, conservés avec
les mêmes identifiants et PID. Aucun onglet fermé et aucune saisie utilisateur
injectée. Les commandes internes `organize:on/off` ont masqué puis réaffiché le
terminal sans changer les sessions ni le layout. Le miroir YouTube précédemment
armé a été réarmé ; le relevé final indique YouTube sélectionné et lecteur en pause,
ce qui ne constitue pas une preuve de cadence vidéo en lecture.

Le chantier terminal concurrent a ajouté pendant ce travail des sources encore
incomplètes (`TerminalMetadata`, `TerminalTabPreferences`, puis éditeur d’onglet).
Le premier build depuis les sources partagées échouait sur les nouvelles propriétés
de `TerminalTabInfo`. Ces sources n’ont pas été corrigées ni supprimées par le
chantier docks. Le build livré provient de `build/dynamic-docks-sources`, copie
isolée des sources des docks avec le terminal antérieur à ce chantier : les deux
nouveaux fichiers terminal présents au moment de la copie et l’entrée
`--terminal-metadata` ont été exclus **dans cette copie seulement**. Le manifeste
SHA-256 et les journaux de publication sont dans `responsive/`. Les sources
partagées restent dans `src/` ; cette copie sert à reproduire le build chargé et
ne remplace pas leur intégration avec le chantier terminal lorsqu’il sera prêt.

Mesures : `responsive/processes-{before,after}.json` inventorie tous les hôtes,
helpers et descendants Battlestation ainsi que Brave/Stremio. Les fenêtres de
mesure courtes et l’activité utilisateur diffèrent ; aucun gain de ressources
n’est revendiqué. Fond et fenêtres relus sur 5120 × 1440, bureau derrière une
application normale : `dotnet-docks-after-windows.json`.

**Restent les gestes physiques** : glisser les trois docks de l’exemple par le bord
vidéo, pousser une chaîne jusqu’au bord, annuler/rétablir, changer/désactiver la
grille, passer sur l’autre écran et exercer Win+D/focus pendant ces gestes.
Computer Use échoue au raccordement natif avec `os error 2`, confirmé deux fois.
Les commandes internes et les événements WPF ne remplacent pas ces essais.

## Bascule Vidéo sans retour de fenêtre — chargée le 14 septembre 2026

Build live : `build/battlestation-video-switch`, bureau PID 25500 au relevé.
Les modules chargés ont été vérifiés. Le lancement Windows cible encore
`build/battlestation-palette-settings` ; aucun changement de tâche de démarrage.

Après avoir indiqué que les deux miroirs fonctionnaient et que les derniers
ajustements étaient satisfaisants, l'utilisateur signalait encore le retour de
la grande fenêtre Stremio lorsqu'il sélectionnait son onglet dans le dock.
La fenêtre est désormais préparée directement à la taille de support, derrière
le bloc. Son maintien est conservé entre les bascules de source ; la sélection
d'onglet ne la restitue plus. La restauration d'une fenêtre initialement réduite
change son rectangle normal avant de l'afficher sans activation. Le bouton ↗
et une activation ultérieure distincte par la barre des tâches/Alt+Tab permettent
de retrouver le lecteur normal ; les événements de focus liés à la bascule et
aux commandes lecture/pause sont écartés de cette restauration.

Cinq bascules internes successives ont conservé Stremio à (3444,388), 480 × 264,
derrière le bloc, avec le même focus. La lecture native a repris à chaque retour.
YouTube était fermé pendant ce dernier contrôle : il valide le maintien de la
fenêtre pendant la bascule, pas une nouvelle lecture YouTube. Les tests isolés
couvrent aussi préparation compacte immédiate, resélection, réduction initiale,
restauration des coordonnées/états et pixels WGC. Compilation sans avertissement.
Le dernier clic physique après cette correction n'a pas été automatisé.

Les onze blocs ont les mêmes coordonnées et visibilités avant/après rechargement.
Les hôtes terminal 22176 et 6312, leurs HWND et toutes leurs sessions sont inchangés.
Le miroir a retrouvé son état d'activation préalable (arrêté au dernier relevé).
Preuves : `artifacts/validation/video-switch`, notamment
`source-switches-final.json` et `loaded-modules.json`.

Les sections suivantes sont les étapes précédentes de la validation.

## Vidéo à la demande — essai live du 14 septembre 2026

Version actuellement chargée au dernier relevé : `build/battlestation-video-smooth`,
bureau PID 18888. Le précédent essai `build/battlestation-video-cockpit`, PID 24984,
réunissait déjà les changements vidéo et ceux du chantier Cockpit ci-dessous.
`build/current.txt` reste sur `build/battlestation-palette-settings` : la validation
visuelle finale n'est pas encore complète, donc la cible du démarrage Windows
n'a pas été changée. Les sections suivantes décrivent des étapes antérieures.

Le miroir démarre sur clic. YouTube utilise l'extension locale Brave et un pont
.NET ; Stremio utilise WGC avec maintien de sa fenêtre derrière le bloc quand
nécessaire. Cliquer l'image pilote le lecteur concerné, sans touche multimédia
globale. La grille accepte désormais une marge exactement égale à 12 px.
Le bloc a été placé sur le second écran et reste déplaçable.

L'utilisateur a validé sur le build 24984 les deux sources, lecture/pause et la
continuité après réduction / Win+D. Il a ensuite signalé une cadence YouTube
trop basse et des bords de fenêtre Stremio visibles derrière l'image. Le build
18888 corrige le masque opaque, le retrait de la fenêtre de support et le remplissage
du cadre sans étirement. L'extension 0.2.2 retire la limite initiale de 20 images/s,
limite les images à 576 px et autorise deux images en transit. WPF réagit maintenant
à l'arrivée des images. **Ces derniers ajustements attendent encore le retour visuel
et la mesure de cadence sur une vidéo YouTube en lecture avec Brave réduit.**

La réception réelle depuis YouTube a été constatée avant optimisation (65 images
en quatre secondes). Ce relevé indiquait le lecteur en pause et ne prouve donc
pas une cadence de vidéo animée. L'essai natif Stremio avec fenêtre de support a
fourni 55 images dont 54 changements en trois secondes ; position, taille, état
réduit et focus restaurés à la fin. Le contrôle réel UI Automation a changé Pause
en Play, puis rétabli Pause : la lecture a été restaurée. Ce n'est pas un clic
physique automatisé ; les clics du dock ont été essayés par l'utilisateur.

Les neuf suites de `scripts/Validate.ps1` et les contrats natifs passent sur le
build intégré : Core, Layout, Desktop, Terminal, Commands, Video, BrowserVideo,
Audio et Experiences. Un premier passage Desktop a été interrompu par un changement
de focus pendant les essais concurrents ; la suite a ensuite passé intégralement.
Les derniers changements du pont ont repassé le test de transport ; les scripts
de capture ont passé le lecteur synthétique dans un profil Brave vierge. Celui-ci
n'établit pas la cadence du navigateur utilisateur minimisé.

Les deux rechargements ont conservé les hôtes terminaux 22176 / HWND 526020 et
6312 / HWND 132134, avec les mêmes identifiants de sessions et PID de shells.
Les positions des blocs existants sont identiques avant/après chaque rechargement ;
seul le déplacement demandé du bloc Vidéo a été appliqué. Fond en 5120 × 1440.
Aucun onglet fermé, prompt de test, compte ou consigne GPU modifié.

Preuves : `artifacts/validation/video-v2`, `artifacts/validation/video-smooth`,
`dotnet-video-v2-live-windows.json`. Le relevé de processus inclut tous les hôtes,
le pont et leurs descendants, ainsi que l'ensemble de Brave et Stremio ; il ne
constitue pas un benchmark comparatif. L'extension chargée par Brave provient de
`.worktrees/video-dock/browser/video-dock`, copie contrôlée des sources racine.
Le manifeste natif personnel pointe désormais sur le pont du build video-smooth ;
un hôte déjà lancé garde son binaire jusqu'au rechargement de l'extension.

Voir [VIDEO.md](VIDEO.md) pour les commandes, limites et mécanismes de restauration.

## Cockpit — préparé, activation différée le 14 septembre 2026

Le chantier audio ajoute les cinq évolutions décrites dans [COCKPIT.md](COCKPIT.md).
Build isolé : `build/battlestation-cockpit`, avec `-NoActivate`.
L'utilisateur demande explicitement de laisser la main à l'agent Vidéo pour
ses essais : aucun rechargement, changement de `build/current.txt` ou fermeture
de terminal effectué par ce chantier. Le bureau observé pendant ce travail
chargeait `build/battlestation-video` (PID 17556), distinct du pointeur de build.
Ces identités doivent être relues avant toute future activation.

Audio : compilation sans avertissement ; test WASAPI sur une session silencieuse
possédée par le test, volume et mute réellement modifiés puis restaurés. Sortie,
volume général et micro utilisateur vérifiés inchangés. Analyse basses/aigus,
silence et migration des préférences vérifiées. Les PNG `audio-widget-preview.png`
et `audio-background-{silent,reactive}.png` sont des rendus **isolés** :
le second contrôle exerce la vraie DLL Direct2D et constate un changement de
pixels sous énergie audio, mais ne prouve pas le bureau installé.

Experiences : profils indépendants, retour Personnel, persistance, rejet de
collisions ; normalisation des URLs, domaines trompeurs rejetés, doublons,
capacité de 100 liens, persistance et métadonnées simulées ; vrais états Git
d'un dépôt temporaire et validation des adresses d'aperçu local.
Pas de lecture ni d'écriture du presse-papiers utilisateur pendant les tests.

Les huit suites managées et les contrats natifs passent. Les derniers ajustements
de profils et de miniatures Spotify ont été republiés dans le build final et
la suite Experiences a repassé. Les endpoints publics YouTube et Spotify ont
répondu avec titre et miniature sur des exemples publics ; un autre exemple
Spotify a renvoyé HTTP 500. La conservation du lien en cas d'échec est couverte.
Restent les validations
réelles détaillées dans COCKPIT.md : clics audio et matériel, copies de liens,
métadonnées depuis une copie réelle, transitions de modes/onglets et intégration du fond.
Computer Use échoue au branchement natif avec l'erreur 2.

## Palette, réglages et volets ancrés — activés le 14 septembre 2026

Version active : `build/battlestation-palette-settings`, désignée par
`build/current.txt`. Bureau PID 20052 au dernier relevé ; chemins des modules
managés et natifs vérifiés dans ce dossier. Les versions des sections suivantes
sont historiques. Aucun changement de configuration du démarrage Windows.

- Ctrl+Espace est réellement enregistré par Windows (`hotkeyRegistered=true`,
  erreur 0). Palette activable hors de la couche des widgets, recherche sans accents
  et par sous-séquence, sélection clavier, mode `>`, projets avec choix explicite
  Explorateur/Codex CLI. Le service utilise MOD_NOREPEAT et libère son propre
  raccourci à l’arrêt. Un conflit reste visible dans Réglages et permet une nouvelle
  tentative ; aucune application concurrente n’est fermée.
- Réglages accessibles depuis la palette, le menu de notification et le menu d’un
  bloc : visibilité, applications du dock, dossier des projets, lieu météo,
  date/heure du compteur, transparence des panneaux et animation du fond. Aperçu
  d’apparence immédiat, enregistrement dans `preferences.json`, restauration de
  l’apparence enregistrée à la fermeture. Blocs et éditeur du dock s’appliquent
  séparément. La date existante et son offset sont conservés si non modifiés.
- Le défaut Conrad était causé par `DesktopLayout.Resize` : l’agrandissement à
  443 px cherchait un emplacement libre sur les deux écrans, puis sauvegardait ce
  déplacement. Les détails sont maintenant des volets temporaires : résumé fixe,
  même écran, ouverture vers le haut au besoin, aucun changement du layout.
  Leur matériau plus dense évite que les libellés du voisin transparaissent.

Build final sans avertissement. `scripts/Validate.ps1` passe les cinq suites
managées et les contrats natifs. Après les derniers ajustements visuels, les
contrôles Commands/Desktop/Layout ont aussi passé ; le dernier ajustement de clip
et survol des volets a été compilé et le volet Codex contrôlé sur le bureau réel.
La nouvelle suite Commands vérifie recherche accentuée et multi-termes, commande
inconnue sans action de substitution, Entrée sur la ligne sélectionnée, les deux
choix projet, persistance/validation des préférences et conflit/libération du
raccourci. Les essais WPF utilisent uniquement des fenêtres et actions isolées.

Vérifications live : les ouvertures/fermetures internes du volet Conrad gardent
X=4272, Y=720, hauteur 218 → 443 → 218 ; fichier de disposition inchangé.
Le volet Codex garde son résumé à (4272,960), étend sa fenêtre à Y=726, puis
revient à 209 px sans sauvegarder de déplacement. Les rapports et captures
`commands-final-conrad-expanded.*`, `palette-settings-codex-expanded.json` et
`commands-settings-live.png` sont dans `artifacts/validation`. La capture des
réglages provient de sa vraie fenêtre WPF via PrintWindow. Les captures ne
conservent pas de contenu de terminal. `commands-preview-final.png` est seulement
un aperçu isolé de palette.

Dernier rechargement : bureau 3884 → 20052, neuf blocs identiques avant/après.
Hôte terminal 14608 / HWND 1114672 : deux sessions avec identifiants et PID
conservés. Hôte historique 6312 / HWND 132134 : une session conservée. Preuves
`palette-settings-{desktop,terminal,legacy}-{before,after}.json`,
`palette-settings-loaded-modules.json` et `dotnet-palette-settings-windows.json`.
Le fond reste attaché en 5120 × 1440, les widgets derrière les applications
ordinaires et le terminal au-dessus de son cadre. Aucun onglet utilisateur fermé,
aucune saisie/prompt de test, aucun réglage GPU appliqué.

L’utilisateur a ensuite confirmé « Tout fonctionne » après l’essai demandé :
Ctrl+Espace, recherche « réglages » et clic sur « 6 cœurs » ou « Refroidissement »
avec Conrad restant en place. Le raccourci, l’accès aux réglages et l’ancrage du
volet sont donc confirmés en usage réel. Computer Use reste indisponible (native
pipe, erreur 2). Cette confirmation ne constitue pas un essai de chaque contrôle
des réglages, ni une validation matérielle des écritures GPU.

## Popup projets transparente — activée le 14 septembre 2026

Le bureau charge `build/battlestation-project-clear`, indiqué par
`build/current.txt`. Relevé live après rechargement : PID 16080 ; modules
Battlestation managés et natifs vérifiés dans ce dossier. Les versions et PID des
sections suivantes sont des relevés antérieurs.

La liste de projets reste dessinée derrière la popup. Ses cartes sont légèrement
floutées et grossies, avec une déformation plus forte dans trois bandes de bord,
un liseré nacré et un reflet lié au pointeur. La teinte sombre native de cette
popup passe de 46 % à 8 %, et son échantillon de fond diffus est mélangé à 22 %.
Le rendu des autres panneaux ne change pas. Il s'agit d'une approximation Windows
inspirée de Liquid Glass, pas du matériau système Apple ni d'une simulation
optique complète. Le cache WPF reste en mémoire et ne contient que les dessins
des cartes projets de cette ouverture, jamais les pixels du terminal ou du bureau.

Build sans avertissement ; `scripts/Validate.ps1` passe. Le contrôle isolé
`artifacts/validation/project-glass-check/Check.csproj` compile les vraies sources
DeskSurface/Surface avec des données factices et sans backend : six cibles dans
la liste, trois dans la popup même après plusieurs rendus, fermeture et nouvelle
sélection vérifiées. Les cartes de fond ne reçoivent pas les clics de la popup.
Les PNG de ce contrôle sont des aperçus, pas des preuves du bureau installé.

L'utilisateur a ensuite ouvert la popup sur le bureau réel. Capture limitée au
bloc Projets : `artifacts/validation/project-clear-popup-live.png`, avec état
`project-clear-popup-live.json`. Les cartes restent perceptibles sous le verre et
les actions sont lisibles. Computer Use n'a pas pu se connecter au native pipe
(erreur 2) : ouverture par l'utilisateur confirmée, mais les actions Explorateur,
Codex CLI et fermeture n'ont pas été réessayées par clics réels dans cette session.

Rechargement du seul bureau : 11908 → 16080. Hôte courant 14608, HWND 1114672,
session `aff613ee-739a-4ea4-b1a4-c005fa642537`, shell 11532 inchangés. Hôte ancien
6312, HWND 132134, session `9b830497-a552-4177-8fa8-e443b8761a46`, shell 9540
inchangés. Les neuf blocs gardent leurs positions ; fond animé en 5120 × 1440,
hôte terminal au-dessus de son cadre. Aucun onglet fermé, aucune saisie de test
dans un terminal utilisateur, aucun changement GPU ni configuration de démarrage.
Preuves avant/après : `project-clear-{desktop,terminal,legacy}-{before,after}.json`
et `dotnet-project-clear-windows.json` dans `artifacts/validation`.

## Onglets et ascenseur glass — activés le 14 septembre 2026

Le bureau **et** son hôte terminal chargent maintenant
`build/battlestation-glass-live`, désigné par `build/current.txt`. Dernier relevé :
bureau PID 11908, hôte PID 2804, HWND 722054, `chromeVersion=1`,
`externalChrome=true`, `headerVisible=false`. La barre glass est dessinée dans
le cadre du bureau ; l'ascenseur nacré est chargé dans le contrôle natif.

Cause du problème persistant : le redémarrage du bureau préservait l'hôte 6312
de `battlestation-single-header`. Fermer puis recréer ses onglets ne renouvelait
pas cet hôte. Le nouvel hôte utilise le pipe et mutex
`Battlestation.NativeTerminal.v1`, séparés de l'ancien protocole. Le script
`Invoke-Battlestation.ps1 -Terminal` cible ce nouvel hôte. La fermeture explicite
du dernier onglet du nouvel hôte provoque désormais sa sortie ; le masquage du
bloc et le rechargement du bureau conservent toujours les sessions ouvertes.

L'ancien hôte reste détaché à (120,120) sur le premier écran, avec son onglet
Battlestation PID 9540, identifiant `9b830497-a552-4177-8fa8-e443b8761a46`,
inchangé avant/après bascule. Trois onglets avaient été observés au début du
diagnostic ; un seul subsistait au relevé juste avant la bascule. Aucune fermeture
d'onglet utilisateur n'a été envoyée. L'ancien hôte reste joignable explicitement
par `-PipeName Battlestation.NativeTerminal` jusqu'à sa fermeture utilisateur.

Le nouveau bureau a ensuite été rechargé une seconde fois : PID 12996 → 11908.
Hôte, HWND, identifiants et PID de ses deux sessions sont identiques avant/après ;
la barre externe est rétablie. Les neuf blocs gardent exactement leur disposition,
le fond est en 5120 × 1440 et le terminal reste au-dessus de son cadre, derrière
les applications ordinaires. Aucune modification GPU ou du démarrage Windows.

Build sans avertissement ; `scripts/Validate.ps1` passe. Le test terminal vérifie
aussi le template glass réellement appliqué au ScrollBar natif, son défilement,
et la notification de fermeture du dernier onglet, sans retraite lors du masquage.
Ces tests WPF ne remplacent pas les clics réels. Aucun prompt Codex envoyé.

Preuves dans `artifacts/validation` : `glass-live-legacy-before.json` et
`glass-live-legacy-after.json`, `glass-live-terminal-before-reload.json` et
`glass-live-terminal-after-reload.json`, `glass-live-desktop-final.json`,
`dotnet-glass-live-final-windows.json`. `terminal-glass-live-screen.png` montre
les onglets sur le bureau réel, avec une fenêtre Explorateur devant sa droite.
`terminal-glass-live-scrollbar.png` capture uniquement l'ascenseur de l'hôte réel
via PrintWindow, sans conserver son contenu terminal.

Les états ci-dessous sont historiques, notamment les mentions d'activation en attente.

## Récupération et démarrage Windows — 14 septembre 2026

Le bureau affiché au début de la session était l'ancien Rainmeter, PID 16400.
Battlestation n'était pas lancé. Le build `battlestation-single-header` et la
disposition personnelle du 13 septembre étaient toujours présents. Le lancement
du build courant a retrouvé les neuf blocs et leurs positions sauvegardées.

À la demande explicite de l'utilisateur le 14 septembre, le démarrage Windows
est maintenant activé : tâche `Battlestation`, utilisateur interactif Ayo,
sans élévation de l'application, priorité High (1), aucun délai de déclenchement,
aucune limite de durée ni restriction secteur/batterie. Le lanceur lit
`build/current.txt`, attend la surface du bureau Windows et quitte après le
lancement. Aucune session terminal n'est fermée par ces scripts.

Rainmeter a été désinstallé avec son désinstalleur. Son démarrage et ses dossiers
installés de skins, plugins et données utilisateur ont été retirés. Les projets
Conrad Sensor, Codex Meter, le fond sous Steam et l'historique Git n'ont pas été
modifiés. Rainmeter était déjà arrêté au relevé précédant la désinstallation ;
sa dernière sonde datée indiquait Canicule désactivée et aucune consigne GPU
chargée. Ce snapshot ne constitue pas une nouvelle mesure matérielle. Aucun
réglage GPU n'a été envoyé.

La tâche a été exécutée réellement après fermeture du seul bureau : PID 12408,
priorité High confirmée, retour de tâche 0, fond attaché sur 5120 × 1440 et
neuf blocs visibles. L'hôte terminal 6312 et son HWND 132134 ont survécu.
Les listes d'onglets ont changé pendant l'essai : l'utilisateur a confirmé avoir
lui-même fermé les deux anciens onglets et ouvert le nouvel onglet Battlestation.
Aucune fermeture de session n'a été envoyée par la migration. Rapports avant/après dans
`artifacts/validation/recovery-20260914`, tâche exportée dans
`artifacts/validation/battlestation-startup.xml`.

Les quatre suites managées et les contrats natifs passent. Les tests terminal
utilisent leurs propres fenêtres et shells, sans pipe de commande utilisateur.
Le démarrage réel du PC et la durée d'un éventuel aperçu du bureau Windows
restent à confirmer au prochain redémarrage ; aucun redémarrage n'a été imposé.

Les sections suivantes décrivent les relevés historiques du 13 septembre.

## Onglets et ascenseur glass — état du 14 septembre

Le build `build/battlestation-glass-chrome` réunit la barre `TerminalTabs` préparée
précédemment et le nouveau style `TerminalScrollBar`. Celui-ci habille le
ScrollBar existant du contrôle Windows Terminal : rail sombre, curseur nacré
arrondi et survol animé. Les événements du moteur et le Track WPF sont conservés.

Compilation réussie sans avertissement ; `scripts/Validate.ps1` passe entièrement.
Le shell de test produit 120 lignes pour vérifier le défilement par page et le
retour en bas avec le nouveau template. Sélection des onglets, bascule des barres,
conservation des PID, ConPTY, UTF-8 et masquage/réaffichage passent également.
Ces commandes WPF ne constituent pas une validation par clics réels.

`artifacts/validation/terminal-glass-chrome-preview.png` présente les vrais
contrôles WPF sur un fond de démonstration sans session terminal. Ce n'est pas une
preuve du rendu installé dans l'hôte utilisateur.

Le bureau seul a été rechargé : PID 12408 → 14484. Ses modules Battlestation
chargés proviennent de `build/battlestation-glass-chrome`, désormais désigné par
`build/current.txt`. La disposition des neuf blocs est strictement conservée,
avec le fond 5120 × 1440 et l'hôte terminal au-dessus de son cadre.

**Le terminal utilisateur conserve encore son ancien style.** Son hôte PID 6312,
HWND 132134, charge toujours `build/battlestation-single-header`. Il n'annonce
pas `chromeVersion` et ne peut pas changer de barre à chaud. Son unique onglet
Battlestation, session `d8ed8883-4a0c-40c2-97e0-95503a28bedc`, PowerShell PID 10868,
est resté prêt, sans erreur et avec la même identité avant/après rechargement.
L'ouverture d'un nouvel onglet dans cet hôte garderait aussi l'ancien style.
L'activation complète exige le remplacement de cet hôte ; l'onglet actif n'a pas
été fermé. Aucune saisie ni génération Codex de test n'a été envoyée.

Preuves : `glass-chrome-terminal-before.json`, `glass-chrome-terminal-after.json`,
`glass-chrome-desktop-before.json`, `glass-chrome-desktop-after.json` et
`dotnet-glass-chrome-ready-windows.json` dans `artifacts/validation`.
Les PID et chemins des sections suivantes sont des relevés historiques.

Le bureau utilise désormais des projets nommés Battlestation et une disposition
modulaire. Le dossier parent est renommé par l'utilisateur, pas par les scripts.
L'historique de travail antérieur est préservé localement dans `.git/local-history`.

## Contrôles automatisés

- 13 contrats de données : absence, zéro réel, bornes GPU, modèles sans prix,
  estimation partielle et passage de minuit. Aucun compte interrogé ni GPU modifié.
- Placement des fenêtres : remontée du shell avec ou sans topmost, retour derrière
  une application, géométrie, focus et survie d'un hôte externe. Le correctif Win+D
  précédent a été confirmé par l'utilisateur ; le nouveau bureau doit aussi être
  essayé en interaction.
- Disposition : neuf blocs, placement sans collision, deux écrans, retrait du
  compteur indépendant, expansion des deux blocs de monitoring, persistance,
  agrandissement du dock et restauration depuis un bureau entièrement masqué.
- Terminal : vraie pseudo-console Windows avec PowerShell, aller-retour de saisie,
  UTF-8, séquences truecolor et écran alternatif, redimensionnement et trois
  masquages/réaffichages. L'entrée est injectée dans un shell de test, pas dans
  Codex. Ce test ne valide pas une frappe physique ou le collage d'image utilisateur.

Les projets de tests ont des sorties séparées afin d'éviter les collisions
d'apphost quand plusieurs projets résident dans le même dossier.

Les contrats C++ de l'horloge, de la météo et de la pochette en mémoire passent
aussi. La solution `Battlestation.slnx` regroupe les trois projets applicatifs et
les quatre projets de tests managés. Les sources actives ne contiennent plus le
nom historique ; les références externes et les archives ne sont pas réécrites.

## Version chargée et preuves du 13 septembre

- Bureau `build/battlestation-desktop/Battlestation.exe`, PID 16760 lors du relevé.
  Le fichier local `build/current.txt` est utilisé par le script de lancement.
- Hôte terminal natif `build/battlestation-modular-native/Battlestation.exe`, PID
  14692, HWND 1180626. PowerShell 21176, session
  `d4edfa11-e0b6-4b1b-886d-3b63e540a5a4`, prêt et sans erreur au relevé.
- Rechargement du bureau 15076 → 16760 : hôte, HWND, identifiant de session et
  processus PowerShell identiques. Preuves `battlestation-reload-before.json` et
  `battlestation-reload-after.json` dans `artifacts/validation`.
- Mode édition exercé par le pipe : terminal masqué puis réaffiché sans changer
  son HWND ni sa session. Preuve `battlestation-edit-terminal-survival.json`.
- Horloge déplacée et compteur Vice City retiré par commandes internes : le verre
  suit l'horloge, le compteur disparaît seul, matériel et Codex restent présents.
  Capture `dotnet-independent-blocks-screen.png`. Disposition initiale restaurée.
- Capture du mode édition : `dotnet-modular-edit-mode-screen.png`. Ces captures
  sont celles du bureau réel ; les actions internes ne sont pas des clics physiques.
- Météo réelle relue : 21 °C, ciel dégagé, mesure de 21:00. Le fond reste en
  5120 × 1440 ; il se met en pause lorsqu'une application plein écran a le focus,
  comme avant, et les changements de verre sont repeints pendant cette pause.
- Les six anciens onglets sont conservés : hôte 9876, moteur 12820 et HWND
  10224726 inchangés. Leur fenêtre détachée est placée à (120,120) sur le premier
  écran pour ne pas couvrir le nouveau terminal. Aucun de ces onglets n'a été fermé.

L'hôte terminal garde volontairement le binaire déjà chargé afin de préserver la
session. Les nouvelles ouvertures d'hôte utilisent le build courant. Revérifier
toutes ces identités avant intervention : ce sont des observations datées.

## Points à confirmer en usage réel

L'utilisateur a confirmé que le déplacement des blocs fonctionne. Menus et focus
clavier du nouveau terminal, sélection,
défilement, collage texte/image et interface Codex sans consommation de crédit
automatique. Veille, changement de DPI et retour de plein écran restent distincts.
Computer Use est indisponible dans cette session (`native pipe`, erreur 2).

Les sessions existantes de l'ancien terminal sont conservées à part. Elles ne
sont pas migrées dans une autre pseudo-console et ne doivent pas être fermées
pour valider le nouveau rendu.

Pas de réglage GPU, de changement d'autostart ni de gain de ressources revendiqué.
Le paquet de distribution complet et la validation matérielle restent ouverts.

## Clic du terminal intercepté par le cadre — correctif installé

L'utilisateur a signalé que le clic ne prenait pas dans le terminal, malgré la
création possible d'onglets depuis les projets. Le test ConPTY précédent vérifiait
le transport de saisie, pas la fenêtre qui reçoit physiquement la souris.

`WindowFromPoint` a identifié le cadre WPF (HWND 1442680) aux trois points de la
zone de saisie (2704,624), (2744,624), (2784,624). Le terminal était sous ce cadre.
`DesktopPlacement` conserve maintenant l'hôte natif immédiatement au-dessus de
son cadre, tout en restant derrière les applications ordinaires. Cet ordre est
réparé même en dehors d'une transition Win+D.

Après installation, les trois points désignent le contrôle natif HWND 6423282,
racine 1180626, correspondant à l'hôte du terminal. Preuves locales :
`terminal-hit-before.json`, `terminal-hit-after.json` et les deux rapports
`terminal-hit-reload-before.json` / `terminal-hit-reload-after.json`.

Le bureau chargé est `build/battlestation-terminal-hit/Battlestation.exe`, PID
2804 au relevé. L'hôte 14692 et ses deux sessions (PowerShell 21176, projet Codex
Meter 1972) sont conservés ; positions et visibilité des neuf blocs sont identiques
avant/après. Aucun texte ni prompt de test n'a été envoyé à ces sessions.

La régression d'ordre cadre/terminal est couverte sur les fenêtres Win32/WPF de
test, avec et sans topmost, retour aux applications et rétrogradation de l'hôte.
Le routage réel de la souris est vérifié sans injection de clic ; la frappe
physique après correction reste à confirmer par l'utilisateur.

## Commandes

### Onglets liquid glass — préparés, activation en attente

`build/battlestation-glass-tabs` contient `TerminalTabs`, une barre WPF transparente
dessinée dans le cadre du bureau : onglets arrondis, sélection nacrée, fermeture,
collage, nouveau terminal et défilement lorsque les onglets débordent. Le fond
Direct2D du cadre reste visible derrière cette barre. L'hôte natif n'affiche que
la console lorsqu'il annonce le protocole `chromeVersion=1`.

Le protocole ajoute `chrome:external`, `chrome:internal`, `select:<guid>` et
`close-tab:<guid>`. L'identifiant est vérifié avant toute action. La barre interne
revient si le terminal est détaché. Les anciens hôtes restent compatibles : sans
`chromeVersion`, le bureau conserve leur barre existante.

Build sans avertissement. Le test terminal ouvre uniquement ses propres shells,
vérifie le passage barre interne/externe, la sélection et la conservation des PID,
rejette une fermeture avec identifiant périmé, puis ferme ses shells de test.
Il n'agit pas sur les sessions utilisateur et n'envoie rien à Codex.

`artifacts/validation/terminal-glass-tabs-preview.png` montre le vrai contrôle
sur un fond de démonstration, sans démarrer de terminal. Ce n'est pas une capture
du bureau installé. Commande de reproduction :
`Battlestation.exe --preview-terminal-tabs <chemin-png>`.

L'hôte utilisateur 14692 est toujours l'ancien binaire et ne sait pas remplacer
sa barre à chaud. Les deux sessions PowerShell 5 (6856) et PowerShell 6 (4388)
restent ouvertes. Le nouveau build n'a pas été activé : fermer ces sessions pour
redémarrer l'hôte nécessite une autorisation explicite de l'utilisateur.
`build/current.txt` reste sur `build/battlestation-single-header`.

### Barre unique du terminal

La rangée native d'onglets est désormais l'unique en-tête du bloc attaché. Le
contenu commence à 12 px du haut du cadre au lieu de 40 px ; il gagne 24 px de
hauteur, avec une marge basse de 12 px. Le « + » natif reste près des onglets.
Coller/précédent/suivant/nouvel onglet restent dans le menu contextuel du cadre.

Build chargé : `build/battlestation-single-header`, bureau 10668 au relevé.
L'hôte 14692 / HWND 1180626 et les deux sessions PowerShell 6856 et 4388 ont été
conservés, ainsi que la disposition réorganisée par l'utilisateur. Le contrôle
natif reste au-dessus du cadre. Capture réelle :
`artifacts/validation/dotnet-single-header-ready-screen.png`. Les rapports
`single-header-before.json` et `single-header-after.json` gardent les identités ;
le relevé `dotnet-single-header-ready-windows.json` confirme le placement final.

```powershell
.\scripts\Build-Battlestation.ps1 -OutputDirectory build/battlestation-next
.\scripts\Validate.ps1
.\scripts\Start-Battlestation.ps1
.\scripts\Invoke-Battlestation.ps1 -Command inspect
.\scripts\Invoke-Battlestation.ps1 -Terminal -Command inspect
.\scripts\Inspect-Battlestation.ps1 -Capture -Label current
```

Ne pas écraser un dossier contenant un binaire encore chargé, y compris l'hôte
terminal. Recharger uniquement le bureau par `close`, attendre la fin de son PID,
puis lancer le nouveau build. Vérifier les identités des sessions avant/après.
