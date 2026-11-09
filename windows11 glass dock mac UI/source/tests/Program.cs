using GlassDock.Core;
using System.Text.Json;
int count=0;
void Expect(WindowAction expected, WindowAction actual) { if(expected!=actual)throw new Exception($"Expected {expected}, got {actual}");count++; }
var p=new ClickPolicy();
WindowState[] two=[new(1,false),new(2,false)];
Expect(WindowAction.Launch,p.Decide("A",[],0));
Expect(WindowAction.Activate,p.Decide("A",two,9));
Expect(WindowAction.MinimizeOne,p.Decide("A",two,1));
Expect(WindowAction.MinimizeAll,p.Decide("A",[new(1,true),new(2,false)],2));
Expect(WindowAction.RestoreAll,p.Decide("A",[new(1,true),new(2,true)],9));
Expect(WindowAction.MinimizeOne,p.Decide("A",[new(1,false)],1));
Expect(WindowAction.RestoreAll,p.Decide("A",[new(1,true)],9));
Expect(WindowAction.MinimizeOne,p.Decide("A",two,1));
p.Reset(); // Workspace or explicit preview selection resets the armed action.
Expect(WindowAction.Activate,p.Decide("A",two,9));
Expect(WindowAction.MinimizeOne,p.Decide("A",two,1));
Expect(WindowAction.Activate,p.Decide("B",[new(3,false)],9));
Expect(WindowAction.Activate,p.Decide("A",two,9));
var nameInsensitive=new ClickPolicy();
Expect(WindowAction.MinimizeOne,nameInsensitive.Decide("A",two,1));
Expect(WindowAction.MinimizeAll,nameInsensitive.Decide("a",two,1));
Expect(WindowAction.Launch,new ClickPolicy().Decide("A",null,0));
Console.WriteLine($"PASS {count} click policy scenarios");
int layoutChecks=0;
foreach(var area in new[]{(Width:1836d,Height:900d),(Width:1196d,Height:560d),(Width:716d,Height:360d)})
foreach(int windowCount in new[]{1,2,3,5,8,12,24,40})
{
    var tiles=OverviewLayout.Create(windowCount,area.Width,area.Height);
    if(tiles.Count!=windowCount)throw new Exception("Overview dropped a window");
    foreach(var tile in tiles)
        if(tile.X<-.001||tile.Y<-.001||tile.Width<=0||tile.Height<=0||tile.X+tile.Width>area.Width+.001||tile.Y+tile.Height>area.Height+.001)throw new Exception("Overview tile outside working area");
    for(int i=0;i<tiles.Count;i++)for(int j=i+1;j<tiles.Count;j++)
    {
        var a=tiles[i];var b=tiles[j];
        if(a.X<b.X+b.Width&&b.X<a.X+a.Width&&a.Y<b.Y+b.Height&&b.Y<a.Y+a.Height)throw new Exception("Overlapping overview tiles");
    }
    layoutChecks++;
}
if(OverviewLayout.Create(0,100,100).Count!=0)throw new Exception("Empty workspace should not create fake windows");
layoutChecks++;
if(OverviewLayout.Create(3,double.NaN,300).Count!=0)throw new Exception("Non-finite dimensions should not generate a layout");
layoutChecks++;
if(OverviewLayout.Create(-2,100,100).Count!=0)throw new Exception("Negative counts should not create fake windows");
layoutChecks++;
foreach(var sourceSize in new[]{(W:1920d,H:1080d),(W:600d,H:1200d),(W:3440d,H:1440d)})
{
    var box=new TileRect(20,30,420,240);var fit=OverviewLayout.Fit(sourceSize.W,sourceSize.H,box);
    if(fit.X<box.X-.001||fit.Y<box.Y-.001||fit.X+fit.Width>box.X+box.Width+.001||fit.Y+fit.Height>box.Y+box.Height+.001||Math.Abs(fit.Width/fit.Height-sourceSize.W/sourceSize.H)>.0001)throw new Exception("Thumbnail aspect ratio changed");
    layoutChecks++;
}
Console.WriteLine($"PASS {layoutChecks} overview layout and aspect ratio scenarios");
int orderChecks=0;
void OrderCheck(bool value,string label){if(!value)throw new Exception(label);orderChecks++;}
var mixed=DockOrder.Reconcile([],new[]{"A","B"},new[]{"A","B","C"},false);
OrderCheck(mixed.SequenceEqual(new[]{DockEntry.App("A"),DockEntry.App("B"),DockEntry.App("C"),DockEntry.ToolBoundary(),DockEntry.Tool(DockOrder.Overview),DockEntry.Tool(DockOrder.TaskView)}),"legacy app order migration");
OrderCheck(DockOrder.MoveApp(mixed,"A",DockEntry.Tool(DockOrder.TaskView)),"move app between tools");
OrderCheck(mixed.IndexOf(DockEntry.App("A"))==mixed.IndexOf(DockEntry.Tool(DockOrder.Overview))+1,"between-tool order");
OrderCheck(DockOrder.MoveApp(mixed,"B",null)&&mixed.Last()==DockEntry.App("B"),"move app behind final tool");
OrderCheck(DockOrder.MoveApp(mixed,"B",DockEntry.App("C"))&&mixed.First()==DockEntry.App("B"),"move app back to leading apps");
var restored=JsonSerializer.Deserialize<List<DockEntry>>(JsonSerializer.Serialize(mixed))!;
OrderCheck(restored.SequenceEqual(mixed),"mixed stable IDs survive restart serialization");
var withoutA=DockOrder.Visible(restored,new[]{"B","C"});
OrderCheck(!withoutA.Contains(DockEntry.App("A"))&&restored.Contains(DockEntry.App("A")),"closed app slot retained in saved data");
var reopened=DockOrder.Reconcile(restored,[],new[]{"A","B","C"},true);
OrderCheck(reopened.SequenceEqual(mixed),"reopened app retains position between tools");
var newApp=DockOrder.Reconcile(reopened,[],new[]{"A","B","C","D"},true);
OrderCheck(newApp.IndexOf(DockEntry.App("D"))<newApp.IndexOf(DockEntry.Tool(DockOrder.Overview)),"unseen app inserted before first tool");
OrderCheck(newApp.IndexOf(DockEntry.App("A"))>newApp.IndexOf(DockEntry.Tool(DockOrder.Overview)),"new app does not reset interleaved app");
var beforeRejected=newApp.ToList();
OrderCheck(!DockOrder.MoveApp(newApp,"A",DockEntry.Tool("recycle-bin"))&&newApp.SequenceEqual(beforeRejected),"trash target rejected without changing order");
OrderCheck(!DockOrder.MoveApp(newApp,DockOrder.Overview,null),"tools are not accepted as app drag sources");
OrderCheck(!DockOrder.MoveApp(newApp,"missing",null),"stale source rejected");
OrderCheck(!DockOrder.MoveApp(newApp,"A",DockEntry.App("A")),"self-drop ignored");
var duplicate=DockOrder.Reconcile(new[]{DockEntry.App("a"),DockEntry.App("A"),DockEntry.Tool(DockOrder.Overview),DockEntry.Tool(DockOrder.Overview),DockEntry.Tool("recycle-bin")},[],new[]{"A"},true);
OrderCheck(duplicate.Count==4&&duplicate.Count(e=>e.Kind=="app")==1,"duplicate IDs normalized and fixed trash excluded");
OrderCheck(newApp.IndexOf(DockEntry.App("D"))<newApp.IndexOf(DockEntry.ToolBoundary()),"new apps stay in left section");
OrderCheck(newApp.IndexOf(DockEntry.App("A"))>newApp.IndexOf(DockEntry.ToolBoundary()),"moved apps remain in tools section");
OrderCheck(DockOrder.MoveApp(newApp,"A",DockEntry.ToolBoundary())&&newApp.IndexOf(DockEntry.App("A"))<newApp.IndexOf(DockEntry.ToolBoundary()),"app can move back to left side of separator");
Console.WriteLine($"PASS {orderChecks} mixed app/tool order scenarios");
