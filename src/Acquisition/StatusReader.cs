using System.Text;
using System.Text.Json;
using ClaudeWatch.Config;
using ClaudeWatch.Diagnostics;
using ClaudeWatch.Model;

namespace ClaudeWatch.Acquisition;

/// <summary>
/// Converts the structured data Claude Code exposes (spec §11) into a
/// <see cref="ClaudeStatus"/>. Claude-provided data is treated as untrusted: fields
/// may be missing, null, renamed, or unfamiliar, and none of that may crash us.
/// This class never throws; on failure it returns a status full of nulls.
/// </summary>
public static class StatusReader
{
    // Only the tail of the transcript matters for current usage/tool, and reading
    // the whole file would violate the performance budget (spec §12). 128 KiB is
    // comfortably enough to contain the latest turn.
    private const int TranscriptTailBytes = 128 * 1024;

    public static ClaudeStatus Read(string stdinJson, Settings settings)
    {
        string? modelDisplay = null;
        string? modelId = null;
        string? cwd = null;
        string? projectDir = null;
        string? transcriptPath = null;

        // Context data straight from Claude Code (authoritative). context_window_size
        // is the real window (200k vs 1M); used_percentage matches Claude's own meter.
        long? cwSize = null;
        long? cwUsed = null;
        int? cwPercent = null;

        try
        {
            // Skip a leading UTF-8 BOM (U+FEFF) / zero-width space (U+200B)
            // that some shells prepend when piping.
            var clean = StripLeadingBom(stdinJson ?? string.Empty).Trim();
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(clean) ? "{}" : clean);
            var root = doc.RootElement;

            if (root.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.Object)
            {
                modelDisplay = GetString(model, "display_name");
                modelId = GetString(model, "id");
            }

            // current_dir follows the shell as Claude cd's around; project_dir is where
            // Claude Code was launched and stays fixed. Fall back to top-level cwd.
            if (root.TryGetProperty("workspace", out var ws) && ws.ValueKind == JsonValueKind.Object)
            {
                cwd = GetString(ws, "current_dir");
                projectDir = GetString(ws, "project_dir");
            }
            cwd ??= GetString(root, "cwd") ?? projectDir;

            transcriptPath = GetString(root, "transcript_path");

            if (root.TryGetProperty("context_window", out var cw) && cw.ValueKind == JsonValueKind.Object)
            {
                cwSize = PositiveOrNull(GetLong(cw, "context_window_size"));
                // total_input_tokens = tokens currently in the window (incl. cache).
                cwUsed = PositiveOrNull(GetLong(cw, "total_input_tokens"));
                cwPercent = GetPercent(cw, "used_percentage");
            }
        }
        catch (Exception e)
        {
            Log.Error("Failed to parse status stdin JSON", e);
        }

        // The transcript is still needed for tool activity (not in stdin) and as a
        // fallback for token usage on older Claude Code versions without context_window.
        var (transcriptUsed, activeTool, lastTool, turnInProgress) = ReadTranscript(transcriptPath);

        long capacity = cwSize ?? InferCapacity(modelId, modelDisplay, settings);
        long? used = cwUsed ?? transcriptUsed;
        // Derive the percentage from the same token count we display, so the label, bar,
        // and percent always agree. Claude's pre-calculated used_percentage can briefly
        // disagree with the reported token count in transient states (e.g. right after
        // /compact), so it's only a fallback for when we have no token count at all.
        int? percent = ComputePercent(used, capacity) ?? cwPercent;

        var (fullDir, dirName) = SplitDirectory(cwd);
        var (projectFull, projectName) = SplitDirectory(projectDir);

