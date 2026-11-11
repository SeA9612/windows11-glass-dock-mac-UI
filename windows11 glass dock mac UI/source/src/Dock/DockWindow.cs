using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
using GlassDock.Core;
namespace GlassDock;
public sealed class DockWindow : Window
{
    readonly DockSettings settings;
    readonly bool persistSettings;
    readonly WindowCatalog catalog=new();
    readonly ClickPolicy policy=new();
    readonly StackPanel root=new(), icons=new(), dockBody=new();
    readonly Border glass=new();
    Border glassBody=null!;
    readonly Border handle=new();
    readonly Grid iconViewport=new() {ClipToBounds=true};
    readonly Canvas insertionLayer=new() {IsHitTestVisible=false};
    readonly Border insertionMark=new() {Background=new SolidColorBrush(Color.FromRgb(121,210,246)),CornerRadius=new CornerRadius(1),Visibility=Visibility.Collapsed};
    readonly StackPanel recycleSection=new();
    readonly Border recycleDivider=new() {Background=new SolidColorBrush(Color.FromArgb(65,107,119,151))};
    readonly Border toolsDivider=new() {Background=new SolidColorBrush(Color.FromArgb(65,107,119,151)),AllowDrop=true,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    readonly Button recycleButton=new();
    readonly AppIconView recycleView=DockArtwork.AppTile("",0,null);
    readonly IRecycleBin recycleBin;
    readonly DispatcherTimer recycleTimer=new() {Interval=TimeSpan.FromSeconds(2)};
    RecycleBinState recycleState=new(null,null);
    MenuItem emptyRecycleItem=null!;
    bool recycleBusy,emptying,isClosed;
    string? recycleIconKey;
    List<DockEntry> displayedEntries=new();
    readonly TextBlock compactMark=new() {Text="⠿",FontSize=22,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Opacity=0,IsHitTestVisible=false};
    bool movingDock, dockPointerDown;
    Point dockPress, dockGrab;
    double expandedWidth, expandedHeight;
    readonly ShellIconCache appIcons=new();
    readonly Dictionary<string,string> iconRequests=new(StringComparer.OrdinalIgnoreCase);
    string? inputRegionKey;
    Button? overviewButton,taskViewButton;
    readonly Dictionary<string,Button> buttons=new(StringComparer.OrdinalIgnoreCase);
    readonly DispatcherTimer edgeTimer=new() { Interval=TimeSpan.FromMilliseconds(150) };
    readonly Forms.NotifyIcon tray;
    PreviewWindow? activePreview;
    readonly DispatcherTimer hoverTimer=new() {Interval=TimeSpan.FromMilliseconds(300)};
    readonly DispatcherTimer previewLeaveTimer=new() {Interval=TimeSpan.FromMilliseconds(250)};
    string? pendingHoverPath,activeHoverPath;
    Button? pendingHoverButton;
    readonly DispatcherTimer activationTimer=new() {Interval=TimeSpan.FromMilliseconds(40)};
    AppWindow? pendingActivation;
    int activationAttempts;
    bool hidden, dragging, refreshing;
    nint foreground;
    string? lastApp;
    DateTime lastClick;
    DateTime ignoreAutomaticForegroundUntil;
    Guid? previousDesktop;
    readonly GenieAnimator genie;
    readonly MinimizeButtonHook minimizeHook;
    // Mode B: minimized windows shown as thumbnails between the apps and the trash.
    sealed class MinimizedSlot
    {
        internal required Button Button {get;init;}
        internal required Grid Cell {get;init;}
        internal required Image Thumb {get;init;}
        internal required Image Badge {get;init;}
        internal bool Removing {get;set;}
        internal bool? Vertical {get;set;}
    }
    readonly StackPanel minimizedPanel=new();
    readonly Dictionary<nint,MinimizedSlot> slots=new();
    readonly HashSet<nint> landingSlots=new();
    public DockWindow():this(SettingsStore.Load(),true){}
    internal DockWindow(DockSettings settings,bool persistSettings,IRecycleBin? recycleBin=null)
    {
        this.settings=settings;this.persistSettings=persistSettings;
        this.recycleBin=recycleBin??new WindowsRecycleBin();
        StartupDiagnostics.Write("DockWindow constructor entered");
        catalog.PinnedPaths=settings.Pins;
        Title="GlassDock 0.3.0 · 지니 최소화 효과"; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
        bool materialReview=Environment.GetEnvironmentVariable("GLASSDOCK_MATERIAL_REVIEW")=="1";
        Background=Brushes.Transparent; Topmost=true; ShowInTaskbar=materialReview; ShowActivated=materialReview;
        GlassBackdrop.Prepare(this);
        SizeToContent=SizeToContent.WidthAndHeight;
        UseLayoutRounding=true;SnapsToDevicePixels=true;
        glass.CornerRadius=GlassTheme.CornerRadius;glass.BorderThickness=new Thickness(0);glass.BorderBrush=Brushes.Transparent;glass.Background=Brushes.Transparent;
        glass.Padding=new Thickness(0);root.Margin=DockMetrics.Padding(false);
        var composition=new Grid();composition.Children.Add(GlassTheme.CreateLayers(out glassBody));composition.Children.Add(root);
        glass.Child=composition;Content=glass;
        icons.HorizontalAlignment=HorizontalAlignment.Center;dockBody.HorizontalAlignment=HorizontalAlignment.Center;
        root.HorizontalAlignment=HorizontalAlignment.Center;
        TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this,TextRenderingMode.ClearType);
        handle.Child=new TextBlock {Text="⠿",TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Foreground=new SolidColorBrush(Color.FromRgb(141,148,171))};
        handle.Width=DockMetrics.Grip;handle.Height=DockMetrics.Cross;handle.Background=Brushes.Transparent;handle.HorizontalAlignment=HorizontalAlignment.Center;handle.Cursor=Cursors.SizeAll;handle.ToolTip="끌어서 화면 가장자리로 이동 · 오른쪽 클릭: 설정";
        handle.MouseLeftButtonDown+=BeginDockMove;
        handle.MouseMove+=ContinueDockMove;
        handle.MouseLeftButtonUp+=(_,e)=>{if(dockPointerDown){FinishDockMove();e.Handled=true;}};
        handle.LostMouseCapture+=(_,_)=>{if(dockPointerDown)FinishDockMove();};
        insertionLayer.Children.Add(insertionMark);
        iconViewport.Children.Add(icons);iconViewport.Children.Add(compactMark);iconViewport.Children.Add(insertionLayer);
        iconViewport.Background=Brushes.Transparent;iconViewport.AllowDrop=true;
        iconViewport.PreviewDragOver+=DockDragOver;iconViewport.PreviewDrop+=DockDrop;
        iconViewport.DragLeave+=(_,_)=>insertionMark.Visibility=Visibility.Collapsed;
        recycleButton.Style=(Style)FindResource("DockButton");recycleButton.Content=recycleView;recycleButton.AllowDrop=false;
        recycleButton.Click+=(_,_)=>OpenRecycleBin();recycleButton.ContextMenu=BuildRecycleMenu();
        AutomationProperties.SetName(recycleButton,"Windows 휴지통");
        recycleSection.Children.Add(recycleDivider);recycleSection.Children.Add(minimizedPanel);recycleSection.Children.Add(recycleButton);
        genie=new GenieAnimator(settings,Dispatcher) {Target=GenieTarget,Edge=()=>settings.Edge};
        genie.MinimizeStarted+=h=>{if(genie.ThumbnailMode){landingSlots.Add(h);Refresh();UpdateLayout();}};
        genie.Landed+=h=>{landingSlots.Remove(h);RefreshSlot(h);Dispatcher.BeginInvoke(catalog.Refresh,DispatcherPriority.Background);};
        genie.RestoreStarted+=RefreshSlot;
        minimizeHook=new MinimizeButtonHook(Dispatcher,MinimizeFromButton) {Enabled=settings.GenieEnabled};
        handle.ContextMenu=BuildSettings();glass.ContextMenu=BuildSettings(); dockBody.Children.Add(handle); dockBody.Children.Add(iconViewport);dockBody.Children.Add(recycleSection);root.Children.Add(dockBody);
        tray=new Forms.NotifyIcon { Icon=System.Drawing.SystemIcons.Application, Text="GlassDock 0.3.0", Visible=true };
        StartupDiagnostics.Write("Tray icon created");
        var menu=new Forms.ContextMenuStrip(); menu.Items.Add("독 표시",null,(_,_)=>Dispatcher.Invoke(()=>{Show();Reveal();Place();}));
        menu.Items.Add("종료",null,(_,_)=>Dispatcher.Invoke(Close)); tray.ContextMenuStrip=menu; tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(()=>{Show();Reveal();});
        catalog.Changed+=()=>{minimizeHook.SetEligible(catalog.Windows.Select(w=>w.Handle));genie.ForgetClosed();};
        catalog.Changed+=Refresh;
        catalog.ForegroundChanged+=h=> {
            if(Native.GetWindowThreadProcessId(h,out uint pid)!=0 && pid==Environment.ProcessId) return;
            foreground=h;
            var path=catalog.Windows.FirstOrDefault(w=>w.Handle==h)?.Path;
            if(DateTime.UtcNow>ignoreAutomaticForegroundUntil && lastApp!=null && !StringComparer.OrdinalIgnoreCase.Equals(path,lastApp)) policy.Reset();
        };
        SourceInitialized+=(_,_)=>{
            StartupDiagnostics.Write($"SourceInitialized hwnd={new WindowInteropHelper(this).Handle}");
            bool backdropApplied=GlassBackdrop.Apply(this);
            glassBody.Background=backdropApplied?GlassTheme.CreateAcrylicTint():GlassTheme.CreateFallbackTint();
            StartupDiagnostics.Write($"Glass backdrop: {GlassBackdrop.MaterialDescription}; Windows {Environment.OSVersion.Version}; foreground AllowsTransparency={AllowsTransparency}");
            var hwnd=new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook((nint hookHwnd,int msg,nint w,nint l,ref bool handled)=>{if(msg==0x84&&hidden){handled=true;return (nint)(-1);}if(msg==0x21){handled=true;return (nint)3;}if(msg==0x02E0||msg==0x47)Dispatcher.BeginInvoke(UpdateGlassRegion,DispatcherPriority.Loaded);return 0;}); };
        Loaded+=(_,_)=>{ foreground=Native.GetForegroundWindow();catalog.Refresh();Place();UpdateGlassRegion();RefreshRecycleBin();recycleTimer.Start();StartupDiagnostics.Write($"Loaded visible={IsVisible} size={ActualWidth}x{ActualHeight} position={Left},{Top} hwnd={new WindowInteropHelper(this).Handle}"); };
        ContentRendered+=(_,_)=>{UpdateGlassRegion();StartupDiagnostics.Write($"ContentRendered visible={IsVisible} size={ActualWidth}x{ActualHeight} position={Left},{Top}");};
        SizeChanged+=(_,_)=> { if(dockPointerDown&&movingDock)MoveDockWithPointer();else if(!dragging)Place();UpdateGlassRegion(); };
        LocationChanged+=(_,_)=>UpdateGlassRegion();
        IsVisibleChanged+=(_,_)=>UpdateGlassRegion();
        DpiChanged+=(_,_)=>Dispatcher.BeginInvoke(()=>{iconRequests.Clear();recycleIconKey=null;Refresh();RefreshRecycleBin();UpdateGlassRegion();});
        if(materialReview)
        {
            var reviewBackground=new GlassMaterialReview();
            Loaded+=(_,_)=>reviewBackground.Show();
            Closed+=(_,_)=>reviewBackground.Close();
        }
        activationTimer.Tick+=(_,_)=>FinishActivation();
        hoverTimer.Tick+=(_,_)=>OpenHoverPreview();
        previewLeaveTimer.Tick+=(_,_)=>{
            previewLeaveTimer.Stop();
            if(activeHoverPath!=null && activePreview?.IsMouseOver!=true &&
               (!buttons.TryGetValue(activeHoverPath,out var button)||!button.IsMouseOver))CloseHoverPreview();
        };
        edgeTimer.Tick+=(_,_)=>CheckEdge();edgeTimer.Start();
        recycleTimer.Tick+=(_,_)=>RefreshRecycleBin();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
        Closed+=(_,_)=> { isClosed=true;minimizeHook.Dispose();genie.Dispose();edgeTimer.Stop();recycleTimer.Stop();activationTimer.Stop();hoverTimer.Stop();previewLeaveTimer.Stop();activePreview?.Close();catalog.Dispose();appIcons.Dispose();tray.Dispose();Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;Save(); };
        StartupDiagnostics.Write("DockWindow constructor completed");
    }
    void DisplayChanged(object? sender,EventArgs e)=>Dispatcher.BeginInvoke(()=>{activePreview?.Close();Place();Refresh();});
    void UpdateGlassRegion()
    {
        GlassBackdrop.Sync(this);
        var hwnd=new WindowInteropHelper(this).Handle;
        if(hwnd==0||!Native.GetWindowRect(hwnd,out var bounds))return;
        double scale=VisualTreeHelper.GetDpi(this).DpiScaleX;
        int w=bounds.Right-bounds.Left,h=bounds.Bottom-bounds.Top,r=(int)Math.Round(GlassTheme.CornerRadiusDip*2*scale);
        string key=$"{w},{h},{r}";if(key==inputRegionKey||w<1||h<1)return;
        var region=Native.CreateRoundRectRgn(0,0,w+1,h+1,r,r);
        if(region!=0){if(Native.SetWindowRgn(hwnd,region,true)!=0)inputRegionKey=key;else Native.DeleteObject(region);}
    }
    public static Button Button(string text,Action action) { var b=new Button {Content=text}; b.Click+=(_,_)=>action();return b; }
    ContextMenu BuildSettings()
    {
        var menu=new ContextMenu();
        foreach(string edge in new[]{"Bottom","Top","Left","Right"})
        { var m=new MenuItem { Header=edge switch {"Bottom"=>"아래","Top"=>"위","Left"=>"왼쪽",_=>"오른쪽"} }; m.Click+=(_,_)=>{settings.Edge=edge;policy.Reset();Refresh();Save();Place();};menu.Items.Add(m); }
        var auto=new MenuItem {Header="자동 숨김",IsCheckable=true,IsChecked=settings.AutoHide}; auto.Click+=(_,_)=>{settings.AutoHide=auto.IsChecked;Reveal();Save();}; menu.Items.Add(auto);
        menu.Items.Add(HoverPreviewSetting());
        menu.Items.Add(GenieSettings());
        var monitors=new MenuItem {Header="모니터"};
        monitors.SubmenuOpened+=(_,_)=>{monitors.Items.Clear(); foreach(var screen in Forms.Screen.AllScreens) {var item=new MenuItem {Header=screen.DeviceName}; item.Click+=(_,_)=>{settings.Monitor=screen.DeviceName;Save();Place();};monitors.Items.Add(item);} };menu.Items.Add(monitors);
        var add=new MenuItem {Header="프로그램 고정 (.exe / .lnk)"};add.Click+=(_,_)=>{
            var dialog=new Microsoft.Win32.OpenFileDialog {Filter="Windows 프로그램 및 바로가기|*.exe;*.lnk"};
            if(dialog.ShowDialog()!=true)return;
            if(!AppIdentity.IsApplication(dialog.FileName)){MessageBox.Show("실행 파일 또는 앱을 가리키는 바로가기를 선택해 주세요.","GlassDock");return;}
            if(!settings.Pins.Contains(dialog.FileName,StringComparer.OrdinalIgnoreCase))settings.Pins.Add(dialog.FileName);Save();Refresh();
        };menu.Items.Add(add);
        var exit=new MenuItem {Header="독 종료"};exit.Click+=(_,_)=>Close();menu.Items.Add(exit);return menu;
    }
    void Save(){if(persistSettings)SettingsStore.Save(settings);}
    MenuItem GenieSettings()
    {
        var root=new MenuItem {Header="최소화 효과 (지니)"};
        var enabled=new MenuItem {Header="지니 효과 사용",IsCheckable=true};
        enabled.Click+=(_,_)=>{settings.GenieEnabled=enabled.IsChecked;minimizeHook.Enabled=settings.GenieEnabled;Save();};
        root.Items.Add(enabled);root.Items.Add(new Separator());
        var iconMode=new MenuItem {Header="앱 아이콘으로 들어가기"};
        var thumbnailMode=new MenuItem {Header="독에 창 썸네일로 남기"};
        void SetMode(string mode){settings.GenieMode=mode;landingSlots.Clear();Save();Refresh();}
        iconMode.Click+=(_,_)=>SetMode("Icon");thumbnailMode.Click+=(_,_)=>SetMode("Thumbnail");
        root.Items.Add(iconMode);root.Items.Add(thumbnailMode);root.Items.Add(new Separator());
        var speed=new MenuItem {Header="속도"};
        var speeds=new List<MenuItem>();
        foreach(var value in GenieAnimator.Speeds)
        {
            var item=new MenuItem {Header=value switch {0.5=>"아주 느리게 (0.5배)",0.75=>"느리게 (0.75배)",1=>"기본 (macOS와 같음)",1.5=>"빠르게 (1.5배)",_=>"아주 빠르게 (2배)"},Tag=value};
            item.Click+=(_,_)=>{settings.GenieSpeed=value;Save();};
            speed.Items.Add(item);speeds.Add(item);
        }
        root.Items.Add(speed);
        root.SubmenuOpened+=(_,_)=>{
            enabled.IsChecked=settings.GenieEnabled;
            iconMode.IsChecked=settings.GenieMode!="Thumbnail";thumbnailMode.IsChecked=settings.GenieMode=="Thumbnail";
            foreach(var item in speeds)item.IsChecked=Math.Abs(settings.GenieSpeed-(double)item.Tag)<0.01;
        };
        return root;
    }
    void MinimizeFromButton(nint hwnd)
    {
        void Minimize()=>Native.PostMessage(hwnd,0x112,0xF020,0); // WM_SYSCOMMAND SC_MINIMIZE, as the button would
        var window=catalog.Windows.FirstOrDefault(w=>w.Handle==hwnd);
        if(window==null||isClosed){Minimize();return;}
        policy.Reset();CloseHoverPreview();
        genie.Minimize(hwnd,window.Path,Minimize);
        Dispatcher.BeginInvoke(catalog.Refresh,DispatcherPriority.Background);
    }
    // Where a window lands in the dock, in physical pixels, keeping its aspect ratio.
    Native.Rect GenieTarget(nint hwnd,string path,Native.Rect source)
    {
        double aspect=(source.Right-source.Left)/(double)Math.Max(1,source.Bottom-source.Top);
        var dpi=VisualTreeHelper.GetDpi(this);
        FrameworkElement? anchor=null;double inset=0;
        if(genie.ThumbnailMode&&slots.TryGetValue(hwnd,out var slot)){anchor=slot.Cell;inset=1;}
        else if(buttons.TryGetValue(path,out var button))anchor=button;
        if(anchor!=null&&!hidden&&IsVisible&&PresentationSource.FromVisual(anchor)!=null)
        {
            var origin=anchor.PointToScreen(new Point());
            double size=(DockMetrics.Icon-inset*2)*dpi.DpiScaleX;
            double w=aspect>=1?size:size*aspect,h=aspect>=1?size/aspect:size;
            double x=origin.X+inset*dpi.DpiScaleX+(size-w)/2,y=origin.Y+inset*dpi.DpiScaleY+(size-h)/2;
            return new Native.Rect {Left=(int)Math.Round(x),Top=(int)Math.Round(y),Right=(int)Math.Round(x+w),Bottom=(int)Math.Round(y+h)};
        }
        // Dock hidden: aim at the middle of the dock's screen edge.
        var area=Screen().WorkingArea;int s=(int)Math.Round(DockMetrics.Icon*dpi.DpiScaleX);
        int cx=area.Left+area.Width/2,cy=area.Top+area.Height/2;
        return settings.Edge switch
        {
            "Top"=>new Native.Rect {Left=cx-s/2,Top=area.Top,Right=cx+s/2,Bottom=area.Top+s},
            "Left"=>new Native.Rect {Left=area.Left,Top=cy-s/2,Right=area.Left+s,Bottom=cy+s/2},
            "Right"=>new Native.Rect {Left=area.Right-s,Top=cy-s/2,Right=area.Right,Bottom=cy+s/2},
            _=>new Native.Rect {Left=cx-s/2,Top=area.Bottom-s,Right=cx+s/2,Bottom=area.Bottom},
        };
    }
    void UpdateMinimizedSlots(bool vertical)
    {
        minimizedPanel.Orientation=vertical?Orientation.Vertical:Orientation.Horizontal;
        var wanted=new List<nint>();
        if(genie.ThumbnailMode)
        {
            foreach(var w in catalog.Windows)if(w.Minimized||landingSlots.Contains(w.Handle)||genie.IsBusy(w.Handle))wanted.Add(w.Handle);
            foreach(var h in landingSlots)if(!wanted.Contains(h))wanted.Add(h);
        }
        foreach(var (h,slot) in slots.ToList())if(!wanted.Contains(h)&&!slot.Removing)AnimateSlot(h,slot,vertical,false);
        foreach(var h in wanted)
        {
            if(!slots.TryGetValue(h,out var slot))
            {
                slot=CreateSlot(h);slots[h]=slot;minimizedPanel.Children.Add(slot.Button);
                SizeSlot(slot,vertical);AnimateSlot(h,slot,vertical,true);
            }
            else if(slot.Removing||slot.Vertical!=vertical){SizeSlot(slot,vertical);AnimateSlot(h,slot,vertical,true);}
            RefreshSlot(h);
        }
    }
    MinimizedSlot CreateSlot(nint hwnd)
    {
        var thumb=new Image {Width=DockMetrics.Icon-2,Height=DockMetrics.Icon-2,Stretch=Stretch.Uniform,Margin=new Thickness(1),IsHitTestVisible=false};
        RenderOptions.SetBitmapScalingMode(thumb,BitmapScalingMode.HighQuality);
        var badge=new Image {Width=13,Height=13,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false};
        // Transparent background so the whole cell takes clicks, not just image pixels.
        var cell=new Grid {Width=DockMetrics.Icon,Height=DockMetrics.Icon,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Background=Brushes.Transparent};
        cell.Children.Add(thumb);cell.Children.Add(badge);
        var button=new Button {Style=(Style)FindResource("DockButton"),Content=cell,ClipToBounds=true,AllowDrop=false};
        button.Click+=(_,_)=>{
            var w=catalog.Windows.FirstOrDefault(x=>x.Handle==hwnd);
            if(w!=null){policy.Reset();CloseHoverPreview();Activate(w);}
        };
        AutomationProperties.SetName(button,"최소화된 창");
        return new MinimizedSlot {Button=button,Cell=cell,Thumb=thumb,Badge=badge};
    }
    void RefreshSlot(nint hwnd)
    {
        if(!slots.TryGetValue(hwnd,out var slot))return;
        var window=catalog.Windows.FirstOrDefault(w=>w.Handle==hwnd);
        ImageSource? icon=window!=null&&buttons.TryGetValue(window.Path,out var b)&&b.Content is AppIconView view?view.Source:null;
        var image=genie.Image(hwnd);
        slot.Thumb.Source=(ImageSource?)image??icon;
        slot.Badge.Source=image!=null?icon:null;
        // Hidden while the genie is flying into or out of this slot.
        slot.Thumb.Opacity=slot.Badge.Opacity=genie.IsBusy(hwnd)||landingSlots.Contains(hwnd)?0:1;
        slot.Button.ToolTip=window!=null?window.Title+" · 클릭해서 복원":"최소화된 창";
    }
    void SizeSlot(MinimizedSlot slot,bool vertical)
    {
        slot.Button.BeginAnimation(WidthProperty,null);slot.Button.BeginAnimation(HeightProperty,null);
        slot.Button.Width=vertical?DockMetrics.Cross:0;slot.Button.Height=vertical?0:DockMetrics.Cross;
        slot.Vertical=vertical;
    }
    void AnimateSlot(nint hwnd,MinimizedSlot slot,bool vertical,bool open)
    {
        slot.Removing=!open;slot.Button.IsHitTestVisible=open;
        var property=vertical?HeightProperty:WidthProperty;
        double from=vertical?slot.Button.ActualHeight:slot.Button.ActualWidth;
        if(double.IsNaN(from)||!slot.Button.IsLoaded)from=open?0:DockMetrics.Icon+DockMetrics.Gap;
        var motion=new DoubleAnimation(from,open?DockMetrics.Icon+DockMetrics.Gap:0,genie.SlotDuration) {EasingFunction=new SineEase {EasingMode=EasingMode.EaseInOut}};
        if(!open)motion.Completed+=(_,_)=>{
            if(!slot.Removing||!slots.TryGetValue(hwnd,out var current)||current!=slot)return;
            slots.Remove(hwnd);minimizedPanel.Children.Remove(slot.Button);
        };
        slot.Button.BeginAnimation(property,motion);
    }
    void Refresh()
    {
        if(refreshing || dragging || movingDock) return;refreshing=true;
        try {
            if(previousDesktop!=catalog.CurrentDesktopId){policy.Reset();CloseHoverPreview();}
            previousDesktop=catalog.CurrentDesktopId;
            if(activeHoverPath!=null&&!catalog.Windows.Any(w=>StringComparer.OrdinalIgnoreCase.Equals(w.Path,activeHoverPath)))CloseHoverPreview();
            if(!catalog.Windows.Any(w=>w.Path==lastApp))policy.Reset();
            bool vertical=settings.Edge is "Left" or "Right";icons.Orientation=vertical?Orientation.Vertical:Orientation.Horizontal;
            root.Orientation=Orientation.Vertical;dockBody.Orientation=icons.Orientation;
            icons.Margin=vertical?new Thickness(0,0,0,DockMetrics.Gap/2):new Thickness(0,0,DockMetrics.Gap/2,0);
            root.Margin=DockMetrics.Padding(vertical);
            handle.Width=vertical?DockMetrics.Cross:DockMetrics.Grip;handle.Height=vertical?DockMetrics.Grip:DockMetrics.Cross;
            handle.Margin=vertical?new Thickness(0,0,0,DockMetrics.GripGap):new Thickness(0,0,DockMetrics.GripGap,0);
            var pinned=settings.Pins.Select(AppIdentity.Canonical).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var paths=catalog.Windows.Select(w=>w.Path).Concat(pinned).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            settings.Order=settings.Order.Select(AppIdentity.Canonical).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var normalized=settings.LayoutOrder.Select(e=>e.Kind=="app"?DockEntry.App(AppIdentity.Canonical(e.Id)):e).ToList();
            var order=DockOrder.Reconcile(normalized,settings.Order,paths,settings.LayoutVersion>=1||normalized.Count>0);
            bool orderChanged=settings.LayoutVersion!=2||!order.SequenceEqual(settings.LayoutOrder);
            settings.LayoutVersion=2;settings.LayoutOrder=order;settings.Order=order.Where(e=>e.Kind=="app").Select(e=>e.Id).ToList();
            if(orderChanged)Save();
            displayedEntries=DockOrder.Visible(order,paths);
            foreach(var stale in buttons.Keys.Except(paths,StringComparer.OrdinalIgnoreCase).ToList()) {icons.Children.Remove(buttons[stale]);buttons.Remove(stale);iconRequests.Remove(stale);}
            // Retain button instances while OS events arrive; never replace a pressed target.
            foreach(var path in paths)
            {
                var windows=catalog.Windows.Where(w=>StringComparer.OrdinalIgnoreCase.Equals(w.Path,path)).ToList();
                if(!buttons.TryGetValue(path,out var b))
                {
                    b=new Button {Style=(Style)FindResource("DockButton"),AllowDrop=true,Tag=path}; buttons[path]=b;
                    bool dragged=false,pressedHere=false;
                    b.Click+=(_,_)=>{if(!dragged)ClickApp(path);};
                    b.MouseEnter+=(_,_)=>QueueHoverPreview(path,b);
                    b.MouseLeave+=(_,_)=>{hoverTimer.Stop();previewLeaveTimer.Stop();previewLeaveTimer.Start();};
                    b.PreviewMouseDown+=(_,_)=>CloseHoverPreview();
                    b.ContextMenuOpening+=(_,_)=>CloseHoverPreview();
                    Point start=default;
                    b.PreviewMouseLeftButtonDown+=(_,e)=>{start=e.GetPosition(b);dragged=false;pressedHere=true;};
                    b.PreviewMouseLeftButtonUp+=(_,_)=>pressedHere=false;
                    b.LostMouseCapture+=(_,_)=>{if(!dragging)pressedHere=false;};
                    b.PreviewMouseMove+=(_,e)=>{
                        var delta=e.GetPosition(b)-start;
                        if(pressedHere&&!dragging&&e.LeftButton==MouseButtonState.Pressed&&(Math.Abs(delta.X)>=Math.Max(6,SystemParameters.MinimumHorizontalDragDistance)||Math.Abs(delta.Y)>=Math.Max(6,SystemParameters.MinimumVerticalDragDistance)))
                        {
                            dragged=true;dragging=true;CloseHoverPreview();policy.Reset();
                            try{DragDrop.DoDragDrop(b,new DataObject("GlassDock.App",path),DragDropEffects.Move);}
                            finally{pressedHere=false;dragging=false;insertionMark.Visibility=Visibility.Collapsed;Dispatcher.BeginInvoke(Refresh);}
                        }
                    };
                }
                b.ToolTip=AppIdentity.DisplayName(path)+" · "+windows.Count+"개 창";
                ToolTipService.SetIsEnabled(b,!settings.HoverPreviews||windows.Count==0);
                if(b.Content is not AppIconView view){view=DockArtwork.AppTile(path,windows.Count,null);b.Content=view;}
                view.Update(windows.Count,vertical);RequestIcon(path,b,view);
                var context=new ContextMenu();
                void Item(string label,Action action) { var i=new MenuItem {Header=label};i.Click+=(_,_)=>action();context.Items.Add(i); }
                Item("열린 창 미리보기",()=>ShowWindows(path));
                context.Items.Add(HoverPreviewSetting());
                Item(pinned.Contains(path,StringComparer.OrdinalIgnoreCase)?"고정 해제":"고정",()=>{int removed=settings.Pins.RemoveAll(p=>AppIdentity.Canonical(p).Equals(path,StringComparison.OrdinalIgnoreCase));if(removed==0)settings.Pins.Add(path);Save();Refresh();});
                Item("새 창 (앱별 지원)",()=>Launch(path));
                Item("창 닫기 요청",()=>{foreach(var w in windows)Native.PostMessage(w.Handle,0x10,0,0);});b.ContextMenu=context;
            }
            if(overviewButton==null)
            {
                overviewButton=Button("",()=>ShowWindows(null));overviewButton.Style=(Style)FindResource("DockButton");
                overviewButton.Content=DockArtwork.Tile("overview","#A9ACC6","#737797");overviewButton.ToolTip="현재 작업공간의 전체 창";
                taskViewButton=Button("",()=>{policy.Reset();System.Diagnostics.Process.Start(new ProcessStartInfo("explorer.exe","shell:::{3080F90E-D7AD-11D9-BD98-0000947B0257}"){UseShellExecute=true});});
                taskViewButton.Style=(Style)FindResource("DockButton");taskViewButton.Content=DockArtwork.Tile("layers","#B795EF","#8052C4");taskViewButton.ToolTip="Windows 작업 보기 · 작업공간 전환";
            }
            toolsDivider.Width=vertical?24:1;toolsDivider.Height=vertical?1:24;
            var desired=displayedEntries.Select(e=>(UIElement)EntryElement(e)).ToList();
            for(int i=0;i<desired.Count;i++)
            {
                var element=(FrameworkElement)desired[i];double gap=i==desired.Count-1?0:DockMetrics.Gap;
                element.Margin=vertical?new Thickness(0,0,0,gap):new Thickness(0,0,gap,0);
                if(element is Button button)
                {
                    button.AllowDrop=true;
                    button.Width=vertical?DockMetrics.Cross:DockMetrics.Icon;button.Height=vertical?DockMetrics.Icon:DockMetrics.Cross;
                    if(button.Content is Grid utility&&button.Content is not AppIconView){utility.Width=button.Width;utility.Height=button.Height;}
                }
            }
            if(!icons.Children.Cast<UIElement>().SequenceEqual(desired)) {icons.Children.Clear();foreach(var child in desired)icons.Children.Add(child);}
            recycleSection.Orientation=icons.Orientation;
            recycleSection.Margin=vertical?new Thickness(0,DockMetrics.Gap/2,0,0):new Thickness(DockMetrics.Gap/2,0,0,0);
            recycleDivider.Width=vertical?24:1;recycleDivider.Height=vertical?1:24;
            recycleDivider.Margin=vertical?new Thickness(0,0,0,DockMetrics.Gap):new Thickness(0,0,DockMetrics.Gap,0);
            recycleDivider.VerticalAlignment=VerticalAlignment.Center;recycleDivider.HorizontalAlignment=HorizontalAlignment.Center;
            recycleButton.Width=vertical?DockMetrics.Cross:DockMetrics.Icon;recycleButton.Height=vertical?DockMetrics.Icon:DockMetrics.Cross;
            recycleView.Update(0,vertical);
            UpdateMinimizedSlots(vertical);
            if(!catalog.DesktopAvailable) ToolTip="현재 가상 데스크톱 확인 실패: 창 제어가 제한됩니다.";
        } finally {refreshing=false;}
    }
    internal bool MoveApp(string from,string target)
    {
        return MoveAppBefore(from,DockEntry.App(target));
    }
    internal bool MoveAppBefore(string from,DockEntry? before)
    {
        if(!buttons.ContainsKey(from)||before!=null&&!displayedEntries.Contains(before))return false;
        if(!DockOrder.MoveApp(settings.LayoutOrder,from,before))return false;
        settings.Order=settings.LayoutOrder.Where(e=>e.Kind=="app").Select(e=>e.Id).ToList();
        if(!settings.Pins.Any(p=>AppIdentity.Canonical(p).Equals(from,StringComparison.OrdinalIgnoreCase)))settings.Pins.Add(from);
        Save();Dispatcher.BeginInvoke(Refresh);return true;
    }
    FrameworkElement EntryElement(DockEntry entry)=>entry.Kind=="app"?buttons[entry.Id]:entry.Kind=="separator"?toolsDivider:entry.Id==DockOrder.Overview?overviewButton!:taskViewButton!;
    internal int DropSlot(Point point)
    {
        bool vertical=settings.Edge is "Left" or "Right";
        if(point.X<0||point.Y<0||point.X>iconViewport.ActualWidth||point.Y>iconViewport.ActualHeight)return -1;
        double axis=vertical?point.Y:point.X;
        for(int i=0;i<displayedEntries.Count;i++)
        {
            var button=EntryElement(displayedEntries[i]);var p=button.TranslatePoint(new Point(),iconViewport);
            if(axis<(vertical?p.Y+button.ActualHeight/2:p.X+button.ActualWidth/2))return i;
        }
        return displayedEntries.Count;
    }
    string? DraggedApp(DragEventArgs e)=>e.Data.GetDataPresent("GlassDock.App",false)&&e.Data.GetData("GlassDock.App",false) is string app&&buttons.ContainsKey(app)?app:null;
    void DockDragOver(object sender,DragEventArgs e)
    {
        int slot=DropSlot(e.GetPosition(iconViewport));
        if(DraggedApp(e)==null||slot<0){e.Effects=DragDropEffects.None;insertionMark.Visibility=Visibility.Collapsed;}
        else {e.Effects=DragDropEffects.Move;ShowInsertion(slot);}
        e.Handled=true;
    }
    void ShowInsertion(int slot)
    {
        bool vertical=settings.Edge is "Left" or "Right";
        double axis;
        if(slot<displayedEntries.Count)
        {
            var p=EntryElement(displayedEntries[slot]).TranslatePoint(new Point(),iconViewport);
            axis=(vertical?p.Y:p.X)-DockMetrics.Gap/2;
        }
        else axis=(vertical?iconViewport.ActualHeight:iconViewport.ActualWidth)-1;
        axis=Math.Clamp(axis,1,Math.Max(1,(vertical?iconViewport.ActualHeight:iconViewport.ActualWidth)-2));
        insertionMark.Width=vertical?DockMetrics.Cross:2;insertionMark.Height=vertical?2:DockMetrics.Cross;
        Canvas.SetLeft(insertionMark,vertical?0:axis);Canvas.SetTop(insertionMark,vertical?axis:0);
        insertionMark.Visibility=Visibility.Visible;
    }
    void DockDrop(object sender,DragEventArgs e)
    {
        int slot=DropSlot(e.GetPosition(iconViewport));string? app=DraggedApp(e);
        e.Effects=app!=null&&slot>=0&&MoveAppBefore(app,slot<displayedEntries.Count?displayedEntries[slot]:null)?DragDropEffects.Move:DragDropEffects.None;
        insertionMark.Visibility=Visibility.Collapsed;e.Handled=true;
    }
    ContextMenu BuildRecycleMenu()
    {
        var menu=new ContextMenu();
        var open=new MenuItem {Header="휴지통 열기"};open.Click+=(_,_)=>OpenRecycleBin();menu.Items.Add(open);
        emptyRecycleItem=new MenuItem {Header="휴지통 비우기",IsEnabled=false};emptyRecycleItem.Click+=(_,_)=>EmptyRecycleBin();menu.Items.Add(emptyRecycleItem);
        menu.Opened+=(_,_)=>{CloseHoverPreview();RefreshRecycleBin();};return menu;
    }
    internal async void RefreshRecycleBin()
    {
        if(recycleBusy||isClosed)return;recycleBusy=true;
        try
        {
            recycleState=await recycleBin.QueryAsync();if(isClosed)return;
            recycleButton.ToolTip=recycleState.Label;AutomationProperties.SetHelpText(recycleButton,recycleState.Label);
            emptyRecycleItem.IsEnabled=recycleState.CanEmpty&&!emptying;
            int pixels=(int)Math.Ceiling(DockMetrics.Icon*VisualTreeHelper.GetDpi(this).DpiScaleX);
            string key=$"{(recycleState.Known?(recycleState.Count==0?"empty":"full"):"unknown")}:{pixels}";
            if(key!=recycleIconKey)
            {
                var image=await recycleBin.IconAsync(recycleState,pixels);if(isClosed)return;
                recycleView.SetImage(image);recycleIconKey=key;
                StartupDiagnostics.Write($"Recycle Bin: {recycleState.Label}; icon={key}; error=0x{recycleState.Error:X8}");
            }
        }
        catch(Exception error)
        {
            recycleState=new(null,null);emptyRecycleItem.IsEnabled=false;recycleIconKey=null;
            recycleButton.ToolTip="휴지통 · 상태/아이콘 확인 불가";
            recycleView.SetImage(DockArtwork.Glyph("app","#65718A"));StartupDiagnostics.Write("Recycle Bin query/icon failed",error);
        }
        finally {recycleBusy=false;}
    }
    void OpenRecycleBin(){CloseHoverPreview();try{recycleBin.Open();}catch(Exception e){SettingsStore.Log(e);MessageBox.Show("Windows 휴지통을 열지 못했습니다: "+e.Message,"GlassDock");}}
    async void EmptyRecycleBin()
    {
        if(emptying||isClosed)return;emptying=true;emptyRecycleItem.IsEnabled=false;CloseHoverPreview();
        try
        {
            var current=await recycleBin.QueryAsync();if(isClosed||!current.CanEmpty)return;
            int result=recycleBin.EmptyWithWindowsConfirmation(new WindowInteropHelper(this).Handle);
            if(result<0&&result!=unchecked((int)0x800704C7))MessageBox.Show($"휴지통을 비우지 못했습니다. 오류: 0x{result:X8}","GlassDock");
        }
        catch(Exception error){SettingsStore.Log(error);MessageBox.Show("휴지통을 비우지 못했습니다: "+error.Message,"GlassDock");}
        finally {emptying=false;if(!isClosed)RefreshRecycleBin();}
    }
    async void RequestIcon(string path,Button button,AppIconView view)
    {
        string source=path.StartsWith(AppIdentity.AppsFolder,StringComparison.OrdinalIgnoreCase)?path:AppIdentity.LaunchTarget(path,settings.Pins);
        int pixels=(int)Math.Ceiling(DockMetrics.Icon*VisualTreeHelper.GetDpi(this).DpiScaleX);
        string request=source+"|"+pixels;
        if(iconRequests.TryGetValue(path,out var previous)&&previous==request)return;
        iconRequests[path]=request;
        try
        {
            var result=await appIcons.GetAsync(source,pixels);
            if(buttons.TryGetValue(path,out var current)&&current==button&&iconRequests.TryGetValue(path,out var latest)&&latest==request)
            {
                view.SetImage(result.Image);
                AutomationProperties.SetHelpText(button,result.Fallback?"앱 아이콘 조회 실패: "+source:"Windows Shell 앱 아이콘: "+source);
            }
        }
        catch(ObjectDisposedException){}
        catch(Exception error){StartupDiagnostics.Write("App icon request failed: "+path+"; "+error.Message);}
    }
    void ClickApp(string path)
    {
        var windows=catalog.Windows.Where(w=>StringComparer.OrdinalIgnoreCase.Equals(w.Path,path)).ToList();
        if(lastApp!=path || DateTime.UtcNow-lastClick>TimeSpan.FromSeconds(4))policy.Reset();
        var action=policy.Decide(path,windows.Select(w=>new GlassDock.Core.WindowState(w.Handle.ToInt64(),Native.IsIconic(w.Handle))).ToList(),foreground.ToInt64());
        lastApp=path;lastClick=DateTime.UtcNow;
        switch(action) {
            case WindowAction.Launch: if(catalog.DesktopAvailable)Launch(path);break;
            case WindowAction.MinimizeOne:
                ignoreAutomaticForegroundUntil=DateTime.UtcNow.AddMilliseconds(700);
                var active=foreground;genie.Minimize(active,path,()=>Native.ShowWindowAsync(active,6));break;
            case WindowAction.MinimizeAll:
                foreach(var w in windows.Where(w=>!Native.IsIconic(w.Handle))){var h=w.Handle;genie.Minimize(h,path,()=>Native.ShowWindowAsync(h,6));}
                break;
            case WindowAction.RestoreAll:
                foreach(var w in windows.Skip(1))genie.Restore(w.Handle,path,null);
                Activate(windows[0]);break;
            case WindowAction.Activate:Activate(windows.FirstOrDefault(w=>!Native.IsIconic(w.Handle))??windows[0]);break;
        }
        Dispatcher.BeginInvoke(catalog.Refresh,DispatcherPriority.Background);
    }
    void Activate(AppWindow w)
    {
        activationTimer.Stop();pendingActivation=null;
        if(!catalog.IsOnCurrentDesktop(w.Handle)){ToolTip="창이 닫혔거나 현재 작업공간에 없습니다.";return;}
        if(genie.IsBusy(w.Handle))return;
        if(Native.IsIconic(w.Handle)&&genie.CanAnimateRestore(w.Handle))
        {
            // Bring the window to the front once the reverse genie has finished.
            genie.Restore(w.Handle,w.Path,()=>{pendingActivation=w;activationAttempts=0;FinishActivation();});
            return;
        }
        pendingActivation=w;activationAttempts=0;
        if(Native.IsIconic(w.Handle))Native.ShowWindowAsync(w.Handle,9);
        FinishActivation();
    }
    void FinishActivation()
    {
        if(pendingActivation is not AppWindow target){activationTimer.Stop();return;}
        if(!catalog.IsOnCurrentDesktop(target.Handle)){pendingActivation=null;activationTimer.Stop();ToolTip="선택한 창이 닫혔거나 작업공간이 바뀌었습니다.";return;}
        if(Native.IsIconic(target.Handle))
        {
            if(++activationAttempts<15){activationTimer.Start();return;}
            pendingActivation=null;activationTimer.Stop();ToolTip="선택한 창의 복원이 지연되고 있습니다. 기본 작업 표시줄에서 확인해 주세요.";return;
        }
        activationTimer.Stop();pendingActivation=null;
        bool activated=Native.SetForegroundWindow(target.Handle);
        if(activated||Native.GetForegroundWindow()==target.Handle){foreground=target.Handle;ToolTip=null;}
        else ToolTip="Windows가 전경 전환을 제한했습니다. 기본 작업 표시줄에서 선택해 주세요.";
    }
    void Launch(string path) {try {var target=AppIdentity.LaunchTarget(path,settings.Pins);Process.Start(target.StartsWith(AppIdentity.AppsFolder,StringComparison.OrdinalIgnoreCase)?new ProcessStartInfo("explorer.exe",target){UseShellExecute=true}:new ProcessStartInfo(target){UseShellExecute=true});}catch(Exception e){MessageBox.Show("실행 실패: "+e.Message);}}
    MenuItem HoverPreviewSetting()
    {
        var item=new MenuItem();
        void UpdateLabel()=>item.Header=settings.HoverPreviews?"마우스 미리보기 끄기":"마우스 미리보기 켜기";
        UpdateLabel();
        item.Loaded+=(_,_)=>UpdateLabel();
        item.Click+=(_,_)=>{settings.HoverPreviews=!settings.HoverPreviews;CloseHoverPreview();Save();UpdateLabel();Refresh();};
        return item;
    }
    void CloseHoverPreview()
    {
        hoverTimer.Stop();previewLeaveTimer.Stop();pendingHoverPath=null;pendingHoverButton=null;
        if(activeHoverPath!=null){activeHoverPath=null;activePreview?.Close();}
    }
    void QueueHoverPreview(string path,Button button)
    {
        hoverTimer.Stop();previewLeaveTimer.Stop();
        if(!settings.HoverPreviews||dragging||movingDock||dockPointerDown||Mouse.LeftButton==MouseButtonState.Pressed||button.ContextMenu?.IsOpen==true)return;
        if(activePreview!=null&&activeHoverPath==null)return;
        if(activeHoverPath==path)return;
        pendingHoverPath=path;pendingHoverButton=button;hoverTimer.Start();
    }
    void OpenHoverPreview()
    {
        hoverTimer.Stop();
        var path=pendingHoverPath;var button=pendingHoverButton;
        if(path==null||button==null||!button.IsMouseOver||!settings.HoverPreviews||dragging||movingDock||dockPointerDown||Mouse.LeftButton==MouseButtonState.Pressed||button.ContextMenu?.IsOpen==true)return;
        if(activePreview!=null&&activeHoverPath==null)return;
        catalog.Refresh();
        var list=catalog.Windows.Where(w=>StringComparer.OrdinalIgnoreCase.Equals(w.Path,path)).ToList();
        CloseHoverPreview();
        if(list.Count==0)return;
        var point=button.PointToScreen(new Point());var dpi=VisualTreeHelper.GetDpi(this);
        Native.GetWindowRect(new WindowInteropHelper(this).Handle,out var dockBounds);
        var anchor=new Rect(point.X,dockBounds.Top,button.ActualWidth*dpi.DpiScaleX,dockBounds.Bottom-dockBounds.Top);
        if(settings.Edge is "Left" or "Right")anchor=new Rect(dockBounds.Left,point.Y,dockBounds.Right-dockBounds.Left,button.ActualHeight*dpi.DpiScaleY);
        var preview=new PreviewWindow(list,w=>{policy.Reset();Activate(w);},Screen(),false,anchor,settings.Edge);
        activePreview=preview;activeHoverPath=path;
        preview.MouseEnter+=(_,_)=>previewLeaveTimer.Stop();
        preview.MouseLeave+=(_,_)=>{previewLeaveTimer.Stop();previewLeaveTimer.Start();};
        preview.Closed+=(_,_)=>{if(activePreview==preview){activePreview=null;activeHoverPath=null;}};
        preview.Show();
    }
    void ShowWindows(string? path)
    {
        policy.Reset();activationTimer.Stop();pendingActivation=null;
        hoverTimer.Stop();
        if(activeHoverPath!=null)CloseHoverPreview();
        if(activePreview!=null){activePreview.Close();return;}
        catalog.Refresh();
        var list=catalog.Windows.Where(w=>path==null||StringComparer.OrdinalIgnoreCase.Equals(w.Path,path)).ToList();
        var preview=new PreviewWindow(list,w=>{policy.Reset();Activate(w);},Screen(),path==null);
        activePreview=preview;
        preview.Closed+=(_,_)=>{if(activePreview==preview)activePreview=null;};
        preview.Show();
    }
    void BeginDockMove(object sender,MouseButtonEventArgs e)
    {
        if(movingDock)return;
        CloseHoverPreview();
        policy.Reset();
        dockGrab=e.GetPosition(handle);dockPress=handle.PointToScreen(dockGrab);
        dockPointerDown=handle.CaptureMouse();
        e.Handled=true;
    }
    void ContinueDockMove(object sender,MouseEventArgs e)
    {
        if(!dockPointerDown)return;
        if(e.LeftButton!=MouseButtonState.Pressed){FinishDockMove();return;}
        Native.GetCursorPos(out var cursor);
        if(!movingDock)
        {
            var dpi=VisualTreeHelper.GetDpi(this);
            if(Math.Abs(cursor.X-dockPress.X)<SystemParameters.MinimumHorizontalDragDistance*dpi.DpiScaleX &&
               Math.Abs(cursor.Y-dockPress.Y)<SystemParameters.MinimumVerticalDragDistance*dpi.DpiScaleY)return;
            activePreview?.Close();movingDock=true;dragging=true;
            StartupDiagnostics.Write("Dock move started; collapsing to one tile");
            expandedWidth=icons.ActualWidth;expandedHeight=icons.ActualHeight;
            icons.Width=expandedWidth;icons.Height=expandedHeight;
            iconViewport.Width=expandedWidth;iconViewport.Height=expandedHeight;
            icons.HorizontalAlignment=HorizontalAlignment.Left;icons.VerticalAlignment=VerticalAlignment.Top;
            icons.IsHitTestVisible=false;
            AnimateDockContent(settings.Edge is "Left" or "Right"?DockMetrics.Cross:DockMetrics.Icon,settings.Edge is "Left" or "Right"?DockMetrics.Icon:DockMetrics.Cross,false);
        }
        MoveDockWithPointer();e.Handled=true;
    }
    void MoveDockWithPointer()
    {
        Native.GetCursorPos(out var cursor);
        var grab=handle.PointToScreen(dockGrab);
        var hwnd=new WindowInteropHelper(this).Handle;
        Native.GetWindowRect(hwnd,out var bounds);
        Native.SetWindowPos(hwnd,0,bounds.Left+(int)Math.Round(cursor.X-grab.X),bounds.Top+(int)Math.Round(cursor.Y-grab.Y),0,0,0x0011);
    }
    void FinishDockMove()
    {
        dockPointerDown=false;
        if(handle.IsMouseCaptured)handle.ReleaseMouseCapture();
        if(!movingDock)return;
        dragging=false;
        SnapToNearest();
        // Refresh the orientation while the viewport still holds its compact size.
        movingDock=false;
        icons.Width=double.NaN;icons.Height=double.NaN;
        Refresh();
        icons.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
        expandedWidth=icons.DesiredSize.Width;expandedHeight=icons.DesiredSize.Height;
        icons.Width=expandedWidth;icons.Height=expandedHeight;
        movingDock=true;
        Place();
        AnimateDockContent(expandedWidth,expandedHeight,true);
    }
    void AnimateDockContent(double width,double height,bool expanding)
    {
        var duration=TimeSpan.FromMilliseconds(expanding?460:280);
        DoubleAnimation Motion(double from,double to)=>new(from,to,duration) {EasingFunction=new SineEase {EasingMode=EasingMode.EaseInOut},FillBehavior=FillBehavior.Stop};
        double fromWidth=iconViewport.ActualWidth,fromHeight=iconViewport.ActualHeight;
        var widthMotion=Motion(fromWidth,width);
        var heightMotion=Motion(fromHeight,height);
        iconViewport.Width=width;iconViewport.Height=height;
        if(expanding)widthMotion.Completed+=(_,_)=>{
            iconViewport.Width=double.NaN;iconViewport.Height=double.NaN;
            icons.Width=double.NaN;icons.Height=double.NaN;
            icons.HorizontalAlignment=HorizontalAlignment.Center;
            icons.IsHitTestVisible=true;movingDock=false;
            Refresh();Place();UpdateGlassRegion();
            StartupDiagnostics.Write($"Dock move completed; edge={settings.Edge} size={ActualWidth}x{ActualHeight}");
        };
        iconViewport.BeginAnimation(WidthProperty,widthMotion);
        iconViewport.BeginAnimation(HeightProperty,heightMotion);
        double iconOpacity=icons.Opacity,markOpacity=compactMark.Opacity;
        icons.Opacity=expanding?1:0;compactMark.Opacity=expanding?0:1;
        icons.BeginAnimation(OpacityProperty,Motion(iconOpacity,expanding?1:0));
        compactMark.BeginAnimation(OpacityProperty,Motion(markOpacity,expanding?0:1));
    }
    Forms.Screen Screen()=>Forms.Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==settings.Monitor)??Forms.Screen.PrimaryScreen??Forms.Screen.AllScreens[0];
    void Place()
    {
        if(dragging)return;
        var r=Screen().WorkingArea;
        var monitor=Native.MonitorFromPoint(new Native.Point(r.Left+10,r.Top+10),2);
        Native.GetDpiForMonitor(monitor,0,out uint dx,out uint dy);
        double scale=(dx==0?96:dx)/96d;
        int w=(int)Math.Ceiling(ActualWidth*scale),h=(int)Math.Ceiling(ActualHeight*scale);
        if(w<1||h<1)return;
        int x=settings.Edge switch {"Left"=>r.Left+8,"Right"=>r.Right-w-8,_=>r.Left+(r.Width-w)/2};
        int y=settings.Edge switch {"Top"=>r.Top+8,"Bottom"=>r.Bottom-h-8,_=>r.Top+(r.Height-h)/2};
        if(w>r.Width||h>r.Height){ToolTip=$"아이콘 길이 {w}×{h}, 작업영역 {r.Width}×{r.Height}: 화면 길이를 초과했습니다. 배치 정책 결정이 필요합니다.";x=Math.Max(r.Left,x);y=Math.Max(r.Top,y);}
        // Place in physical pixels; dividing virtual-screen origins by a monitor's DPI
        // shifts the dock onto the wrong display in mixed-DPI configurations.
        var hwnd=new WindowInteropHelper(this).Handle;
        if(hwnd!=0)Native.SetWindowPos(hwnd,0,x,y,0,0,0x0011); // NOSIZE | NOACTIVATE
    }
    void SnapToNearest()
    {
        Native.GetCursorPos(out var p);var s=Forms.Screen.FromPoint(new System.Drawing.Point(p.X,p.Y));settings.Monitor=s.DeviceName;var r=s.WorkingArea;
        var candidates=new[]{("Left",Math.Abs(p.X-r.Left)),("Right",Math.Abs(p.X-r.Right)),("Top",Math.Abs(p.Y-r.Top)),("Bottom",Math.Abs(p.Y-r.Bottom))};settings.Edge=candidates.MinBy(x=>x.Item2).Item1;Save();
    }
    void SetClickThrough(bool value) {var h=new WindowInteropHelper(this).Handle;int style=Native.GetWindowLong(h,-20);Native.SetWindowLong(h,-20,value?style|0x20:style&~0x20);}
    void Reveal(){hidden=false;if(!IsVisible)Show();SetClickThrough(false);GlassBackdrop.Fade(1,480);glass.BeginAnimation(OpacityProperty,new DoubleAnimation(glass.Opacity,1,TimeSpan.FromMilliseconds(480)));}
    void CheckEdge()
    {
        if(!settings.AutoHide||activePreview!=null||dragging||movingDock||dockPointerDown||emptying||recycleButton.ContextMenu?.IsOpen==true||(!hidden&&IsMouseOver)){if(hidden)Reveal();return;}
        Native.GetCursorPos(out var p);var r=Screen().WorkingArea;
        bool near=settings.Edge switch {"Left"=>Math.Abs(p.X-r.Left)<8&&p.Y>=r.Top&&p.Y<=r.Bottom,"Right"=>Math.Abs(p.X-r.Right)<8&&p.Y>=r.Top&&p.Y<=r.Bottom,"Top"=>Math.Abs(p.Y-r.Top)<8&&p.X>=r.Left&&p.X<=r.Right,_=>Math.Abs(p.Y-r.Bottom)<8&&p.X>=r.Left&&p.X<=r.Right};
        if(near){if(hidden)Reveal();}else if(!hidden){hidden=true;SetClickThrough(true);GlassBackdrop.Fade(0,480);var fade=new DoubleAnimation(glass.Opacity,0,TimeSpan.FromMilliseconds(480));fade.Completed+=(_,_)=>{if(hidden)Hide();};glass.BeginAnimation(OpacityProperty,fade);}
    }
}
