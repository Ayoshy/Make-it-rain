# Terminal Battlestation

Le terminal est rendu par le contrôle natif de Windows Terminal et connecté aux
shells par ConPTY. Il ne lance pas d'application de terminal externe.

Les onglets vivent dans un hôte Battlestation indépendant de l'interface du bureau.
Déplacer, masquer ou réafficher le bloc conserve les processus. Une fermeture de
l'interface détache l'hôte ; le rechargement retrouve les mêmes sessions.

Les boutons +, précédent et suivant gèrent les onglets. Le bouton × d'un onglet
ferme explicitement sa session. Fermer la fenêtre d'hôte la masque pour éviter
une interruption accidentelle. Ctrl+V colle le texte, ou transmet la demande de
collage d'image au CLI. Le contenu du terminal n'est pas journalisé.

`Start-Shell.ps1` prépare les couleurs et la commande Codex dans l'onglet seulement.
`Start-Codex.ps1` démarre le CLI dans le projet choisi sans envoyer de prompt.
Aucun paramètre global ni identifiant Codex n'est lu ou modifié par ces scripts.

Des sessions de l'ancien terminal peuvent rester ouvertes pendant la transition.
Leur fermeture appartient à l'utilisateur ; elles ne sont pas importables dans
une nouvelle pseudo-console.
