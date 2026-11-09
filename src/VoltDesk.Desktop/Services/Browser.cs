using System.Diagnostics;

namespace VoltDesk.Desktop.Services;

public static class Browser
{
    /// <summary>Opens a web link (a Drive folder, a sheet) in the default browser.</summary>
    public static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
