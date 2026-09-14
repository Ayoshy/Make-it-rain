using System.Windows.Automation;

namespace Battlestation;
internal sealed record StremioControlState(bool Available,bool? Playing);
internal static class StremioControls
{
    static AutomationElement? Find(VideoSource source)
    {
        if(source.Kind!="stremio"||!Native.IsWindow(source.Handle))return null;
        Native.GetWindowThreadProcessId(source.Handle,out uint pid);if(pid!=source.Pid)return null;
        // Stremio WebView2 exposes its transport as a Group with InvokePattern,
        // not a Button. Restrict lookup to the selected client's exact window.
        var root=AutomationElement.FromHandle(source.Handle);
        var names=new[]{"Pause","Play","Lecture","Mettre en pause","Reprendre"};
        var condition=new OrCondition(names.Select(name=>new PropertyCondition(AutomationElement.NameProperty,name)).ToArray());
        var elements=root.FindAll(TreeScope.Descendants,condition);
        foreach(AutomationElement element in elements)
            if(element.Current.IsEnabled&&element.TryGetCurrentPattern(InvokePattern.Pattern,out _))return element;
        return null;
    }
    internal static StremioControlState Read(VideoSource source)
    {
        try
        {
            var element=Find(source);if(element is null)return new(false,null);
            return new(true,element.Current.Name is "Pause" or "Mettre en pause");
        }
        catch(Exception e) when(e is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException){return new(false,null);}
    }
    internal static bool Toggle(VideoSource source)
    {
        try
        {
            var element=Find(source);if(element is null)return false;
            ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();return true;
        }
        catch(Exception e) when(e is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException){return false;}
    }
}
