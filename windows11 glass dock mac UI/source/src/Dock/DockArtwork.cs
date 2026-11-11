using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
namespace GlassDock;
internal static class DockArtwork
{
    static Color ColorOf(string hex)=>(Color)ColorConverter.ConvertFromString(hex);
    internal static ImageSource Glyph(string name,string color="White")
    {
        string[] paths=name switch {
            "folder"=>new[]{"M3,7 L3,5 Q3,4 4,4 L9,4 11,7 20,7 Q21,7 21,8 L21,18 Q21,20 19,20 L5,20 Q3,20 3,18 Z","M3,10 L20,10 17,17 3,17"},
            "compass"=>new[]{"M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12","M16,8 L13.5,13.5 8,16 10.5,10.5 Z"},
            "note"=>new[]{"M9,5 L6,5 Q5,5 5,6 L5,19 Q5,20 6,20 L17,20 Q18,20 18,19 L18,13","M3,8 L7,8 M3,12 L7,12 M3,16 L7,16","M11,12 L12,8 18,2 21,5 15,11 Z"},
            "photo"=>new[]{"M5,4 L19,4 Q21,4 21,6 L21,18 Q21,20 19,20 L5,20 Q3,20 3,18 L3,6 Q3,4 5,4 Z","M3,16 L9,10 13,14 16,11 21,16","M16,8 A1,1 0 1 1 14,8 A1,1 0 1 1 16,8"},
            "overview"=>new[]{"M5,4 L19,4 Q21,4 21,6 L21,18 Q21,20 19,20 L5,20 Q3,20 3,18 L3,6 Q3,4 5,4 Z","M3,9 L21,9 M10,9 L10,20"},
            "chat"=>new[]{"M5,4 L19,4 Q21,4 21,6 L21,15 Q21,17 19,17 L10,17 5,21 5,17 Q3,17 3,15 L3,6 Q3,4 5,4 Z","M7,8 L17,8 M7,12 L14,12"},
            "app"=>new[]{"M4,4 L10,4 10,10 4,10 Z M14,4 L20,4 20,10 14,10 Z M4,14 L10,14 10,20 4,20 Z M14,14 L20,14 20,20 14,20 Z"},
            "layers"=>new[]{"M12,3 L22,8 12,13 2,8 Z","M2,12 L12,17 22,12 M2,16 L12,21 22,16"},
            _=>new[]{"M10,6 L12,12 18,14 12,16 10,22 8,16 2,14 8,12 Z","M18,1 L19,5 23,6 19,7 18,11 17,7 13,6 17,5 Z"}
        };
        var drawing=new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,24,24))));
        var pen=new Pen(new SolidColorBrush(ColorOf(color)),1.45) {StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
        foreach(var path in paths)drawing.Children.Add(new GeometryDrawing(null,pen,Geometry.Parse(path)));
        drawing.Freeze();var image=new DrawingImage(drawing);image.Freeze();return image;
    }
    internal static AppIconView AppTile(string path,int count,ImageSource? original)=>new(original??Glyph("app","#65718A"),count);
    internal static UIElement Tile(string glyph,string top,string bottom,int? count=null,ImageSource? original=null,bool selected=false)
    {
        var grid=new Grid {Width=32,Height=38,UseLayoutRounding=true};
        var tile=new Border {Width=32,Height=32,VerticalAlignment=VerticalAlignment.Top,CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(selected?1.5:1),BorderBrush=new SolidColorBrush(Color.FromArgb(selected?(byte)240:(byte)135,255,255,255)),Background=new LinearGradientBrush(ColorOf(top),ColorOf(bottom),90)};
        var inner=new Grid();
        inner.Children.Add(new Border {CornerRadius=new CornerRadius(7),Background=new LinearGradientBrush(new GradientStopCollection {new GradientStop(Color.FromArgb(55,255,255,255),0),new GradientStop(Colors.Transparent,.6)},90)});
        inner.Children.Add(new Image {Source=original??Glyph(glyph),Width=original==null?18:26,Height=original==null?18:26,Stretch=Stretch.Uniform});tile.Child=inner;grid.Children.Add(tile);
        if(count>0)grid.Children.Add(new Ellipse {Width=3,Height=3,Fill=new SolidColorBrush(ColorOf("#38465C")),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,1)});
        if(count>1)grid.Children.Add(new Border {Background=new SolidColorBrush(ColorOf("#F4F6FC")),CornerRadius=new CornerRadius(7),Padding=new Thickness(4,0,4,0),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Child=new TextBlock {Text=count.ToString(),FontSize=9,Foreground=new SolidColorBrush(ColorOf("#4A5871"))}});
        return grid;
    }
}

// Retained across catalogue/count/DPI changes; no replacement of a pressed button
// or its content. Program artwork is exactly the Shell image without a tile.
internal sealed class AppIconView : Grid
{
    readonly Image image=new() {Width=DockMetrics.Icon,Height=DockMetrics.Icon,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false};
    readonly Ellipse dot=new() {Width=DockMetrics.Dot,Height=DockMetrics.Dot,Fill=new SolidColorBrush(Color.FromRgb(56,70,92)),IsHitTestVisible=false};
    readonly TextBlock countText=new() {FontSize=9,Foreground=new SolidColorBrush(Color.FromRgb(74,88,113))};
    readonly Border badge;
    internal AppIconView(ImageSource source,int count)
    {
        UseLayoutRounding=true;SnapsToDevicePixels=true;Background=Brushes.Transparent;
        RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);
        image.Source=source;Children.Add(image);Children.Add(dot);
        badge=new Border {Background=new SolidColorBrush(Color.FromRgb(244,246,252)),CornerRadius=new CornerRadius(6),Padding=new Thickness(3,0,3,0),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Child=countText};Children.Add(badge);
        Update(count,false);
    }
    internal void SetImage(ImageSource source)=>image.Source=source;
    internal ImageSource? Source=>image.Source;
    internal void Update(int count,bool vertical)
    {
        Width=vertical?DockMetrics.Cross:DockMetrics.Icon;Height=vertical?DockMetrics.Icon:DockMetrics.Cross;
        dot.HorizontalAlignment=vertical?HorizontalAlignment.Right:HorizontalAlignment.Center;
        dot.VerticalAlignment=vertical?VerticalAlignment.Center:VerticalAlignment.Bottom;
        dot.Visibility=count>0?Visibility.Visible:Visibility.Hidden;
        badge.Margin=vertical?new Thickness(0,0,6,0):new Thickness(0);
        badge.Visibility=count>1?Visibility.Visible:Visibility.Collapsed;countText.Text=count.ToString();
    }
}
