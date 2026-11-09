using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
namespace GlassDock;
public sealed record AppWindow(nint Handle,string Path,string Title,bool Minimized);
public sealed class WindowCatalog : IDisposable
{
    readonly Native.EventProc callback;
    readonly List<nint> hooks=new();
    readonly DispatcherTimer debounce;
    readonly DispatcherTimer recovery;
    readonly IVirtualDesktopManager? desktops;
    bool disposed,monitoringStopped;
    public bool DesktopAvailable => desktops != null;
    public Guid? CurrentDesktopId { get; private set; }
    public List<string> PinnedPaths { get; set; }=new();
    public List<AppWindow> Windows { get; private set; }=new();
    public HashSet<string> RunningPaths { get; private set; }=new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> VisiblePaths { get; private set; }=new(StringComparer.OrdinalIgnoreCase);
    public event Action? Changed;
    public event Action<nint>? ForegroundChanged;
    public WindowCatalog()
    {
        try { desktops=(IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A"))!)!; } catch(Exception e) { SettingsStore.Log(e); }
        debounce=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(140) };
        debounce.Tick+=(_,_)=>{ debounce.Stop(); Refresh(); };
        callback=(_,evt,hwnd,obj,child,thread,time)=> {
            if (evt==3) ForegroundChanged?.Invoke(hwnd);
            if (obj==0 || evt==3) { debounce.Stop(); debounce.Start(); }
        };
        foreach(var range in new[] {(3u,3u),(0x8000u,0x8002u),(0x800Bu,0x800Cu),(0x0016u,0x0017u)})
        { var h=Native.SetWinEventHook(range.Item1,range.Item2,0,callback,0,0,0); if(h!=0) hooks.Add(h); }
        recovery=new DispatcherTimer { Interval=TimeSpan.FromSeconds(3) };
        recovery.Tick+=(_,_)=>Refresh(); recovery.Start();
    }
    public void Refresh()
    {
        if(disposed||monitoringStopped)return;
        var result=new List<AppWindow>(); var visible=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paths=new Dictionary<uint,string>();
        string PathFor(uint pid)
        {
            if(paths.TryGetValue(pid,out var p)) return p;
            try { using var process=Process.GetProcessById((int)pid); p=process.MainModule?.FileName??""; } catch { p=""; }
            paths[pid]=p; return p;
        }
        Native.EnumWindows((hwnd,_)=>{
            if(!Native.IsWindowVisible(hwnd)) return true;
            Native.GetWindowThreadProcessId(hwnd,out var pid);
            if(pid==Environment.ProcessId) return true;
            int style=Native.GetWindowLong(hwnd,-20);
            if((style&0x80)!=0 || (Native.GetWindow(hwnd,4)!=0 && (style&0x40000)==0)) return true;
            var title=new StringBuilder(1024); Native.GetWindowText(hwnd,title,title.Capacity);
            if(title.Length==0 || title.ToString()=="Program Manager") return true;
            var executable=PathFor(pid); if(executable.Length==0) return true;
            var path=AppIdentity.ForWindow(hwnd,pid,executable);
            visible.Add(path);
            if(desktops==null || desktops.IsWindowOnCurrentVirtualDesktop(hwnd,out bool current)!=0 || !current) return true;
            Native.DwmGetWindowAttribute(hwnd,14,out int cloaked,4); if(cloaked!=0) return true;
            result.Add(new(hwnd,path,title.ToString(),Native.IsIconic(hwnd))); return true;
        },0);
        CurrentDesktopId=null;
        foreach(var w in result.OrderByDescending(w=>w.Handle==Native.GetForegroundWindow()))
        {if(desktops!=null && desktops.GetWindowDesktopId(w.Handle,out var id)==0 && id!=Guid.Empty){CurrentDesktopId=id;break;}}
        var running=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pinnedNames=PinnedPaths.Select(AppIdentity.Canonical).Where(p=>!p.StartsWith(AppIdentity.AppsFolder,StringComparison.OrdinalIgnoreCase)).Select(System.IO.Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var process in Process.GetProcesses())
        { using(process) { try { if(!pinnedNames.Contains(process.ProcessName))continue;var path=PathFor((uint)process.Id); if(path.Length>0) running.Add(AppIdentity.Canonical(path)); } catch { } } }
        running.UnionWith(visible);
        Windows=result; VisiblePaths=visible; RunningPaths=running; Changed?.Invoke();
    }
    public bool IsOnCurrentDesktop(nint hwnd)
    {
        if(disposed||!Native.IsWindow(hwnd)||!Native.IsWindowVisible(hwnd)||desktops==null)return false;
        if(desktops.IsWindowOnCurrentVirtualDesktop(hwnd,out bool current)!=0||!current)return false;
        return Native.DwmGetWindowAttribute(hwnd,14,out int cloaked,4)==0&&cloaked==0;
    }
    internal void StopMonitoring(){monitoringStopped=true;recovery.Stop();debounce.Stop();foreach(var h in hooks)Native.UnhookWinEvent(h);hooks.Clear();}
    public void Dispose() { if(disposed)return;disposed=true;StopMonitoring();if(desktops!=null) Marshal.FinalReleaseComObject(desktops); }
}
