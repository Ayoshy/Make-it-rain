namespace Battlestation;
internal sealed class TerminalSurface : Surface
{
    readonly TerminalTabs tabs=new();
    protected override int VisualChildrenCount=>1;
    protected override System.Windows.Media.Visual GetVisualChild(int index)=>index==0?tabs:throw new ArgumentOutOfRangeException(nameof(index));
    public TerminalSurface(Station s):base(s,5)
    {
        Width=s.Layout["terminal"].Width;Height=s.Layout["terminal"].Height;AddVisualChild(tabs);AddLogicalChild(tabs);
        tabs.Selected+=id=>Station.Terminal?.SelectTab(id);tabs.Closed+=id=>Station.Terminal?.CloseTab(id);
        tabs.Added+=()=>Station.Terminal?.Command("NewShell");tabs.PasteRequested+=()=>Station.Terminal?.Command("Paste");
        tabs.Customize+=Customize;
        tabs.CacheRefreshRequested+=()=>Station.Terminal?.RefreshCache();
    }
    void Customize(Guid id,TerminalTabAction action,string? value)
    {
        var terminal=Station.Terminal;if(terminal is null)return;
        var tab=terminal.Tabs.FirstOrDefault(t=>t.Id==id);if(tab is null)return;
        var preference=terminal.TabPreference(id);
        try
        {
            switch(action)
            {
                case TerminalTabAction.Rename:
                    OverlayStyle.Reveal(new TerminalTabEditor("Renommer l’onglet",tab.Title,false,name=>terminal.SetTabPreference(id,terminal.TabPreference(id) with{Name=name,AutomaticTitle=false})),Station.Settings.AnimateBackground);break;
                case TerminalTabAction.CustomColor:
                    OverlayStyle.Reveal(new TerminalTabEditor("Couleur de l’onglet",preference.Color??"#BBA0EA",true,color=>terminal.SetTabPreference(id,terminal.TabPreference(id) with{Color=color})),Station.Settings.AnimateBackground);break;
                case TerminalTabAction.Color:terminal.SetTabPreference(id,preference with{Color=value});break;
                case TerminalTabAction.AutomaticTitle:terminal.SetTabPreference(id,preference with{AutomaticTitle=bool.Parse(value!),Name=preference.Name??tab.Title});break;
                case TerminalTabAction.Effects:terminal.SetTabPreference(id,preference with{Effects=bool.Parse(value!)});break;
                case TerminalTabAction.Reset:terminal.SetTabPreference(id,new());break;
                case TerminalTabAction.StatusHelp:System.Windows.MessageBox.Show("Dans Codex CLI, ouvre /title et active run-state et activity. Garde thread-name et project-name pour le titre.","États Codex");break;
            }
            tabs.SetTabs(terminal.Tabs);
        }
        catch(Exception e) when(e is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException){System.Windows.MessageBox.Show(e.Message,"Onglet terminal");}
    }
    protected override System.Windows.Size MeasureOverride(System.Windows.Size available){tabs.Measure(new System.Windows.Size(Math.Max(0,Width-28),40));return new(Width,Height);}
    protected override System.Windows.Size ArrangeOverride(System.Windows.Size size){tabs.Arrange(new System.Windows.Rect(14,10,Math.Max(0,size.Width-28),40));return size;}
    protected override void Paint()
    {
        tabs.Visibility=Station.Terminal?.UseGlassTabs==true?System.Windows.Visibility.Visible:System.Windows.Visibility.Collapsed;
        if(Station.Terminal is {} terminal)tabs.SetTabs(terminal.Tabs);
        // The stable host's tab strip is the sole header once it is attached.
        if(Station.Terminal?.RemoteHandle==0)
        {
            Header("TERMINAL");
            Button("StartTerminal","Ouvrir le terminal",Width/2-130,Height/2-24,260,48,()=>Station.Terminal?.Start(),13);
        }
    }
}
