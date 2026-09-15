using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Battlestation;

// Shell lookup also resolves .lnk icons. Never execute the target to get artwork.
internal sealed class AppIcons
{
    readonly Dictionary<string,BitmapSource?> cache=new(StringComparer.OrdinalIgnoreCase);
    public BitmapSource? Get(string path)
    {
        path=Environment.ExpandEnvironmentVariables(path);
        if(cache.TryGetValue(path,out var cached))return cached;
        BitmapSource? image=null;
        if(File.Exists(path))
        {
            var info=new ShellFileInfo();
            if(SHGetFileInfo(path,0,ref info,(uint)Marshal.SizeOf<ShellFileInfo>(),0x100)!=0&&info.Icon!=0)
            {
                try
                {
                    image=Imaging.CreateBitmapSourceFromHIcon(info.Icon,System.Windows.Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
                    image.Freeze();
                }
                finally { DestroyIcon(info.Icon); }
            }
        }
        if(cache.Count>=32)cache.Clear();
        return cache[path]=image;
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct ShellFileInfo
    {
        public nint Icon;public int IconIndex;public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)] public string TypeName;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern nint SHGetFileInfo(string path,uint attributes,ref ShellFileInfo info,uint size,uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(nint icon);
}
