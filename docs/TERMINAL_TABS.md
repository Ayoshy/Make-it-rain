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

Les onglets qui font tourner Codex portent un compte à rebours du cache de prompt :
« ⏳ 12 min » tant que le préfixe reste réutilisable, « cache expiré » ensuite. La
fenêtre de 30 minutes est celle documentée par OpenAI après la dernière écriture ou
réutilisation du préfixe ; l'indicateur est donc une estimation, pas une garantie du
fournisseur. Il suit l'horodatage du dernier événement du rollout le plus récent du
projet suivi (`~/.codex/sessions`), relu toutes les 10 s par le minuteur unique de la
barre d'onglets, sur une tâche de fond et sans écrire sur le disque. Seuls
l'horodatage et les compteurs sont lus ; jamais le contenu des messages.

Tant qu'aucune couleur n'est choisie, le fond de l'onglet suit le restant : vert
au-delà de 20 minutes, ambre de 10 à 20, rouge en dessous de 10, gris une fois
expiré. Une couleur choisie reste toujours prioritaire : seul le texte du compte à
rebours s'ajoute. Les onglets Codex (DS) n'ont pas de compte à rebours, DeepSeek ne
publiant aucune durée de vie ; ils affichent le taux de hit du **dernier tour de leur
propre session** — « cache 99,8 % », au dixième et jamais surestimé, lu dans les
compteurs `last_token_usage` du rollout de l'onglet, le total de la session servant
tant que le tour courant n'est pas compté. Il bouge donc à chaque tour, comme le
compte à rebours bouge à chaque événement, et deux onglets du même projet gardent
chacun le leur. Sans compteur, sans rollout ou sur un onglet qui ne fait pas tourner
Codex, aucun badge n'apparaît.

Le projet et la variante viennent du CLI lui-même : le helper lit la ligne de
commande du processus Codex de l'onglet — `model_provider="deepseek"` et le chemin
de `-C` — sans ouvrir de console ni écrire quoi que ce soit. Deux onglets du même
projet gardent donc chacun sa variante : l'onglet (DS) montre le taux de hit,
l'onglet ChatGPT le compte à rebours. Sans ligne de commande lisible, le dernier
champ du titre Codex sert d'indice de projet, comme l'indice de projet des docks, et
le fournisseur du rollout le plus récent tranche pour la variante. La sélection du
rollout compare les derniers candidats par l'horodatage de leur dernier événement :
la date de modification du fichier d'une session en cours peut retarder sur son
contenu.

Validation du 21 septembre 2026 : sur le bureau chargé, les deux onglets Codex du
même projet ont affiché chacun son état — deux sessions DeepSeek avec leur propre
taux de dernier tour (« cache 95 % » puis « cache 99,9 % », puis « cache 99,8 % »
sans rechargement), et un onglet ChatGPT avec son compte à rebours vert (« ⏳ 27 min »
puis « ⏳ 26 min »), la valeur décroissant de même. Le helper a rendu, pour chaque
shell, son projet et sa variante (`DeepSeek=true` sur les deux sessions `codex-ds`,
`false` sur l'onglet ChatGPT). Un rollout synthétique du même projet, plus récent
par son dernier événement mais daté d'une heure plus tôt sur le disque, avait déjà
fait apparaître en quatorze secondes un compte à rebours vert, puis le badge
précédent est revenu après suppression : la relecture périodique et la sélection par
horodatage réel sont observées en direct. La session du terminal a survécu à quatre
rechargements du bureau (même PID de shell). Les teintes ambre, rouge et grise et
l'absence de badge sans activité sont couvertes par les tests de rendu et l'aperçu
`--preview-terminal-tabs`.

Validation du 16 septembre 2026 : les titres Kilo live `Kilo CLI | <sujet>` sont
repris comme titres automatiques d'onglets sur le bureau (sujets observés dans
l'inspection des onglets). Le parsing Kilo (icônes comprises) et la non-régression
Codex sont vérifiés par harnais temporaire ; les icônes d'état restent à observer
avec `tui.title_icon` activé.
