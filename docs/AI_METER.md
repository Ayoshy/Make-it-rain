# AI Meter

Le dock `usage` conserve sa disposition et ses matériaux communs. Il propose :

- **Synthèse** : quota Codex restant, solde API DeepSeek et quota Claude restant.
- **Quotas** : limites Codex et réserve, dates de reset, liste à la molette.
  L'onglet **Claude** montre les fenêtres de l'abonnement Claude Code.
  **100 % ≈** ouvre la valeur estimée de la fenêtre principale et son historique.
- **Coûts** : modèles OpenAI, DeepSeek, Claude ou non attribués, en cumul ou pour
  le jour local. Le bouton des tokens ouvre entrée hors cache, entrée lue en cache,
  écriture de cache quand le fournisseur la publie, et sortie. Les lignes de
  modèles défilent à la molette.

Les cartes et onglets sont explicites ; **Synthèse** revient aux comptes.
**Actualiser** conserve les lecteurs existants, sans génération ni dépense API.

## Sources et limites du candidat

Les quotas Codex proviennent du lecteur de compte Codex ; le solde DeepSeek de son
API de compte ; les quotas Claude de l'API de compte Anthropic, la même que celle
de la commande de quota de Claude Code. Les modèles, tokens et coûts sont
reconstruits depuis les seuls compteurs des sessions locales — rollouts Codex et
sessions Claude Code. Kilo et Shopping ne sont pas couverts. Leur activité ne doit
pas être déduite d'une variation du solde.

La famille est identifiée par le nom du modèle. Cela ne prouve ni le mode de
facturation du compte OpenAI, ni le client app/CLI : l'interface indique donc
« OpenAI » et « sessions Codex locales ». Les noms inconnus restent dans Autres.
Un modèle sans tarif reste non tarifé ; un compte sans données affiche un tiret.
Le prix est un équivalent API pour OpenAI, un coût API estimé pour DeepSeek,
jamais une facture. Les tarifs déjà présents sont conservés.

Le lecteur Claude lit le jeton OAuth de Claude Code dans
`%USERPROFILE%\.claude\.credentials.json` **en mémoire seulement** : il n'est
jamais journalisé, copié, conservé dans les sources, dans Git ni transmis ailleurs.
Un seul GET est envoyé à l'API de compte, sans prompt ni consommation de crédit.
Jeton expiré ou absent, l'onglet affiche **À connecter** et conserve la dernière
mesure. Les fenêtres publiées (5 heures, 7 jours, par modèle) sont affichées telles
quelles, avec les crédits d'appoint en euros quand ils sont activés. Un abonnement
ne produit pas de facture à l'usage : le coût des modèles Claude reste un
**équivalent API**, calculé aux tarifs publics de la famille (Opus, Sonnet, Haiku).
Les sessions Claude Code locales sont comptées depuis les compteurs écrits par le
CLI ; un même message réécrit plusieurs fois n'est compté qu'une fois.

Les montants en dollars ne sont repris que lorsqu'ils sont publiés : une fenêtre de
quota ou le crédit d'appoint affiche alors le restant en dollars, avec le solde de
crédits s'il est renseigné. Pour un compte à limites en pourcentage, l'API laisse
ces champs nuls et le dock n'invente aucune conversion. Le solde de crédits
prépayés du Console (facturation API) n'est pas exposé par l'API de compte : il
reste visible dans la console Claude, pas dans le dock.

Les compteurs locaux se rafraîchissent même si le compte Codex est indisponible.
L'agrégation par famille se fait hors du fil UI, une fois par actualisation.
Pas de nouvelle sonde ni de timer de dessin. Les transitions s'arrêtent quand
le dock est masqué. [Valeur de la fenêtre principale](QUOTA_WEEKS.md).

## Vérifications

`dotnet run --project tests/Battlestation.AiUsage.Tests.csproj -c Release`

`dotnet run --project tests/Battlestation.TerminalCache.Tests.csproj -c Release`

`dotnet run --project tests/Battlestation.CodexWeeks.Tests.csproj -c Release`

`dotnet run --project tests/Battlestation.DockReview.Tests.csproj -c Release -- . --ai-only`

Ces contrôles couvrent les familles, les compteurs quotidiens, les données
absentes, l'exclusion de DeepSeek du quota Codex, la lecture et la déduplication
des sessions Claude, les fenêtres de compte Claude, les titres et l'état des
onglets Claude Code, et les rendus WPF aux tailles 440 × 209, 592 × 224 et
779 × 360. Les clics et la fluidité du bureau réel se vérifient pendant l'essai.
Atelier conserve le build sur action utilisateur.
