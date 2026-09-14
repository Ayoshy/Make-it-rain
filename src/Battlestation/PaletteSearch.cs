using System.Globalization;
using System.Text;

namespace Battlestation;
internal sealed record PaletteEntry(string Id,string Title,string Detail,string Icon,Action Execute,bool Command=false);
internal static class PaletteSearch
{
    internal static string Normalize(string value)=>string.Concat(value.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)).ToLowerInvariant();
    internal static IReadOnlyList<PaletteEntry> Find(IEnumerable<PaletteEntry> entries,string query)
    {
        bool commands=query.TrimStart().StartsWith('>');string q=Normalize(commands?query.TrimStart()[1..].Trim():query.Trim());
        var candidates=entries.Where(e=>!commands||e.Command);
        if(q.Length==0)return candidates.Take(60).ToArray();
        return candidates.Select(e=>(Entry:e,Score:Score(q,e))).Where(e=>e.Score>0).OrderByDescending(e=>e.Score).ThenBy(e=>e.Entry.Title,StringComparer.CurrentCultureIgnoreCase).Take(60).Select(e=>e.Entry).ToArray();
    }
    static int Score(string query,PaletteEntry entry)
    {
        string title=Normalize(entry.Title),detail=Normalize(entry.Detail);int total=0;
        foreach(string token in query.Split(' ',StringSplitOptions.RemoveEmptyEntries))
        {
            int score=title==token?1200:title.StartsWith(token)?950:title.Contains(token)?700:detail.Contains(token)?300:Subsequence(token,title)?100:0;
            if(score==0)return 0;total+=score;
        }
        return total;
    }
    static bool Subsequence(string query,string candidate){int i=0;foreach(char c in candidate)if(c==query[i]&&++i==query.Length)return true;return false;}
}
