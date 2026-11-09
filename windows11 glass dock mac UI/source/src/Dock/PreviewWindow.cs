using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
using GlassDock.Core;
namespace GlassDock;
// Live frames are continuously supplied by Windows Graphics Capture. WPF clips
// the image and title together; native window regions expose the backdrop.
public sealed class PreviewWindow : Window
{
    const double CardCornerRadius=16;
    sealed record Card(AppWindow Window,Button Button,Border Preview,TextBlock Status,Button Restore,Image LiveImage,TextBlock Placeholder,Border? GlassBody);
    readonly List<Card> cards=new();
    readonly Dictionary<nint,LiveWindowCapture> captures=new();
    readonly HashSet<nint> failedCaptures=new();
    readonly Dictionary<Button,HostBackdrop> glassHosts=new();
    readonly HashSet<Button> failedGlassHosts=new();
    readonly Canvas canvas=new() {Background=Brushes.Transparent};
    readonly List<AppWindow> windows;
    readonly Action<AppWindow> select;
    readonly Forms.Screen screen;
    readonly bool overview;
    readonly Rect? hoverAnchor;
    readonly string dockEdge;
    readonly DispatcherTimer layoutTimer=new() {Interval=TimeSpan.FromMilliseconds(50)};
    readonly DispatcherTimer refreshTimer=new() {Interval=TimeSpan.FromMilliseconds(750)};
    readonly DispatcherTimer entranceTimer=new(DispatcherPriority.Render) {Interval=TimeSpan.FromMilliseconds(16)};
    readonly Stopwatch entranceClock=new();
    Grid surface=null!;
    Native.Rect entranceBounds;
    string? appliedCardRegion;
    AppWindow? chosen;
    bool ready,closing;
    public PreviewWindow(List<AppWindow> windows,Action<AppWindow> select,Forms.Screen screen,bool overview,Rect? hoverAnchor=null,string dockEdge="Bottom")
    {
        this.windows=windows;this.select=select;this.screen=screen;this.overview=overview;
        this.hoverAnchor=hoverAnchor;this.dockEdge=dockEdge;
        Title=overview?"GlassDock · 전체 창 보기":"GlassDock · 앱 창 선택";
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;
        ShowActivated=!hoverAnchor.HasValue;
        if(hoverAnchor.HasValue)GlassBackdrop.Prepare(this);
        else Background=new LinearGradientBrush(Color.FromRgb(234,240,246),Color.FromRgb(219,211,233),90);
        UseLayoutRounding=true;SnapsToDevicePixels=true;
        var root=new Grid {Margin=hoverAnchor.HasValue?new Thickness(4):new Thickness(42,26,42,24)};
        surface=root;
        root.RowDefinitions.Add(new RowDefinition {Height=new GridLength(hoverAnchor.HasValue?52:64)});
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition {Height=new GridLength(30)});
        var header=new Grid();header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(86)});
        var heading=new StackPanel();heading.Children.Add(new TextBlock {Text=overview?"현재 작업공간":"앱의 열린 창",FontSize=hoverAnchor.HasValue?15:23,FontWeight=FontWeights.SemiBold});
        heading.Children.Add(new TextBlock {Text=hoverAnchor.HasValue?$"{windows.Count}개 창 · 클릭해서 전환":$"{windows.Count}개의 창 · 미리보기를 누르면 해당 창으로 전환합니다.",Foreground=new SolidColorBrush(Color.FromRgb(113,125,147)),Margin=new Thickness(0,7,0,0)});header.Children.Add(heading);
        var close=DockWindow.Button("닫기  Esc",Close);close.VerticalAlignment=VerticalAlignment.Top;Grid.SetColumn(close,1);header.Children.Add(close);root.Children.Add(header);
        Grid.SetRow(canvas,1);root.Children.Add(canvas);
        var footer=new TextBlock {Text=hoverAnchor.HasValue?"마우스를 밖으로 옮기면 닫힙니다.":"창 선택: 클릭 또는 Tab · Enter    /    취소: 빈 곳 클릭 또는 Esc",Foreground=new SolidColorBrush(Color.FromRgb(113,125,147)),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom};Grid.SetRow(footer,2);root.Children.Add(footer);Content=root;
        if(hoverAnchor.HasValue)
        {
            header.Visibility=Visibility.Collapsed;footer.Visibility=Visibility.Collapsed;
            root.RowDefinitions[0].Height=new GridLength(0);root.RowDefinitions[2].Height=new GridLength(0);
        }
        root.MouseLeftButtonDown+=(_,e)=>{if(e.OriginalSource==canvas||e.OriginalSource==root)Close();};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;Close();}};
        foreach(var w in windows)AddCard(w);
        if(windows.Count==0)
        {
            var empty=new StackPanel {VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
            empty.Children.Add(new TextBlock {Text="현재 작업공간에 열린 창이 없습니다.",FontSize=20});
            empty.Children.Add(new TextBlock {Text="독에서 앱을 열거나 Windows 작업 보기에서 다른 공간을 선택하세요.",Margin=new Thickness(0,12,0,0)});
            Grid.SetRow(empty,1);root.Children.Add(empty);
        }
        layoutTimer.Tick+=(_,_)=>{layoutTimer.Stop();UpdateLivePreviews();};
        canvas.SizeChanged+=(_,_)=>{ArrangeCards();ScheduleLayout();};
        SizeChanged+=(_,_)=>ScheduleLayout();
        DpiChanged+=(_,_)=>{appliedCardRegion=null;ScheduleLayout();};
        refreshTimer.Tick+=(_,_)=>{
            if(!hoverAnchor.HasValue&&!IsActive){Close();return;}
            foreach(var card in cards)
            {
                if(!Native.IsWindow(card.Window.Handle))RemoveClosedCard(card);
            }
            UpdateLivePreviews();
        };
        SourceInitialized+=(_,_)=>PlaceSurface();
        entranceTimer.Tick+=(_,_)=>AdvanceEntrance();
        Loaded+=(_,_)=>{PlaceSurface();ready=true;ArrangeCards();UpdateLivePreviews();if(hoverAnchor.HasValue)StartEntrance();if(!hoverAnchor.HasValue){Activate();Dispatcher.BeginInvoke(()=>cards.FirstOrDefault()?.Button.Focus(),DispatcherPriority.Input);}refreshTimer.Start();};
        LocationChanged+=(_,_)=>{SyncGlass();ScheduleLayout();};
        IsVisibleChanged+=(_,_)=>SyncGlass();
        Closing+=(_,_)=>{closing=true;layoutTimer.Stop();refreshTimer.Stop();entranceTimer.Stop();foreach(var host in glassHosts.Values.Reverse())host.Dispose();glassHosts.Clear();ReleaseAll();};
        Closed+=(_,_)=>{if(chosen is AppWindow target)Dispatcher.BeginInvoke(()=>select(target),DispatcherPriority.ApplicationIdle);};
    }
    void StartEntrance()
    {
        Native.GetWindowRect(new WindowInteropHelper(this).Handle,out entranceBounds);
        entranceClock.Restart();AdvanceEntrance();entranceTimer.Start();
    }
    void AdvanceEntrance()
    {
        double progress=Math.Clamp(entranceClock.Elapsed.TotalMilliseconds/300,0,1);
        double eased=Math.Sin(progress*Math.PI/2);
        int distance=(int)Math.Round(18*VisualTreeHelper.GetDpi(this).DpiScaleX*(1-eased));
        int dx=dockEdge=="Left"?-distance:dockEdge=="Right"?distance:0;
        int dy=dockEdge=="Top"?-distance:dockEdge=="Bottom"?distance:0;
        Native.SetWindowPos(new WindowInteropHelper(this).Handle,0,entranceBounds.Left+dx,entranceBounds.Top+dy,0,0,0x0015);
        // Move the complete preview HWND, keeping its rounded hit region in sync.
        surface.Opacity=eased;
        foreach(var host in glassHosts.Values)host.SetOpacity((float)eased);
        if(progress>=1){entranceTimer.Stop();surface.Opacity=1;UpdateLivePreviews();}
    }
    void PlaceSurface()
    {
        var r=screen.WorkingArea;var hwnd=new WindowInteropHelper(this).Handle;
        if(hoverAnchor is Rect anchor)
        {
            var dpi=VisualTreeHelper.GetDpi(this);double scale=dpi.DpiScaleX;
            int columns=Math.Max(1,Math.Min(Math.Min(windows.Count,4),(int)((r.Width/scale-8)/224)));
            int rows=(int)Math.Ceiling(windows.Count/(double)columns);
            int width=Math.Min(r.Width-24,(int)Math.Round((columns*216+(columns-1)*8+8)*scale));
            int height=Math.Min(r.Height-24,(int)Math.Round((rows*136+(rows-1)*8+8)*scale));
            int gap=(int)Math.Round(8*scale);
            double x=anchor.X+(anchor.Width-width)/2,y=anchor.Y-height-gap;
            if(dockEdge=="Top")y=anchor.Bottom+gap;
            else if(dockEdge=="Left"){x=anchor.Right+gap;y=anchor.Y+(anchor.Height-height)/2;}
            else if(dockEdge=="Right"){x=anchor.Left-width-gap;y=anchor.Y+(anchor.Height-height)/2;}
            Native.SetWindowPos(hwnd,0,(int)Math.Clamp(x,r.Left+8,r.Right-width-8),(int)Math.Clamp(y,r.Top+8,r.Bottom-height-8),width,height,0x14);
            return;
        }
        if(overview)Native.SetWindowPos(hwnd,0,r.Left,r.Top,r.Width,r.Height,0x14);
        else {int w=Math.Min(1040,r.Width-32),height=Math.Min(700,r.Height-32);Native.SetWindowPos(hwnd,0,r.Left+(r.Width-w)/2,r.Top+(r.Height-height)/2,w,height,0x14);}
    }
    void AddCard(AppWindow w)
    {
        var content=new Grid();content.RowDefinitions.Add(new RowDefinition());content.RowDefinitions.Add(new RowDefinition {Height=new GridLength(hoverAnchor.HasValue?24:44)});
        var preview=new Border {Background=hoverAnchor.HasValue?Brushes.Transparent:new SolidColorBrush(Color.FromRgb(214,222,234)),CornerRadius=new CornerRadius(10),Margin=new Thickness(0,0,0,2)};
        var liveImage=new Image {Stretch=Stretch.Uniform,IsHitTestVisible=false};
        RenderOptions.SetBitmapScalingMode(liveImage,BitmapScalingMode.HighQuality);
        var placeholder=new TextBlock {Text=w.Minimized?"최소화된 창 · 클릭해서 복원":"실시간 연결 중…",TextWrapping=TextWrapping.Wrap,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12),FontSize=11};
        var liveSurface=new Grid();liveSurface.Children.Add(liveImage);liveSurface.Children.Add(placeholder);preview.Child=liveSurface;
        var status=new TextBlock {Text=w.Minimized?"최소화됨 · 마지막 화면":"실시간 연결 준비 중…",Foreground=new SolidColorBrush(Color.FromRgb(112,123,145)),FontSize=10,TextTrimming=TextTrimming.CharacterEllipsis};
        content.Children.Add(preview);
        var title=new TextBlock {Text=w.Title,TextTrimming=TextTrimming.CharacterEllipsis,FontSize=13};
        var caption=new Grid {Margin=hoverAnchor.HasValue?new Thickness(8,2,8,0):new Thickness(8,3,8,0)};
        caption.RowDefinitions.Add(new RowDefinition());caption.RowDefinitions.Add(new RowDefinition());
        caption.ColumnDefinitions.Add(new ColumnDefinition());caption.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        caption.Children.Add(title);Grid.SetColumnSpan(title,2);Grid.SetRow(status,1);caption.Children.Add(status);
        var restore=DockWindow.Button("실시간 보기",()=>{
            if(Native.IsWindow(w.Handle)){failedCaptures.Remove(w.Handle);Native.ShowWindowAsync(w.Handle,4);ScheduleLayout();}
        });
        restore.FontSize=10;restore.Padding=new Thickness(4,0,4,0);restore.Margin=new Thickness(4,0,0,0);restore.Visibility=w.Minimized?Visibility.Visible:Visibility.Collapsed;
        // Mark the nested button click handled so it restores without selecting/closing the card.
        restore.AddHandler(Button.ClickEvent,new RoutedEventHandler((_,e)=>e.Handled=true));
        restore.ToolTip="최소화된 원본 창을 복원해 실시간 화면을 봅니다.";
        Grid.SetRow(restore,1);Grid.SetColumn(restore,1);caption.Children.Add(restore);
        if(hoverAnchor.HasValue)
        {
            status.Visibility=Visibility.Collapsed;
            caption.RowDefinitions[1].Height=new GridLength(0);
            Grid.SetColumnSpan(title,1);Grid.SetRow(restore,0);
            restore.Content="↗";
        }
        Grid.SetRow(caption,1);content.Children.Add(caption);
        var button=new Button {Content=content,Padding=hoverAnchor.HasValue?new Thickness(0):new Thickness(9),Background=hoverAnchor.HasValue?Brushes.Transparent:new SolidColorBrush(Color.FromArgb(190,255,255,255)),BorderThickness=hoverAnchor.HasValue?new Thickness(0):new Thickness(1),HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch,ToolTip=w.Title+" · "+Path.GetFileNameWithoutExtension(w.Path)};
        Border? glassBody=null;
        if(hoverAnchor.HasValue)
        {
            var layers=GlassTheme.CreateLayers(out glassBody,CardCornerRadius);
            button.Content=null;
            var composition=new Grid {Background=Brushes.Transparent};composition.Children.Add(layers);composition.Children.Add(content);button.Content=composition;
            var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(HorizontalAlignmentProperty,HorizontalAlignment.Stretch);presenter.SetValue(VerticalAlignmentProperty,VerticalAlignment.Stretch);
            button.Template=new ControlTemplate(typeof(Button)){VisualTree=presenter};
        }
        button.Click+=(_,_)=>{if(!Native.IsWindow(w.Handle)){RemoveClosedCard(cards.First(c=>c.Window==w));return;}chosen=w;Close();};
        cards.Add(new(w,button,preview,status,restore,liveImage,placeholder,glassBody));canvas.Children.Add(button);
    }
    void RemoveClosedCard(Card card)
    {
        if(captures.Remove(card.Window.Handle,out var capture))capture.Dispose();
        card.Button.IsEnabled=false;card.Status.Text="종료된 창입니다.";
        card.Restore.Visibility=Visibility.Collapsed;
        card.LiveImage.Source=null;card.Placeholder.Text="종료된 창입니다.";card.Placeholder.Visibility=Visibility.Visible;
    }
    void ArrangeCards()
    {
        if(hoverAnchor.HasValue)
        {
            int columns=Math.Max(1,Math.Min(Math.Min(cards.Count,4),(int)((canvas.ActualWidth+8)/224)));
            double width=(canvas.ActualWidth-(columns-1)*8)/columns;
            int rows=(cards.Count+columns-1)/columns;
            double height=rows>0?(canvas.ActualHeight-(rows-1)*8)/rows:0;
            for(int i=0;i<cards.Count;i++)
            {
                var button=cards[i].Button;
                Canvas.SetLeft(button,(i%columns)*(width+8));Canvas.SetTop(button,(i/columns)*(height+8));
                button.Width=Math.Max(0,width);button.Height=Math.Max(0,height);
            }
            return;
        }
        var layout=OverviewLayout.Create(cards.Count,canvas.ActualWidth,canvas.ActualHeight);
        for(int i=0;i<cards.Count&&i<layout.Count;i++)
        {
            var rect=layout[i];var button=cards[i].Button;
            Canvas.SetLeft(button,rect.X);Canvas.SetTop(button,rect.Y);button.Width=rect.Width;button.Height=rect.Height;
        }
    }
    void ScheduleLayout(){if(!ready||closing)return;layoutTimer.Stop();layoutTimer.Start();}
    void SyncGlass()
    {
        if(!ready||closing||!hoverAnchor.HasValue)return;
        foreach(var card in cards)
        {
            if(card.Button.ActualWidth<1||card.Button.ActualHeight<1)continue;
            if(!glassHosts.TryGetValue(card.Button,out var host)&&!failedGlassHosts.Contains(card.Button))
            {
                try
                {
                    if(!GlassBackdrop.IsSupported)throw new NotSupportedException("Native glass unsupported");
                    host=new HostBackdrop(CardCornerRadius);glassHosts.Add(card.Button,host);
                    card.GlassBody!.Background=GlassTheme.CreateAcrylicTint();
                }
                catch(Exception error){failedGlassHosts.Add(card.Button);card.GlassBody!.Background=GlassTheme.CreateFallbackTint();StartupDiagnostics.Write("Preview native glass unavailable; using shared fallback",error);}
            }
            if(host!=null)
            {
                var p=card.Button.TranslatePoint(new Point(),this);
                host.Sync(this,new Rect(p.X,p.Y,card.Button.ActualWidth,card.Button.ActualHeight));
            }
        }
    }
    void UpdateCardRegion()
    {
        var hwnd=new WindowInteropHelper(this).Handle;
        var dpi=VisualTreeHelper.GetDpi(this);
        var rectangles=new List<Native.Rect>();
        foreach(var card in cards)
        {
            if(card.Button.ActualWidth<1||card.Button.ActualHeight<1)continue;
            var p=card.Button.TranslatePoint(new Point(),this);
            rectangles.Add(new Native.Rect {Left=(int)Math.Round(p.X*dpi.DpiScaleX),Top=(int)Math.Round(p.Y*dpi.DpiScaleY),Right=(int)Math.Round((p.X+card.Button.ActualWidth)*dpi.DpiScaleX),Bottom=(int)Math.Round((p.Y+card.Button.ActualHeight)*dpi.DpiScaleY)});
            // Image and caption now share this WPF clip; DWM thumbnails bypass it.
            card.Button.Clip=new RectangleGeometry(new Rect(0,0,card.Button.ActualWidth,card.Button.ActualHeight),CardCornerRadius,CardCornerRadius);
        }
        if(rectangles.Count==0)return;
        string key=$"{ActualWidth},{ActualHeight},{dpi.DpiScaleX},{dpi.DpiScaleY}:"+string.Join(";",rectangles.Select(r=>$"{r.Left},{r.Top},{r.Right},{r.Bottom}"));
        if(key==appliedCardRegion)return;
        nint region=Native.CreateRectRgn(0,0,hoverAnchor.HasValue?0:(int)Math.Ceiling(ActualWidth*dpi.DpiScaleX),hoverAnchor.HasValue?0:(int)Math.Ceiling(ActualHeight*dpi.DpiScaleY));
        if(region==0)throw new InvalidOperationException("미리보기 카드 영역을 만들 수 없습니다.");
        try
        {
            foreach(var r in rectangles)
            {
                int diameterX=(int)Math.Round(CardCornerRadius*2*dpi.DpiScaleX),diameterY=(int)Math.Round(CardCornerRadius*2*dpi.DpiScaleY);
                nint rounded=Native.CreateRoundRectRgn(r.Left,r.Top,r.Right+1,r.Bottom+1,diameterX,diameterY);
                nint rectangle=Native.CreateRectRgn(r.Left,r.Top,r.Right+1,r.Bottom+1);
                try
                {
                    if(rounded==0||rectangle==0||(!hoverAnchor.HasValue&&Native.CombineRgn(region,region,rectangle,4)==0)||Native.CombineRgn(region,region,rounded,2)==0)
                        throw new InvalidOperationException("미리보기 카드 영역을 결합할 수 없습니다.");
                }
                finally {if(rounded!=0)Native.DeleteObject(rounded);if(rectangle!=0)Native.DeleteObject(rectangle);}
            }
            // Windows owns the region after success. Only the preview destination HWND is changed.
            if(Native.SetWindowRgn(hwnd,region,true)==0)throw new InvalidOperationException("미리보기 카드 영역을 적용할 수 없습니다.");
            region=0;appliedCardRegion=key;
        }
        finally {if(region!=0)Native.DeleteObject(region);}
    }
    void UpdateLivePreviews()
    {
        if(!ready||closing)return;
        UpdateCardRegion();
        SyncGlass();
        foreach(var card in cards)
        {
            if(!card.Button.IsEnabled)continue;
            if(!Native.IsWindow(card.Window.Handle)){RemoveClosedCard(card);continue;}
            bool minimized=Native.IsIconic(card.Window.Handle);
            card.Restore.Visibility=minimized?Visibility.Visible:Visibility.Collapsed;
            if(!minimized&&!captures.ContainsKey(card.Window.Handle)&&!failedCaptures.Contains(card.Window.Handle))
            {
                void Failed(string message)
                {
                    failedCaptures.Add(card.Window.Handle);
                    if(captures.Remove(card.Window.Handle,out var stopped))stopped.Dispose();
                    card.LiveImage.Source=null;card.Placeholder.Text=message;card.Placeholder.Visibility=Visibility.Visible;card.Status.Text=message;
                }
                try
                {
                    var capture=new LiveWindowCapture(card.Window.Handle,card.LiveImage,()=>card.Placeholder.Visibility=Visibility.Collapsed,Failed);
                    captures.Add(card.Window.Handle,capture);
                }
                catch(Exception error){Failed("실시간 캡처 불가 · 클릭해서 창 선택");StartupDiagnostics.Write("Preview capture initialization failed",error);}
            }
            if(!failedCaptures.Contains(card.Window.Handle))
            {
                card.Status.Text=minimized?"최소화됨 · 마지막 화면":"실시간 미리보기";
                if(minimized&&card.LiveImage.Source==null){card.Placeholder.Text="최소화된 창 · 클릭해서 복원";card.Placeholder.Visibility=Visibility.Visible;}
            }
            card.Button.ToolTip=card.Window.Title+"\n"+card.Status.Text;
        }
    }
    void ReleaseAll(){foreach(var capture in captures.Values)capture.Dispose();captures.Clear();failedCaptures.Clear();}
}
