using System.IO;
using System.Windows;
using Battlestation;

static partial class DockReviewTests
{
    static void AiMeterChecks(Station station, string output)
    {
        Native.Values["remaining"]="28%"; Native.Values["quotaLabel"]="7 jours";
        Native.Values["reset"]="Reset 28/09 14:30"; Native.Values["codexStatus"]="MAJ 12:24";
        Native.Values["deepseekTotal"]="12,84"; Native.Values["deepseekCurrency"]="USD";
        Native.Values["deepseekStatus"]="MAJ 12:24";Native.Values["aiStatus"]="MAJ 12:24";
        Native.Values["claudeStatus"]="MAJ 12:24";Native.Values["claudePlan"]="Abonnement Pro";
        Native.Values["claudeRemaining"]="97%";Native.Values["claudeWindowLabel"]="5 HEURES";
        Native.Values["claudeReset"]="Reset 25/09 20:19 · 35,00 $ restants";
        Native.Values["claudeSpend"]="Solde 12,50 $ · 5,00 / 50,00 USD · crédit d'appoint";
        Native.Values["claudeWindowCount"]="2";
        Native.Values["claudeWindow:0:name"]="5 heures";Native.Values["claudeWindow:0:remaining"]="97% restant";Native.Values["claudeWindow:0:reset"]="25/09 20:19 · 35,00 $ restants";
        Native.Values["claudeWindow:1:name"]="7 jours";Native.Values["claudeWindow:1:remaining"]="88% restant";Native.Values["claudeWindow:1:reset"]="28/09 23:59";
        Native.Values["quotaDetails"]="codex\n28% restant / 7 jours / 28/09 14:30   ·   64% restant / 5 heures / 25/09 16:30\n\ngpt-reserve\n91% restant / 7 jours / 28/09 14:30";
        Native.Values["credits"]="1 crédit de reset";
        Native.Values["weekTitle"]="QUOTA HEBDOMADAIRE";Native.Values["weekUsed"]="72 %";
        Native.Values["weekValue"]="186 M";Native.Values["weekValueCost"]="420 $ · partiel";
        Native.Values["weekValueState"]="mesuré sur 46 % · 8 relevés";Native.Values["weekReset"]="Reset 28 sept. · 14:30";
        Native.Values["weekWindowCount"]="2";Native.Values["weekModelCount"]="0";
        for(int i=0;i<2;i++){Native.Values[$"weekWindow:{i}:period"]=i==0?"21 → 28 sept.":"18 → 21 sept.";Native.Values[$"weekWindow:{i}:value"]=i==0?"186 M":"179 M";Native.Values[$"weekWindow:{i}:cost"]="420 $";Native.Values[$"weekWindow:{i}:current"]=i==0?"1":"0";Native.Values[$"weekWindow:{i}:delta"]="";}
        foreach(string family in new[]{"openai","deepseek","claude","unknown"})
        foreach(string period in new[]{"total","today"})
        {
            string key=$"ai:{family}:{period}:";
            Native.Values[key+"count"]="3";
            Native.Values[key+"tokens"]=period=="total"?"128 M":"4,2 M";
            Native.Values[key+"cost"]=family=="deepseek"?"2,83 $":"420 $";
            Native.Values[key+"coverage"]="Tarification partielle";
            Native.Values[key+"input"]="14 M";Native.Values[key+"cached"]="112 M";Native.Values[key+"output"]="2 M";
            Native.Values[key+"write"]=family=="claude"?"21 k":"—";
            for(int i=0;i<3;i++){Native.Values[key+i+":name"]=family=="deepseek"?"deepseek-flash":family=="unknown"?"Modèle inconnu":"gpt-6-astra";Native.Values[key+i+":effort"]="high";Native.Values[key+i+":tokens"]="42 M";Native.Values[key+i+":cost"]="140 $";Native.Values[key+i+":cache"]="84 %";}
        }
        foreach(var size in new[]{new Size(592,224),new Size(440,209),new Size(779,360)})
        {
            var meter=new DashboardSurface(station,false){Width=size.Width,Height=size.Height};
            string prefix=Path.Combine(output,$"ai-{size.Width}x{size.Height}");
            Render(meter,prefix+"-summary.png");
            Check(Has(meter,"Provider:DeepSeek")&&Has(meter,"Provider:Codex"),"Provider cards are reachable");
            Invoke(meter,"Provider:DeepSeek");meter.Transition.Settle(4);
            Render(meter,prefix+"-costs.png");
            var deepseek=Pixels(meter);Invoke(meter,"Cost:openai");
            Check(Different(deepseek,Pixels(meter)),"Changing provider changes displayed costs");
            Invoke(meter,"CostPeriod");
            Check(Different(deepseek,Pixels(meter)),"Day counters render separately");
            Invoke(meter,"CostTokens");Render(meter,prefix+"-tokens.png");
            Check(Has(meter,"CostBack"),"Token split has a return action");Invoke(meter,"CostBack");Pixels(meter);
            Invoke(meter,"Cost:claude");Render(meter,prefix+"-claude.png");
            Check(Has(meter,"CostTokens"),"Claude counters render like the other families");
            Invoke(meter,"CostTokens");Render(meter,prefix+"-claude-tokens.png");
            var split=Pixels(meter);
            Native.Values["ai:claude:total:write"]="—";Native.Values["ai:claude:today:write"]="—";
            Check(Different(split,Pixels(meter)),"La colonne d'ecriture de cache suit le compteur publie");
            Native.Values["ai:claude:total:write"]="21 k";Native.Values["ai:claude:today:write"]="21 k";Invoke(meter,"CostBack");
            Invoke(meter,"Quotas");meter.Transition.Settle(3);Render(meter,prefix+"-quotas.png");
            var codexQuotas=Pixels(meter);
            Invoke(meter,"QuotaClaude");Render(meter,prefix+"-claude-quotas.png");
            Check(Different(codexQuotas,Pixels(meter)),"Le quota Claude est une liste distincte");
            Check(!Has(meter,"Week"),"La valeur 100 % reste propre au quota Codex");
            Invoke(meter,"QuotaCodex");Pixels(meter);
            Check(Has(meter,"Week"),"100 percent value is reachable from quotas");
            Check(meter.ScrollPage(1),"Quota rows can be scrolled");Pixels(meter);
            Invoke(meter,"Week");meter.Transition.Settle(6);Render(meter,prefix+"-value.png");
            Invoke(meter,"Summary");Pixels(meter);
            Check(meter.Drawer==0,"Summary restores provider cards");
            meter.SetDisplayed(false);Check(!meter.Transition.Running&&!meter.Transition.HasAnimatedProperties,"Hidden AI Meter stops page effects");
        }
        // Claude connecté sans compteur local : l'onglet des coûts ne propose pas de partage inventé.
        Native.Values["ai:claude:total:count"]="0";
        var bare=new DashboardSurface(station,false){Width=592,Height=224};
        Render(bare,Path.Combine(output,"ai-claude-empty-summary.png"));
        Invoke(bare,"Provider:Claude");bare.Transition.Settle(4);
        Render(bare,Path.Combine(output,"ai-claude-empty.png"));
        Check(!Has(bare,"CostTokens")&&Has(bare,"Quotas"),"Sans compteur local, aucun partage de tokens n'est propose");
        Native.Values["ai:claude:total:count"]="3";
        Console.WriteLine("AI_METER_RENDER_PASS: provider selection, day/cumulative costs, token split, missing Claude, quotas, value, bounds, hidden effects. Isolated WPF; no live clicks.");
    }
}
