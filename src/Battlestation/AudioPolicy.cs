using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace Battlestation;
internal static class AudioPolicy
{
    // Windows policy COM ABI; unused slots preserve the interface's vtable.
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        void GetMixFormat(); void GetDeviceFormat(); void ResetDeviceFormat(); void SetDeviceFormat();
        void GetProcessingPeriod(); void SetProcessingPeriod(); void GetShareMode(); void SetShareMode();
        void GetPropertyValue(); void SetPropertyValue();
        void SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, Role role);
    }
    internal static void SetDefault(string id,Role role)
    {
        object policy=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9"),true)!)!;
        try{((IPolicyConfig)policy).SetDefaultEndpoint(id,role);}
        finally{Marshal.ReleaseComObject(policy);}
    }
}
