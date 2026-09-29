namespace ClaudeWatch.Rendering;

/// <summary>
/// Small ANSI color helper. Colors are given as friendly names or raw SGR params, so
/// the config file can say "orange", "brightCyan", or "38;5;208". Rendering is
/// width-aware elsewhere, so <see cref="VisibleLength"/> exists to measure text while
/// ignoring the (zero-width) escape sequences.
/// </summary>
public static class Ansi
{
    private const string Esc = "\x1b[";
    public const string Reset = "\x1b[0m";

    // Friendly name -> SGR foreground parameter(s).
    private static readonly Dictionary<string, string> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "30", ["red"] = "31", ["green"] = "32", ["yellow"] = "33",
        ["blue"] = "34", ["magenta"] = "35", ["cyan"] = "36", ["white"] = "37",
        ["brightblack"] = "90", ["gray"] = "90", ["grey"] = "90",
        ["brightred"] = "91", ["brightgreen"] = "92", ["brightyellow"] = "93",
        ["brightblue"] = "94", ["brightmagenta"] = "95", ["brightcyan"] = "96",
        ["brightwhite"] = "97",
        // No true orange in the basic palette; use a 256-color code.
        ["orange"] = "38;5;208",
    };

    /// <summary>Wraps text in a color, or returns it unchanged if color is disabled/none.</summary>
    public static string Wrap(string text, string? color, bool useColor)
    {
        if (!useColor) return text;
        var code = Resolve(color);
        return code is null ? text : $"{Esc}{code}m{text}{Reset}";
    }

    private static string? Resolve(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return null;
        color = color.Trim();
        if (color.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            color.Equals("default", StringComparison.OrdinalIgnoreCase))
            return null;
        if (Named.TryGetValue(color, out var code)) return code;
        if (IsSgr(color)) return color; // raw params like "91" or "38;5;208"
        return null;                    // unknown -> render uncolored, never crash
    }

    private static bool IsSgr(string s)
    {
        foreach (var c in s)
            if (!char.IsDigit(c) && c != ';') return false;
        return s.Length > 0;
    }

    /// <summary>Visible character count, skipping ESC-[ ... m sequences (spec §9 width tiers).</summary>
    public static int VisibleLength(string s)
    {
        int len = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\x1b' && i + 1 < s.Length && s[i + 1] == '[')
            {
                i += 2;
                while (i < s.Length && s[i] != 'm') i++;
                continue; // the for-loop's i++ steps past the 'm'
            }
            len++;
        }
        return len;
    }
}
