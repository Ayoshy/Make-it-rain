# Valeur du quota

Page **Quotas → 100 % ≈** du dock AI Meter : combien représente 100 % d'une fenêtre de
quota, en tokens et en prix API, et ce que valaient les fenêtres précédentes.
Elle n'apparaît pas sur la vue principale du dock.

La maille n'est pas la semaine calendaire mais **la fenêtre entre deux resets**,
qu'ils soient automatiques ou banked et consommés plus tôt. Une fenêtre s'arrête
au reset suivant : un reset manuel raccourcit donc la fenêtre précédente au lieu
de la prolonger.

## Mesure

Chaque relevé du compteur (toutes les quinze minutes, plus le bouton
**Actualiser**) enregistre deux choses :

1. le pourcentage consommé que Codex renvoie pour chaque limite, avec sa date de
   reset ;
2. les tokens OpenAI comptés par les sessions locales, par modèle, au moment du relevé.

Les familles DeepSeek, Claude et non identifiées sont exclues, y compris lors
du recalcul des relevés bruts déjà enregistrés. Les agrégats anciens dont les
relevés ont expiré ne sont pas réécrits. La valeur porte sur la fenêtre principale
Codex ; elle ne prétend pas mesurer séparément les réserves et limites courtes.

Entre deux relevés de la **même** fenêtre, l'écart de tokens divisé par l'écart de
pourcentage donne ce que vaut un point de quota ; multiplié par cent, les 100 %.
Le prix API d'un quota plein applique le prix moyen par token des modèles tarifés
aux tokens d'un quota plein ; les modèles sans tarif public sont hors calcul et
l'affichage porte alors la mention **partiel**. Aucun tarif n'est inventé. Le
relevé par modèle n'utilise que les intervalles courts (≤ 3 h) où un seul modèle
domine (≥ 90 % des tokens) ; un intervalle mixte alimente le total de la fenêtre
sans fabriquer de ratio par modèle.

Tant qu'aucun intervalle n'a été mesuré, la page affiche une estimation initiale
en tokens, calculée avec les sessions de la fenêtre en cours, et le dit : elle
n'affiche alors aucun prix. **Ces compteurs locaux ne sont pas le compteur
officiel de Codex** : une journée peut différer (sessions reprises, sous-agents).

## Stockage

`%LOCALAPPDATA%\Battlestation\quota-weeks.json`

- pourcentages, dates de reset, totaux de tokens et coûts API uniquement ; aucun
  prompt, aucun identifiant, aucune session n'est lue en écriture ;
- relevés bruts conservés 90 jours, fenêtres agrégées conservées (40 maximum) ;
- écriture atomique (fichier temporaire puis remplacement) : une coupure ne
  laisse pas d'historique tronqué ; un fichier illisible repart vide ;
- supprimer ce fichier remet la mesure à zéro.

## Métriques du dock

`weekTitle`, `weekUsed`, `weekReset`, `weekValue`, `weekValueCost`,
`weekValueState`, `weekDelta`, `weekSince`, `weekWindowCount`,
`weekWindow:<i>:period|value|cost|delta|current|samples`, `weekModelCount`,
`weekModel:<i>:name|tokens|ratio`.

## Vérifications

```powershell
dotnet run --project tests/Battlestation.CodexWeeks.Tests.csproj -c Release          # ratio, fenêtres, dérive, historique
dotnet run --project tests/Battlestation.CodexWeeks.Tests.csproj -c Release -- --live # vraies données, lecture seule
dotnet run --project tests/Battlestation.DockReview.Tests.csproj -c Release -- .     # page SEMAINE, barres, tailles du dock
```

Le rendu isolé prouve la géométrie et la peinture ; le clic réel sur le bouton
**VALEUR** et la fluidité perçue se valident sur le bureau.
