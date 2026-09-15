using System.Reflection;
using System.Runtime.InteropServices;
using Battlestation;

static class AppActivityTests
{
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    static DockApp App(string name, string path) => new(name, path);
    static AppProcessSnapshot Process(string name, string? path, bool accessible = true) => new(name, path, accessible);

    static void DirectPathAndExternalChanges()
    {
        var app = App("Codex", @"C:\Users\Ayo\AppData\Local\Programs\OpenAI\Codex\bin\codex.exe");
        var stopped = AppActivity.Snapshot([app], []);
        Check(stopped.For(app) == AppActivityState.Stopped, "No process is stopped");

        var running = AppActivity.Snapshot([app], [Process("codex", app.Path)]);
        Check(running.For(app) == AppActivityState.Running, "The exact Codex CLI path is running");

        var otherPath = AppActivity.Snapshot([app], [Process("codex", @"C:\Temp\codex.exe")]);
        Check(otherPath.For(app) == AppActivityState.Stopped, "A same-name executable at another path is excluded");
        var renamed = app with { Name = "My CLI" };
        Check(AppActivity.Snapshot([renamed], [Process("codex", app.Path)]).For(renamed) == AppActivityState.Running, "A display-name change keeps executable identity");
        Check(AppActivity.Snapshot([app], new FailedProcesses()).For(app) == AppActivityState.Unknown, "A failed process enumeration never means stopped");
        Check(AppActivity.Unknown([app], "resolution failed").For(app) == AppActivityState.Unknown, "Failure fallback does not resolve again");
    }

    static void InaccessibleAndMultipleProcesses()
    {
        var app = App("Brave", @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe");
        var unknown = AppActivity.Snapshot([app], [Process("brave", null, false)]);
        Check(unknown.For(app) == AppActivityState.Unknown, "An inaccessible relevant process is unknown");

        var conclusive = AppActivity.Snapshot([app], [
            Process("brave", null, false),
            Process("brave", app.Path),
        ]);
        Check(conclusive.For(app) == AppActivityState.Running, "A conclusive process wins over an inaccessible one");
    }

    static void UrlAndLauncherProcessesAreNotFalsePositives()
    {
        var root = Path.Combine(Path.GetTempPath(), "Battlestation-AppActivity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var discordLink = Path.Combine(root, "Discord.lnk");
            CreateShortcut(discordLink, Path.Combine(root, "Discord", "Update.exe"), "--processStart \"Discord.exe\"");
            var discord = App("Discord", discordLink);
            var updaterOnly = AppActivity.Snapshot([discord], [Process("Update", Path.Combine(root, "Discord", "Update.exe"))]);
            Check(updaterOnly.For(discord) == AppActivityState.Stopped, "Discord updater alone is excluded");

            var discordPath = Path.Combine(root, "Discord", "app-1.0.9257", "Discord.exe");
            var discordRunning = AppActivity.Snapshot([discord], [Process("Discord", discordPath)]);
            Check(discordRunning.For(discord) == AppActivityState.Running, "Discord version child is matched");

            var battleNet = App("battle.net", Path.Combine(root, "Battle.net", "Battle.net Launcher.exe"));
            var agent = AppActivity.Snapshot([battleNet], [Process("Agent", Path.Combine(root, "Battle.net", "Agent.exe"))]);
            Check(agent.For(battleNet) == AppActivityState.Stopped, "Battle.net Agent is excluded");
            var launcher = AppActivity.Snapshot([battleNet], [Process("Battle.net Launcher", battleNet.Path)]);
            Check(launcher.For(battleNet) == AppActivityState.Stopped, "Battle.net launcher is excluded");
            var battleChild = Path.Combine(root, "Battle.net", "1.0", "Battle.net.exe");
            var battleRunning = AppActivity.Snapshot([battleNet], [Process("Battle.net", battleChild)]);
            Check(battleRunning.For(battleNet) == AppActivityState.Running, "Battle.net version child is matched");

            var urlPath = Path.Combine(root, "Brave.url");
            File.WriteAllText(urlPath, "[InternetShortcut]\nURL=https://example.test");
            var url = App("Brave", urlPath);
            var urlResult = AppActivity.Snapshot([url], [Process("brave", @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe")]);
            Check(urlResult.For(url) == AppActivityState.Unknown, "A URL shortcut stays unknown");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    sealed class FailedProcesses : IReadOnlyList<AppProcessSnapshot>
    {
        public int Count => throw new System.ComponentModel.Win32Exception();
        public AppProcessSnapshot this[int index] => throw new System.ComponentModel.Win32Exception();
        public IEnumerator<AppProcessSnapshot> GetEnumerator() => throw new System.ComponentModel.Win32Exception();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    static void HardLinkedExecutable()
    {
        var root=Path.Combine(Path.GetTempPath(),"Battlestation-identity-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var first=Path.Combine(root,"cli.exe");
            var second=Path.Combine(root,"versioned.exe");
            var copy=Path.Combine(root,"copy.exe");
            File.WriteAllText(first,"test fixture, never executed");
            Check(CreateHardLink(second,first,0),"Fixture hard link created");
            File.Copy(first,copy);
            var app=App("CLI",first);
            Check(AppActivity.Snapshot([app],[Process("cli",second)]).For(app)==AppActivityState.Running,"Another hard link to the configured executable is running");
            Check(AppActivity.Snapshot([app],[Process("cli",copy)]).For(app)==AppActivityState.Stopped,"An identical copy is a different file identity");
        }
        finally { Directory.Delete(root,true); }
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool CreateHardLink(string file,string existing,nint attributes);

    static void RiotLauncherDoesNotMatchGenericRiotProcess()
    {
        var root = Path.Combine(Path.GetTempPath(), "Battlestation-AppActivity-Riot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var link = Path.Combine(root, "League of Legends.lnk");
            CreateShortcut(link, @"C:\Riot Games\Riot Client\RiotClientServices.exe", "--launch-product=league_of_legends");
            var app = App("League of Legends", link);
            var riot = AppActivity.Snapshot([app], [Process("RiotClientServices", @"C:\Riot Games\Riot Client\RiotClientServices.exe")]);
            Check(riot.For(app) == AppActivityState.Stopped, "Riot client services alone is excluded");
            var league = AppActivity.Snapshot([app], [Process("LeagueClient", @"E:\LoL\Riot Games\League of Legends\LeagueClient.exe")]);
            Check(league.For(app) == AppActivityState.Running, "Registered League install child is matched");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    static void CreateShortcut(string path, string target, string arguments)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell unavailable");
        object shell = Activator.CreateInstance(shellType)!;
        object? shortcut = null;
        try
        {
            shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [path]);
            if (shortcut is null) throw new InvalidOperationException("Shortcut creation failed");
            var type = shortcut.GetType();
            type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, [target]);
            type.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, [arguments]);
            type.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
            if (Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }

    [STAThread]
    static void Main(string[] args)
    {
        if(args.Contains("--live"))
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(AppActivity.CaptureProcesses(["steam","brave","codex","claude","chrome","msedge","Discord","LeagueClient","LeagueClientUx","stremio-shell-ng","qbittorrent","Battle.net"])));
            return;
        }
        DirectPathAndExternalChanges();
        InaccessibleAndMultipleProcesses();
        HardLinkedExecutable();
        UrlAndLauncherProcessesAreNotFalsePositives();
        RiotLauncherDoesNotMatchGenericRiotProcess();
        Console.WriteLine("PASS: exact paths, external snapshots, launcher exclusions, unknown access, and Riot registration");
    }
}
