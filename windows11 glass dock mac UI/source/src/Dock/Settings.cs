using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace GlassDock;
public sealed class DockSettings
{
    public string Edge { get; set; } = "Bottom";
    public string Monitor { get; set; } = "";
    public bool AutoHide { get; set; }
    public bool HoverPreviews { get; set; } = true;
    // Genie minimize effect. Mode "Icon": window goes into its app icon.
    // Mode "Thumbnail": window stays in the dock as a thumbnail next to the trash.
    public bool GenieEnabled { get; set; } = true;
    public string GenieMode { get; set; } = "Icon";
    public double GenieSpeed { get; set; } = 1.0;
    public List<string> Pins { get; set; } = new();
    public List<string> Order { get; set; } = new();
    public int LayoutVersion { get; set; }
    public List<GlassDock.Core.DockEntry> LayoutOrder { get; set; } = new();
    public List<string> Services { get; set; } = new();
    public string SelectedService { get; set; } = "";
    public string AiLaunchPath { get; set; } = "";
    public Dictionary<string,string> Drafts { get; set; } = new();
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string,JsonElement>? AdditionalSettings { get; set; }
}
public static class SettingsStore
{
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlassDock");
    public static DockSettings Load()
    {
        try { return JsonSerializer.Deserialize<DockSettings>(File.ReadAllText(Path.Combine(DirectoryPath,"settings.json"))) ?? new(); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { Log(e); return new(); }
    }
    public static void Save(DockSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        string path = Path.Combine(DirectoryPath,"settings.json");
        File.WriteAllText(path+".tmp", JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented=true }));
        File.Move(path+".tmp",path,true);
    }
    public static void Log(Exception e) { try { Directory.CreateDirectory(DirectoryPath); File.AppendAllText(Path.Combine(DirectoryPath,"errors.log"), DateTimeOffset.Now+" "+e+Environment.NewLine); } catch { } }
}
