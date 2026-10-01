namespace ClaudeWatch.Model;

/// <summary>
/// The small internal status model (spec §10). Data acquisition fills this in;
/// rendering only ever reads it. Every field is nullable so that missing data is
/// represented explicitly and degrades gracefully rather than crashing (spec §11, §13).
/// </summary>
public sealed class ClaudeStatus
{
    /// <summary>Human-readable model name, e.g. "Opus 4.1". Null if unknown.</summary>
    public string? Model { get; init; }

    /// <summary>Tokens currently occupying the context window. Null if unknown.</summary>
    public long? ContextUsed { get; init; }

    /// <summary>Total context window capacity for the active model. Null if unknown.</summary>
    public long? ContextCapacity { get; init; }

    /// <summary>Whole-number percentage (0-100) of context used. Null if unknown.</summary>
    public int? ContextPercent { get; init; }

    /// <summary>Full working directory path (retained for future/diagnostic use, spec §6.1).</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Final path component of <see cref="WorkingDirectory"/>. Null if unknown.</summary>
    public string? WorkingDirectoryName { get; init; }

    /// <summary>Tool believed to be executing right now, or null if idle/unknown (spec §7.1).</summary>
    public string? ActiveTool { get; init; }

    /// <summary>Most recently used tool, regardless of whether it is still running (spec §7.2).</summary>
    public string? LastTool { get; init; }

    /// <summary>
    /// True when Claude owes a response — the last transcript entry is a user message
    /// (a fresh prompt or an unanswered tool result), i.e. the spinner is still going.
    /// Distinguishes "working between tools" from genuinely idle/waiting for the user.
    /// </summary>
    public bool TurnInProgress { get; init; }
}
