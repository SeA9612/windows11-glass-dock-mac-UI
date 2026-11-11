using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using GlassDock.Core;
namespace GlassDock;

// Transparent, click-through window that draws the captured window as a bent 3D
// mesh. All rectangles are physical screen pixels.
internal sealed class GenieOverlay : Window
{
    const int Rows=72;
    readonly Native.Rect source;
    readonly Func<Native.Rect> target;
    readonly string edge;
    readonly bool restoring,fade;
    readonly TimeSpan duration;
    readonly Native.Rect bounds;
    readonly Viewport3D viewport=new() {IsHitTestVisible=false};
    readonly OrthographicCamera camera=new() {LookDirection=new Vector3D(0,0,-1),UpDirection=new Vector3D(0,1,0)};
    readonly MeshGeometry3D mesh=new();
    readonly Stopwatch clock=new();
    bool ready,finished;
    int warmupFrames;
    internal event Action? Ready;
    internal event Action? Finished;

    internal GenieOverlay(BitmapSource image,Native.Rect source,Func<Native.Rect> target,string edge,bool restoring,TimeSpan duration,bool fade)
    {
        this.source=source;this.target=target;this.edge=edge;this.restoring=restoring;this.duration=duration;this.fade=fade;
        var first=target();
        bounds=Inflate(Union(source,first),96);
        var screen=System.Windows.Forms.SystemInformation.VirtualScreen;
        bounds=new Native.Rect {Left=Math.Max(bounds.Left,screen.Left),Top=Math.Max(bounds.Top,screen.Top),Right=Math.Min(bounds.Right,screen.Right),Bottom=Math.Min(bounds.Bottom,screen.Bottom)};
        Title="GlassDock · 최소화 효과";
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
        ShowInTaskbar=false;ShowActivated=false;Topmost=true;Focusable=false;IsHitTestVisible=false;
        WindowStartupLocation=WindowStartupLocation.Manual;
        Left=bounds.Left;Top=bounds.Top;Width=Math.Max(1,bounds.Right-bounds.Left);Height=Math.Max(1,bounds.Bottom-bounds.Top);
        var brush=new ImageBrush(image) {Stretch=Stretch.Fill};
        RenderOptions.SetBitmapScalingMode(brush,BitmapScalingMode.HighQuality);
        brush.Freeze();
        var material=new DiffuseMaterial(brush);
        var textures=new PointCollection(Rows*2);
        var indices=new Int32Collection((Rows-1)*6);
        for(int i=0;i<Rows;i++)
        {
            double v=i/(double)(Rows-1);
            var a=GenieMath.Texture(edge,v,0);var b=GenieMath.Texture(edge,v,1);
            textures.Add(new Point(a.U,a.V));textures.Add(new Point(b.U,b.V));
            if(i<Rows-1){int k=i*2;indices.Add(k);indices.Add(k+2);indices.Add(k+1);indices.Add(k+1);indices.Add(k+2);indices.Add(k+3);}
        }
        textures.Freeze();indices.Freeze();
        mesh.TextureCoordinates=textures;mesh.TriangleIndices=indices;
        var group=new Model3DGroup();
        group.Children.Add(new AmbientLight(Colors.White));
        group.Children.Add(new GeometryModel3D(mesh,material) {BackMaterial=material});
        viewport.Camera=camera;
        viewport.Children.Add(new ModelVisual3D {Content=group});
        Content=viewport;
        SourceInitialized+=(_,_)=>{
            var hwnd=new WindowInteropHelper(this).Handle;
            // Click-through, never activated, hidden from Alt+Tab.
            Native.SetWindowLong(hwnd,-20,Native.GetWindowLong(hwnd,-20)|0x20|0x80|0x08000000);
            PlaceInPixels();
        };
        Loaded+=(_,_)=>PlaceInPixels();
        Closed+=(_,_)=>CompositionTarget.Rendering-=OnRendering;
        Render(restoring?1:0);
        CompositionTarget.Rendering+=OnRendering;
    }

    void PlaceInPixels()
    {
        var hwnd=new WindowInteropHelper(this).Handle;
        if(hwnd!=0)Native.SetWindowPos(hwnd,0,bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top,0x0010|0x0004);
    }

    void OnRendering(object? sender,EventArgs e)
    {
        if(finished)return;
        if(!ready)
        {
            // Give DWM a couple of frames to put the overlay on screen before the
            // real window disappears underneath it.
            Render(restoring?1:0);
            if(IsVisible&&++warmupFrames>=2){ready=true;clock.Start();Ready?.Invoke();}
            return;
        }
        double progress=Math.Min(1,clock.Elapsed.TotalMilliseconds/Math.Max(1,duration.TotalMilliseconds));
        Render(restoring?1-progress:progress);
        if(progress>=1){finished=true;CompositionTarget.Rendering-=OnRendering;Finished?.Invoke();}
    }

    void Render(double t)
    {
        var hwnd=new WindowInteropHelper(this).Handle;
        Native.Rect window=bounds;
        if(hwnd!=0)Native.GetWindowRect(hwnd,out window);
        double pixelWidth=Math.Max(1,window.Right-window.Left),pixelHeight=Math.Max(1,window.Bottom-window.Top);
        double width=ActualWidth>0?ActualWidth:Width,height=ActualHeight>0?ActualHeight:Height;
        double sx=width/pixelWidth,sy=height/pixelHeight;
        camera.Width=width;camera.Position=new Point3D(width/2,-height/2,10);
        var rows=GenieMath.Rows(ToGenie(source),ToGenie(target()),edge,t,Rows);
        var points=new Point3DCollection(Rows*2);
        foreach(var row in rows)
        {
            points.Add(new Point3D((row.X0-window.Left)*sx,-(row.Y0-window.Top)*sy,0));
            points.Add(new Point3D((row.X1-window.Left)*sx,-(row.Y1-window.Top)*sy,0));
        }
        points.Freeze();
        mesh.Positions=points;
        viewport.Opacity=GenieMath.Opacity(t,fade);
    }

    static GenieRect ToGenie(Native.Rect r)=>new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);
    static Native.Rect Union(Native.Rect a,Native.Rect b)=>new() {Left=Math.Min(a.Left,b.Left),Top=Math.Min(a.Top,b.Top),Right=Math.Max(a.Right,b.Right),Bottom=Math.Max(a.Bottom,b.Bottom)};
    static Native.Rect Inflate(Native.Rect r,int by)=>new() {Left=r.Left-by,Top=r.Top-by,Right=r.Right+by,Bottom=r.Bottom+by};
}
