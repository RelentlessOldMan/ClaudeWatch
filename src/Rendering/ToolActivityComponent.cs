namespace ClaudeWatch.Rendering;

/// <summary>
/// Renders tool activity (spec §7). Three states:
///   - a named tool is executing  -> the tool name (ActiveTool)
///   - Claude is working between tools (turn in progress, spinner still going) -> "Working"
///   - the turn is finished, waiting for the user -> "Idle"
/// The model distinguishes ActiveTool / LastTool / TurnInProgress so this behaviour can
/// be refined later without redesign.
/// </summary>
public sealed class ToolActivityComponent : IStatusComponent
{
    public string? Render(StatusContext ctx)
    {
        var s = ctx.Settings;
        var st = ctx.Status;

        var active = st.ActiveTool;
        if (!string.IsNullOrWhiteSpace(active))
            return Ansi.Wrap(active.Trim(), s.Colors.ActiveTool, s.UseColor);

        // No named tool, but Claude still owes a response -> it's working, not idle.
        if (st.TurnInProgress && !string.IsNullOrWhiteSpace(s.WorkingText))
            return Ansi.Wrap(s.WorkingText.Trim(), s.Colors.Working, s.UseColor);

        // Turn finished, waiting for the user. Show Idle if enabled; otherwise omit.
        return s.ShowIdle ? Ansi.Wrap("Idle", s.Colors.Idle, s.UseColor) : null;
    }
}
