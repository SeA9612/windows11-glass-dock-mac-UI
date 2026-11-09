using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GlassDock;
internal sealed record RecycleBinState(long? Count,long? Bytes,int Error=0)
{
    internal bool Known=>Count.HasValue;
    internal bool CanEmpty=>Count>0;
    internal string Label=>Count is null?"휴지통 · 상태 확인 불가":Count==0?"휴지통 · 비어 있음":$"휴지통 · {Count:N0}개 항목";
}
internal interface IRecycleBin
{
    Task<RecycleBinState> QueryAsync();
    Task<ImageSource> IconAsync(RecycleBinState state,int pixels);
    void Open();
    int EmptyWithWindowsConfirmation(nint owner);
}
internal sealed class WindowsRecycleBin : IRecycleBin
{
    internal const string ShellFolder="shell:RecycleBinFolder";
    readonly ConcurrentDictionary<string,Task<ImageSource>> icons=new();
    public Task<RecycleBinState> QueryAsync()=>Task.Run(()=>{
        var info=new QueryInfo {Size=(uint)Marshal.SizeOf<QueryInfo>()};
        int error=SHQueryRecycleBin(null,ref info);
        return error==0&&info.Items>=0?new RecycleBinState(info.Items,info.Bytes):new RecycleBinState(null,null,error);
    });
    public Task<ImageSource> IconAsync(RecycleBinState state,int pixels)
    {
        if(!state.Known)return Task.FromResult<ImageSource>(UnknownIcon());
        int stock=state.Count==0?31:32;
        return icons.GetOrAdd($"{stock}:{pixels}",_=>Task.Run(()=>StockIcon(stock,pixels)));
    }
    static ImageSource UnknownIcon()
    {
        var drawing=new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(null,new Pen(new SolidColorBrush(Color.FromRgb(127,139,155)),1.5),Geometry.Parse("M 6,8 L 26,8 M 10,8 L 11,29 21,29 22,8 M 13,8 L 13,4 19,4 19,8 M 14,14 C 14,10 21,11 19,15 L 17,18 M 17,21 L 17,23")));
        drawing.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,32,32))));drawing.Freeze();var image=new DrawingImage(drawing);image.Freeze();return image;
    }
    static ImageSource StockIcon(int stock,int pixels)
    {
        var info=new StockInfo {Size=(uint)Marshal.SizeOf<StockInfo>(),Path=""};
        Marshal.ThrowExceptionForHR(SHGetStockIconInfo(stock,0,ref info));
        nint large=0,small=0;
        try
        {
            Marshal.ThrowExceptionForHR(SHDefExtractIcon(info.Path,info.Index,0,out large,out small,(uint)pixels));
            if(large==0)throw new InvalidOperationException("Windows 휴지통 아이콘을 가져오지 못했습니다.");
            var image=Imaging.CreateBitmapSourceFromHIcon(large,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());image.Freeze();return image;
        }
        finally {if(large!=0)DestroyIcon(large);if(small!=0&&small!=large)DestroyIcon(small);}
    }
    public void Open()=>Process.Start(new ProcessStartInfo("explorer.exe",ShellFolder){UseShellExecute=true});
    // Flags=0 preserves Windows' own confirmation, progress and error UI.
    // Called only by the explicit menu action after a fresh nonempty-state query.
    public int EmptyWithWindowsConfirmation(nint owner)=>SHEmptyRecycleBin(owner,null,0);
    [StructLayout(LayoutKind.Sequential)] struct QueryInfo {internal uint Size;internal long Bytes,Items;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct StockInfo {internal uint Size;internal nint Icon;internal int ImageIndex,Index;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] internal string Path;}
    [DllImport("shell32.dll",EntryPoint="SHQueryRecycleBinW",CharSet=CharSet.Unicode)] static extern int SHQueryRecycleBin(string? root,ref QueryInfo info);
    [DllImport("shell32.dll",EntryPoint="SHEmptyRecycleBinW",CharSet=CharSet.Unicode)] static extern int SHEmptyRecycleBin(nint owner,string? root,uint flags);
    [DllImport("shell32.dll")] static extern int SHGetStockIconInfo(int id,uint flags,ref StockInfo info);
    [DllImport("shell32.dll",EntryPoint="SHDefExtractIconW",CharSet=CharSet.Unicode)] static extern int SHDefExtractIcon(string file,int index,uint flags,out nint large,out nint small,uint size);
    [DllImport("user32.dll")] static extern bool DestroyIcon(nint icon);
}
