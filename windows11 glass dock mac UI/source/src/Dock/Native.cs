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
    [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] internal static extern int DwmGetWindowRect(nint hwnd,int attribute,out Rect value,int size);
    // Window snapshot (genie effect).
    [DllImport("user32.dll")] internal static extern bool PrintWindow(nint hwnd,nint dc,uint flags);
    [DllImport("user32.dll")] internal static extern bool IsHungAppWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(nint hwnd,nint dc);
    [DllImport("gdi32.dll")] internal static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] internal static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] internal static extern nint SelectObject(nint dc,nint obj);
    [DllImport("gdi32.dll")] internal static extern nint CreateDIBSection(nint dc,ref BitmapInfoHeader info,uint usage,out nint bits,nint section,uint offset);
    [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfoHeader
    { public int Size,Width,Height; public short Planes,BitCount; public int Compression,SizeImage,XPelsPerMeter,YPelsPerMeter,ClrUsed,ClrImportant; }
    // Hiding the OS minimize animation.
    [DllImport("user32.dll")] internal static extern bool SetLayeredWindowAttributes(nint hwnd,uint colorKey,byte alpha,uint flags);
    [DllImport("user32.dll")] internal static extern bool RedrawWindow(nint hwnd,nint rect,nint region,uint flags);
    // Minimize button hook.
    internal delegate nint HookProc(int code,nint w,nint l);
    [DllImport("user32.dll")] internal static extern nint SetWindowsHookEx(int id,HookProc proc,nint module,uint thread);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook,int code,nint w,nint l);
    [DllImport("user32.dll")] internal static extern int GetMessage(out Msg msg,nint hwnd,uint min,uint max);
    [DllImport("user32.dll")] internal static extern bool PostThreadMessage(uint thread,uint msg,nint w,nint l);
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] internal static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint hwnd,uint flags);
    [DllImport("user32.dll")] internal static extern bool PhysicalToLogicalPointForPerMonitorDPI(nint hwnd,ref Point point);
    [DllImport("user32.dll")] internal static extern nint SendMessageTimeout(nint hwnd,uint msg,nint w,nint l,uint flags,uint timeout,out nint result);
    [StructLayout(LayoutKind.Sequential)] internal struct Msg { public nint Hwnd; public uint Message; public nint W,L; public uint Time; public Point Point; }
    [StructLayout(LayoutKind.Sequential)] internal struct MouseHookData { public Point Point; public uint MouseData,Flags,Time; public nint Extra; }
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
