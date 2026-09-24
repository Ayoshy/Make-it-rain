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

**⚙ → Clé API Serper** ouvre un champ masqué. Copier la clé depuis le compte
Serper, la coller puis choisir **Enregistrer**. Elle est chiffrée par Windows
pour l'utilisateur courant dans `%LOCALAPPDATA%/Battlestation/serper-key.bin` ;
elle n'est ni affichée ensuite, ni écrite en clair dans les réglages ou Git.
L'enregistrement ne lance aucun appel API. Une clé enregistrée sélectionne Serper
pour les recherches suivantes, sans redémarrage. Sans clé, Tavily sans compte
reste sélectionné ; une erreur Serper ne déclenche pas de bascule automatique.

La barre d'onglets conserve plusieurs recherches indépendantes. **+** ouvre une
demande vide, un clic sur le titre retrouve ses résultats et son journal. Une
pastille indique les recherches en cours, y compris dans les autres onglets.
**Arrêter** annule uniquement la recherche sélectionnée ; fermer un onglet en
cours l'annule aussi. Plusieurs recherches peuvent travailler simultanément.
Les onglets, leurs résultats et leur niveau de réflexion restent en mémoire pour
la session du bureau ; ils ne sont pas restaurés après redémarrage. La liste des
articles surveillés et leur historique restent communs et enregistrés en SQLite,
avec un seul traitement de veille en arrière-plan.

Dans la fenêtre **Demande**, **Réflexion** propose **Sans réflexion**, **Low**,
**High** et **Max** pour DeepSeek. Le niveau est propre à l'onglet, visible dans
son en-tête et utilisé par la lecture de la demande, le parcours web et la
comparaison finale. Il ne change pas un appel déjà lancé. Max reste présélectionné
pour une nouvelle recherche ; le sélecteur est désactivé avec Ollama.

Le bloc s'ajoute depuis **clic droit → Ajouter un bloc → Achats** ; il arrive
masqué pour ne pas modifier les scènes existantes. Cliquer le champ de demande
ouvre la saisie ancrée (le bureau ne prend pas le focus clavier), **Chercher**
relance la dernière demande, la molette fait défiler les résultats, un clic sur une offre
ouvre l'annonce et l'étoile met l'article en veille. Les formats d'écriture
acceptés : `max 800 €`, `budget de 1 200 €`, `300 L`, `no frost`, `classe C`,
une couleur, une marque ou une référence de modèle.

La saisie n'impose plus de limite de caractères. Les demandes longues défilent
dans le champ et sont transmises intégralement à l'analyse ; le texte abrégé
dans le dock n'est qu'un aperçu.

Les fiches ont un fond sombre teinté par le thème et une hauteur adaptée à leur
contenu : titre, prix, résumé et réserve passent sur plusieurs lignes, sans
ellipse. La liste défile dans son cadre ; seules les zones visibles sont cliquables.
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
ne sépare ces groupes ; un petit dock fait défiler la liste entière.
Les pistes restent visibles même lorsqu'aucun choix n'est confirmé.
Le récapitulatif affiche les compteurs et signale les sources partielles. **Détails**
ouvre la demande, les critères, le budget et les incidents de sources en texte
complet et défilable ; le survol du récapitulatif donne aussi ces informations.
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
d'offres supplémentaires, dans une fenêtre de 30 minutes pour laisser le temps
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
recherche et la comparaison. Ces appels utilisent le niveau de réflexion choisi
dans l'onglet : `none`, `low`, `high` ou `max`. Sans réflexion désactive aussi
`thinking.type`. Le budget de génération reste celui de l'API pour le niveau
choisi (128K par défaut en max au 22 septembre 2026), car il inclut la réflexion ; les
anciens plafonds de 768/4 500 tokens étaient trop courts pour ce fonctionnement.
Chaque appel DeepSeek expire après 15 minutes ; Ollama conserve ses 45 secondes.
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
Le dock interroge Serper si sa clé est enregistrée, sinon Tavily, puis lit les
pages choisies. Serper reçoit une recherche française via `/search` ; seuls les
liens organiques sont utilisés ici. Les liens sont gardés 24 h, dans un cache
séparé par fournisseur. Les recherches identiques entre onglets attendent la
même porte d'entrée, puis relisent le cache pour éviter un second appel inutile.
Les pages marchandes conservent leur fraîcheur distincte de six heures.
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
huit pages initiales et quatre pages d'offres, douze candidats et trente minutes.
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

