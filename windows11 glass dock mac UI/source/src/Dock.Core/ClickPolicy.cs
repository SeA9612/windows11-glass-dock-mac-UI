namespace GlassDock.Core;
public enum WindowAction { Launch, Activate, MinimizeOne, MinimizeAll, RestoreAll }
public sealed record WindowState(long Id, bool Minimized);
public sealed class ClickPolicy
{
    private string? armed;
    public void Reset() => armed = null;
    public WindowAction Decide(string? app, IReadOnlyList<WindowState>? windows, long foreground)
    {
        if (string.IsNullOrWhiteSpace(app))
        {
            Reset();
            return windows is { Count: > 0 } ? WindowAction.Activate : WindowAction.Launch;
        }

        var normalizedApp = app.Trim();
        if (windows is null || windows.Count == 0) { Reset(); return WindowAction.Launch; }
        if (windows.All(w => w.Minimized)) { Reset(); return WindowAction.RestoreAll; }
        if (string.Equals(armed, normalizedApp, StringComparison.OrdinalIgnoreCase)) { Reset(); return WindowAction.MinimizeAll; }
        if (windows.Any(w => w.Id == foreground)) { armed = normalizedApp; return WindowAction.MinimizeOne; }
        Reset(); return WindowAction.Activate;
    }
}
