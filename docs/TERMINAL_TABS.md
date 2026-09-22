# Onglets du terminal

Clic droit sur un onglet : renommer, choisir une couleur (palette ou hexadécimal),
revenir au titre automatique, activer/désactiver les effets ou réinitialiser.
Ces choix sont conservés par identifiant de session dans
`%LOCALAPPDATA%\Battlestation\terminal-tabs.json`.
La couleur choisie teinte le fond de l’onglet, même inactif ; Par défaut retrouve
le verre commun. L’indicateur d’activité garde sa propre couleur.

Les états Codex utilisent uniquement le titre de console : anneau bleu tournant
pour Working, anneau lavande plus lent pour Thinking, ✓ vert pour Ready,
! ambre pour une action requise. ? gris indique un état inconnu. Les animations
respectent le réglage Windows et l'option Effets d'activité de l'onglet.

Dans Codex CLI 0.154.0, `/title` propose `run-state` (Ready, Working, Thinking)
et `activity` (dont l'action requise). Garder aussi `thread-name` et `project-name`.
Le lanceur local fournit ces champs aux nouvelles sessions ; les CLI déjà ouverts
doivent régler leur titre individuellement. Le nom du champ est `run-state`,
pas `Status`. Un nom manuel reste prioritaire jusqu'au retour à Titre automatique.

Depuis Kilo 7.7.2, le CLI écrit `Kilo CLI | <sujet de session>` (tronqué à
40 caractères), et `Kilo CLI` seul sur l'écran d'accueil. Battlestation reprend le
sujet comme titre automatique, sans le préfixe ; seul ce format est reconnu, les
titres transitoires du lanceur sont ignorés. Les icônes d'état `◔`, `⚠` et `✓`
n'apparaissent que si l'option `title_icon` vaut `unicode` ou `emojis` dans
`%USERPROFILE%\.config\kilo\tui.json` (fichier TUI séparé de `kilo.jsonc`,
défaut `none`) ; elles correspondent à Working, Attention et Ready.

Un helper isolé lit le titre des consoles ConPTY existantes et vérifie la présence
de Codex ou de Kilo dans leur liste de processus. Il ne lit ni sortie terminal, ni historique,
ni configuration de compte ; il n'envoie aucune saisie et ne lance aucune génération.
Le titre dynamique reste en mémoire. Les préférences seules sont sauvegardées.
Le helper appartient au bureau et peut être remplacé sans redémarrer l'hôte terminal.
La barre interne d'un ancien hôte détaché reste celle de son ancien build.

Validation du 14 septembre 2026 : couleurs, renommage, retour automatique et menu
confirmés par les clics utilisateur. Working, Ready et le titre réel
`[ ! ] Action Required` ont été observés. Le dernier remplacement du point par un
anneau a été contrôlé en aperçu WPF et en tests ; sa lisibilité en usage réel reste
à confirmer par l'utilisateur. Tests : préférences, parsing, menu, stabilité des
cartes, animation et lecture du titre d'une console synthétique sans saisie.

## Cache de prompt

La barre reste passive : aucune requête de maintien du cache, aucune saisie dans
les sessions et aucun appel facturé.

Chaque onglet est rattaché au **processus Codex de sa console**, puis à son journal
ouvert en écriture dans `~/.codex/sessions`. Le helper expose seulement son PID et
le titre de console. La lecture de mémoire et de ligne de commande a été retirée.
Les handles sont dupliqués localement pour lire leur chemin, jamais leur contenu ;
les handles du processus distant ne sont ni fermés ni modifiés. Les journaux des
sous-agents sont exclus par `session_meta.source`. Sans association certaine, ou
si plusieurs conversations principales sont ouvertes dans le même processus,
aucun journal n'est deviné à partir du projet ou de sa date de modification.

Le lecteur parcourt initialement le journal lié, puis uniquement les octets
ajoutés. Il conserve en mémoire les métadonnées, compteurs et horodatages utiles,
jamais les messages. Les lignes incomplètes attendent le prochain passage ; une
ligne dépassant 4 Mio est ignorée et invalide la mesure jusqu'à la prochaine
observation exploitable. Aucun cache n'est écrit sur disque. Un minuteur commun
relit l'état toutes les dix secondes et s'arrête lorsque la barre est masquée.

Pour OpenAI, « ⏳ ≈ 12 min » est une **estimation** depuis la dernière réponse
ayant effectivement rapporté des jetons lus ou écrits en cache. Un événement
local ou une notification de compteurs cumulés identique ne remet pas le compteur
à zéro. Un changement de modèle, une compaction, un cache miss ou une remise à zéro
des compteurs invalide l'estimation. En l'absence de compteur d'écriture, un
premier appel sans lecture de cache ne suffit donc pas à annoncer un cache chaud.

La fenêtre de trente minutes s'applique aux identifiants connus GPT-6 Astra et
GPT-5.6 / Sol / Terra / Luna. Un modèle inconnu ou ancien ne reçoit pas cette durée
par substitution. Après la fenêtre, le badge devient **« cache incertain »** :
l'expiration côté serveur n'est pas observable et la conservation peut durer plus
longtemps. Le survol explique l'estimation. Référence :
[documentation OpenAI sur la durée du cache](https://developers.openai.com/api/docs/guides/prompt-caching#cache-lifetime).

Sans couleur personnelle, les teintes restent verte au-delà de vingt minutes,
ambre de dix à vingt, rouge en dessous de dix et grise lorsque le cache est
incertain. La couleur personnelle conserve toujours la priorité.

DeepSeek affiche « cache 99,8 % », la part d'entrée réutilisée dans la **dernière
réponse mesurée de la conversation liée**. Ce n'est pas une durée de disponibilité
ni une moyenne de session. La valeur est tronquée au dixième. Sans compteur valide,
aucun taux n'apparaît : absence ne signifie pas zéro, et une valeur incohérente
n'est pas ramenée artificiellement à 0 ou 100 %. Les notifications `info:null`
concernant les quotas sont ignorées sans interrompre les autres onglets.

La validation historique du 21 septembre 2026 est remplacée par les régressions
ci-dessus : elle ne démontrait pas l'isolation entre deux sessions du même projet
et du même fournisseur. `tests/Battlestation.TerminalCache.Tests.csproj` couvre
maintenant cette isolation, les sous-agents, les notifications dupliquées,
compactions, modèles, compteurs absents, lignes incomplètes, fichiers tronqués,
liaison Windows à un processus synthétique et rendu WPF des badges/couleurs.
Validation et livraison du 22 septembre 2026 : suite TerminalCache et compilation
complète réussies ; le rendu WPF et une capture du bureau réel montrent les badges
« ⏳ ≈ … min ». Ayo a confirmé la lisibilité, le changement d'onglet au clic et
l'explication au survol sur `battlestation-cache-observation-01`. Computer Use
n'avait pas de connexion native disponible ; les gestes sont donc validés par
l'utilisateur, pas par l'automatisation. Les trois identifiants d'onglets, leurs
PID de shell et l'hôte 2196 sont conservés. Les empreintes de `layout.json` et de
`terminal-tabs.json` sont identiques avant/après.

Le bureau et son helper chargent `build/battlestation-cache-observation-01` ;
`build/current.txt` sélectionne ce même build. La tâche Windows appelle toujours
`Start-Battlestation.ps1 -AtLogon` sans build fixé. Aucun raccourci Battlestation
n'a été trouvé sur les bureaux, menus Démarrer ou éléments épinglés contrôlés.
Le build remplacé `shopping-relevance-02`, libéré de ses processus/modules et sans
référence du pont vidéo, est archivé sous `backups/retired-builds/2026-09-22/`.
L'hôte terminal `deepseek-cli-03` et le pont vidéo `cache-tabs-08` restent en place.

Les mesures de vingt secondes incluent bureau, helper de métadonnées, hôte terminal
et pont vidéo. CPU bureau/helper : 5 171,9 / 31,2 ms avant, 7 343,8 / 140,6 ms après.
Les widgets exposés et leur activité ont varié pendant l'usage : ces échantillons
ne permettent pas d'attribuer un gain ou une régression au cache. Gain global :
non mesurable. Le lecteur seul, sur les conversations actives, prend environ six
secondes au premier parcours en arrière-plan puis 14–16 ms aux passages suivants.
Preuves locales : `artifacts/cache-review-20260922/`.
Validation du 16 septembre 2026 : les titres Kilo live `Kilo CLI | <sujet>` sont
repris comme titres automatiques d'onglets sur le bureau (sujets observés dans
l'inspection des onglets). Le parsing Kilo (icônes comprises) et la non-régression
Codex sont vérifiés par harnais temporaire ; les icônes d'état restent à observer
avec `tui.title_icon` activé.
