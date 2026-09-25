# Gmail

Dock personnel en lecture seule : nombre de messages non lus dans la boîte de
réception, douze derniers messages, expéditeur, objet, aperçu et date. La molette
fait défiler la liste. Un clic ouvre la conversation dans Gmail avec le compte
connecté ; la lecture dans le navigateur peut alors marquer le message comme lu.
Le dock lui-même n'envoie, n'archive et ne modifie aucun mail.

## Première connexion

1. Dans [Google Cloud](https://console.cloud.google.com/), crée ou sélectionne un
   projet personnel et active **Gmail API** dans la bibliothèque des API.
2. Dans **Google Auth Platform**, configure le nom de l'application et l'adresse
   de contact. Pour un Gmail personnel, choisis l'audience **Externe** et ajoute
   ton adresse comme utilisateur de test si le projet est en test.
3. Dans **Accès aux données**, ajoute uniquement
   `https://www.googleapis.com/auth/gmail.readonly`.
4. Dans **Clients**, crée un client **Application de bureau**, puis télécharge
   son fichier JSON.
5. Dans Battlestation, ajoute **Gmail** via les réglages des blocs. Clique
   **Configurer Gmail → Importer le fichier OAuth…** et sélectionne ce JSON.
6. Clique **Connecter Gmail**, puis choisis ton compte et autorise la lecture
   dans ton navigateur. Tu peux annuler depuis le dock ; l'attente expire après
   trois minutes.

Pour une utilisation durable, passe le statut de publication de l'application
OAuth de **Testing** à **In production**. Le mode Testing limite le jeton de
renouvellement à sept jours avec les autorisations Gmail. Cela ne publie pas
Battlestation sur un magasin. L'usage strictement personnel bénéficie d'une
exception à la vérification Google ; l'écran « application non vérifiée » peut
rester présent. Vérifie que c'est bien ton propre projet.

## Stockage et fonctionnement

- `gmail-client.bin` et `gmail-token.bin`, sous `%LOCALAPPDATA%\Battlestation`,
  sont chiffrés par DPAPI pour le compte Windows courant. Le JSON téléchargé
  d'origine reste à l'emplacement choisi dans le navigateur : ne le mets pas dans Git.
- Les contenus des messages ne sont pas enregistrés sur disque ni inclus dans
  `gmail-inspect`. Aucun serveur intermédiaire, aucune analyse par IA.
- La bibliothèque officielle Google gère OAuth avec PKCE et le retour local
  au navigateur. Seul un clic sur **Connecter Gmail** lance l'autorisation.
- Actualisation toutes les deux minutes lorsque le dock est exposé, avec bouton
  manuel. Masquer le dock ou passer en édition suspend le sondage et annule la
  lecture en cours. Une connexion explicitement lancée reste possible.
- En cas d'erreur réseau, les dernières données restent affichées avec leur
  date. Un compteur absent est affiché « — », jamais remplacé par zéro.
- **Déconnecter ce PC** efface le jeton local et les messages affichés, en gardant
  la configuration OAuth. Pour retirer aussi l'autorisation chez Google, utilise
  les connexions tierces de ton compte Google.

## Vérification

`dotnet run --project tests/Battlestation.Gmail.Tests.csproj` contrôle le
chiffrement, l'import desktop, la lecture seule, le compteur, l'absence de
données, les erreurs, l'annulation et la suspension. Les rendus synthétiques
se trouvent dans `artifacts/validation/gmail/` ; ils ne valident pas une connexion
réelle. Le consentement, le renouvellement réel et l'ouverture de la bonne
conversation doivent être essayés avec le compte personnel connecté.

Sources : [OAuth desktop](https://developers.google.com/identity/protocols/oauth2/native-app),
[permissions Gmail](https://developers.google.com/workspace/gmail/api/auth/scopes),
[usage personnel](https://developers.google.com/identity/protocols/oauth2/production-readiness/restricted-scope-verification),
[expiration OAuth](https://developers.google.com/identity/protocols/oauth2).
