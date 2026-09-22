# Radar d'achat

Le dock **Achats** part d'une demande libre et recherche sur le web. DeepSeek
formule les requêtes, sélectionne les pages à lire puis compare les produits
à partir des fiches consultées. Il distingue **À privilégier**, **À comparer**
et **À vérifier**, avec une raison, des éléments cités et des réserves. Le prix
reste un jugement séparé : sans relevés suffisants, **Historique insuffisant**.
Les articles surveillés sont revérifiés en fond et préviennent dans la zone de
notification.

Aucun achat, aucune commande et aucun message ne partent d'ici : le dernier clic
reste à l'utilisateur, et le dock se contente d'ouvrir l'annonce dans le
navigateur.

## Utilisation

Le bloc s'ajoute depuis **clic droit → Ajouter un bloc → Achats** ; il arrive
masqué pour ne pas modifier les scènes existantes. Cliquer le champ de demande
ouvre la saisie ancrée (le bureau ne prend pas le focus clavier), **Chercher**
relance la dernière demande, la molette change de page, un clic sur une offre
ouvre l'annonce et l'étoile met l'article en veille. Les formats d'écriture
acceptés : `max 800 €`, `budget de 1 200 €`, `300 L`, `no frost`, `classe C`,
une couleur, une marque ou une référence de modèle.

La saisie n'impose plus de limite de caractères. Les demandes longues défilent
dans le champ et sont transmises intégralement à l'analyse ; le texte abrégé
dans le dock n'est qu'un aperçu.

Le survol d'une offre montre l'analyse complète, les citations de la fiche, les
réserves et les motifs sur le prix. Une analyse indisponible reste indiquée comme
telle. Les offres non vérifiées ne servent pas à compléter artificiellement une
sélection argumentée jusqu'à six lignes.

Pendant une recherche, un reflet nacré traverse une ligne fine, avec
la durée, l'étape active et les dernières actions : préparation des requêtes,
liens trouvés, page consultée, erreur d'un site, comparaison. **Journal** permet
de revoir le dernier passage après la recherche ; il reste affiché en cas d'échec
ou lorsqu'aucun produit n'a été retenu.
Le nombre de lignes affichées suit la hauteur disponible. Toutes les étapes de
la recherche sont gardées en mémoire, puis remises à zéro à la recherche suivante.
L'animation est composée par WPF, sans redessiner le journal à chaque image ;
elle s'arrête lorsque le dock est masqué ou la recherche terminée.
Aucun prompt, réponse du modèle ou secret n'est écrit dans un journal.

Une seule liste affiche d'abord les **Choix** dont les exigences ont été
confirmées, puis les **Pistes · À vérifier**, sous un titre distinct. Aucun bouton
ne sépare ces groupes ; un petit dock conserve la pagination de la liste entière.
Les pistes restent visibles même lorsqu'aucun choix n'est confirmé.
Le pourcentage indique la part des critères obligatoires confirmés par l'analyse,
budget compris lorsqu'il est demandé. Chaque critère a le même poids et une
information inconnue reste non confirmée. Ce taux ne mesure ni la qualité ni la
probabilité d'un bon achat ; aucune valeur n'est affichée sans critère évalué.
Le prompt demande des exigences distinctes, sans doublons synonymes ni score
de confiance inventé. Le calcul est local, à partir des contrôles de DeepSeek.
Au survol : état de chaque critère, citation,
fiche utilisée pour les caractéristiques et autres offres observées. Un modèle
hors budget à prix confirmé ne peut pas devenir un choix, même si le modèle de
langage le recommande par erreur.

Le contrat partagé de tous les appels DeepSeek est dans
`src/Battlestation.Shopping/ShoppingPrompts.cs`. Il est complété par le schéma de
chaque étape : compréhension, navigation, extraction et comparaison. Les critères
doivent être restitués séparément avec un état confirmé/contredit/inconnu et une
preuve ; une appréciation globale ne remplace pas ces contrôles. Les notes de
vérification ajoutées par l'application ne sont pas des citations marchandes.

La découverte identifie d'abord les modèles. Jusqu'à deux références effectivement
lues servent ensuite à chercher d'autres offres exactes ; le même modèle peut
être regroupé entre vendeurs. La fiche la plus détaillée du groupe est conservée
avec son URL, indépendamment du vendeur retenu pour son prix. Cette recherche est
bornée à trois requêtes de découverte et deux recherches d'offres (une pour
chacune des deux références retenues), huit lectures initiales et quatre lectures
d'offres supplémentaires, dans une fenêtre de 10 minutes pour laisser le temps
aux appels de réflexion maximale.

