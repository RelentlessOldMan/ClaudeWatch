namespace ClaudeWatch.Diagnostics;

/// <summary>
/// Diagnostics go to a log file, never to stdout (spec §13). Logging must itself
/// never throw — a failure to log can never be allowed to break the status line.
/// </summary>
public static class Log
{
    private static readonly string LogPath = ResolvePath();

    private static string ResolvePath()
    {
        try
        {
            var dir = Environment.GetEnvironmentVariable("TEMP")
                      ?? Environment.GetEnvironmentVariable("TMPDIR")
                      ?? Path.GetTempPath();
            return Path.Combine(dir, "claudewatch.log");
        }
        catch
        {
            return "claudewatch.log";
        }
    }

    public static void Error(string message, Exception? ex = null)
    {
        try
        {
            var line = ex is null
                ? $"{Stamp()} {message}{Environment.NewLine}"
                : $"{Stamp()} {message}: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}";
            File.AppendAllText(LogPath, line);
        }
        catch
        {
            // Diagnostics are strictly best-effort; swallow everything.
        }
    }

    private static string Stamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}