À la demande d'Ayo après cet essai, la réflexion `max` est conservée et le délai
est porté à 15 minutes par appel DeepSeek, avec 30 minutes pour le parcours web.
Les délais courts des lectures de pages et d'Ollama restent inchangés. Il s'agit
de plafonds d'attente, pas d'une durée attendue ni d'une garantie de réponse.
Les 302 contrôles Shopping passent et la compilation réussit sans requête payante.
Build chargé/sélectionné : `battlestation-shopping-wait-01` (PID 3948), hôte 2196,
sessions et réglages conservés. Le build remplacé `shopping-glass-01` est archivé
sous `backups/retired-builds/2026-09-22/` après contrôle des processus, modules et
références. Validation : `artifacts/validation/shopping-wait-20260922/`.

### Recherches parallèles et niveau au choix, 22 septembre

`ShoppingTabs` associe un radar, un moteur, un journal et une pagination à chaque
onglet. Le magasin SQLite et les transports HTTP restent partagés ; les sources
web et leur planificateur ont un état indépendant. Seul le moteur principal
assure la veille. Les caches de pages et d'images utilisent des fichiers
temporaires uniques pour éviter les collisions entre recherches simultanées.

La réflexion sélectionnée dans la demande est transmise aux trois étapes du
même onglet. L'annulation est propagée aux appels réseau et au moteur de cette
recherche. Les réglages du fournisseur ne sont pas réécrits pour un changement
de niveau. Le journal et les résultats des autres onglets restent disponibles.
Les commandes internes `shopping-new`, `shopping-select:<id>`,
`shopping-close:<id>` et `shopping-stop` complètent `shopping-inspect`, qui
expose les onglets, leur niveau et `anyBusy` pour vérifier toutes les recherches
avant une relance du bureau.

309 contrôles Shopping et DockReview réussis : deux recherches simultanées sur
le même magasin, journaux et niveaux distincts, arrêt/fermeture isolés, veille
commune et navigation des onglets sur dock étroit. Le menu de réflexion a été
ouvert et rendu avec les quatre niveaux ; les paramètres HTTP ont été contrôlés
avec un transport simulé. Aucun appel API payant lancé par l'agent.

Build chargé et sélectionné : `battlestation-shopping-tabs-02` (PID 17336).
Création, sélection et fermeture d'un onglet vide vérifiées par les commandes du
bureau ; ces commandes et les rendus WPF ne prouvent pas les clics physiques.
Hôte terminal 2196 et sessions conservés. Les fichiers fournisseur/préférences
sont inchangés ; layout/profiles ont changé pendant la vérification live et
n'ont pas été rétablis depuis une sauvegarde. Une recherche utilisateur a été
observée ensuite ; aucune seconde relance n'a été effectuée.
Le build remplacé `shopping-wait-01` est archivé après contrôle des processus,
modules, références et raccourcis. Changements locaux non commités.
Preuves : `artifacts/validation/shopping-tabs-20260922/` et les rendus
`artifacts/validation/dock-review/shopping-tabs-*.png`, `shopping-reasoning-options.png`.

### Lecture Amazon et fiches lisibles, 22 septembre

La fiche Amazon.fr remontée par la recherche a répondu HTTP 200 avec 3 224 999
octets décompressés (3,08 Mio). Le rejet venait du plafond local de 3 Mio. La
lecture HTML accepte désormais 16 Mio par page, y compris en transfert sans
taille annoncée ; l'extrait destiné au modèle reste limité à 6 000 caractères.
Le vrai HTML a été rejoué hors réseau dans le lecteur de production : lecture
acceptée, référence L50s présente dans l'extrait. Cela prouve la lecture de cette
fiche, pas la vérification automatique de tous ses prix ni l'accès à toutes les
pages Amazon. Les refus HTTP et vérifications de navigateur restent respectés.

