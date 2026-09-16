using Battlestation;

static void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS " + text); }

PaletteEntry App(string name) => new("app:" + name, name, "Application", "\uE71D", () => { });
PaletteEntry Project(string name) => new("project:" + name, name, "Projet", "\uE8B7", () => { });
PaletteEntry Command(string name, string detail) => new("cmd:" + name, name, detail, "\uE713", () => { }, true);

var catalog = new List<PaletteEntry>
{
    App("Steam"), App("Brave"), App("Discord"), App("Claude Desktop"), App("Streamlabs"),
    Project("Battlestation"), Project("Travel Boy"), Project("Conrad Sensor"), Project("Kash"), Project("Badventurers 2"),
    Command("Réglages", "Paramètres · settings · transparence"),
    Command("Scène Personnel", "Scènes"), Command("Scène Jeu", "Scènes"),
    App("Météo"), App("Lecteur réseau"),
};

string[] Titles(string query) => PaletteSearch.Find(catalog, query).Select(e => e.Title).ToArray();

Check(Titles("ste").Contains("Steam"), "Trois caractères trouvent l’application");
Check(Titles("stea").Contains("Steam"), "Quatre caractères continuent de trouver l’application");
Check(Titles("steam").First() == "Steam", "Le préfixe exact arrive en tête");
Check(Titles("batt").Contains("Battlestation"), "Un préfixe de projet est trouvé");
Check(Titles("battl").Contains("Battlestation"), "Une requête de cinq caractères reste trouvée");
Check(Titles("battlestation").First() == "Battlestation", "Le nom complet d’un projet arrive en tête");
Check(Titles("meteo").Contains("Météo"), "Les accents sont ignorés");
Check(Titles("reglages").Contains("Réglages"), "Une commande accentuée est trouvée sans accent");
Check(Titles("scene jeu").First() == "Scène Jeu", "Deux mots filtrent la scène visée");
Check(Titles("battelstation").Contains("Battlestation"), "Une lettre en trop reste tolérée");
Check(Titles("dscord").Contains("Discord"), "Une requête sans voyelles retrouve l’application");
Check(Titles("badven").Contains("Badventurers 2"), "Un préfixe long de projet est trouvé");
Check(Titles("claude desk").First() == "Claude Desktop", "Les mots partiels sont acceptés");
Check(Titles("").Length == catalog.Count, "Une requête vide garde le catalogue");
Check(Titles(">reg").Select(t => t).First() == "Réglages", "Le mode commande filtre les commandes");
Check(!PaletteSearch.Find(catalog, ">steam").Any(e => e.Id.StartsWith("app:")), "Le mode commande ignore les applications");
Check(Titles("zzzz").Length == 0, "Aucun résultat pour une requête absente");
Check(Titles("steam").First() == "Steam" && Titles("steam").All(t => t != "Streamlabs" || Titles("steam").ToList().IndexOf("Steam") < Titles("steam").ToList().IndexOf("Streamlabs")), "Le préfixe est mieux classé que la correspondance lâche");