        return new ClaudeStatus
        {
            Model = modelDisplay ?? modelId,
            ContextUsed = used,
            // Capacity is inferred from the model, so it's known even when usage isn't;
            // this lets the context label show the window size (e.g. "Context (1M)").
            ContextCapacity = capacity,
            ContextPercent = percent,
            WorkingDirectory = fullDir,
            WorkingDirectoryName = dirName,
            ProjectDirectory = projectFull,
            ProjectDirectoryName = projectName,
            ActiveTool = activeTool,
            LastTool = lastTool,
            TurnInProgress = turnInProgress,
        };
    }

    /// <summary>
    /// Fallback context window for older Claude Code versions that don't send
    /// <c>context_window</c>. A size token in the model id or display name is trusted
    /// (e.g. "[1m]", "(1M context)", "(200k)"); otherwise the standard 200k default.
    /// Deliberately does NOT depend on current usage, so the window can't flip-flop.
    /// </summary>
    private static long InferCapacity(string? modelId, string? modelDisplay, Settings settings)
    {
        return ParseSizeToken(modelDisplay)
               ?? ParseSizeToken(modelId)
               ?? settings.DefaultContextCapacity;
    }

    /// <summary>
    /// Extracts a token like "1m" / "200k" / "128000" from text and returns it as a
    /// token count, or null if none is present. Case-insensitive.
    /// </summary>
    private static long? ParseSizeToken(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var s = text.ToLowerInvariant();
        for (int i = 0; i < s.Length; i++)
        {
            if (!char.IsDigit(s[i])) continue;
            int j = i;
            while (j < s.Length && char.IsDigit(s[j])) j++;
            if (!long.TryParse(s[i..j], out long n)) { i = j; continue; }

            char suffix = j < s.Length ? s[j] : '\0';
            if (suffix == 'm') return n * 1_000_000;
            if (suffix == 'k') return n * 1_000;
            if (n >= 100_000) return n; // a bare large number, e.g. "128000"
            i = j; // otherwise it's a version number etc.; skip it
        }
        return null;
    }

    private static int? ComputePercent(long? used, long capacity)
    {
        if (used is null || capacity <= 0) return null;
        double pct = 100.0 * used.Value / capacity;
        int rounded = (int)Math.Round(pct, MidpointRounding.AwayFromZero);
        return Math.Clamp(rounded, 0, 100); // clamp to 0-100 (spec §5.3)
    }

    private static (string? full, string? name) SplitDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return (null, null);
        try
        {
            var trimmed = path.TrimEnd('/', '\\');
            if (trimmed.Length == 0) return (path, path); // root
            int slash = trimmed.LastIndexOfAny(new[] { '/', '\\' });
            var name = slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
            return (path, name.Length > 0 ? name : trimmed);
        }
        catch (Exception e)
        {
            Log.Error("Failed to split working directory", e);
            return (path, path);
        }
    }

    /// <summary>
    /// Reads the tail of the transcript JSONL to recover current context size, tool
    /// activity, and whether Claude's turn is still in progress. Any of the first three
    /// may be null. Never throws.
    /// </summary>
    private static (long? used, string? active, string? last, bool turnInProgress) ReadTranscript(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (null, null, null, false);

        string text;
        try
        {
            if (!File.Exists(path)) return (null, null, null, false);
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long length = fs.Length;
            int toRead = (int)Math.Min(length, TranscriptTailBytes);
            fs.Seek(length - toRead, SeekOrigin.Begin);
            var buffer = new byte[toRead];
            int read = fs.Read(buffer, 0, toRead);
            text = Encoding.UTF8.GetString(buffer, 0, read);
            // If we seeked into the middle of a line, drop the leading partial line.
            if (toRead < length)
            {
                int nl = text.IndexOf('\n');
                if (nl >= 0) text = text[(nl + 1)..];
            }
        }
        catch (Exception e)
        {
            Log.Error("Failed to read transcript tail", e);
            return (null, null, null, false);
        }

        long? used = null;
        var toolOrder = new List<(string id, string name)>(); // tool_use blocks in order
        var answered = new HashSet<string>();                 // tool_use_ids that got a result
        string? lastRole = null;                              // role of the last user/assistant entry

        foreach (var raw in text.Split('\n'))
        {
            // .Trim() does not strip U+FEFF/U+200B in modern .NET, so do it explicitly.
            var line = StripLeadingBom(raw.Trim());
            if (line.Length == 0) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var type = GetString(root, "type");
                if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
                    continue;

                if (type == "assistant")
                {
                    lastRole = "assistant";
                    // Latest usage wins (forward scan) -> current context occupancy.
                    if (message.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
                    {
                        long u = GetLong(usage, "input_tokens")
                                 + GetLong(usage, "cache_read_input_tokens")
                                 + GetLong(usage, "cache_creation_input_tokens");
                        if (u > 0) used = u;
                    }

                    if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var block in content.EnumerateArray())
                        {
                            if (block.ValueKind != JsonValueKind.Object) continue;
                            if (GetString(block, "type") == "tool_use")
                            {
                                var name = GetString(block, "name");
                                var id = GetString(block, "id") ?? name ?? "?";
                                if (name is not null) toolOrder.Add((id, name));
                            }
                        }
                    }
                }
                else if (type == "user")
                {
                    lastRole = "user";
                    // Record tool_results so we can tell which tool_use is still pending.
                    if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var block in content.EnumerateArray())
                        {
                            if (block.ValueKind != JsonValueKind.Object) continue;
                            if (GetString(block, "type") == "tool_result")
                            {
                                var id = GetString(block, "tool_use_id");
                                if (id is not null) answered.Add(id);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Skipping malformed transcript line", e);
            }
        }

        string? lastTool = toolOrder.Count > 0 ? toolOrder[^1].name : null;

        // ActiveTool = the most recent tool_use with no matching tool_result yet.
        string? activeTool = null;
        for (int i = toolOrder.Count - 1; i >= 0; i--)
        {
            if (!answered.Contains(toolOrder[i].id))
            {
                activeTool = toolOrder[i].name;
                break;
            }
        }

        // A trailing user entry (fresh prompt or an unanswered tool result) means Claude
        // still owes a response — the turn is in progress.
        bool turnInProgress = lastRole == "user";

        return (used, activeTool, lastTool, turnInProgress);
    }

    // U+FEFF (BOM) and U+200B (zero-width space) are not stripped by string.Trim()
    // in modern .NET, yet shells and some writers prepend them; skip them explicitly.
    private const char Bom = '\uFEFF';
    private const char ZeroWidth = '\u200B';

    private static string StripLeadingBom(string s)
    {
        int i = 0;
        while (i < s.Length && (s[i] == Bom || s[i] == ZeroWidth)) i++;
        return i == 0 ? s : s[i..];
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static long GetLong(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)
            ? n
            : 0;

    private static long? PositiveOrNull(long v) => v > 0 ? v : null;

    /// <summary>Reads a percentage (may be null / fractional), rounded and clamped to 0-100.</summary>
    private static int? GetPercent(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Number ||
            !v.TryGetDouble(out var d))
            return null;
        return Math.Clamp((int)Math.Round(d, MidpointRounding.AwayFromZero), 0, 100);
    }
}
