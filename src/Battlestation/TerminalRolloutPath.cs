using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Battlestation;

// Windows x64: identify the rollout actually held open for writing by this CLI.
// Only duplicate local handles; never read their contents or close a remote handle.
internal static class TerminalRolloutPath
{
    [DllImport("kernel32.dll", SetLastError = true)] static extern nint OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll")] static extern nint GetCurrentProcess();
    [DllImport("kernel32.dll")] static extern bool DuplicateHandle(nint source, nint handle, nint target, out nint copy, uint access, bool inherit, uint options);
    [DllImport("kernel32.dll")] static extern uint GetFileType(nint handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern uint GetFinalPathNameByHandle(nint file, StringBuilder path, uint length, uint flags);
    [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(nint process, int infoClass, byte[] info, int length, out int returned);

    public static string[] Find(int pid, string sessionsRoot)
    {
        if (pid <= 0) return [];
        nint process = OpenProcess(0x0440, false, pid); // QUERY_INFORMATION | DUP_HANDLE
        if (process == 0) return [];
        try
        {
            var buffer = new byte[64 * 1024];
            int status = NtQueryInformationProcess(process, 51, buffer, buffer.Length, out int needed);
            if (status < 0 && needed > buffer.Length && needed <= 4 * 1024 * 1024)
            {
                buffer = new byte[needed];
                status = NtQueryInformationProcess(process, 51, buffer, buffer.Length, out _);
            }
            if (status < 0) return [];
            long count = BitConverter.ToInt64(buffer, 0);
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var path = new StringBuilder(32768);
            for (int i = 0; i < count && 16L + (i + 1L) * 40 <= buffer.Length; i++)
            {
                int offset = 16 + i * 40;
                uint access = BitConverter.ToUInt32(buffer, offset + 24);
                if ((access & 6) == 0) continue; // FILE_WRITE_DATA | FILE_APPEND_DATA
                nint handle = (nint)BitConverter.ToInt64(buffer, offset);
                if (!DuplicateHandle(process, handle, GetCurrentProcess(), out nint copy, 0, false, 2)) continue;
                try
                {
                    if (GetFileType(copy) != 1) continue; // Never query names of pipes/devices.
                    path.Clear();
                    uint length = GetFinalPathNameByHandle(copy, path, (uint)path.Capacity, 8);
                    if (length == 0 || length >= path.Capacity) continue;
                    var candidate = ScopedPath(path.ToString(), sessionsRoot);
                    if (candidate is null) continue;
                    result.Add(candidate);
                }
                finally { CloseHandle(copy); }
            }
            return [..result];
        }
        finally { CloseHandle(process); }
    }

    internal static string? ScopedPath(string path, string root)
    {
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) path = path[4..];
        var full = Path.GetFullPath(path);
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(full).StartsWith("rollout-", StringComparison.OrdinalIgnoreCase)
            && full.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) ? full : null;
    }
}
