using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinRT;

namespace GlassDock;

// Continuous Windows Graphics Capture frames, never a saved screenshot. WPF owns
// the resulting live surface, so the image shares the card's rounded clip.
internal sealed class LiveWindowCapture : IDisposable
{
    readonly Image target;
    readonly Action<string> failed;
    readonly Action ready;
    readonly int renderLimit;
    Windows.Graphics.SizeInt32 poolSize;
    IDirect3DDevice? device;
    GraphicsCaptureItem? item;
    Direct3D11CaptureFramePool? pool;
    GraphicsCaptureSession? session;
    byte[] pixels=Array.Empty<byte>(),scaled=Array.Empty<byte>();
    Windows.Storage.Streams.Buffer? buffer;
    WriteableBitmap? bitmap;
    int busy,pending;
    volatile bool disposed;
    long lastFrame;
    internal int FrameCount {get;private set;}

    internal LiveWindowCapture(nint hwnd,Image target,Action ready,Action<string> failed)
    {
        this.target=target;this.ready=ready;this.failed=failed;
        var dpi=VisualTreeHelper.GetDpi(target);
        renderLimit=(int)Math.Clamp(Math.Max(target.ActualWidth*dpi.DpiScaleX,target.ActualHeight*dpi.DpiScaleY)*2,480,1280);
        nint nativeDevice=0,context=0,dxgi=0,inspectable=0;
        try
        {
            if(!GraphicsCaptureSession.IsSupported())throw new NotSupportedException("Windows 실시간 캡처 미지원");
            Guid itemId=new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
            var interop=GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
            interop.CreateForWindow(hwnd,ref itemId,out nint itemPointer);
            try {item=MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer);}
            finally {Marshal.Release(itemPointer);}
            Marshal.ThrowExceptionForHR(D3D11CreateDevice(0,1,0,0x20,0,0,7,out nativeDevice,out _,out context));
            Guid dxgiId=new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(nativeDevice,ref dxgiId,out dxgi));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi,out inspectable));
            device=MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
            poolSize=item.Size;
            pool=Direct3D11CaptureFramePool.CreateFreeThreaded(device,DirectXPixelFormat.B8G8R8A8UIntNormalized,2,poolSize);
            session=pool.CreateCaptureSession(item);
            session.IsCursorCaptureEnabled=false;
            CapturePermission.Apply(session);
            CapturePermission.Changed+=ApplyBorderPermission;
            pool.FrameArrived+=FrameArrived;
            item.Closed+=SourceClosed;
            session.StartCapture();
        }
        catch {Dispose();throw;}
        finally
        {
            if(inspectable!=0)Marshal.Release(inspectable);if(dxgi!=0)Marshal.Release(dxgi);
            if(context!=0)Marshal.Release(context);if(nativeDevice!=0)Marshal.Release(nativeDevice);
        }
    }
    void SourceClosed(GraphicsCaptureItem sender,object args)=>ReportFailure("종료된 창입니다.");
    void ApplyBorderPermission(){if(!disposed&&session!=null)CapturePermission.Apply(session);}
    void ReportFailure(string message)
    {
        if(disposed||target.Dispatcher.HasShutdownStarted)return;
        target.Dispatcher.BeginInvoke(()=>{if(!disposed){failed(message);Dispose();}});
    }
    async void FrameArrived(Direct3D11CaptureFramePool sender,object args)
    {
        if(disposed)return;
        if(Interlocked.CompareExchange(ref busy,1,0)!=0){Interlocked.Exchange(ref pending,1);return;}
        try
        {
            double remaining=1000d/30-(Stopwatch.GetTimestamp()-lastFrame)*1000d/Stopwatch.Frequency;
            if(remaining>0)await Task.Delay((int)Math.Ceiling(remaining));
            if(disposed)return;
            using var frame=TakeLatestFrame(sender);
            if(frame==null||disposed)return;
            lastFrame=Stopwatch.GetTimestamp();
            using var software=await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface,BitmapAlphaMode.Premultiplied);
            if(disposed)return;
            int width=Math.Min(software.PixelWidth,frame.ContentSize.Width),height=Math.Min(software.PixelHeight,frame.ContentSize.Height);
            if(width<1||height<1)return;
            uint length=checked((uint)(software.PixelWidth*software.PixelHeight*4));
            if(buffer==null||pixels.Length!=length){buffer=new Windows.Storage.Streams.Buffer(length);pixels=new byte[length];}
            software.CopyToBuffer(buffer);
            using(var reader=DataReader.FromBuffer(buffer))reader.ReadBytes(pixels);
            // Bound the WPF surface to a thumbnail-sized buffer; only one frame is
            // in flight and the existing arrays/bitmap are reused on later frames.
            double factor=Math.Min(1,renderLimit/(double)Math.Max(width,height));
            int outputWidth=Math.Max(1,(int)Math.Round(width*factor)),outputHeight=Math.Max(1,(int)Math.Round(height*factor));
            int bytes=outputWidth*outputHeight*4;
            if(scaled.Length!=bytes)scaled=new byte[bytes];
            for(int y=0;y<outputHeight;y++)
                for(int x=0;x<outputWidth;x++)
                {
                    int source=((y*height/outputHeight)*software.PixelWidth+x*width/outputWidth)*4;
                    int destination=(y*outputWidth+x)*4;
                    scaled[destination]=pixels[source];scaled[destination+1]=pixels[source+1];scaled[destination+2]=pixels[source+2];scaled[destination+3]=255;
                }
            await target.Dispatcher.InvokeAsync(()=>{
                if(disposed)return;
                if(bitmap==null||bitmap.PixelWidth!=outputWidth||bitmap.PixelHeight!=outputHeight)
                {bitmap=new WriteableBitmap(outputWidth,outputHeight,96,96,PixelFormats.Bgra32,null);target.Source=bitmap;}
                bitmap.WritePixels(new Int32Rect(0,0,outputWidth,outputHeight),scaled,outputWidth*4,0);
                if(FrameCount++==0)ready();
            });
            if(!disposed&&frame.ContentSize.Width>0&&frame.ContentSize.Height>0&&(frame.ContentSize.Width!=poolSize.Width||frame.ContentSize.Height!=poolSize.Height))
            {
                poolSize=frame.ContentSize;frame.Dispose();
                sender.Recreate(device!,DirectXPixelFormat.B8G8R8A8UIntNormalized,2,poolSize);
            }
        }
        catch(Exception error){if(!disposed){ReportFailure("실시간 캡처 불가 · 클릭해서 창 선택");StartupDiagnostics.Write("Preview live capture failed",error);}}
        finally
        {
            Interlocked.Exchange(ref busy,0);
            // A static app may publish only one changed frame. Do not lose that
            // final update just because it arrived while the prior frame was busy.
            if(Interlocked.Exchange(ref pending,0)!=0&&!disposed)_=Task.Run(()=>FrameArrived(sender,EventArgs.Empty));
        }
    }
    static Direct3D11CaptureFrame? TakeLatestFrame(Direct3D11CaptureFramePool pool)
    {
        var frame=pool.TryGetNextFrame();
        while(pool.TryGetNextFrame() is { } next){frame?.Dispose();frame=next;}
        return frame;
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        CapturePermission.Changed-=ApplyBorderPermission;
        if(item!=null)item.Closed-=SourceClosed;
        if(pool!=null)pool.FrameArrived-=FrameArrived;
        session?.Dispose();pool?.Dispose();device?.Dispose();
        session=null;pool=null;device=null;item=null;
    }
    [ComImport,Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IGraphicsCaptureItemInterop
    {
        void CreateForWindow(nint hwnd,ref Guid iid,out nint result);
        void CreateForMonitor(nint monitor,ref Guid iid,out nint result);
    }
    [DllImport("d3d11.dll")] static extern int D3D11CreateDevice(nint adapter,int driver,nint software,uint flags,nint featureLevels,uint count,uint version,out nint device,out int featureLevel,out nint context);
    [DllImport("d3d11.dll")] static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice,out nint graphicsDevice);
}
