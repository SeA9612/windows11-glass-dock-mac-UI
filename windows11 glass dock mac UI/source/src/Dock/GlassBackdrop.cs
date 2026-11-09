using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;

namespace GlassDock;

internal static class GlassBackdrop
{
    internal static bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);
    internal static string MaterialDescription { get; private set; } = "translucent WPF gradient fallback";
    static HostBackdrop? host;

    internal static void Prepare(Window window)
    {
        window.AllowsTransparency = true;
        window.Background = Brushes.Transparent;
        WindowChrome.SetWindowChrome(window, null);
    }

    internal static bool Apply(Window window)
    {
        if (!IsSupported) return false;
        try
        {
            host = new HostBackdrop();
            window.Closed += (_, _) => { host?.Dispose(); host = null; };
            MaterialDescription = "Windows Composition HostBackdrop; separate native HWND and shadow";
            return true;
        }
        catch (Exception error)
        {
            SettingsStore.Log(error);
            StartupDiagnostics.Write("Native backdrop failed; gradient fallback: " + error.Message);
            return false;
        }
    }

    internal static void Sync(Window window) => host?.Sync(window);
    internal static void Fade(float opacity, int milliseconds) => host?.Fade(opacity, milliseconds);
}
