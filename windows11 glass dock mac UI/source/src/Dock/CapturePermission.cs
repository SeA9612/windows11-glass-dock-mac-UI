using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Security.Authorization.AppCapabilityAccess;

namespace GlassDock;

// Requests the official, app-scoped permission. Never changes privacy settings
// or suppresses the border if Windows has not granted this capability.
internal static class CapturePermission
{
    static Task<bool>? request;
    internal static bool Allowed {get;private set;}
    internal static string Status {get;private set;}="not requested";
    internal static event Action? Changed;
    internal static Task<bool> RequestAsync()=>request??=RequestCoreAsync();
    static async Task<bool> RequestCoreAsync()
    {
        try
        {
            if(!OperatingSystem.IsWindowsVersionAtLeast(10,0,20348)){Status="unsupported Windows version";return false;}
            uint length=0;
            if(GetCurrentPackageFullName(ref length,null)!=122){Status="package identity missing";return false;}
            var name=new StringBuilder((int)length);GetCurrentPackageFullName(ref length,name);
            StartupDiagnostics.Write("Capture identity: "+name);
            Status="waiting for Windows consent";
            var access=await GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless);
            Allowed=access==AppCapabilityAccessStatus.Allowed;Status=access.ToString();
            Changed?.Invoke();return Allowed;
        }
        catch(Exception error){Allowed=false;Status=$"unavailable (0x{error.HResult:X8})";StartupDiagnostics.Write("Borderless capture permission failed",error);return false;}
        finally {StartupDiagnostics.Write($"Borderless capture permission: {Status}; allowed={Allowed}");}
    }
    internal static void Apply(GraphicsCaptureSession session)
    {
        if(!Allowed||!OperatingSystem.IsWindowsVersionAtLeast(10,0,20348))return;
        try {session.IsBorderRequired=false;StartupDiagnostics.Write("Preview capture: border not required (Windows permission granted)");}
        catch(Exception error){StartupDiagnostics.Write("Could not apply borderless capture; keeping Windows border",error);}
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern int GetCurrentPackageFullName(ref uint length,StringBuilder? name);
}
