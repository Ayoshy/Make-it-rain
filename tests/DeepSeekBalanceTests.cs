using Battlestation.Core;

static void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS " + text); }

var fetched = new DateTimeOffset(2026, 9, 16, 14, 0, 0, TimeSpan.FromHours(2));
var balance = DeepSeekBalanceReader.Parse("""
    {"is_available":true,"balance_infos":[{"currency":"CNY","total_balance":"110.00","granted_balance":"10.50","topped_up_balance":"99.50"}]}
    """, fetched);
Check(balance is not null, "Un solde valide est analysé");
Check(balance!.Available, "La disponibilité du compte est conservée");
Check(balance.Primary is { Currency: "CNY", Total: 110m, Granted: 10.5m, ToppedUp: 99.5m }, "Les montants et la devise sont lus en décimales");
Check(balance.FetchedAt == fetched, "L’horodatage de lecture accompagne le solde");
Check(DeepSeekBalanceReader.Parse("""{"is_available":false,"balance_infos":[]}""", fetched) is null, "Une liste vide reste indisponible, jamais zéro");
Check(DeepSeekBalanceReader.Parse("{}", fetched) is null, "Une réponse sans solde reste indisponible");
Check(DeepSeekBalanceReader.Parse("""{"is_available":true,"balance_infos":[{"currency":"","total_balance":"0.00"}]}""", fetched) is { } zero && zero.Primary!.Total == 0m && zero.Primary.Granted is null, "Un solde nul déclaré est conservé et les montants absents restent non renseignés");
try { DeepSeekBalanceReader.Parse("pas du json", fetched); Check(false, "Le JSON invalide est refusé"); }
catch (System.Text.Json.JsonException) { Console.WriteLine("PASS Le JSON invalide est refusé"); }
