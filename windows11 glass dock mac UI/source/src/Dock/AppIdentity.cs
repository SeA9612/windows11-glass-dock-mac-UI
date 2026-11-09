using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace GlassDock;

// Identity comes from exact executable paths, Shell links or an explicit AUMID.
// Window icons/titles and similar-looking app names are never used as identities.
internal static class AppIdentity
{
    internal const string AppsFolder="shell:AppsFolder\\";
    static readonly ConcurrentDictionary<string,string> pins=new(StringComparer.OrdinalIgnoreCase);
    static readonly ConcurrentDictionary<string,string> packagedExecutables=new(StringComparer.OrdinalIgnoreCase);
    internal static bool IsApplication(string source)
    {
        if(source.StartsWith(AppsFolder,StringComparison.OrdinalIgnoreCase)||Path.GetExtension(source).Equals(".exe",StringComparison.OrdinalIgnoreCase))return true;
        if(!Path.GetExtension(source).Equals(".lnk",StringComparison.OrdinalIgnoreCase))return false;
        string target=Canonical(source);
        return !target.Equals(source,StringComparison.OrdinalIgnoreCase)&&(target.StartsWith(AppsFolder,StringComparison.OrdinalIgnoreCase)||Path.GetExtension(target).Equals(".exe",StringComparison.OrdinalIgnoreCase));
    }
    internal static string Canonical(string path)
    {
        if(path.StartsWith(AppsFolder,StringComparison.OrdinalIgnoreCase))return path;
        if(packagedExecutables.TryGetValue(path,out var packaged))return packaged;
        if(pins.TryGetValue(path,out var known))return packagedExecutables.TryGetValue(known,out packaged)?packaged:known;
        string identity=path;
        if(Path.GetExtension(path).Equals(".lnk",StringComparison.OrdinalIgnoreCase))
        {
            object? link=null;
            try
            {
                link=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))!);
                ((IPersistFile)link!).Load(path,0);
                var target=new StringBuilder(32768);
                ((IShellLink)link!).GetPath(target,target.Capacity,0,4);
                var resolved=Environment.ExpandEnvironmentVariables(target.ToString());
                if(Path.GetExtension(resolved).Equals(".exe",StringComparison.OrdinalIgnoreCase))identity=Path.GetFullPath(resolved);
                else
                {
                    string? id=ReadAppId(link as IPropertyStore);
                    if(!string.IsNullOrEmpty(id))identity=AppsFolder+id;
                    else StartupDiagnostics.Write("App identity: link has no executable/AUMID target: "+path);
                }
            }
            catch(Exception error){StartupDiagnostics.Write("App identity: shortcut unresolved: "+path+"; "+error.Message);}
            finally {if(link!=null&&Marshal.IsComObject(link))Marshal.FinalReleaseComObject(link);}
        }
        pins[path]=identity;return packagedExecutables.TryGetValue(identity,out packaged)?packaged:identity;
    }
    internal static string ForWindow(nint hwnd,uint pid,string executable)
    {
        string? appId=null;
        // Packaged process identity is authoritative. The window property also
        // identifies hosted packaged apps when the process is ApplicationFrameHost.
        nint process=OpenProcess(0x1000,false,pid);
        if(process!=0)
        {
            try
            {
                uint size=0;
                if(GetApplicationUserModelId(process,ref size,null)==122&&size>1)
                {var value=new StringBuilder((int)size);if(GetApplicationUserModelId(process,ref size,value)==0)appId=value.ToString();}
            }
            finally {CloseHandle(process);}
        }
        if(string.IsNullOrWhiteSpace(appId)&&Path.GetFileName(executable).Equals("ApplicationFrameHost.exe",StringComparison.OrdinalIgnoreCase))
        {
            Guid iid=typeof(IPropertyStore).GUID;
            if(SHGetPropertyStoreForWindow(hwnd,ref iid,out var store)==0)
            {try {appId=ReadAppId(store);}finally {Marshal.FinalReleaseComObject(store);}}
        }
        if(string.IsNullOrWhiteSpace(appId))return executable;
        string key=AppsFolder+appId;
        if(!Path.GetFileName(executable).Equals("ApplicationFrameHost.exe",StringComparison.OrdinalIgnoreCase))packagedExecutables[executable]=key;
        return key;
    }
    internal static string LaunchTarget(string identity,IEnumerable<string> pinned)
    {
        foreach(string pin in pinned)if(Canonical(pin).Equals(identity,StringComparison.OrdinalIgnoreCase))return pin;
        return identity;
    }
    internal static string DisplayName(string identity)=>identity.StartsWith(AppsFolder,StringComparison.OrdinalIgnoreCase)?identity.Substring(AppsFolder.Length):Path.GetFileNameWithoutExtension(identity);
    static string? ReadAppId(IPropertyStore? store)
    {
        if(store==null)return null;
        var key=new PropertyKey {Format=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),Id=5};
        if(store.GetValue(ref key,out var value)!=0)return null;
        try {return value.Type is 31 or 8?Marshal.PtrToStringUni(value.Pointer):null;}
        finally {PropVariantClear(ref value);}
    }
    [StructLayout(LayoutKind.Sequential)] struct PropertyKey {internal Guid Format;internal uint Id;}
    [StructLayout(LayoutKind.Explicit,Size=24)] struct PropVariant {[FieldOffset(0)] internal ushort Type;[FieldOffset(8)] internal nint Pointer;}
    [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index,out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key,out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key,ref PropVariant value);
        [PreserveSig] int Commit();
    }
    [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellLink {void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder path,int length,nint findData,uint flags);}
    [DllImport("shell32.dll")] static extern int SHGetPropertyStoreForWindow(nint hwnd,ref Guid iid,[MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant value);
    [DllImport("kernel32.dll")] static extern nint OpenProcess(uint access,bool inherit,uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern int GetApplicationUserModelId(nint process,ref uint length,StringBuilder? appId);
}
