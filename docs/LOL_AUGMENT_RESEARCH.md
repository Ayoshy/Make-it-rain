# Accès direct aux améliorations LoL

Exploration du 25 septembre 2026, sur l'installation personnelle
`E:\LoL\Riot Games\League of Legends`, client `16.19.820.7193`.
Objectif : lire les trois propositions ARAM Mayhem et leur changement après un
reroll, sans Overwolf. Le dock et sa version chargée ne sont pas modifiés par
cette exploration.

## Sources examinées

| Source | Observation | Ce que cela permet de conclure |
| --- | --- | --- |
| API LCU, `GET /help?format=Full` | 1 473 fonctions, 897 événements, 3 605 types | Les définitions qui mentionnent des améliorations concernent notamment l'historique, les replays, TFT et les cosmétiques ; aucune sélection Mayhem en direct identifiée. |
| LCU, `GET /lol-game-data/assets/v1/cherry-augments.json` | HTTP 200, 554 identifiants distincts ; 170 noms internes préfixés `ARAM_` | Catalogue statique local avec identifiant, nom, rareté et chemin d'icône. Ce n'est pas la liste proposée dans la partie. Le préfixe seul ne définit pas l'ensemble des améliorations compatibles Mayhem. |
| LCU, `GET /lol-match-history/v1/products/lol/current-summoner/matches?begIndex=0&endIndex=1` | HTTP 200, champs `playerAugment1` à `playerAugment6` dans les statistiques | Une source pour les améliorations prises dans une partie terminée ; aucune preuve concernant les propositions actuelles. |
| LCU, `GET /lol-inventory/v1/cherry-inventory` | Fonction présente dans l'aide, HTTP 404 au salon | Aucune donnée exploitable dans cette phase. |
| LCU, `GET /lol-game-data/assets/v1/kiwi-augments.json` | HTTP 400 | Ce chemin supposé n'est pas une source validée. |
| LCU, les deux endpoints `lol-end-of-game/v1/*eog-stats-block` | HTTP 404 au salon | Pas de statistiques de fin de partie disponibles à cet instant. |
| Journaux `r3dlog` des quatre dernières parties du 24 septembre | 190 à 198 lignes chacun ; zéro occurrence `augment`, `reroll` ou `kiwi` ; la seule occurrence `Cherry` nomme une plante | Aucun événement de sélection identifié dans ces journaux. Ce filtrage lexical n'est pas une preuve d'absence dans tous les fichiers du jeu. |

Le 24 septembre, l'API de partie avait déjà été examinée : douze endpoints
`liveclientdata` dans son OpenAPI, aucun champ de choix d'amélioration identifié.
Le 25 septembre, une partie Mayhem (`KIWI`, Sona) est observée dès le temps de
jeu 0,025 s. Son schéma confirme douze endpoints `liveclientdata`.
L'aide complète du processus de jeu (`GET /help?format=Full` sur le port 2999)
contient 27 fonctions, 24 types et un seul événement technique `OnCallback`.
La recherche dans les noms de champs **et les chaînes des définitions** ne trouve
aucune occurrence `augment`, `kiwi`, `reroll` ou `cherry` dans ces deux schémas.

À 10:39:55, heure locale : relevés jusqu'à 286,85 s de jeu, niveau 6 ; aucun
champ candidat. Les trois fichiers de la partie en cours (`r3dlog`, `netlog`,
`netstats`) ont été ouverts en lecture partagée : 159, 58 et 31 lignes, sans
occurrence `augment`, `reroll` ou `kiwi`. Leur taille affichée dans le répertoire
était encore zéro pendant l'écriture ; ce sont les contenus effectivement lus,
pas cette métadonnée, qui ont servi à la vérification.

L'accessibilité Windows examinée à 10:36:22 expose un seul nœud
`ControlType.Pane` / `RiotWindowClass`, aucun enfant ni `TextPattern`.
Cela ne fournit aucun texte de carte. L'instant n'était pas confirmé comme un
écran de sélection ; une observation à ce moment précis reste nécessaire.

La capture continue ci-dessous couvre la partie suivante. Elle ne fournit pas
encore de correspondance entre les trois cartes affichées et des données d'API.

## Capture continue de la partie Brand — 25 septembre 2026

Enregistreur autonome sous `artifacts/validation/lol-continuous/`, indépendant
du dock et des tours de conversation. Les valeurs candidates textuelles sont
également conservées ; l'écoute LCU souscrit aux événements JSON et ne sauvegarde
que les routes liées au jeu, avec filtrage des données personnelles.

- 5 357 lectures Live Client Data réussies, de **00:33,50 à 23:56,94** de jeu,
  entre 10:58:25 et 11:21:48 heure locale. Intervalle moyen : **262 ms**.
- Deux intervalles dépassent 500 ms : 650 ms et **9,99 s**, ce dernier entre
  **20:08,57 et 20:18,57** de jeu. Les 33 premières secondes et cette interruption
  limitent la couverture ; la cause du trou n'est pas établie.
- Aucun champ candidat dans les 5 357 réponses live. Les 18 instantanés de
  capacités/runes ont une seule configuration de runes distincte.
