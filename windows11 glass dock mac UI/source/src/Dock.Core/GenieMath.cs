namespace GlassDock.Core;

public readonly record struct GenieRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

// One mesh row: the two outer edge points of a strip that runs across the window.
public readonly record struct GenieRow(double X0, double Y0, double X1, double Y1);

// macOS-style genie geometry. t=0 is the window at full size, t=1 is the window
// inside the dock target. The math runs in a frame where "along" points from the
// window toward the dock edge, so every dock edge shares one formula.
public static class GenieMath
{
    // Share of the timeline spent bending the window into a funnel, and the point
    // where the window starts sliding into it. The two phases overlap slightly.
    public const double FunnelEnd = 0.4, SlideStart = 0.25;

    public static double Ease(double v)
    {
        v = Clamp01(v);
        return v < .5 ? 4 * v * v * v : 1 - Math.Pow(-2 * v + 2, 3) / 2;
    }

    public static double Opacity(double t, bool fade) => fade ? 1 - 0.85 * Ease((t - 0.82) / 0.18) : 1;

    public static GenieRow[] Rows(GenieRect source, GenieRect target, string edge, double t, int rows)
    {
        rows = Math.Max(2, rows);
        t = Clamp01(t);
        var s = ToFrame(source, edge);
        var d = ToFrame(target, edge);
        double sourceLength = s.A1 - s.A0, targetLength = d.A1 - d.A0;
        double funnel = Ease(t / FunnelEnd);
        double slide = Ease((t - SlideStart) / (1 - SlideStart));
        double finish = Ease((t - 0.85) / 0.15);
        double maxTravel = d.A0 - s.A0;
        double span = Math.Max(1, maxTravel);
        var result = new GenieRow[rows];
        for (int i = 0; i < rows; i++)
        {
            double v = i / (double)(rows - 1);
            double start = s.A0 + v * sourceLength, end = d.A0 + v * targetLength, travel = end - start;
            // Rows nearest the dock reach the target first and pile up in it, while
            // the far edge keeps sliding: the window pours into the icon.
            double along = maxTravel > 0
                ? start + (travel >= 0 ? Math.Min(travel, slide * maxTravel) : travel * slide)
                : start + travel * slide;
            double squeeze = funnel * Smooth((along - s.A0) / span);
            squeeze += (1 - squeeze) * finish;
            double c0 = Lerp(s.C0, d.C0, squeeze), c1 = Lerp(s.C1, d.C1, squeeze);
            var p0 = FromFrame(along, c0, edge);
            var p1 = FromFrame(along, c1, edge);
            result[i] = new GenieRow(p0.X, p0.Y, p1.X, p1.Y);
        }
        return result;
    }

    // Texture coordinate of a row edge. v runs from the far side of the window (0)
    // to the side facing the dock (1); side 0/1 picks the row's two edges.
    public static (double U, double V) Texture(string edge, double v, int side) => edge switch
    {
        "Top" => (side, 1 - v),
        "Left" => (1 - v, side),
        "Right" => (v, side),
        _ => (side, v),
    };

    static (double A0, double A1, double C0, double C1) ToFrame(GenieRect r, string edge) => edge switch
    {
        "Top" => (-r.Bottom, -r.Y, r.X, r.Right),
        "Left" => (-r.Right, -r.X, r.Y, r.Bottom),
        "Right" => (r.X, r.Right, r.Y, r.Bottom),
        _ => (r.Y, r.Bottom, r.X, r.Right),
    };

    static (double X, double Y) FromFrame(double along, double across, string edge) => edge switch
    {
        "Top" => (across, -along),
        "Left" => (-along, across),
        "Right" => (along, across),
        _ => (across, along),
    };

    static double Smooth(double v) { v = Clamp01(v); return v * v * (3 - 2 * v); }
    static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    static double Lerp(double a, double b, double p) => a + (b - a) * p;
}
