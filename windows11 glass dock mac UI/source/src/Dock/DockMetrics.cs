using System.Windows;
namespace GlassDock;
internal static class DockMetrics
{
    internal const double Icon=32,Gap=8,Dot=3,DotGap=3,Grip=12,GripGap=4,LongPadding=9,CrossPadding=7,Divider=1;
    internal const double Cross=Icon+DotGap+Dot;
    internal static Thickness Padding(bool vertical)=>vertical?new(CrossPadding,LongPadding,CrossPadding,LongPadding):new(LongPadding,CrossPadding,LongPadding,CrossPadding);
    internal static double Length(int icons,bool divider)=>LongPadding*2+Grip+GripGap+icons*Icon+System.Math.Max(0,icons-1)*Gap+(divider?Divider+Gap:0);
}