- **13 événements LCU**, tous reçus à la fin de partie. Les champs de sélection
  de champions et le booléen de configuration `gameTypeConfig/reroll` ne sont
  pas les propositions ni les rerolls d'améliorations en partie.
- L'événement `/lol-end-of-game/v1/eog-stats-block` donne pour `localPlayer`
  quatre améliorations prises, répétées à l'identique dans trois notifications :

| Emplacement | Identifiant | Libellé du catalogue local (`nameTRA`) |
| --- | --- | --- |
| 1 | 1308 | FireFox |
| 2 | 1388 | Infinite Recursion |
| 3 | 1415 | Twin Fire |
| 4 | 2137 | Pat On The Back |

Les emplacements 5 et 6 valent zéro. Ces identifiants proviennent des statistiques
de fin de partie ; ils ne prouvent ni les alternatives proposées, ni les choix
écartés, ni le nombre ou le résultat des rerolls. Aucune liste de trois propositions
n'a été récupérée dans cette capture. Cela borne le constat à ces sources et aux
instants effectivement enregistrés, sans prétendre exclure toute autre extraction.

Deux requêtes expirent après la dernière réponse live, lors de la fermeture du
jeu. L'enregistreur se termine automatiquement à 11:22:52 (`completed-game`).
Le bureau diagnostic PID 3584 et l'hôte terminal PID 19224 restent en place,
sans nouvelle trace de crash.

Preuves : `artifacts/validation/lol-continuous/run-20260925-105824/`
(`observations.jsonl`, `status.json`, `analysis.json`). La taille et l'horodatage
affichés par le répertoire étaient périmés pendant l'écriture : la lecture du
flux complet confirme **5 675 enregistrements, 13 467 042 octets**.

## Sonde temporaire et confidentialité

Les outils et preuves locaux sont sous `artifacts/validation/lol-direct/`
(ignoré par Git). Ils constituent une investigation, pas une fonctionnalité du dock.

- `Probe.csproj` / `Program.cs` : requêtes GET uniquement vers les deux API locales.
- Le port et le secret LCU sont lus en mémoire depuis le lockfile LoL partagé en
  lecture. Ils ne sont ni affichés ni écrits dans les résultats.
- Le certificat autosigné est accepté uniquement sur `https://127.0.0.1` et le
  port de la connexion concernée. Proxy et redirections sont désactivés.
- Les réponses personnelles brutes ne sont pas sauvegardées. Les relevés gardent
  les chemins des champs, des candidats numériques, le temps/mode/niveau et les
  capacités/runes du joueur ; les noms des joueurs et leurs identifiants sont omis.
- Les schémas et le catalogue statique sont sauvegardés séparément.
- La sonde s'arrête au terme de la durée demandée. Elle n'interroge pas le port
  2999 sans processus de partie, n'effectue aucune capture d'écran et n'injecte
  aucun code dans le jeu.
- `Inspect-GameAccessibility.ps1` examine seulement la structure d'accessibilité
  de la fenêtre de partie, sans interaction ni lecture des textes personnels.
- Un second mode `events` écoute, par WebSocket local, 30 canaux LCU relatifs au
  jeu, à la sélection de champion, aux runes, à l'inventaire et à la fin de partie.
  Les messages de chat et canaux d'authentification ne sont pas souscrits.
  Seuls les chemins de champs et candidats numériques sont conservés.

Exécution depuis la racine du dépôt :

```powershell
dotnet build artifacts/validation/lol-direct/Probe.csproj -o artifacts/validation/lol-direct/schema-bin
dotnet artifacts/validation/lol-direct/schema-bin/Probe.dll schema
dotnet artifacts/validation/lol-direct/schema-bin/Probe.dll candidates
dotnet artifacts/validation/lol-direct/schema-bin/Probe.dll watch 1200
dotnet artifacts/validation/lol-direct/schema-bin/Probe.dll events 900
powershell -NoProfile -File artifacts/validation/lol-direct/Inspect-GameAccessibility.ps1
```

La sonde compile. Cela ne valide ni la lecture des propositions, ni les rerolls.
Les relevés de salon ne permettent pas de conclure sur l'API pendant un choix.

Coût observé des deux processus de sonde sur 10,04 s en partie : 0,0312 s CPU
pour le lecteur HTTP (51,8 Mio de mémoire de travail), 0 s CPU mesurable pour
l'observateur d'événements (77,1 Mio). Il ne s'agit ni d'une mesure de FPS ni
d'une comparaison du jeu avec et sans sonde.

## Critère de réussite et suite

Une source directe est établie seulement si trois identifiants/noms récupérés
correspondent aux cartes visibles, puis changent correctement après un reroll.
Un catalogue, une liste d'améliorations déjà prises ou un historique ne satisfait
pas ce critère. Si les API, journaux et mécanismes d'accessibilité examinés ne
livrent pas ces propositions, cette exploration s'arrête avec ce constat borné ;
elle ne démontre pas l'impossibilité de toute extraction indépendante.

