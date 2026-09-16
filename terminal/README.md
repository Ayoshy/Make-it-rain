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

`Start-Shell.ps1` prépare les couleurs et les commandes Codex dans l'onglet
seulement. `Start-Codex.ps1` démarre le CLI dans le projet choisi sans envoyer de
prompt. L'action Codex CLI (DS) crée aussi un onglet PowerShell dans le projet
sélectionné, puis `codex-ds` surcharge le fournisseur DeepSeek, le modèle et le
catalogue `codex-deepseek-models.json` du dossier courant sur la ligne de commande,
sans modifier la configuration Codex globale. La clé reste dans l'environnement
utilisateur (`DEEPSEEK_API_KEY`).
Le lancement ne force jamais la méthode d'authentification : les options
`preferred_auth_method`/`forced_login_method` font répondre au CLI « API key
login is required, but ChatGPT is currently being used. Logging out. » et
suppriment `auth.json`, ce qui oblige à refaire la connexion ChatGPT.
L'action Kilo CLI (DS) crée un onglet PowerShell dans le projet sélectionné et lance
`kilo` (`kilo.cmd` du dossier npm de l'utilisateur), sans envoyer de prompt ni
fournir d'identifiant. Le CLI met à jour son titre de console ; Battlestation le
reprend comme titre automatique de l'onglet.
Aucun paramètre global ni identifiant Codex n'est lu ou modifié par ces scripts.

Des sessions de l'ancien terminal peuvent rester ouvertes pendant la transition.
Leur fermeture appartient à l'utilisateur ; elles ne sont pas importables dans
une nouvelle pseudo-console.
