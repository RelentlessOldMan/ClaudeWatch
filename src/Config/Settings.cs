namespace ClaudeWatch.Config;

/// <summary>
/// How the working directory is presented. Only NameOnly is used by default, but the
/// enum exists so the renderer never hardcodes the choice (spec §6.1, §14).
/// </summary>
public enum DirectoryMode
{
    NameOnly,
    FullPath,
}

/// <summary>
/// Colors for each element of the status line, as color names (e.g. "brightCyan",
/// "orange") or raw ANSI SGR params (e.g. "38;5;208"). "default"/"none"/"" mean no
/// color. Overridable via the config file. See <see cref="Rendering.Ansi"/>.
/// </summary>
public sealed record Palette
{
    public string Model { get; init; } = "brightCyan";
    public string Directory { get; init; } = "brightBlue";
    public string ActiveTool { get; init; } = "brightWhite";
    public string Working { get; init; } = "brightWhite";
    public string Idle { get; init; } = "brightBlack";
    public string Separator { get; init; } = "brightBlack";

    // The whole context section is tinted one of these by usage severity
    // (green -> orange -> red by default).
    public string ContextLow { get; init; } = "green";
    public string ContextMed { get; init; } = "orange";
    public string ContextHigh { get; init; } = "red";
}

/// <summary>
/// Minimal configuration (spec §14). These are defaults; <see cref="ConfigLoader"/>
/// overrides them from an optional JSON file. Values live here rather than being
/// buried inside rendering logic.
/// </summary>
public sealed record Settings
{
    public int ContextBarWidth { get; init; } = 10;
    public bool UseUnicode { get; init; } = true;
    public bool UseColor { get; init; } = true;
    public bool ShowContextLabel { get; init; } = true;
    public bool ShowContextCapacity { get; init; } = true;
    public bool ShowIdle { get; init; } = true;

    /// <summary>Label shown when Claude is working but no named tool is running.</summary>
    public string WorkingText { get; init; } = "Working";
    public DirectoryMode DirectoryMode { get; init; } = DirectoryMode.NameOnly;

    /// <summary>Fallback context window when it cannot be inferred from the model.</summary>
    public long DefaultContextCapacity { get; init; } = 200_000;

    // Percentage boundaries for the context severity colors: <=Med green, <=High
    // orange, above High red. Defaults: green to 50%, orange to 80%, then red.
    public int ContextMedPercent { get; init; } = 50;
    public int ContextHighPercent { get; init; } = 80;

    public Palette Colors { get; init; } = new();

    public static Settings Default { get; } = new();

    // Glyphs are derived from UseUnicode so components don't each re-decide.
    public char BarFilled => UseUnicode ? '█' : '#'; // █
    public char BarEmptyGlyph => UseUnicode ? '░' : '-'; // ░
    public char BarUnknown => '?';
    public string Separator => UseUnicode ? " │ " : " | "; // │
}
