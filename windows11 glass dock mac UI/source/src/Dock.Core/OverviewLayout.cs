namespace GlassDock.Core;
public readonly record struct TileRect(double X,double Y,double Width,double Height);
public static class OverviewLayout
{
    public static IReadOnlyList<TileRect> Create(int count,double width,double height)
    {
        if(count<=0 || !double.IsFinite(width) || !double.IsFinite(height) || width<=0 || height<=0)
            return Array.Empty<TileRect>();

        const double gap=18,label=44;
        int columns=1;double best=-1,cellW=0,cellH=0;
        for(int c=1;c<=count;c++)
        {
            int rows=(count+c-1)/c;
            double usableWidth = width-gap*(c-1);
            double usableHeight = height-gap*(rows-1);
            double w = Math.Min(520, usableWidth/c);
            double h = Math.Min(340, usableHeight/rows);
            if(w<=0 || h<=label || !double.IsFinite(w) || !double.IsFinite(h)) continue;
            double previewW=Math.Min(w,(h-label)*1.6);
            double score=previewW*previewW/1.6;
            if(score>best){best=score;columns=c;cellW=w;cellH=h;}
        }
        if(best<0 || !double.IsFinite(cellW) || !double.IsFinite(cellH))return Array.Empty<TileRect>();
        int totalRows=(count+columns-1)/columns;
        double top=(height-(totalRows*cellH+(totalRows-1)*gap))/2;
        var result=new List<TileRect>();
        for(int i=0;i<count;i++)
        {
            int row=i/columns,col=i%columns,inRow=Math.Min(columns,count-row*columns);
            double left=(width-(inRow*cellW+(inRow-1)*gap))/2;
            result.Add(new(left+col*(cellW+gap),top+row*(cellH+gap),cellW,cellH));
        }
        return result;
    }
    public static TileRect Fit(double sourceWidth,double sourceHeight,TileRect bounds)
    {
        if(!double.IsFinite(sourceWidth) || !double.IsFinite(sourceHeight) || !double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) || sourceWidth<=0 || sourceHeight<=0 || bounds.Width<=0 || bounds.Height<=0)
            return bounds;
        double scale=Math.Min(bounds.Width/sourceWidth,bounds.Height/sourceHeight);
        double w=sourceWidth*scale,h=sourceHeight*scale;
        return new(bounds.X+(bounds.Width-w)/2,bounds.Y+(bounds.Height-h)/2,w,h);
    }
}
