using System;
using System.IO;
using System.Text;

namespace GlassDock;

internal static class StartupDiagnostics
{
    internal static string Path => System.IO.Path.Combine(SettingsStore.DirectoryPath, "startup.log");

    internal static void Write(string message, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.DirectoryPath);
            var line = $"{DateTimeOffset.Now:O} pid={Environment.ProcessId} {message}";
            if (exception is not null) line += Environment.NewLine + exception;
            File.AppendAllText(Path, line + Environment.NewLine, Encoding.UTF8);
        }
        catch { }
    }
}
