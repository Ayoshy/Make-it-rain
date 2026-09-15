namespace Battlestation;

// Browser tabs carry their real source type. Keeping this policy separate makes
// Auto deterministic and keeps manual source selection independent of discovery.
internal static class VideoSelection
{
    internal static bool IsBrowserKind(string kind)=>kind is "youtube" or "twitch";

    internal static string Resolve(string mode,BrowserVideoState web,bool braveForeground,bool stremioForeground,bool stremioAvailable,string currentKind)
    {
        if(mode is "youtube" or "twitch" or "stremio")return mode;

        var ready=web.Connected?web.Tabs.Where(tab=>tab.Ready).ToArray():[];
        var active=ready.FirstOrDefault(tab=>tab.Active);
        if(braveForeground&&active is not null)return active.Kind;
        if(stremioForeground&&stremioAvailable)return "stremio";
        if(IsBrowserKind(currentKind)&&ready.Any(tab=>tab.Kind==currentKind))return currentKind;
        if(currentKind=="stremio"&&stremioAvailable)return "stremio";

        var stable=ready.OrderByDescending(tab=>tab.Playing).ThenByDescending(tab=>tab.Used).FirstOrDefault();
        return stable?.Kind??(stremioAvailable?"stremio":"");
    }

    internal static VideoTab? SelectBrowserTab(BrowserVideoState web,string wanted,bool braveForeground)
    {
        if(!IsBrowserKind(wanted))return null;
        var tabs=web.Tabs.Where(tab=>tab.Kind==wanted&&tab.Ready).ToArray();
        return (braveForeground?tabs.FirstOrDefault(tab=>tab.Active):tabs.FirstOrDefault(tab=>tab.Id==web.TabId))
            ??tabs.OrderByDescending(tab=>tab.Playing).ThenByDescending(tab=>tab.Used).FirstOrDefault();
    }
}