Pour une demande d'aspirateur robot, le type d'appareil est conservé et les pièces
(brosses, filtres, roues, etc.) sont écartées avant de limiter les résultats et
de comparer les prix. Un appareil vendu avec une brosse ou une station reste
éligible ; une demande explicite de pièce reste possible. Les offres identifiées
comme reconditionnées ou d'occasion sont écartées. La pertinence et les critères
vérifiés passent avant le verdict sur le prix.

Les critères absents du titre sont indiqués **à vérifier** : une liste d'offres
ne prouve pas à elle seule qu'un robot convient à 100 m², aux poils d'animaux ou
aux cheveux longs.

## Où sont les données

Tout reste dans `%LOCALAPPDATA%\Battlestation` :

| Fichier | Contenu |
| --- | --- |
| `shopping.db` | produits, relevés de prix datés, veilles et repères promo (SQLite) |
| `shopping.json` | fournisseur, modèle, seuils, cadence, sujet ntfy |
| `promo-events.json` | repères par enseigne écrits à la main (facultatif) |
| `shopping-cache/` | pages de liste et de fiche déjà lues, relues hors réseau |
| `shopping-images/` | aperçus des offres, téléchargés une fois |

`shopping.json` est créé au premier lancement avec les valeurs par défaut :

```json
{
  "provider": "deepseek",
  "model": "deepseek-flash",
  "endpoint": "https://api.deepseek.com",
  "cadenceMinutes": 30,
  "historyDays": 90,
  "minPricePoints": 3,
  "buyMargin": 1.02,
  "waitWindowDays": 14,
  "maxResults": 6,
  "ntfyTopic": "",
  "enabled": true
}
```

