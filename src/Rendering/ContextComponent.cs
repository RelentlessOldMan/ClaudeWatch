using System.Text;
using ClaudeWatch.Config;
using ClaudeWatch.Model;

namespace ClaudeWatch.Rendering;

/// <summary>
/// Renders context usage — the most visually distinctive component (spec §5).
/// Adapts to the detail level: full bar, shortened bar, percentage only, or bare
/// percentage (spec §9). The whole section (label, bar, and percentage) is tinted a
/// single usage-severity color: green -> orange -> red. The label carries usage over
/// the window size, e.g. "Context (120k/1M)".
/// </summary>
public sealed class ContextComponent : IStatusComponent
{
    public string? Render(StatusContext ctx)
    {
        var s = ctx.Settings;
        var st = ctx.Status;
        int? pct = st.ContextPercent;

        // Moderately-narrow uses roughly half the configured bar width.
        int narrowBar = Math.Max(4, s.ContextBarWidth / 2);

        string plain = ctx.Level switch
        {
            DetailLevel.Normal => Compose(Label(false, st, s), Bar(pct, s.ContextBarWidth, s), Percent(pct)),
            DetailLevel.ModeratelyNarrow => Compose(Label(true, st, s), Bar(pct, narrowBar, s), Percent(pct)),
            DetailLevel.Narrow => Compose(Label(true, st, s, withCapacity: false), null, Percent(pct)),
            DetailLevel.VeryNarrow => Percent(pct),
            _ => Compose(Label(false, st, s), Bar(pct, s.ContextBarWidth, s), Percent(pct)),
        };

        // Tint the entire section one severity color.
        return Ansi.Wrap(plain, SeverityColor(pct, s), s.UseColor);
    }

    private static string Compose(string label, string? bar, string percent)
    {
        var sb = new StringBuilder();
        if (label.Length > 0) sb.Append(label).Append(' ');
        if (bar is not null) sb.Append(bar).Append(' ');
        sb.Append(percent);
        return sb.ToString();
    }

    private static string Label(bool shortForm, ClaudeStatus st, Settings s, bool withCapacity = true)
    {
        if (!s.ShowContextLabel) return "";
        var text = shortForm ? "Ctx" : "Context";
        if (withCapacity && s.ShowContextCapacity && st.ContextCapacity is long cap && cap > 0)
        {
            // "(used/capacity)", e.g. "(120k/1M)". Usage is "?" when unknown.
            var used = st.ContextUsed is long u && u >= 0 ? HumanTokens(u) : "?";
            text += $" ({used}/{HumanTokens(cap)})";
        }
        return text;
    }

    private static string Percent(int? pct) => pct is null ? "?%" : $"{pct}%";

    private static string Bar(int? pct, int width, Settings s)
    {
        var sb = new StringBuilder(width + 2);
        sb.Append('[');
        if (pct is null)
        {
            sb.Append(new string(s.BarUnknown, width)); // [????...]
        }
        else
        {
            int filled = (int)Math.Round(width * (pct.Value / 100.0), MidpointRounding.AwayFromZero);
            filled = Math.Clamp(filled, 0, width);
            sb.Append(new string(s.BarFilled, filled));
            sb.Append(new string(s.BarEmptyGlyph, width - filled));
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string SeverityColor(int? pct, Settings s)
    {
        if (pct is null) return s.Colors.Idle; // dim when usage is unknown
        if (pct.Value > s.ContextHighPercent) return s.Colors.ContextHigh;
        if (pct.Value > s.ContextMedPercent) return s.Colors.ContextMed;
        return s.Colors.ContextLow;
    }

    /// <summary>
    /// Human-readable token count: 1000000 -> "1M", 200000 -> "200k", 84532 -> "85k".
    /// Sub-1000 values are shown as-is.
    /// </summary>
    private static string HumanTokens(long v)
    {
        if (v >= 1_000_000)
        {
            double m = v / 1_000_000.0;
            return m.ToString("0.#") + "M";
        }
        if (v >= 1_000)
        {
            long k = (long)Math.Round(v / 1_000.0, MidpointRounding.AwayFromZero);
            return k + "k";
        }
        return v.ToString();
    }
}
