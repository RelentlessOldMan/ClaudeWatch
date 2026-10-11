namespace ClaudeWatch.Rendering;

/// <summary>
/// Renders the 7-day plan usage ("Week 41%"), tinted with the same severity colors and
/// thresholds as the context section. Claude Code only sends this for Pro/Max
/// subscribers and only after the first API response, so the field is omitted
/// entirely when the data is absent rather than showing a placeholder.
/// </summary>
public sealed class WeeklyUsageComponent : IStatusComponent
{
    public string? Render(StatusContext ctx)
    {
        var s = ctx.Settings;
        if (!s.ShowWeeklyUsage || ctx.Status.WeeklyPercent is not int pct) return null;

        string text = ctx.Level switch
        {
            DetailLevel.Normal => $"Week {pct}%",
            DetailLevel.ModeratelyNarrow or DetailLevel.Narrow => $"Wk {pct}%",
            _ => $"W{pct}%",
        };
        return Ansi.Wrap(text, ContextComponent.SeverityColor(pct, s), s.UseColor);
    }
}
