using System.Text;
using RetailStorePOS.Data;

namespace RetailStorePOS.WinUiLogin.Common;

internal static class StartupTrace
{
    private static readonly object SyncRoot = new();
    private static readonly string LogPath = AppDataPaths.Combine("Logs", "winui-startup-trace.log");

    public static void Write(string message)
    {
        try
        {
            var line = $"[{DateTime.Now:O}] {message}{Environment.NewLine}";
            lock (SyncRoot)
            {
                var directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(LogPath, line, Encoding.UTF8);
                System.Diagnostics.Debug.Write(line);
            }
        }
        catch
        {
        }
    }
}


