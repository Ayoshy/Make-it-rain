namespace Battlestation;
internal sealed class TerminalSurface : Surface
{
    readonly TerminalTabs tabs=new();
    protected override int VisualChildrenCount=>1;
    protected override System.Windows.Media.Visual GetVisualChild(int index)=>index==0?tabs:throw new ArgumentOutOfRangeException(nameof(index));
    public TerminalSurface(Station s):base(s)
    {
        Width=s.Layout["terminal"].Width;Height=s.Layout["terminal"].Height;AddVisualChild(tabs);AddLogicalChild(tabs);
        tabs.Selected+=id=>Station.Terminal?.SelectTab(id);tabs.Closed+=id=>Station.Terminal?.CloseTab(id);
        tabs.Added+=()=>Station.Terminal?.Command("NewShell");tabs.PasteRequested+=()=>Station.Terminal?.Command("Paste");
    }
    protected override System.Windows.Size MeasureOverride(System.Windows.Size available){tabs.Measure(new System.Windows.Size(Math.Max(0,Width-28),40));return new(Width,Height);}
    protected override System.Windows.Size ArrangeOverride(System.Windows.Size size){tabs.Arrange(new System.Windows.Rect(14,10,Math.Max(0,size.Width-28),40));return size;}
    protected override void Paint()
    {
        Panel(0,0,Width,Height);Glass(5,0,0,Width,Height);
        tabs.Visibility=Station.Terminal?.UseGlassTabs==true?System.Windows.Visibility.Visible:System.Windows.Visibility.Collapsed;
        if(Station.Terminal is {} terminal)tabs.SetTabs(terminal.Tabs);
        // The stable host's tab strip is the sole header once it is attached.
        if(Station.Terminal?.RemoteHandle==0)
        {
            Text("TERMINAL",24,18,9,"#B8ABCA");
            Button("StartTerminal","Ouvrir le terminal",Width/2-130,Height/2-24,260,48,()=>Station.Terminal?.Start(),13);
        }
    }
}