Le récapitulatif est compact ; les critères complets et diagnostics de sources
sont accessibles par Détails et au survol. Chaque fiche enveloppe ses textes
sans ellipse, mesure sa hauteur et reçoit un fond sombre issu du thème. La liste
défile à la molette, avec une barre de position ; les zones cliquables sont
découpées au cadre visible. Les onglets reprennent les teintes du thème. Une fin
de recherche laisse une pastille verte et déclenche deux respirations douces
(4,8 secondes au total), composées par WPF. Une annulation ne déclenche pas ce
signal et le masquage du dock libère les animations.

311 contrôles Shopping et DockReview réussis : grande page et cache, texte long
intégral, défilement/interactions, trois thèmes, état terminé et arrêt des
animations. Rendus inspectés dans `artifacts/validation/dock-review/shopping-cards-*.png`
et `shopping-completed-tab.png`. Tests API simulés ; aucun appel DeepSeek payant.
Les clics physiques et la lisibilité sur le fond du bureau restent à apprécier
à l'usage, distinctement de ces rendus isolés.

Build chargé/sélectionné : `battlestation-shopping-cards-01` (PID 20332). Hôte
terminal 2196, sessions et fichiers de réglages inchangés. Démarrage via le
lanceur du repo ; ancien `shopping-tabs-02` archivé après contrôle des processus,
modules et références. Changements locaux non commités. Preuves complémentaires :
`artifacts/validation/shopping-amazon-20260922/`.

### Connexion Serper, 22 septembre

Ajout de Serper Search (`q`, `gl=fr`, `hl=fr`, dix résultats demandés, huit liens
utilisés), avec la clé exclusivement dans l'en-tête `X-API-KEY` du fournisseur.
Le transport de production ne suit pas les redirections. Le bouton ⚙ du dock
ouvre une saisie PasswordBox ; DPAPI protège le fichier pour le compte Windows
courant. Aucune clé n'est journalisée ni renvoyée par `shopping-inspect` : seules
la présence d'une clé et la sélection du fournisseur y figurent. La modification
de connexion est bloquée pendant les recherches. `shopping-settings` ouvre le
même dialogue depuis les commandes internes, sans lancer de recherche.

Les liens sont conservés 24 h. Une porte commune à tous les onglets vérifie le
cache avant chaque appel : deux demandes identiques n'envoient pas deux requêtes
simultanées. Annuler l'onglet qui attend ne coupe pas celui qui recherche déjà.
Cette modification n'ajoute pas de bouton Réanalyser : il reste à implémenter.

319 contrôles Shopping et DockReview réussis, dont paramètres/erreurs Serper,
déduplication entre onglets, saisie masquée et relecture DPAPI avec une clé
factice. Compilation réussie ; aucune requête Serper ou DeepSeek consommant du
crédit n'a été lancée pour les tests. La clé réelle doit être enregistrée par
l'utilisateur ; sa validité sera vérifiée lors de sa propre recherche.

Build chargé et sélectionné : `battlestation-shopping-serper-01` (PID 19400), hôte
terminal 2196, sessions et réglages existants conservés. La fenêtre de connexion
a été ouverte pour la saisie utilisateur. Ancien `shopping-cards-01` archivé
après vérification des processus, modules, références et raccourcis. Changements
locaux non commités. Preuves : `artifacts/validation/shopping-serper-20260922/`
et `artifacts/validation/dock-review/shopping-serper-settings.png`.

### Intégration Git, 24 septembre

Les changements locaux décrits ci-dessus sont repris dans les sources publiées :
onglets indépendants, réflexion par recherche, arrêt isolé, cartes défilantes,
récapitulatif détaillé et connexion Serper. Nouvelle vérification : 319 contrôles
Shopping réussis et suite DockReview réussie, avec transports simulés et clé
factice ; aucun appel payant. Compilation de `battlestation-bureau-model-01`
réussie. Ces tests et rendus ne remplacent pas les clics ni une recherche réelle.
Preuves : `artifacts/validation/shopping-git-20260924/`.
