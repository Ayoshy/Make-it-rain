using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Battlestation;

/// <summary>The material state of an application represented by a dock item.</summary>
internal enum AppActivityState
{
    Unknown,
    Stopped,
    Running,
}

/// <summary>A process view used by the detector. It deliberately contains no command line.</summary>
internal sealed record AppProcessSnapshot(string Name, string? Path, bool PathAccessible = true)
{
    internal string ProcessName
    {
        get
        {
            var value = System.IO.Path.GetFileName(Name ?? "").Trim();
            return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? value[..^4] : value;
        }
    }
}

internal sealed record AppActivityResult(
    DockApp App,
    AppActivityState State,
    string? MatchedPath = null);

internal sealed record AppActivityRuleInfo(
    string Name,
    string ConfigurationPath,
    string? TargetPath,
    string Kind,
    IReadOnlyList<string> CandidateNames,
    IReadOnlyList<string> ExactPaths,
    IReadOnlyList<string> Roots,
    bool Resolved);

/// <summary>A process scan and the state it established for an immutable app-list snapshot.</summary>
internal sealed record AppActivitySnapshot(
    IReadOnlyList<AppActivityResult> Results,
    DateTimeOffset CapturedAt,
    TimeSpan ScanDuration,
    bool ProcessScanSucceeded,
    string? Error,
    IReadOnlyList<AppActivityRuleInfo> Rules)
{
    internal AppActivityState For(DockApp app)
        => Results.FirstOrDefault(result => result.App == app)?.State ?? AppActivityState.Unknown;

    internal AppActivityResult? ResultFor(DockApp app)
        => Results.FirstOrDefault(result => result.App == app);
}

/// <summary>
/// Resolves dock configuration and determines application activity from process presence.
/// The synchronous entry point is intentionally deterministic and accepts a supplied process
/// snapshot so the UI and tests never need to launch or foreground an application.
/// </summary>
internal static class AppActivity
{
    static readonly ConcurrentDictionary<string, Lazy<ResolvedApp>> resolutionCache = new(StringComparer.OrdinalIgnoreCase);
    static readonly string[] Empty = [];

    internal static AppActivitySnapshot Snapshot(
        IReadOnlyList<DockApp> apps,
        IReadOnlyList<AppProcessSnapshot>? processes = null,
        DateTimeOffset? capturedAt = null)
    {
        // Copy the caller's list before resolving. A settings dialog may reorder its own list
        // while a worker is scanning; a scan always describes one immutable configuration.
        var appSnapshot = apps?.ToArray() ?? [];
        var started = Stopwatch.GetTimestamp();
        var resolved = appSnapshot.Select(Resolve).ToArray();
        var rules = resolved.Select(item => item.Rule.ToInfo()).ToArray();

        IReadOnlyList<AppProcessSnapshot> processSnapshot;
        bool processScanSucceeded = true;
        string? error = null;
        try
        {
            processSnapshot = (processes ?? CaptureProcesses(resolved.SelectMany(item => item.Rule.CandidateNames))).ToArray();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            processSnapshot = [];
            processScanSucceeded = false;
            error = exception.GetType().Name;
        }

        var results = processScanSucceeded
            ? resolved.Select(item => item.Evaluate(processSnapshot)).ToArray()
            : appSnapshot.Select(app => new AppActivityResult(app, AppActivityState.Unknown)).ToArray();
        return new AppActivitySnapshot(
            results,
            capturedAt ?? DateTimeOffset.UtcNow,
            Stopwatch.GetElapsedTime(started),
            processScanSucceeded,
            error,
            rules);
    }

    internal static AppActivitySnapshot Unknown(IReadOnlyList<DockApp> apps, string error)
    {
        var appSnapshot = apps?.ToArray() ?? [];
        return new AppActivitySnapshot(
            appSnapshot.Select(app => new AppActivityResult(app, AppActivityState.Unknown)).ToArray(),
            DateTimeOffset.UtcNow,
            TimeSpan.Zero,
            false,
            error,
            []);
    }