La source d'éventuelles statistiques reste un chantier distinct. Les règles
publiées par Riot excluent actuellement l'affichage de taux de victoire des
améliorations des usages approuvés ; ne pas présenter une intégration de ces taux
comme déjà validée.

## Overwolf : autorisation utilisateur et accès développeur

Vérification du 25 septembre 2026, après autorisation de l'utilisateur de passer
par Overwolf, avec préférence pour une version autonome comme CurseForge.

- La variante **Overwolf Electron** existe : elle fonctionne sans le client
  Overwolf Native et peut utiliser le fournisseur d'événements GEP. Cela reste
  du code propriétaire Overwolf, pas une extraction indépendante.
- La documentation prévoit une activation des jeux par application. Les pages
  d'onboarding **Electron et Native** exigent une application approuvée pour
  l'accès aux API et indiquent ne pas approuver actuellement les applications
  privées. Leur définition inclut l'usage personnel et les passerelles qui
  alimentent un autre service : Battlestation correspond à ce cas.
- Le refus d'approbation ne prouve pas une impossibilité technique. Une sonde
  locale a donc été exécutée avec son propre nom et auteur, sans emprunter
  l'identité d'une application approuvée (résultat ci-dessous).
- Electron propose `app.overwolf.disableAnonymousAnalytics()` pour réduire
  la collecte ; selon la documentation, un minimum de télémétrie demeure.
  « Autonome » ne signifie donc pas « sans collecte ».
- Le runtime autonome a été téléchargé dans le dossier de validation, sans
  installer le client complet, modifier le démarrage Windows ou recharger le
  bureau. Aucune demande externe n'a été envoyée.

### Essai technique autonome

Sonde temporaire : `artifacts/validation/lol-overwolf/`. Runtime npm officiel
`@overwolf/ow-electron` **42.7.1**, gestionnaire de modules **5.0.14**, seul module
demandé : `gep`. Les deux options `disableAdsOptimization()` et
`disableAnonymousAnalytics()` sont appelées avant `app.ready`.

Essai du 25 septembre, 11:47–11:48, PID 2732, application
`Battlestation Mayhem Probe`, auteur `Ayo`. L'application arrive à `app-ready`,
mais le gestionnaire écrit :

```text
[SECURITY] Dev mode missing credentials
[SECURITY] Dev credentials invalid
[SECURITY] Package cache cleared for blocked app
closing package manager: 'invalid verification'
```

Aucun événement `package-ready` ni événement de jeu reçu. Arrêt automatique
après 60 secondes, sans processus de sonde restant. Cela prouve un blocage
d'authentification **sur cette version et cette configuration**, pas
l'impossibilité de tout mode local. Le jeu n'a pas été lancé pour cet essai.
La réception des cartes et des rerolls n'est donc pas validée.

La documentation actuelle du [Dev Mode](https://dev.overwolf.com/ow-electron/guides/dev-tools/dev-mode/)
confirme que le chargement GEP exige soit `OW_CLI_EMAIL` et `OW_CLI_API_KEY`,
soit `OW_DEV_KEY`. Cette dernière se génère dans le profil développeur approuvé.
Le client Native demande lui aussi un compte développeur autorisé pour charger
une extension locale selon sa
[documentation](https://dev.overwolf.com/ow-native/getting-started/onboarding-resources/setting-up-dev-environment/).
Cette seconde voie n'a pas été testée sur le PC.

Preuves : `runs/2026-09-25T09-47-00-074Z/events.jsonl`, `status.json`, et journal
`ow-electron/khkaomjalhojamnmfbkfhbfbnnelfihaknplpjdg/logs/owpm.log` dans le
dossier de sonde. Les logs du runtime peuvent inclure son identifiant machine ;
ils restent locaux et ne doivent pas être partagés bruts.

Sources officielles :

- [Comparaison Native / Electron](https://dev.overwolf.com/ow-electron/getting-started/onboarding-resources/frameworks-overview/).
- [Accès développeur Electron et applications privées](https://dev.overwolf.com/ow-electron/getting-started/project-roadmap/).
- [Accès développeur Native et applications privées](https://dev.overwolf.com/ow-native/getting-started/project-roadmap/).
- [Configuration Electron et limitation des analytics](https://dev.overwolf.com/ow-electron/getting-started/onboarding-resources/first-app/).

## Références

- [Riot : API locale du jeu et Swagger](https://developer.riotgames.com/docs/lol#game-client-api).
- [Riot : usages non approuvés](https://developer.riotgames.com/docs/lol#game-policy_examples-of-unapproved-use-cases).
- [Hasagi : schéma d'aide produit par le client](https://github.com/dysolix/hasagi-schema).
- [Overwolf : événements LoL et améliorations](https://dev.overwolf.com/ow-native/live-game-data-gep/supported-games/league-of-legends/#augments).
- [Mayhem Overlay : choix reconnus par OCR, historique par LCU](https://github.com/zp96-cmd/mayhem-overlay).

Les documents Overwolf consultés décrivent les données exposées, pas le mécanisme
interne d'extraction LoL. Seul le runtime autonome de la sonde a été téléchargé ;
le client Overwolf complet n'a pas été installé.
