using System;
using System.Runtime.InteropServices;
using System.Text;
namespace GlassDock;
internal static class Native
{
    internal delegate bool EnumProc(nint hwnd, nint data);
    internal delegate void EventProc(nint hook, uint evt, nint hwnd, int obj, int child, uint thread, uint time);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, nint data);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")] internal static extern int SetWindowLong(nint hwnd,int index,int value);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd,out Rect rect);
    [DllImport("user32.dll")] internal static extern int SetWindowRgn(nint hwnd,nint region,bool redraw);
    [DllImport("gdi32.dll")] internal static extern nint CreateRoundRectRgn(int left,int top,int right,int bottom,int ellipseWidth,int ellipseHeight);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint handle);
    [DllImport("gdi32.dll")] internal static extern nint CreateRectRgn(int left,int top,int right,int bottom);
    [DllImport("gdi32.dll")] internal static extern int CombineRgn(nint destination,nint source1,nint source2,int mode);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern int GetWindowText(nint hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] internal static extern nint GetWindow(nint hwnd,uint command);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")] internal static extern int GetWindowLong(nint hwnd,int index);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd,out uint pid);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(nint hwnd,int command);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool PostMessage(nint hwnd,uint message,nint w,nint l);
    [DllImport("user32.dll")] internal static extern nint SetWinEventHook(uint min,uint max,nint module,EventProc callback,uint pid,uint thread,uint flags);
    [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] internal static extern bool ScreenToClient(nint hwnd,ref Point point);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern nint MonitorFromPoint(Point point,uint flags);
    [DllImport("shcore.dll")] internal static extern int GetDpiForMonitor(nint monitor,int type,out uint x,out uint y);
    [StructLayout(LayoutKind.Sequential)] internal struct Margins {public int Left,Right,Top,Bottom;}
    [DllImport("dwmapi.dll")] internal static extern int DwmExtendFrameIntoClientArea(nint hwnd,ref Margins margins);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint hwnd,int attribute,ref int value,int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint hwnd,int attribute,out int value,int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmRegisterThumbnail(nint destination,nint source,out nint thumbnail);
    [DllImport("dwmapi.dll")] internal static extern int DwmQueryThumbnailSourceSize(nint thumbnail,out Point size);
    [DllImport("dwmapi.dll")] internal static extern int DwmUnregisterThumbnail(nint thumbnail);
    [DllImport("dwmapi.dll")] internal static extern int DwmUpdateThumbnailProperties(nint thumbnail,ref ThumbnailProperties properties);
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X,Y; public Point(int x,int y) { X=x;Y=y; } }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct ThumbnailProperties
    { public uint Flags; public Rect Destination,Source; public byte Opacity; [MarshalAs(UnmanagedType.Bool)] public bool Visible; [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly; }
}
[ComImport,Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktopManager
{
    [PreserveSig] int IsWindowOnCurrentVirtualDesktop(nint hwnd,[MarshalAs(UnmanagedType.Bool)] out bool current);
    [PreserveSig] int GetWindowDesktopId(nint hwnd,out Guid id);
    [PreserveSig] int MoveWindowToDesktop(nint hwnd,ref Guid id);
}