    internal static IReadOnlyList<AppProcessSnapshot> CaptureProcesses(IEnumerable<string> candidateNames)
    {
        var candidates = candidateNames
            .Select(NormalizeProcessName)
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found = new List<AppProcessSnapshot>();

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                string name;
                try { name = process.ProcessName; }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                { continue; }
                if (!candidates.Contains(NormalizeProcessName(name))) continue;

                var path = TryGetProcessPath(process.Id, out var accessible);
                found.Add(new AppProcessSnapshot(name, path, accessible));
            }
        }
        return found.ToArray();
    }

    static ResolvedApp Resolve(DockApp app)
    {
        var configurationPath = ExpandPath(app.Path);
        var lazy = resolutionCache.GetOrAdd(
            configurationPath,
            key => new Lazy<ResolvedApp>(() => ResolveUncached(app, key), LazyThreadSafetyMode.ExecutionAndPublication));
        var cached = lazy.Value;
        // The cache is by path as required, while the visible label is allowed to change in the
        // settings editor without carrying the old label into the result.
        return cached with { Rule = cached.Rule with { App = app, Name = app.Name, ConfigurationPath = configurationPath } };
    }

    static ResolvedApp ResolveUncached(DockApp app, string configurationPath)
    {
        var extension = Path.GetExtension(configurationPath);
        if (extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
            return Unresolved(app, configurationPath, "URL shortcut");

        string? targetPath = null;
        string arguments = "";
        if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(configurationPath) || !TryResolveShortcut(configurationPath, out targetPath, out arguments))
                return Unresolved(app, configurationPath, "unresolved shortcut");
        }
        else if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            targetPath = configurationPath;
        }
        else
        {
            // The editor normally permits only .lnk/.exe/.url. Keep unknown extensions safe:
            // they cannot accidentally turn into a generic browser match.
            return Unresolved(app, configurationPath, "unsupported configuration");
        }

        targetPath = NormalizePath(targetPath);
        if (targetPath is null)
            return Unresolved(app, configurationPath, "missing target");

        var targetName = Path.GetFileNameWithoutExtension(targetPath);
        var targetFileName = Path.GetFileName(targetPath);

        if (targetFileName.Equals("Update.exe", StringComparison.OrdinalIgnoreCase) &&
            Regex.IsMatch(arguments, @"(?:^|\s)--processStart\s+[""']?Discord\.exe[""']?(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var installRoot = DirectoryName(targetPath);
            var rule = Rule(app, configurationPath, targetPath, "Discord version child", ["Discord"], [], installRoot is null ? [] : [installRoot], resolved: installRoot is not null);
            return new ResolvedApp(rule, Matcher.VersionedChild("Discord", installRoot, "Discord.exe", "app-"));
        }

        if (targetFileName.Equals("RiotClientServices.exe", StringComparison.OrdinalIgnoreCase) &&
            Regex.IsMatch(arguments, @"(?:^|\s)--launch-product(?:=|\s+)league_of_legends(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var roots = RiotInstallRoots(targetPath);
            var names = new[] { "LeagueClient", "LeagueClientUx", "League of Legends" };
            var executables = new[] { "LeagueClient.exe", "LeagueClientUx.exe", "League of Legends.exe" };
            var rule = Rule(app, configurationPath, targetPath, "League client child", names, [], roots, resolved: roots.Count > 0);
            return new ResolvedApp(rule, Matcher.RootedNames(names, roots, executables));
        }

        if (targetFileName.Equals("Battle.net Launcher.exe", StringComparison.OrdinalIgnoreCase))
        {
            var installRoot = DirectoryName(targetPath);
            var rule = Rule(app, configurationPath, targetPath, "Battle.net version child", ["Battle.net"], [], installRoot is null ? [] : [installRoot], resolved: installRoot is not null);
            return new ResolvedApp(rule, Matcher.RootedNames(["Battle.net"], installRoot is null ? [] : [installRoot], ["Battle.net.exe"]));
        }

        var directRule = Rule(app, configurationPath, targetPath, "Executable", [targetName], [targetPath], [], resolved: true);
        return new ResolvedApp(directRule, Matcher.Exact(targetName, targetPath));
    }

    static AppActivityRule Rule(
        DockApp app,
        string configurationPath,
        string? targetPath,
        string kind,
        IReadOnlyList<string> candidateNames,
        IReadOnlyList<string> exactPaths,
        IReadOnlyList<string> roots,
        bool resolved)
        => new(app.Name, configurationPath, app, targetPath, kind,
            candidateNames.ToArray(), exactPaths.Select(NormalizePath).Where(path => path is not null).Cast<string>().ToArray(),
            roots.Select(NormalizePath).Where(path => path is not null).Cast<string>().ToArray(), resolved);

    static ResolvedApp Unresolved(DockApp app, string configurationPath, string kind)
    {
        var rule = new AppActivityRule(app.Name, configurationPath, app, null, kind, Empty, Empty, Empty, false);
        return new ResolvedApp(rule, Matcher.Unresolved);
    }

    static IReadOnlyList<string> RiotInstallRoots(string targetPath)
    {
        var roots = new List<string>();
        try
        {
            var registration = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games", "RiotClientInstalls.json");
            if (!File.Exists(registration)) return roots.ToArray();
            using var document = JsonDocument.Parse(File.ReadAllText(registration));
            if (!document.RootElement.TryGetProperty("associated_client", out var associated) || associated.ValueKind != JsonValueKind.Object)
                return roots.ToArray();
            foreach (var property in associated.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String) continue;
                var client = NormalizePath(property.Value.GetString());
                if (client is null || !client.Equals(targetPath, StringComparison.OrdinalIgnoreCase)) continue;
                var root = NormalizePath(property.Name);
                if (root is not null && Directory.Exists(root)) roots.Add(root);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            // An unreadable registration must never broaden the rule to all League/Riot names.
        }
        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    static bool TryResolveShortcut(string path, out string? target, out string arguments)
    {
        target = null;
        arguments = "";
        object? shell = null;
        object? shortcut = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: false);
            if (shellType is null) return false;
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [path]);
            if (shortcut is null) return false;
            var shortcutType = shortcut.GetType();
            target = shortcutType.InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null) as string;
            arguments = shortcutType.InvokeMember("Arguments", BindingFlags.GetProperty, null, shortcut, null) as string ?? "";
            return !string.IsNullOrWhiteSpace(target);
        }
        catch (Exception exception) when (exception is COMException or InvalidComObjectException or MemberAccessException or TargetInvocationException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }

    static string ExpandPath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path?.Trim() ?? "");
        try { return Path.GetFullPath(expanded); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        { return expanded; }
    }

    static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim())).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return full.Length == 0 ? Path.DirectorySeparatorChar.ToString() : full;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        { return null; }
    }

    static string? DirectoryName(string path)
    {
        try { return Directory.GetParent(path)?.FullName; }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        { return null; }
    }

    static string NormalizeProcessName(string name)
    {
        var value = Path.GetFileName(name ?? "").Trim();
        return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? value[..^4] : value;
    }

    // QueryFullProcessImageName may report another hard link to the same binary
    // (the installed Codex CLI does this). Compare metadata, never executable bytes.
    static bool? SameExecutable(string first, string second)
    {
        try
        {
            using var a = File.OpenHandle(first, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var b = File.OpenHandle(second, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (!GetFileInformationByHandle(a, out var left) || !GetFileInformationByHandle(b, out var right)) return null;
            return left.Volume == right.Volume && left.IndexHigh == right.IndexHigh && left.IndexLow == right.IndexLow;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle handle, out FileInformation information);

    static string? TryGetProcessPath(int processId, out bool accessible)
    {
        accessible = false;
        const uint QueryLimitedInformation = 0x1000;
        var handle = OpenProcess(QueryLimitedInformation, false, processId);
        if (handle == 0) return null;
        try
        {
            var capacity = 32768u;
            var buffer = new char[capacity];
            if (!QueryFullProcessImageName(handle, 0, buffer, ref capacity) || capacity == 0) return null;
            accessible = true;
            return new string(buffer, 0, checked((int)capacity));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OverflowException)
        { return null; }
        finally { CloseHandle(handle); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern nint OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool QueryFullProcessImageName(nint processHandle, int flags, char[] exeName, ref uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool CloseHandle(nint handle);

    internal sealed record AppActivityRule(
        string Name,
        string ConfigurationPath,
        DockApp App,
        string? TargetPath,
        string Kind,
        IReadOnlyList<string> CandidateNames,
        IReadOnlyList<string> ExactPaths,
        IReadOnlyList<string> Roots,
        bool Resolved)
    {
        internal AppActivityRuleInfo ToInfo()
            => new(Name, ConfigurationPath, TargetPath, Kind, CandidateNames, ExactPaths, Roots, Resolved);
    }

    sealed record ResolvedApp(AppActivityRule Rule, Matcher Matcher)
    {
        internal AppActivityResult Evaluate(IReadOnlyList<AppProcessSnapshot> processes)
        {
            if (!Rule.Resolved || !Matcher.Resolved)
                return new(Rule.App, AppActivityState.Unknown);

            var relevant = processes.Where(process => Matcher.CandidateNames.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (relevant.Length == 0) return new(Rule.App, AppActivityState.Stopped);

            bool inaccessible = false;
            foreach (var process in relevant)
            {
                if (!process.PathAccessible || string.IsNullOrWhiteSpace(process.Path))
                {
                    inaccessible = true;
                    continue;
                }
                var path = NormalizePath(process.Path);
                var matches = path is null ? null : Matcher.Matches(path);
                if (matches == true) return new(Rule.App, AppActivityState.Running, path);
                if (matches is null) inaccessible = true;
            }
            return new(Rule.App, inaccessible ? AppActivityState.Unknown : AppActivityState.Stopped);
        }
    }

    sealed class Matcher
    {
        internal static readonly Matcher Unresolved = new([], [], [], false, (_, _) => false);
        readonly Func<string, string, bool?> match;
        internal IReadOnlyList<string> CandidateNames { get; }
        internal bool Resolved { get; }
        Matcher(IReadOnlyList<string> candidateNames, IReadOnlyList<string> exactPaths, IReadOnlyList<string> roots, bool resolved, Func<string, string, bool?> match)
        {
            CandidateNames = candidateNames;
            Resolved = resolved;
            this.match = match;
        }
        internal bool? Matches(string path) => match(path, Path.GetFileName(path));
        internal static Matcher Exact(string candidateName, string exactPath)
            => new([candidateName], [exactPath], [], true, (path, _) => path.Equals(exactPath, StringComparison.OrdinalIgnoreCase) ? true : SameExecutable(path, exactPath));

        internal static Matcher VersionedChild(string candidateName, string? root, string executable, string directoryPrefix)
            => new([candidateName], [], root is null ? [] : [root], root is not null,
                (path, file) => root is not null && file.Equals(executable, StringComparison.OrdinalIgnoreCase) &&
                    IsUnder(path, root) && Directory.GetParent(path)?.Name.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase) == true);

        internal static Matcher RootedNames(IReadOnlyList<string> candidateNames, IReadOnlyList<string> roots, IReadOnlyList<string> executables)
            => new(candidateNames, [], roots, roots.Count > 0,
                (path, file) => executables.Any(executable => file.Equals(executable, StringComparison.OrdinalIgnoreCase)) && roots.Any(root => IsUnder(path, root)));

        static bool IsUnder(string path, string root)
            => path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

}
