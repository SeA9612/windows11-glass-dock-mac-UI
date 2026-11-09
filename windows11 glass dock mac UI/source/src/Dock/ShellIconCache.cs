using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GlassDock;
internal sealed record AppIconResult(ImageSource Image,bool Fallback,string Source);

// Only Shell application icons (ICONONLY); no window WM_GETICON, document
// thumbnail, favicon or screenshot. One STA worker and a cache per identity/DPI.
internal sealed class ShellIconCache : IDisposable
{
    readonly BlockingCollection<Action> work=new();
    readonly ConcurrentDictionary<string,Task<AppIconResult>> cache=new(StringComparer.OrdinalIgnoreCase);
    readonly Thread worker;
    bool disposed;
    internal ShellIconCache()
    {
        worker=new Thread(()=>{foreach(var action in work.GetConsumingEnumerable())action();}) {IsBackground=true,Name="GlassDock Shell icons"};
        worker.SetApartmentState(ApartmentState.STA);worker.Start();
    }
    internal Task<AppIconResult> GetAsync(string source,int pixels)
    {
        pixels=Math.Clamp(pixels,16,128);
        string key=source+"|"+pixels;
        return cache.GetOrAdd(key,_=>{
            var result=new TaskCompletionSource<AppIconResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            if(disposed){result.SetException(new ObjectDisposedException(nameof(ShellIconCache)));return result.Task;}
            try {work.Add(()=>{try {result.SetResult(Load(source,pixels));}catch(Exception e){result.SetException(e);}});}
            catch(InvalidOperationException){result.TrySetException(new ObjectDisposedException(nameof(ShellIconCache)));}
            return result.Task;
        });
    }
    internal int CachedCount=>cache.Count;
    static AppIconResult Load(string source,int pixels)
    {
        object? shell=null;nint bitmap=0;
        try
        {
            if(!AppIdentity.IsApplication(source)||System.IO.Path.GetFileName(source).Equals("ApplicationFrameHost.exe",StringComparison.OrdinalIgnoreCase))throw new NotSupportedException("Exact app identity unavailable; not substituting a document/host icon");
            Guid iid=typeof(IShellItemImageFactory).GUID;
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(source,0,ref iid,out shell));
            Marshal.ThrowExceptionForHR(((IShellItemImageFactory)shell).GetImage(new Size(pixels,pixels),0x4|0x1,out bitmap));
            if(bitmap==0)throw new InvalidOperationException("Shell returned no icon bitmap");
            var image=ReadBitmap(bitmap);
            StartupDiagnostics.Write($"App icon: Shell ICONONLY; {source}; request={pixels}px; image={image.PixelWidth}x{image.PixelHeight}");
            return new(image,false,source);
        }
        catch(Exception error)
        {
            StartupDiagnostics.Write($"App icon FAILED: {source}; request={pixels}px; {error.Message}; using neutral fallback");
            return new(DockArtwork.Glyph("app","#65718A"),true,source);
        }
        finally {if(bitmap!=0)Native.DeleteObject(bitmap);if(shell!=null&&Marshal.IsComObject(shell))Marshal.FinalReleaseComObject(shell);}
    }
    static BitmapSource ReadBitmap(nint handle)
    {
        if(GetObject(handle,Marshal.SizeOf<Bitmap>(),out var bitmap)==0)throw new InvalidOperationException("Icon bitmap metadata unavailable");
        int width=bitmap.Width,height=Math.Abs(bitmap.Height),stride=checked(width*4);
        var info=new BitmapInfo {Header=new BitmapHeader {Size=40,Width=width,Height=-height,Planes=1,BitCount=32,ImageSize=(uint)checked(stride*height)}};
        var bytes=new byte[checked(stride*height)];nint dc=GetDC(0);
        try {if(GetDIBits(dc,handle,0,(uint)height,bytes,ref info,0)==0)throw new InvalidOperationException("Icon pixels unavailable");}
        finally {ReleaseDC(0,dc);}
        // Old non-alpha Shell bitmaps use zero in every alpha byte. Preserve the
        // alpha of normal BGRA icons; normalize only that legacy opaque format.
        bool anyAlpha=false;for(int i=3;i<bytes.Length;i+=4)if(bytes[i]!=0){anyAlpha=true;break;}
        if(!anyAlpha)for(int i=3;i<bytes.Length;i+=4)bytes[i]=255;
        var image=BitmapSource.Create(width,height,96,96,PixelFormats.Pbgra32,null,bytes,stride);image.Freeze();return image;
    }
    public void Dispose(){if(disposed)return;disposed=true;work.CompleteAdding();}
    [ComImport,Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory {[PreserveSig] int GetImage(Size size,uint flags,out nint bitmap);}
    [StructLayout(LayoutKind.Sequential)] readonly struct Size {readonly int Width,Height;internal Size(int width,int height){Width=width;Height=height;}}
    [StructLayout(LayoutKind.Sequential)] struct Bitmap {internal int Type,Width,Height,WidthBytes;internal ushort Planes,BitsPixel;internal nint Bits;}
    [StructLayout(LayoutKind.Sequential)] struct BitmapHeader {internal uint Size;internal int Width,Height;internal ushort Planes,BitCount;internal uint Compression,ImageSize;internal int XPels,YPels;internal uint Used,Important;}
    [StructLayout(LayoutKind.Sequential)] struct BitmapInfo {internal BitmapHeader Header;internal uint Colors;}
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SHCreateItemFromParsingName(string name,nint context,ref Guid iid,[MarshalAs(UnmanagedType.Interface)] out object item);
    [DllImport("gdi32.dll",EntryPoint="GetObjectW")] static extern int GetObject(nint handle,int size,out Bitmap bitmap);
    [DllImport("gdi32.dll")] static extern int GetDIBits(nint dc,nint bitmap,uint start,uint lines,[Out] byte[] pixels,ref BitmapInfo info,uint usage);
    [DllImport("user32.dll")] static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(nint hwnd,nint dc);
}
