namespace ClaudeWatch.Rendering;

/// <summary>
/// Renders tool activity (spec §7). Prefers the tool believed to be executing now
/// (ActiveTool); when nothing is running it shows "Idle". The model distinguishes
/// ActiveTool from LastTool so this behaviour can be refined later without redesign.
/// </summary>
public sealed class ToolActivityComponent : IStatusComponent
{
    public string? Render(StatusContext ctx)
    {
        var s = ctx.Settings;
        var active = ctx.Status.ActiveTool;
        if (!string.IsNullOrWhiteSpace(active))
            return Ansi.Wrap(active.Trim(), s.Colors.ActiveTool, s.UseColor);

        // Nothing executing. Show Idle if enabled; otherwise omit the component.
        return s.ShowIdle ? Ansi.Wrap("Idle", s.Colors.Idle, s.UseColor) : null;
    }
}
