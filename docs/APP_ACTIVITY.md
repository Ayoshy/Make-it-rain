# Icônes des applications en cours

Une application dont le processus est identifié prend le matériau néon Vice City
(rose, violet, cyan). L'arrêt de ses processus lui rend son matériau nacré.
Une fenêtre réduite ou en arrière-plan reste une application en cours. Fermer
une fenêtre peut laisser l'application fonctionner dans la zone de notification.

## Référence visuelle

Recherche effectuée dans toutes les branches, le reflog, les assets et les
références locales du dépôt. Le premier commit contenant `dock/icons`,
`86cd1d2`, possède déjà les cinq variantes nacrées. Le commit initial `e17ba3e`
ne contient pas cette bibliothèque. Aucun original coloré n'a été retrouvé.
Le 15 septembre 2026, l'utilisateur a confirmé « néon / Vice City » et demandé
de recréer les variantes. Ce rendu ne prétend donc pas reproduire un original
retrouvé.

`dock/icons/neon` conserve le nacré actuel, malgré son nom historique.
`dock/icons/running` contient les variantes colorées. Le générateur
`scripts/Build-RunningDockIcons.py` part des SVG nacrés existants et conserve
leurs tracés, transformations, proportions et contours. Il change les couleurs
du matériau et ajoute un halo contenu. Il ne télécharge rien et ne tourne jamais
dans l'application.

```powershell
python scripts/Build-RunningDockIcons.py
```

La palette est `#ff5dbb`, `#d96eff`, `#987bff`, `#63ddec`. Le dossier historique
reste intact ; les nouvelles silhouettes doivent d'abord recevoir leur variante
nacrée avant de régénérer les variantes actives.

## Détection

Le dock relève les processus en arrière-plan, au démarrage puis environ toutes
les deux secondes. Un seul relevé peut être en cours. Les raccourcis sont résolus
en cache ; seuls les processus candidats font l'objet d'une lecture du chemin
d'exécutable. Aucun relevé périodique WMI, aucune lecture de ligne de commande
des processus et aucun lancement ne servent à la détection.

| Entrée | Identité recherchée |
|---|---|
| Steam, Brave, Codex, Claude Code, Edge, Chrome, qBittorrent | Exécutable exact indiqué par la configuration ou le raccourci |
| Stremio | `stremio-shell-ng.exe`, cible réelle du raccourci actuel |
| Discord | `Discord.exe` dans un sous-dossier `app-*` de l'installation désignée par `Update.exe --processStart Discord.exe` |
| League of Legends | Client, interface du client ou jeu dans une installation associée au raccourci Riot ; le lanceur Riot seul est exclu |
| Battle.net | `Battle.net.exe` sous l'installation du raccourci `Battle.net Launcher.exe` ; `Agent.exe` est exclu |

Le CLI Codex installé possède plusieurs liens physiques vers le même binaire.
Lorsque le chemin remonté par Windows diffère, la détection compare l'identité
du fichier (volume et index) avec `GetFileInformationByHandle`, sans lire son
contenu. Une simple copie portant le même nom n'est pas acceptée.
Voir la [référence Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfileinformationbyhandle).

Une erreur d'accès ou un raccourci non résolu produit un état **inconnu**. Le
dernier matériau confirmé est conservé et le survol précise l'incertitude.
Une entrée nouvelle sans état confirmé commence en nacré. Un raccourci `.url`
reste inconnu : le simple fonctionnement d'un navigateur n'identifie pas
l'application visée par une URL.

Les ouvertures et fermetures produisent un fondu de 260 ms. Les clics, le zoom
et le déplacement au survol restent ceux du dock existant. Les applications
ajoutées sans asset local conservent leur icône Windows habituelle.

## Validation du 15 septembre 2026

- Build complet et tests `Battlestation.AppActivity.Tests` / `Battlestation.DockReview.Tests` réussis.
- Comparaison des onze SVG : tracés, transformations et dimensions inchangés.
- Tests des lanceurs exclus, chemins étrangers, liens physiques, accès inconnu,
  changement de nom, fondu intermédiaire/final, arrêt, ordre et zones de clic.
- Bureau réel : applications déjà présentes reconnues ; lancement extérieur d'un
  Chrome avec profil de test isolé, fenêtre effectivement réduite vérifiée par
  Windows, puis arrêt du processus de test et retour visible au nacré.
- Captures réelles avant/après dans `artifacts/validation/apps-resume-external-*.png`.
  Les captures de rendu isolé ne sont pas utilisées comme preuve du bureau réel.
- Les trois derniers relevés ont pris 1,99 à 2,68 ms, espacés d'environ deux
  secondes. Les relevés de la session précédente avaient pris environ 4 à 7 ms.
- Disposition, zones de clic et identités des hôtes/shells terminal préservées.

Restent non vérifiés : clics et survol physiques après modification, Win+D, cycle
individuel de toutes les applications. L'outil Computer Use a échoué à se connecter
à son canal natif. Aucun prompt n'a été envoyé à une application IA.

Le build validé est `build/battlestation-app-activity-final`. Le pointeur de
démarrage Windows n'a pas été modifié. Pour le lancer explicitement :

```powershell
.\scripts\Start-Battlestation.ps1 -Build build/battlestation-app-activity-final
```

## Bilan Eclipse

Le principal a recherché l'original, conçu le matériau autorisé, vérifié les
raccourcis et les processus, relu les changements et effectué la validation réelle.
Le worker a reçu `gpt-5.6-luna`, effort `xhigh`, avec un contexte neuf ; le lanceur
a accepté la demande sans exposer de métadonnées de modèle effectif ni d'usage.
Il a implémenté la détection, son intégration WPF et les premiers tests.

Le retour correctif du principal a porté sur les erreurs de scan, Battle.net,
la normalisation des noms, les lanceurs et la suppression d'un second moniteur
inutile. Le principal a ensuite repris le traitement d'erreur, simplifié le
polling, corrigé les preuves de transition et ajouté la reconnaissance des liens
physiques, nécessaire pour Codex et découverte pendant l'essai réel.

La délégation a fourni une base utile ; les cas Windows et l'acceptation ont
demandé une revue et des reprises substantielles. Tokens et coût observé par
modèle, total et économie estimée : **non mesurable**, faute de compteurs et de
scénario de comparaison mesuré. Aucun montant ni gain n'est déduit des seuls
paramètres du worker.
