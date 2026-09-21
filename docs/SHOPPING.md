# Radar d'achat

Le dock **Achats** transforme une phrase (« frigo max 800 €, no frost, 300 L,
blanc ») en recherche multi-boutiques, relève les prix, les confronte à
l'historique et au calendrier promotionnel, puis répond **achète**, **attends**
ou **surveille** avec les raisons et la source de chaque chiffre. Les articles
surveillés sont revérifiés en fond et préviennent dans la zone de notification.

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
  "provider": "ollama",
  "model": "qwen2.5:7b",
  "endpoint": "http://127.0.0.1:11434",
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

`provider` accepte `ollama` (service local, modèle configurable) ou `deepseek`
(`chat/completions`, clé lue dans `DEEPSEEK_API_KEY` de l'environnement
utilisateur, jamais écrite dans un fichier ni dans Git). Le modèle ne sert qu'à
mieux lire la demande : sans Ollama, sans clé ou sans réseau, l'analyse locale
donne le même résultat sur les cas courants. `ntfyTopic` active une notification
téléphone sur `ntfy.sh` ; laissé vide, rien n'est envoyé.

## Boutiques

`IPriceSource` sépare le moteur des boutiques. Deux adaptateurs sont livrés et
vérifiés sur ce PC :

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

`VerdictEngine` ne répond jamais sur une impression :

- **achète** — prix au plancher observé (ou sous lui) avec un historique
  suffisant, et aucun rendez-vous commercial imminent ;
- **attends** — un événement promo arrive dans la fenêtre configurée et le prix
  du jour est au-dessus du plancher ;
- **surveille** — historique trop court, prix au-dessus du budget, ou prix
  au-dessus de la cible calculée.

Chaque raison cite le prix, le plancher, la date de l'événement ou le budget
utilisés. La cible vaut `plancher × buyMargin`, plafonnée par le budget.

Le calendrier est calculé, jamais saisi : soldes d'hiver (deuxième mercredi de
janvier), soldes d'été (dernier mercredi de juin, ou l'avant-dernier s'il tombe
après le 28), French Days de printemps et d'automne, Prime Day, Black Friday et
Cyber Monday. `promo-events.json` ajoute ou remplace un repère, par exemple :

```json
[{"id":"boulanger-anniversaire","shop":"Boulanger","label":"Anniversaire Boulanger",
  "start":"2026-10-10","end":"2026-10-12","discountHint":0.12}]
```

## Veille

La veille démarre avec le bureau, même dock masqué. Un article est revérifié à
la cadence configurée ; un échec de lecture double l'attente jusqu'à six heures
au lieu de marteler la boutique. Un relevé n'est enregistré que s'il apporte une
valeur ou six heures après le précédent. Une notification part quand le prix
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
la veille tout de suite).

Le rendu isolé et les commandes ne remplacent pas l'essai sur le bureau : la
fluidité perçue, le clic réel et l'ouverture du navigateur restent à confirmer
sur la version livrée.
