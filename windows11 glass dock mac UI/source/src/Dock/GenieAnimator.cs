using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace GlassDock;

// Runs the genie effect for real windows: snapshot, overlay, then the actual
// minimize/restore with the Windows animation suppressed for that window.
internal sealed class GenieAnimator : IDisposable
{
    const int DwmTransitionsForceDisabled=3;
    readonly DockSettings settings;
    readonly Dispatcher dispatcher;
    readonly Dictionary<nint,WindowSnapshot> snapshots=new();
    readonly HashSet<nint> busy=new();
    readonly HashSet<nint> dwmSuppressed=new();
    readonly Dictionary<nint,int> alphaHidden=new();
    bool disposed;
    // Target rectangle in the dock (physical pixels) for a window of the given size.
    internal Func<nint,string,Native.Rect,Native.Rect> Target { get; set; }=(_,_,r)=>r;
    internal Func<string> Edge { get; set; }=()=>"Bottom";
    internal event Action<nint>? MinimizeStarted;
    internal event Action<nint>? Landed;
    internal event Action<nint>? RestoreStarted;

    internal GenieAnimator(DockSettings settings,Dispatcher dispatcher){this.settings=settings;this.dispatcher=dispatcher;}

    internal bool Enabled=>settings.GenieEnabled&&!disposed;
    internal bool ThumbnailMode=>settings.GenieMode=="Thumbnail";
    internal static double[] Speeds=>new[]{0.5,0.75,1,1.5,2};
    TimeSpan Duration=>TimeSpan.FromMilliseconds(500/Math.Clamp(settings.GenieSpeed,0.25,4));
    internal TimeSpan SlotDuration=>TimeSpan.FromMilliseconds(180/Math.Clamp(settings.GenieSpeed,0.25,4));
    internal bool IsBusy(nint hwnd)=>busy.Contains(hwnd);
    internal BitmapSource? Image(nint hwnd)=>snapshots.TryGetValue(hwnd,out var s)?s.Image:null;
    internal void ForgetClosed()
    {
        foreach(var h in snapshots.Keys.Where(h=>!busy.Contains(h)&&!Native.IsWindow(h)).ToList())snapshots.Remove(h);
    }

    internal void Minimize(nint hwnd,string path,Action minimize)
    {
        if(busy.Contains(hwnd))return;
        var snapshot=Enabled?WindowSnapshot.Capture(hwnd):null;
        if(snapshot==null){minimize();return;}
        snapshots[hwnd]=snapshot;busy.Add(hwnd);
        StartupDiagnostics.Write($"Genie minimize 0x{hwnd:X}: start, mode={settings.GenieMode}");
        MinimizeStarted?.Invoke(hwnd);
        GenieOverlay overlay;
        try {overlay=new(snapshot.Image,snapshot.Bounds,()=>Target(hwnd,path,snapshot.Bounds),Edge(),false,Duration,!ThumbnailMode);}
        catch(Exception error){StartupDiagnostics.Write("Genie overlay failed",error);busy.Remove(hwnd);minimize();Landed?.Invoke(hwnd);return;}
        overlay.Ready+=()=>{bool dwm=Suppress(hwnd);StartupDiagnostics.Write($"Genie minimize 0x{hwnd:X}: overlay ready, dwm={dwm}");minimize();};
        overlay.Finished+=()=>{
            overlay.Close();busy.Remove(hwnd);Landed?.Invoke(hwnd);
            WaitFor(()=>Native.IsIconic(hwnd)||!Native.IsWindowVisible(hwnd),()=>Release(hwnd));
        };
        overlay.Show();
    }

    internal bool CanAnimateRestore(nint hwnd)=>Enabled&&snapshots.ContainsKey(hwnd)&&Native.IsIconic(hwnd);

