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

Un helper isolé lit le titre des consoles ConPTY existantes et vérifie la présence
de Codex dans leur liste de processus. Il ne lit ni sortie terminal, ni historique,
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
