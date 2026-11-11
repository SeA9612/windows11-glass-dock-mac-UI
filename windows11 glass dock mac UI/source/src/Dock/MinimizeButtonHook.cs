using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;
namespace GlassDock;

// Watches left clicks on the minimize (_) button of app windows so the dock can
// play the genie effect before minimizing. Only that click is held back: the
// button press and release are swallowed and the dock minimizes the window itself.
// Runs on its own thread so dock rendering never delays the system mouse.
internal sealed class MinimizeButtonHook : IDisposable
{
    const int MouseLowLevel=14;
    const uint LeftDown=0x201,LeftUp=0x202,NcHitTest=0x84,HtMinButton=8,AbortIfHung=0x2;
    readonly Dispatcher dispatcher;
    readonly Action<nint> clicked;
    readonly Thread thread;
    readonly uint ownProcess=(uint)Environment.ProcessId;
    Native.HookProc? proc;
    nint hook,pressed;
    Native.Point pressedAt;
    uint threadId;
    volatile bool enabled;
    volatile HashSet<nint> eligible=new();
    internal bool Enabled { get=>enabled; set=>enabled=value; }
    // Windows the dock manages; other windows keep the normal Windows behaviour.
    internal void SetEligible(IEnumerable<nint> windows)=>eligible=new HashSet<nint>(windows);

    internal MinimizeButtonHook(Dispatcher dispatcher,Action<nint> clicked)
    {
        this.dispatcher=dispatcher;this.clicked=clicked;
        thread=new Thread(Run) {IsBackground=true,Name="GlassDock minimize button hook"};
        thread.Start();
    }

    void Run()
    {
        threadId=Native.GetCurrentThreadId();
        proc=Callback;
        hook=Native.SetWindowsHookEx(MouseLowLevel,proc,Native.GetModuleHandle(null),0);
        if(hook==0){StartupDiagnostics.Write($"Minimize button hook failed: {Marshal.GetLastWin32Error()}");return;}
        while(Native.GetMessage(out _,0,0,0)>0){}
        Native.UnhookWindowsHookEx(hook);hook=0;
    }

    nint Callback(int code,nint message,nint data)
    {
        if(code>=0)
        {
            uint kind=(uint)message;
            if(kind==LeftDown)
            {
                pressed=0;
                if(enabled)
                {
                    var point=Marshal.PtrToStructure<Native.MouseHookData>(data).Point;
                    var target=MinimizeButtonAt(point);
                    if(target!=0){pressed=target;pressedAt=point;return 1;}
                }
            }
            else if(kind==LeftUp&&pressed!=0)
            {
                var target=pressed;pressed=0;
                var point=Marshal.PtrToStructure<Native.MouseHookData>(data).Point;
                // Dragging off the button cancels, like a normal button. Only the
                // distance is checked here: asking the app again can time out while
                // it is busy and would silently drop the click.
                uint dpi=Native.GetDpiForWindow(target);double scale=(dpi==0?96:dpi)/96d;
                bool inside=Math.Abs(point.X-pressedAt.X)<=24*scale&&Math.Abs(point.Y-pressedAt.Y)<=16*scale;
                if(enabled&&inside)dispatcher.BeginInvoke(()=>{StartupDiagnostics.Write($"Minimize button clicked 0x{target:X}");clicked(target);});
                else StartupDiagnostics.Write($"Minimize button press cancelled 0x{target:X}");
                return 1;
            }
        }
        return Native.CallNextHookEx(hook,code,message,data);
    }

    nint MinimizeButtonAt(Native.Point point)
    {
        var child=Native.WindowFromPoint(point);
        if(child==0)return 0;
        var root=Native.GetAncestor(child,2);
        if(root==0||!eligible.Contains(root))return 0;
        Native.GetWindowThreadProcessId(root,out uint pid);
        if(pid==ownProcess||!Native.GetWindowRect(root,out var bounds))return 0;
        // Caption buttons live in the top strip; skip the hit test anywhere else.
        uint dpi=Native.GetDpiForWindow(root);
        if(point.Y-bounds.Top>64*(dpi==0?96:dpi)/96)return 0;
        foreach(var hwnd in child==root?new[]{root}:new[]{child,root})
        {
            var logical=point;
            Native.PhysicalToLogicalPointForPerMonitorDPI(hwnd,ref logical);
            nint position=(nint)((logical.Y&0xFFFF)<<16|(logical.X&0xFFFF));
            if(Native.SendMessageTimeout(hwnd,NcHitTest,0,position,AbortIfHung,50,out nint hit)!=0&&hit==HtMinButton)return root;
        }
        return 0;
    }

    public void Dispose()
    {
        enabled=false;
        if(threadId!=0)Native.PostThreadMessage(threadId,0x12,0,0); // WM_QUIT
    }
}
