using System.Collections.Concurrent;
using System.Text;
using RetailStorePOS.Data;

namespace RetailStorePOS.UI.Common;

public static class StartupTrace
{
    private static readonly string LogPath = AppDataPaths.Combine("Logs", "winui-startup-trace.log");
    private static readonly ConcurrentQueue<string> PendingLines = new();
    private static int _flushScheduled;
    private static int _directoryCreated;

    public static void Write(string message)
    {
        try
        {
            var line = $"[{DateTime.Now:O}] {message}{Environment.NewLine}";
            System.Diagnostics.Debug.Write(line);
            PendingLines.Enqueue(line);
            ScheduleFlush();
        }
        catch
        {
        }
    }

    private static void ScheduleFlush()
    {
        if (Interlocked.CompareExchange(ref _flushScheduled, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(FlushPendingLinesAsync);
    }

    private static async Task FlushPendingLinesAsync()
    {
        try
        {
            EnsureDirectory();

            while (true)
            {
                var batch = new StringBuilder(capacity: 4096);
                while (PendingLines.TryDequeue(out var line))
                {
                    batch.Append(line);
                    if (batch.Length >= 16384)
                    {
                        break;
                    }
                }

                if (batch.Length == 0)
                {
                    return;
                }

                await File.AppendAllTextAsync(LogPath, batch.ToString(), Encoding.UTF8).ConfigureAwait(false);

                if (PendingLines.IsEmpty)
                {
                    return;
                }
            }
        }
        catch
        {
        }
        finally
        {
            Interlocked.Exchange(ref _flushScheduled, 0);
            if (!PendingLines.IsEmpty)
            {
                ScheduleFlush();
            }
        }
    }

    private static void EnsureDirectory()
    {
        if (Interlocked.CompareExchange(ref _directoryCreated, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch
        {
            Interlocked.Exchange(ref _directoryCreated, 0);
            throw;
        }
    }
}


