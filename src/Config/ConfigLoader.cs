using System.Text.Json;
using ClaudeWatch.Diagnostics;

namespace ClaudeWatch.Config;

/// <summary>
/// Loads <see cref="Settings"/> from an optional JSON config file, falling back to
/// defaults for anything not specified. Search order:
///   1. %CLAUDEWATCH_CONFIG% (explicit file path)
///   2. claudewatch.json next to the executable
///   3. %USERPROFILE%\.claude\claudewatch.json
/// The NO_COLOR environment variable disables color regardless of the file.
/// Never throws; malformed or missing config degrades to defaults (spec §11, §13).
/// </summary>
public static class ConfigLoader
{
    public static Settings Load()
    {
        var s = Settings.Default;
        try
        {
            var path = FindPath();
            if (path is not null && File.Exists(path))
                s = Apply(s, File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Log.Error("Failed to load config file", e);
        }

        // NO_COLOR (any non-empty value) disables color, per the informal standard.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))
            s = s with { UseColor = false };

        return s;
    }

    private static string? FindPath()
    {
        var explicitPath = Environment.GetEnvironmentVariable("CLAUDEWATCH_CONFIG");
        if (!string.IsNullOrWhiteSpace(explicitPath)) return explicitPath;

        try
        {
            var beside = Path.Combine(AppContext.BaseDirectory, "claudewatch.json");
            if (File.Exists(beside)) return beside;
        }
        catch { /* ignore */ }

        var home = Environment.GetEnvironmentVariable("USERPROFILE")
                   ?? Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrWhiteSpace(home))
        {
            var p = Path.Combine(home, ".claude", "claudewatch.json");
            if (File.Exists(p)) return p;
        }
        return null;
    }

    private static Settings Apply(Settings s, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return s;

        s = s with
        {
            ContextBarWidth = Int(root, "contextBarWidth", s.ContextBarWidth),
            UseUnicode = Bool(root, "useUnicode", s.UseUnicode),
            UseColor = Bool(root, "useColor", s.UseColor),
            ShowContextLabel = Bool(root, "showContextLabel", s.ShowContextLabel),
            ShowContextCapacity = Bool(root, "showContextCapacity", s.ShowContextCapacity),
            ShowIdle = Bool(root, "showIdle", s.ShowIdle),
            WorkingText = Str(root, "workingText") ?? s.WorkingText,
            ContextMedPercent = Int(root, "contextMedPercent", s.ContextMedPercent),
            ContextHighPercent = Int(root, "contextHighPercent", s.ContextHighPercent),
            DirectoryMode = ParseDirMode(Str(root, "directoryMode"), s.DirectoryMode),
        };

        if (root.TryGetProperty("colors", out var c) && c.ValueKind == JsonValueKind.Object)
        {
            var p = s.Colors;
            s = s with
            {
                Colors = p with
                {
                    Model = Str(c, "model") ?? p.Model,
                    Directory = Str(c, "directory") ?? p.Directory,
                    ActiveTool = Str(c, "activeTool") ?? p.ActiveTool,
                    Working = Str(c, "working") ?? p.Working,
                    Idle = Str(c, "idle") ?? p.Idle,
                    Separator = Str(c, "separator") ?? p.Separator,
                    ContextLow = Str(c, "contextLow") ?? p.ContextLow,
                    ContextMed = Str(c, "contextMed") ?? p.ContextMed,
                    ContextHigh = Str(c, "contextHigh") ?? p.ContextHigh,
                }
            };
        }

        return s;
    }

    private static int Int(JsonElement obj, string name, int fallback) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)
            ? n : fallback;

    private static bool Bool(JsonElement obj, string name, bool fallback) =>
        obj.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
            ? v.GetBoolean() : fallback;

    private static string? Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static DirectoryMode ParseDirMode(string? value, DirectoryMode fallback) =>
        value is null ? fallback
        : value.Equals("FullPath", StringComparison.OrdinalIgnoreCase) ? DirectoryMode.FullPath
        : value.Equals("NameOnly", StringComparison.OrdinalIgnoreCase) ? DirectoryMode.NameOnly
        : fallback;
}
