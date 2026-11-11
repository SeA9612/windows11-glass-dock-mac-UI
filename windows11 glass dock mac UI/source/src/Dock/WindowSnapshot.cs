using System;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace GlassDock;

// One still image of a window, taken right before it minimizes. PrintWindow is used
// instead of Windows Graphics Capture so no yellow capture border appears.
internal sealed record WindowSnapshot(BitmapSource Image,Native.Rect Bounds)
{
    const uint RenderFullContent=2;

    internal static WindowSnapshot? Capture(nint hwnd)
    {
        if(!Native.IsWindow(hwnd)||!Native.IsWindowVisible(hwnd)||Native.IsIconic(hwnd)||Native.IsHungAppWindow(hwnd))return null;
        if(!Native.GetWindowRect(hwnd,out var window))return null;
        // The visible frame excludes the invisible resize borders around the window.
        if(Native.DwmGetWindowRect(hwnd,9,out var frame,Marshal.SizeOf<Native.Rect>())!=0)frame=window;
        int width=window.Right-window.Left,height=window.Bottom-window.Top;
        if(width<8||height<8||width>16384||height>16384)return null;
        int left=Math.Clamp(frame.Left-window.Left,0,width-1),top=Math.Clamp(frame.Top-window.Top,0,height-1);
        int cropWidth=Math.Clamp(frame.Right-frame.Left,1,width-left),cropHeight=Math.Clamp(frame.Bottom-frame.Top,1,height-top);
        var pixels=new byte[cropWidth*cropHeight*4];
        nint screen=Native.GetDC(0),dc=0,bitmap=0,previous=0;
        try
        {
            dc=Native.CreateCompatibleDC(screen);
            var info=new Native.BitmapInfoHeader {Size=Marshal.SizeOf<Native.BitmapInfoHeader>(),Width=width,Height=-height,Planes=1,BitCount=32};
            bitmap=Native.CreateDIBSection(screen,ref info,0,out nint bits,0,0);
            if(dc==0||bitmap==0||bits==0)return null;
            previous=Native.SelectObject(dc,bitmap);
            if(!Native.PrintWindow(hwnd,dc,RenderFullContent))return null;
            for(int y=0;y<cropHeight;y++)
                Marshal.Copy(bits+((top+y)*width+left)*4,pixels,y*cropWidth*4,cropWidth*4);
        }
        catch(Exception error){StartupDiagnostics.Write("Genie snapshot failed",error);return null;}
        finally
        {
            if(previous!=0)Native.SelectObject(dc,previous);
            if(bitmap!=0)Native.DeleteObject(bitmap);
            if(dc!=0)Native.DeleteDC(dc);
            Native.ReleaseDC(0,screen);
        }
        if(LooksEmpty(pixels))return null;
        for(int i=3;i<pixels.Length;i+=4)pixels[i]=255;
        // Windows 11 draws window corners rounded unless the window is maximized.
        if(OperatingSystem.IsWindowsVersionAtLeast(10,0,22000)&&!Native.IsZoomed(hwnd))
        {
            uint dpi=Native.GetDpiForWindow(hwnd);
            RoundCorners(pixels,cropWidth,cropHeight,8*(dpi==0?96:dpi)/96d);
        }
        var image=BitmapSource.Create(cropWidth,cropHeight,96,96,PixelFormats.Pbgra32,null,pixels,cropWidth*4);
        image.Freeze();
        return new(image,new Native.Rect {Left=window.Left+left,Top=window.Top+top,Right=window.Left+left+cropWidth,Bottom=window.Top+top+cropHeight});
    }

    // PrintWindow returns an all-black image for windows it cannot draw.
    static bool LooksEmpty(byte[] pixels)
    {
        int count=pixels.Length/4;
        for(int i=0;i<64;i++)
        {
            int p=(int)((long)count*i/64)*4;
            if(pixels[p]!=0||pixels[p+1]!=0||pixels[p+2]!=0)return false;
        }
        return true;
    }

    static void RoundCorners(byte[] pixels,int width,int height,double radius)
    {
        int size=(int)Math.Ceiling(radius);
        if(size*2>width||size*2>height)return;
        for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                double dx=radius-(x+0.5),dy=radius-(y+0.5);
                double coverage=Math.Clamp(radius-Math.Sqrt(dx*dx+dy*dy)+0.5,0,1);
                if(dx<0||dy<0||coverage>=1)continue;
                Scale(x,y);Scale(width-1-x,y);Scale(x,height-1-y);Scale(width-1-x,height-1-y);
                void Scale(int px,int py)
                {
                    int i=(py*width+px)*4;
                    for(int c=0;c<4;c++)pixels[i+c]=(byte)(pixels[i+c]*coverage);
                }
            }
    }
}