`provider` utilise `deepseek` par défaut (`chat/completions`, clé lue dans
`DEEPSEEK_API_KEY` de l'environnement utilisateur, jamais écrite dans le fichier
de réglages ni dans Git). Flash extrait la demande en JSON, puis pilote la
recherche et la comparaison. Tous ces appels activent `thinking.type=enabled`
et `reasoning_effort=max`. Le budget de génération reste celui de l'API pour ce
mode (128K par défaut au 22 septembre 2026), car il inclut la réflexion ; les
anciens plafonds de 768/4 500 tokens étaient trop courts pour ce fonctionnement.
Chaque appel DeepSeek expire après 3 minutes ; Ollama conserve ses 45 secondes.
Seul le contenu final est exploité, sans conserver le texte de réflexion.
Une réponse signalée comme tronquée est refusée. Le protocole suit les guides
[JSON Output](https://api-docs.deepseek.com/guides/json_mode/) et
[Thinking Mode](https://api-docs.deepseek.com/guides/thinking_mode/) de DeepSeek.
`ollama` reste disponible avec un modèle local (par défaut `qwen2.5:7b`) et
`endpoint: http://127.0.0.1:11434`. Le modèle ne sert qu'à
mieux lire la demande : sans Ollama, sans clé ou sans réseau, l'analyse locale
donne le même résultat sur les cas courants. `ntfyTopic` active une notification
téléphone sur `ntfy.sh` ; laissé vide, rien n'est envoyé.

Les réglages déjà enregistrés restent conservés : pour passer une installation
Ollama à DeepSeek, modifier `Provider`, `Model` et `Endpoint` comme ci-dessus,
puis recharger le bureau. Les seuils, la cadence et la base de veille restent
inchangés. L'authentification peut être vérifiée par `GET /models` sans générer
de réponse facturée ; une recherche avec le modèle distant utilise le crédit
du compte.

## Recherche et lecture des offres

L'API DeepSeek ne fournit pas elle-même le navigateur ou le moteur de recherche.
Le dock interroge l'API de recherche générale Tavily puis lit les pages choisies.
L'accès **keyless** est officiellement proposé sans compte ni clé supplémentaire,
gratuit mais soumis à des limites d'usage ; une limite est signalée explicitement,
sans bascule payante ni nouvelle tentative en boucle. Aucune API par marchand
n'est nécessaire. Voir [l'accès sans clé Tavily](https://docs.tavily.com/documentation/keyless).
La page HTML DuckDuckGo n'est plus utilisée pour la découverte, car elle demandait
une vérification anti-bot sur la demande de frigo. L'orchestration du modèle suit
le fonctionnement documenté des
[outils DeepSeek](https://api-docs.deepseek.com/guides/tool_calls/).

La découverte n'est pas limitée à une liste de marchands. Un produit doit être
identifié dans une fiche consultée, par ses données structurées ou par la lecture
du titre et des éléments textuels cités. Les liens proposés par le modèle sont
limités aux résultats de recherche et aux liens réellement observés dans les pages
déjà lues. Une catégorie sert de point d'entrée : ses liens de fiches sont soumis
au modèle et les fiches choisies sont lues avant une nouvelle recherche générale.
Les citations sont contrôlées dans les textes fournis. Le prix n'est repris que si l'offre structurée peut être
associée sans ambiguïté au produit et à sa variante. Sinon le dock indique
**Prix à confirmer** et ne propose pas de veille de prix. Une page bloquée ne
produit aucun prix ni produit de secours.

Les anciennes veilles Rue du Commerce et Boulanger conservent leurs adaptateurs.
Ils ne limitent plus la découverte web aux deux enseignes.

`IPriceSource` conserve ces deux lecteurs pour les liens déjà surveillés :

| Source | Liste | Fiche produit |
| --- | --- | --- |
| Rue du Commerce | `pdt-item` : référence, lien, titre, image, prix remisé ou courant | JSON-LD `Product` |
| Boulanger | attributs de carte : référence, libellé, marque, image, prix TTC | JSON-LD `Product` |

La cadence est lente (4 s minimum entre deux requêtes d'une même boutique),
bornée (20 s par requête) et mise en cache disque (30 min pour une liste, 6 h
pour une fiche). Un refus de lecture devient une erreur visible dans le dock :
aucun prix n'est inventé à la place.

**Idealo et Le Dénicheur ne sont pas utilisés.** Testés le 17 septembre 2026
depuis ce PC, ils répondent 403 par une page de vérification (Idealo) ou une
recherche vide et scriptée (Le Dénicheur). Les contourner demanderait un
navigateur automatisé permanent, ce que ce projet refuse. Adapter une autre
enseigne revient à écrire une classe `HtmlPriceSource` : URL de recherche,
lecture de la liste, lecture du JSON-LD de la fiche.

## Verdict

`ShoppingAdvisor` juge l'aptitude à la demande, à partir des données fournies et
avec des citations contrôlées. Une information commerciale reste une affirmation
de la boutique, pas la preuve d'un essai indépendant. Les inconnues restent
explicites ; le prix bas et la notoriété supposée ne suffisent pas à recommander.

`VerdictEngine` traite séparément l'historique de prix :

- **achète** — prix au plancher observé (ou sous lui) avec un historique
  suffisant, et aucun rendez-vous commercial imminent ;
- **attends** — un événement promo arrive dans la fenêtre configurée et le prix
  du jour est au-dessus du plancher ;
- **surveille** — historique trop court, prix au-dessus du budget, ou prix
  au-dessus de la cible calculée.

Chaque raison cite le prix, le plancher, la date de l'événement ou le budget
utilisés. La cible vaut `plancher × buyMargin`, plafonnée par le budget.

Le calendrier conserve les repères fixes des soldes, Black Friday et Cyber Monday.
Les dates de French Days et Prime Day ne sont plus déduites arbitrairement et
aucun pourcentage de remise n'est promis. Un événement proche ne déclenche pas
« attends » sans historique suffisant. `promo-events.json` ajoute ou remplace
une date connue pour une enseigne, par exemple :

```json
[{"id":"boulanger-anniversaire","shop":"Boulanger","label":"Anniversaire Boulanger",
  "start":"2026-10-10","end":"2026-10-12","discountHint":0.12}]
```

## Veille

La veille démarre avec le bureau, même dock masqué. Un article est revérifié à
la cadence configurée ; un échec de lecture double l'attente jusqu'à six heures
au lieu de marteler la boutique. Un relevé est enregistré si le prix ou le stock
change, y compris une rupture sans prix, ou vingt heures après le précédent
relevé identique. Les lignes du dock suivent les mises à jour de la veille en
fond ; les lectures et écritures SQLite restent hors du fil d'interface.
Une notification part quand le prix
cible est atteint, quand le prix baisse d'au moins 5 %, ou quand l'article
revient en stock ; elle n'est pas répétée tant que la situation ne change pas.

## Contrôles

```powershell
dotnet run --project tests/Battlestation.Shopping.Tests.csproj              # analyse, verdicts, calendrier, SQLite, veille
dotnet run --project tests/Battlestation.Shopping.Tests.csproj -- --live     # plus deux vraies boutiques
dotnet run --project tests/Battlestation.DockReview.Tests.csproj -- .        # rendu du dock (offres, veille, clic)
```

Commandes du bureau : `shopping-inspect` (demande, offres, veilles),
`shopping-search:<texte>` (lance une recherche), `shopping-recheck` (revérifie
la veille sans attendre son échéance, en conservant le cache des fiches).

Les tests Shopping couvrent aussi la requête DeepSeek avec un transport simulé,
sans crédit API. DockReview vérifie le passage du texte du dialogue à la recherche,
l'état occupé dès la soumission, le rafraîchissement automatique de la veille et
la disponibilité du dispatcher pendant une lecture bloquée. `--live` utilise
l'analyse locale et teste les boutiques, sans appel de modèle payant.

Le rendu isolé et les commandes ne remplacent pas l'essai sur le bureau : la
fluidité perçue, le clic réel et l'ouverture du navigateur restent à confirmer
sur la version livrée.

### Correction du 22 septembre 2026

Le dialogue envoyait la demande précédente au lieu du texte saisi ; une première
recherche restait donc vide. Le texte est désormais soumis et l'état occupé couvre
aussi l'analyse par DeepSeek. Le rafraîchissement de la veille, les alertes répétées,
la conservation du stock et la quarantaine SQLite ont été corrigés.

- Shopping et DockReview : réussis, y compris dialogue WPF et rendus inspectés.
- Boutiques en direct : Rue du Commerce et Boulanger, 18 offres chacune et deux
  fiches produit lues, avec analyse locale.
- DeepSeek : clé existante acceptée par `GET /models`, `deepseek-flash` disponible.
  Format de requête et repli local vérifiés par transport simulé ; aucun prompt
  payant envoyé pour les tests.
- Build `build/battlestation-shopping-deepseek-01` compilé et chargé (PID 19704),
  puis sélectionné dans `build/current.txt`. La tâche Windows suit le lanceur.
- Hôte terminal 2196 et ses trois identifiants d'onglets/processus shell conservés.
  `layout.json`, `preferences.json`, `profiles.json` et `network.json` identiques
  avant/après relance. La visibilité enregistrée du dock Achats reste conservée.
- L'ancien bureau `network-contrast-01`, libéré, est archivé sous
  `backups/retired-builds/2026-09-22/`. Aucun autre ancien build retiré.

Preuves locales : `artifacts/validation/shopping-fix-20260922/` et
`artifacts/validation/dock-review/shopping-*.png`. Gestes physiques et fluidité
sur le bureau non vérifiés par ces contrôles. Changements non commités.

### Pertinence des aspirateurs, complément du 22 septembre

La demande libre du robot envoyait `aspirateur robot robot mon` aux boutiques :
un tableau de mots-clés vide renvoyé par DeepSeek réinjectait les mots locaux.
Ce tableau vide est maintenant respecté. L'analyse locale conserve aussi le type
robot et traite `100m²` comme une surface, sans l'interpréter comme une référence.

Le filtrage appareil/pièce intervient dans les adaptateurs avant leur limite,
puis dans le moteur avant l'historique et les verdicts. Les régressions couvrent
les six pièces de la capture utilisateur, les appareils avec station/brosse et
vingt cartes d'accessoires placées avant deux appareils complets.

Vérification sur les pages réelles : requête `aspirateur robot`, deux réponses
HTTP 200, six appareils complets retenus. Sur les 88 cartes de l'ancienne recherche,
48 accessoires et 13 reconditionnés sont rejetés, 27 robots neufs conservés.
Les cartes ne prouvent toujours pas l'adéquation aux 100 m², poils et cheveux longs.
Le test utilise l'analyse locale sans appel LLM facturé ; la réponse DeepSeek à
mots-clés vides est couverte par simulation. Shopping et DockReview passent.

Version chargée et sélectionnée : `build/battlestation-shopping-relevance-02`
(PID 1784 lors du contrôle). Seul le module Shopping a été recompilé ; le bureau
et les composants natifs reprennent les binaires validés de la version précédente.
Les sessions terminal, disposition, profils, préférences et réglages Shopping sont
conservés. `shopping-deepseek-01` est archivé sous
`backups/retired-builds/2026-09-22/`, après contrôle des processus et références.
Preuves : `artifacts/validation/shopping-relevance-20260922/`.

### Recherche web et comparaison, complément du 22 septembre

La découverte utilise désormais le web général. DeepSeek prépare les requêtes,
choisit les pages, peut affiner la recherche après lecture et extrait les faits
des fiches HTML ordinaires. Le dock exécute ces lectures publiques puis fournit
les candidats au comparateur DeepSeek. L'API du modèle ne dispose pas d'une
recherche web intégrée : les opérations sont fournies par le dock.

Bornes de découverte : trois recherches initiales et deux recherches d'offres,
huit pages initiales et quatre pages d'offres, douze candidats et dix minutes.
Jusqu'à quatre appels de planification/extraction, en plus de l'interprétation
initiale et de la comparaison finale. Aucune analyse LLM ne part lors de la veille
automatique. Les refus de sites restent explicites et ne sont pas contournés.

Les cartes affichent l'aptitude et ses réserves ; le pourcentage de confiance de
l'ancien verdict tarifaire est retiré. Le suivi de prix indique l'insuffisance
d'historique au lieu de conseiller systématiquement d'attendre. Les dates
commerciales variables et les remises calculées sans annonce ont été retirées.

- 230 contrôles Shopping et DockReview : réussis, rendus inspectés.
- DDG public et téléchargement de pages réelles : vérifiés. Les pages Amazon et
  Electro Dépôt effectivement téléchargées ont été rejouées avec un modèle simulé
  pour vérifier titres, faits, URLs et prix laissés à confirmer. Ce rejeu ne
  démontre pas encore la qualité du jugement réel de DeepSeek.
- Aucun appel LLM facturé pendant les tests. Certains marchands ont répondu 403 ;
  cette limite d'accès reste visible dans la recherche.
- `build/battlestation-shopping-web-02` compilé depuis une copie vérifiée de 131
  fichiers sources/ressources, puis chargé (PID 19884 lors du contrôle) et
  sélectionné au démarrage. Les composants natifs et helpers reprennent le build
  actif `cache-observation-01`, sans changement matériel.
- Hôte terminal et identifiants d'onglets/processus shell préservés. Disposition,
  profils, préférences et `shopping.json` identiques avant/après relance.
- `cache-observation-01` archivé sous `backups/retired-builds/2026-09-22/` après
  libération et contrôle des références actives ; son ancien relevé d'inspection
  reste conservé. Changements non commités.

Preuves : `artifacts/validation/shopping-advisor-20260922/`,
`artifacts/validation/shopping-web-20260922/VALIDATION.md` et rendus DockReview.
Les gestes physiques et le jugement réel du modèle ne sont pas validés par les
tests simulés.

### Chargement et navigation des catalogues, complément du 22 septembre

Le refus anti-bot de la page HTML DuckDuckGo a conduit à remplacer la découverte
par l'accès officiel Tavily sans clé. Un essai réel gratuit, sans compte, a reçu
HTTP 200 et huit liens multi-enseignes pour la demande de frigo. Cette preuve
valide le moteur de recherche, pas huit offres adaptées au budget.

Le dock affiche maintenant une barre indéterminée, la durée, l'étape en cours et
un journal limité à douze événements en mémoire. L'animation est arrêtée quand
le dock est masqué, déchargé ou lorsque la recherche est terminée. Le journal
reste accessible après la fin ; il s'ouvre sur un échec ou une recherche vide.

Le premier essai utilisateur a ensuite montré un autre problème : les résultats
menaient à des catégories dont les liens produits n'étaient pas transmis au
modèle. `ProductPageLinks` récupère désormais les href observés et le choix après
lecture donne priorité à ces fiches avant une nouvelle liste de résultats.
Les expressions dynamiques `:href` ne deviennent pas des URL inventées.
Un nettoyeur HTML interprétait aussi un SVG contenu dans un attribut JSON comme
une vraie balise et pouvait supprimer le titre et les caractéristiques ; les
balises sont maintenant lues en respectant leurs guillemets, et les tableaux
techniques passent avant le texte de paiement dans les extraits.

Des refus partiels ne deviennent plus une panne globale quand d'autres pages ont
été lues. Le journal distingue pages refusées, pages lues et absence de produit
identifié.

- 281 contrôles Shopping, DockReview et compilation : réussis.
- Trois vraies fiches atteintes depuis les liens des catégories : HTTP 200 chez
  Boulanger, Electro Dépôt et Excedent. Leur HTML a été rejoué dans le parcours
  complet avec un planificateur simulé : un Samsung à 1 599 € est retrouvé chez
  Boulanger ; le VALBERG est conservé sans prix inventé avec 474 L, No Frost et
  des dimensions réellement présentes. Le Samsung est hors du budget de 800 € :
  ces essais sont des preuves de navigation, pas des recommandations d'achat.
- Build chargé et sélectionné : `build/battlestation-shopping-navigation-01`
  (PID 17340 lors du contrôle). Hôte terminal, sessions et processus shell
  conservés. Disposition, profils, préférences et réglages Shopping identiques
  avant/après cette relance ; les ajustements utilisateur intervenus sur le dock
  pendant la livraison précédente n'ont pas été rétablis depuis les sauvegardes.
- Les versions remplacées `shopping-web-02` et `shopping-loading-01` sont archivées
  sous `backups/retired-builds/2026-09-22/`, après vérification des processus,
  modules et références de démarrage. Changements non commités.

Preuves : `artifacts/validation/shopping-loading-20260922/`,
`artifacts/validation/shopping-tavily-20260922/` et
`artifacts/validation/shopping-fridge-20260922/product-pages/`.

### Modèles, critères et contrat DeepSeek, complément du 22 septembre

Le cas utilisateur HAIER HCR3818ENMM reste une référence positive de test, sans
modèle imposé dans le code de production. Le HTML public déjà en cache de BUT
est reconnu comme fiche de modèle ; la page Pulsat « Réfrigérateur - congélateur
en bas » est reconnue comme catalogue et ne peut plus devenir un produit.
Le prix et les exigences manquantes maintiennent un candidat dans **Pistes**.

- Contrat partagé ajouté à chaque appel DeepSeek ; schémas adaptés à chaque
  étape. Vérification séparée de chaque exigence avec citation, et budget
  contrôlé localement. La pertinence sémantique d'une citation reste en partie
  évaluée par le modèle : sa présence seule ne garantit pas sa justesse.
- Recherche d'offres supplémentaires à partir de références observées,
  regroupement conservant les différences de finition, et provenance de la
  fiche de caractéristiques indépendante du vendeur au meilleur prix.
- 300 contrôles Shopping, DockReview, compilation et inspection des rendus
  **Choix / Pistes** réussis. Réponses DeepSeek et prix supplémentaires simulés
  dans ces tests ; aucun appel API payant ni nouvelle recherche utilisateur.
- Build chargé et sélectionné : `build/battlestation-shopping-criteria-03`
  (PID 26176 lors du contrôle). Hôte terminal 2196 et ses deux sessions conservés,
  dispositions et réglages personnels inchangés. Lancement Windows toujours
  via `Start-Battlestation.ps1`. Aucun nouvel incident dans `error.txt`.
- Builds remplacés `palette-glass-02` et `shopping-criteria-02` archivés sous
  `backups/retired-builds/2026-09-22/`, après contrôle des processus, modules et
  références. Le candidat non activé `shopping-criteria-01` reste en place.
- Changements non commités. Clics physiques sur les nouveaux contrôles et
  pertinence d'une recherche DeepSeek réelle restent à confirmer par l'essai
  utilisateur ; les rendus et commandes internes ne prouvent pas ces points.

Preuves : `artifacts/validation/shopping-upgrade-20260922/` et
`artifacts/validation/dock-review/shopping-{results,pending}.png`.

### Saisie longue, complément du 22 septembre

Suppression de `MaxLength=180` dans la fenêtre de demande ; hauteur du champ
bornée à 240 px avec défilement automatique. Aucun changement des requêtes web
courtes préparées par DeepSeek ni des limites de lecture de pages.

DockReview et compilation réussis. Une vérification temporaire du vrai contrôle
WPF conserve une insertion de plus de 2 000 caractères et son rendu montre le
défilement, sans appel réseau. Ce contrôle automatisé ne remplace pas un collage
physique sur le bureau. Preuves : `artifacts/validation/shopping-input-20260922/`.

Build chargé et sélectionné : `build/battlestation-shopping-input-02` (PID 20632),
intégrant les sources et binaires natifs de la version `scenes-tri-01` chargée en
parallèle. Hôte terminal 2196, ses sessions et réglages personnels conservés.
`scenes-tri-01` reste disponible pour la validation des scènes en cours dans
l'autre chantier ; son archivage est différé pour préserver ce travail concurrent.
Modifications non commitées ; aucune recherche DeepSeek payante déclenchée.

### Réflexion maximale et affichage direct, complément du 22 septembre

Le réglage local utilise bien `deepseek-flash`. La réflexion auparavant désactivée
est désormais activée au niveau `max` dans tous les appels du dock. Les limites
de temps et de génération sont adaptées comme décrit plus haut. Sans choix
confirmé, les pistes apparaissent immédiatement avec leurs réserves ; le bouton
de bascule reste présent lorsqu'il existe à la fois des choix et des pistes.

302 contrôles Shopping et DockReview réussis, dont la requête HTTP simulée en
mode max, le rejet d'une réponse tronquée, l'affichage automatique après recherche
et la réouverture du dock avec seulement des pistes. Rendu WPF inspecté dans
`artifacts/validation/dock-review/shopping-pending-automatic.png`. Aucun appel
DeepSeek payant ; la durée réelle, la qualité des résultats et les clics physiques
restent à vérifier lors de l'essai utilisateur.

Build chargé et sélectionné : `build/battlestation-shopping-max-01` (PID 3140),
hôte terminal 2196 et sessions conservés, fichiers de réglages inchangés.
Démarrage Windows toujours via le lanceur du repo. Le build remplacé
`shopping-input-02` est archivé sous `backups/retired-builds/2026-09-22/` après
contrôle des processus, modules, références et raccourcis. Changements non commités.
Preuves de validation : `artifacts/validation/shopping-max-20260922/`.

### Journal adaptatif, reflet et liste unique, complément du 22 septembre

Le journal n'est plus limité à six lignes affichées ni à douze événements
conservés. La hauteur décide du nombre de lignes visibles, avec marge basse et
priorité aux dernières étapes. La recherche suivante remet toujours le journal
à zéro. Le chargement utilise un reflet nacré de 3 px, à bords dégradés, animé
par une transformation WPF sur 3,2 secondes. Le texte de durée est rafraîchi une
fois par seconde ; animation et minuterie sont arrêtées lorsque le dock est
masqué, déchargé, détruit ou n'a plus de recherche active.

Les choix et pistes sont réunis dans une liste ordonnée avec deux titres, sans
bouton de bascule. Le pourcentage décrit les critères confirmés (chaque exigence
et le budget ont le même poids) ; le survol donne le numérateur, le dénominateur
et les preuves. Le prompt interdit les doublons de critères et le score de
confiance inventé. Une analyse indisponible ne reçoit aucun pourcentage.

302 contrôles Shopping et DockReview réussis. Les rendus vérifiés couvrent la
liste mixte (100 % / 33 % dans les données simulées), les pistes seules, 24
événements sur un dock haut et cinq lignes récentes sur un dock compact. Le
reflet a changé de position et de pixels sans aucun rendu complet du dock dans
une mesure de 455 ms ; le processus de test WPF a consommé 219 ms CPU pendant
cette fenêtre. Cette mesure courte inclut le banc de test et ne prouve pas un
gain de CPU sur le bureau ni une fréquence d'affichage réelle. La fluidité perçue
sur le verre du bureau reste à confirmer à l'usage. Aucun appel DeepSeek payant.

Build chargé et sélectionné : `build/battlestation-shopping-glass-01` (PID 25216),
sur la base native `scenes-tri-03` chargée pendant ce travail. Hôte terminal 2196,
sessions et fichiers de réglages conservés ; lanceur Windows inchangé. La version
`scenes-tri-03` reste disponible pour le chantier de validation des scènes en
parallèle : archivage différé pour préserver ce travail. Changements non commités.
Preuves : `artifacts/validation/shopping-shimmer-20260922/` et les rendus
`artifacts/validation/dock-review/shopping-{results,loading-tall,loading-compact,loading-shimmer}.png`.

Essai utilisateur observé avant publication Git : la recherche web s'est achevée,
puis la comparaison DeepSeek a atteint la limite de 180 secondes (17:32:01 à
17:35:01, heure locale du 22 septembre). Les candidats restent des pistes avec
analyse indisponible. Le délai est imposé par le client ; l'observation seule ne
distingue pas une réflexion longue d'une attente du service. Réflexion `max` et
délai inchangés dans cette publication, sans nouvelle requête de test facturée.
