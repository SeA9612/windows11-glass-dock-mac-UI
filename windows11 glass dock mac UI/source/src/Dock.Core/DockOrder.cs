namespace GlassDock.Core;

public sealed record DockEntry(string Kind,string Id)
{
    public static DockEntry App(string id)=>new("app",id);
    public static DockEntry Tool(string id)=>new("tool",id);
    public static DockEntry ToolBoundary()=>new("separator","apps-tools");
}

// Dormant applications stay in saved order so closing/reopening an app does not
// reset its position between tools. The fixed Recycle Bin never enters this list.
public static class DockOrder
{
    public const string Overview="overview",TaskView="task-view";
    static bool Same(DockEntry a,DockEntry b)=>a.Kind==b.Kind&&StringComparer.OrdinalIgnoreCase.Equals(a.Id,b.Id);
    public static List<DockEntry> Reconcile(IEnumerable<DockEntry> saved,IEnumerable<string> legacy,IEnumerable<string> apps,bool migrated)
    {
        var result=new List<DockEntry>();
        void Add(DockEntry e){if(!result.Any(x=>Same(x,e)))result.Add(e);}
        if(migrated)
        {
            foreach(var entry in saved)
                if(!string.IsNullOrWhiteSpace(entry.Id)&&(entry.Kind=="app"||entry.Kind=="tool"&&entry.Id is Overview or TaskView||entry==DockEntry.ToolBoundary()))Add(entry);
        }
        else foreach(string id in legacy.Where(s=>!string.IsNullOrWhiteSpace(s)))Add(DockEntry.App(id));
        foreach(string id in apps.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var entry=DockEntry.App(id);if(result.Any(x=>Same(x,entry)))continue;
            int firstTool=result.FindIndex(x=>x.Kind is "separator" or "tool");
            if(firstTool<0)result.Add(entry);else result.Insert(firstTool,entry);
        }
        Add(DockEntry.Tool(Overview));Add(DockEntry.Tool(TaskView));
        // The boundary is a saved position: apps before it belong to the app
        // section, and apps after it share the tools section. It cannot be dragged.
        if(!result.Contains(DockEntry.ToolBoundary()))result.Insert(result.FindIndex(e=>e.Kind=="tool"),DockEntry.ToolBoundary());
        return result;
    }
    public static List<DockEntry> Visible(IEnumerable<DockEntry> saved,IEnumerable<string> apps)
    {
        var visible=apps.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return saved.Where(e=>e.Kind=="tool"||e==DockEntry.ToolBoundary()||e.Kind=="app"&&visible.Contains(e.Id)).ToList();
    }
    public static bool MoveApp(List<DockEntry> order,string app,DockEntry? before)
    {
        var from=order.FirstOrDefault(e=>e.Kind=="app"&&StringComparer.OrdinalIgnoreCase.Equals(e.Id,app));
        if(from==null||before!=null&&Same(from,before))return false;
        var anchor=before==null?null:order.FirstOrDefault(e=>Same(e,before));
        if(before!=null&&anchor==null)return false;
        order.Remove(from);if(anchor==null)order.Add(from);else order.Insert(order.IndexOf(anchor),from);return true;
    }
}
