using System.Globalization;
using System.Text;

namespace Battlestation;
internal sealed record PalettePreviewBlock(double X,double Y,double Width,double Height);
internal sealed record PaletteEntry(string Id,string Title,string Detail,string Icon,Action Execute,bool Command=false,string? ProfileName=null,IReadOnlyList<PalettePreviewBlock>? Preview=null);
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
            int score=Token(token,title,detail);
            if(score==0)return 0;
            total+=score;
        }
        return total;
    }
    static int Token(string token,string title,string detail)
    {
        if(title.Length>0)
        {
            if(title==token)return 1200;
            if(title.StartsWith(token,StringComparison.Ordinal))return 950;
            if(WordStart(title,token))return 820;
            int index=title.IndexOf(token,StringComparison.Ordinal);
            if(index>=0)return 700-Math.Min(140,index*6);
        }
        if(detail.Contains(token,StringComparison.Ordinal))return 300;
        int fuzzy=Fuzzy(token,title);
        if(fuzzy>0)return 180+fuzzy;
        fuzzy=Fuzzy(token,detail);
        return fuzzy>0?60+fuzzy:0;
    }
    static bool WordStart(string text,string token)
    {
        for(int i=1;i+token.Length<=text.Length;i++)
            if((text[i-1]==' '||text[i-1]=='-'||text[i-1]=='·')&&text.AsSpan(i).StartsWith(token,StringComparison.Ordinal))return true;
        return false;
    }
    static int Fuzzy(string query,string candidate)
    {
        if(query.Length<3||candidate.Length<query.Length-2)return 0;
        const int gap=1,typo=14;
        int n=query.Length,m=candidate.Length;
        var previous=new int[m+1];var current=new int[m+1];
        for(int j=1;j<=m;j++)previous[j]=previous[j-1]-gap;
        for(int i=1;i<=n;i++)
        {
            current[0]=previous[0]-typo;
            for(int j=1;j<=m;j++)
            {
                int best=Math.Max(previous[j]-typo,current[j-1]-gap);
                if(query[i-1]==candidate[j-1])
                {
                    int bonus=10;
                    if(i>1&&j>1&&query[i-2]==candidate[j-2])bonus+=14;
                    if(j==1||candidate[j-2]==' '||candidate[j-2]=='-'||candidate[j-2]=='·')bonus+=18;
                    best=Math.Max(best,previous[j-1]+bonus);
                }
                current[j]=best;
            }
            var swap=previous;previous=current;current=swap;
        }
        return Math.Max(0,previous[m]);
    }
}
