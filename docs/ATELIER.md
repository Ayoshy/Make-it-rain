# Atelier

Deux actions pour les builds de ce PC :

- **Garder cette version** sélectionne le build réellement ouvert pour les
  prochains lancements. Le bouton vaut acceptation explicite de cette version.
  L'état devient **Version conservée** après l'écriture de `build/current.txt`.
- **Nettoyer les builds** supprime les dossiers compilés inutilisés sous `build/`.
  L'espace libérable est affiché avant le clic, l'espace réellement libéré après.

**Détails** donne les chemins du build ouvert, du prochain lancement et la liste
des builds protégés. Aucun nom d'ancien chantier n'est présenté comme un essai en
cours. Il n'y a plus de note, de verdict ni d'essai à préparer. Les anciens fichiers
locaux `atelier-trial.json`, `atelier-feedback.json` et `atelier-runtime.json`
restent intacts mais ne sont plus utilisés.

## Workflow

1. Compiler dans un nouveau dossier avec `Build-Battlestation.ps1`.
2. Charger explicitement le candidat en préservant les hôtes terminal.
3. Vérifier les comportements concernés ; cliquer **Garder cette version** quand
   le résultat convient. Aucun rechargement ni fermeture de session n'en découle.
4. **Nettoyer les builds** peut être utilisé séparément. Il ne change pas la version
   sélectionnée et ne ferme aucun processus.

Le raccourci et la tâche Windows doivent utiliser `Start-Battlestation.ps1`, qui
lit `build/current.txt`. Le chargement d'un candidat seul ne le sélectionne jamais.
Les modifications de disposition ne créent pas de build ni d'essai artificiel.

## Nettoyage

`scripts/Clean-BattlestationBuilds.ps1 -LoadedBuild <chemin>` donne un bilan JSON
sans supprimer. `-Apply` exécute la même action que le bouton.

Les protections portent sur le build ouvert, le prochain lancement, les chemins
et commandes des processus (dont les hôtes terminal), les modules accessibles,
le manifeste du pont vidéo, les raccourcis du bureau/démarrage et les tâches
Windows. Les références actives sont relues avant chaque suppression. Un fichier
verrouillé fait laisser le build en place. Les liens de fichiers et dossiers sans
exécutable Battlestation restent hors nettoyage. Une sélection de démarrage
manquante ou invalide bloque le nettoyage.

Le nettoyage vise uniquement les dossiers immédiats de `build/`, jamais Git,
les sources, les archives ou les réglages personnels. Il tourne dans un helper
PowerShell caché, hors du fil d'interface. Le calcul complet a lieu à la première
exposition du dock puis après une action ; seule la sélection de démarrage est
relue toutes les huit secondes lorsque le dock est visible et exposé.

## Vérification

- `dotnet run --project tests/Battlestation.Atelier.Tests.csproj -c Release`
- `tests/BuildCleanup.Tests.ps1` : protection des builds, aperçu sans mutation,
  fichier verrouillé et espace libéré dans un dossier de fixtures.
- Les rendus WPF sont distincts des clics et de la validation sur le bureau.