    internal void Restore(nint hwnd,string path,Action? restored)
    {
        if(busy.Contains(hwnd))return;
        if(!CanAnimateRestore(hwnd)){Native.ShowWindowAsync(hwnd,9);restored?.Invoke();return;}
        var snapshot=snapshots[hwnd];busy.Add(hwnd);
        StartupDiagnostics.Write($"Genie restore 0x{hwnd:X}: start");
        RestoreStarted?.Invoke(hwnd);
        GenieOverlay overlay;
        try {overlay=new(snapshot.Image,snapshot.Bounds,()=>Target(hwnd,path,snapshot.Bounds),Edge(),true,Duration,!ThumbnailMode);}
        catch(Exception error){StartupDiagnostics.Write("Genie overlay failed",error);busy.Remove(hwnd);Native.ShowWindowAsync(hwnd,9);restored?.Invoke();return;}
        bool restoredEarly=false;
        overlay.Ready+=()=>{
            // Without DWM suppression the window is restored right away while fully
            // transparent, so the Windows animation plays unseen under the overlay.
            if(!Suppress(hwnd)){restoredEarly=true;Native.ShowWindowAsync(hwnd,9);}
        };
        overlay.Finished+=()=>{
            if(!restoredEarly)Native.ShowWindowAsync(hwnd,9);
            WaitFor(()=>!Native.IsIconic(hwnd),()=>{
                Release(hwnd);
                // Keep the last frame up briefly so the real window can paint first.
                Later(50,()=>{overlay.Close();busy.Remove(hwnd);snapshots.Remove(hwnd);restored?.Invoke();});
            });
        };
        overlay.Show();
    }

    // Returns true when DWM disabled the animation; otherwise falls back to making
    // the window transparent for the duration of the effect.
    bool Suppress(nint hwnd)
    {
        int on=1;
        if(Native.DwmSetWindowAttribute(hwnd,DwmTransitionsForceDisabled,ref on,4)==0){dwmSuppressed.Add(hwnd);return true;}
        int style=Native.GetWindowLong(hwnd,-20);
        // Windows that are already layered manage their own transparency; leave them.
        if((style&0x80000)==0&&!alphaHidden.ContainsKey(hwnd))
        {
            Native.SetWindowLong(hwnd,-20,style|0x80000);
            if(Native.SetLayeredWindowAttributes(hwnd,0,0,2))alphaHidden[hwnd]=style;
            else Native.SetWindowLong(hwnd,-20,style);
        }
        return false;
    }

    void Release(nint hwnd)
    {
        if(dwmSuppressed.Remove(hwnd)){int off=0;Native.DwmSetWindowAttribute(hwnd,DwmTransitionsForceDisabled,ref off,4);}
        if(alphaHidden.Remove(hwnd,out int style))
        {
            Native.SetLayeredWindowAttributes(hwnd,0,255,2);
            Native.SetWindowLong(hwnd,-20,style);
            Native.RedrawWindow(hwnd,0,0,0x0001|0x0004|0x0080|0x0400); // INVALIDATE | ERASE | ALLCHILDREN | FRAME
        }
    }

    // Polls every frame for up to one second; gives up waiting but still runs done.
    void WaitFor(Func<bool> condition,Action done)
    {
        int ticks=0;
        var timer=new DispatcherTimer(DispatcherPriority.Normal,dispatcher) {Interval=TimeSpan.FromMilliseconds(16)};
        timer.Tick+=(_,_)=>{if(condition()||++ticks>60||disposed){timer.Stop();done();}};
        timer.Start();
    }

    void Later(int milliseconds,Action action)
    {
        var timer=new DispatcherTimer(DispatcherPriority.Normal,dispatcher) {Interval=TimeSpan.FromMilliseconds(milliseconds)};
        timer.Tick+=(_,_)=>{timer.Stop();action();};
        timer.Start();
    }

    public void Dispose()
    {
        if(disposed)return;disposed=true;
        // Never leave a window invisible or without its animation after the dock exits.
        foreach(var h in dwmSuppressed.Concat(alphaHidden.Keys).ToList())Release(h);
    }
}
