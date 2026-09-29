using System.Text;
using ClaudeWatch.Config;
using ClaudeWatch.Diagnostics;
using ClaudeWatch.Model;

namespace ClaudeWatch.Rendering;

/// <summary>
/// Combines the components into the final status line (spec §8, §10) and applies
/// terminal-width handling (spec §9): it renders the line at successively more
/// compact detail levels and picks the richest one that fits the available width.
/// A single component failing is isolated — it is omitted rather than breaking the
/// whole line (spec §13).
/// </summary>
public sealed class StatusLineRenderer
{
    private readonly Settings _settings;
    private readonly IReadOnlyList<IStatusComponent> _components;

    // Detail levels from richest to most compact (spec §9 ordering).
    private static readonly DetailLevel[] Levels =
    {
        DetailLevel.Normal,
        DetailLevel.ModeratelyNarrow,
        DetailLevel.Narrow,
        DetailLevel.VeryNarrow,
    };

    public StatusLineRenderer(Settings settings)
        : this(settings, DefaultComponents())
    {
    }

    public StatusLineRenderer(Settings settings, IReadOnlyList<IStatusComponent> components)
    {
        _settings = settings;
        _components = components;
    }

    // Default layout ordering: MODEL │ CONTEXT │ DIRECTORY │ ACTIVITY (spec §8).
    private static IReadOnlyList<IStatusComponent> DefaultComponents() => new IStatusComponent[]
    {
        new ModelComponent(),
        new ContextComponent(),
        new DirectoryComponent(),
        new ToolActivityComponent(),
    };

    public string Render(ClaudeStatus status)
    {
        int width = TerminalWidth();

        string best = string.Empty;
        foreach (var level in Levels)
        {
            best = RenderAtLevel(status, level);
            // Unknown width -> don't compress; the first (richest) level wins.
            if (width <= 0 || VisibleWidth(best) <= width)
                return best;
        }
        // Nothing fit; the most compact rendering is the best we can do (spec §9:
        // never truncate into something confusing — a slightly-too-long minimal
        // line is clearer than a chopped one).
        return best;
    }

    private string RenderAtLevel(ClaudeStatus status, DetailLevel level)
    {
        var parts = new List<string>(_components.Count);
        foreach (var component in _components)
        {
            string? rendered;
            try
            {
                rendered = component.Render(new StatusContext(status, _settings, level));
            }
            catch (Exception e)
            {
                // Isolate the failure: omit this component only.
                Log.Error($"Component {component.GetType().Name} failed", e);
                rendered = null;
            }
            if (!string.IsNullOrEmpty(rendered))
                parts.Add(rendered);
        }
        var separator = Ansi.Wrap(_settings.Separator, _settings.Colors.Separator, _settings.UseColor);
        return string.Join(separator, parts);
    }

    /// <summary>Visible width, ignoring color escape sequences so width tiers fit correctly.</summary>
    private static int VisibleWidth(string s) => Ansi.VisibleLength(s);

    /// <summary>
    /// Best-effort available width. When stdout is redirected (the normal case for a
    /// status-line command) Console.WindowWidth may throw or be meaningless, so we
    /// fall back to the COLUMNS env var and finally to "unknown" (no compression).
    /// </summary>
    private static int TerminalWidth()
    {
        try
        {
            int w = Console.WindowWidth;
            if (w > 0) return w;
        }
        catch
        {
            // redirected / no console — fall through
        }

        var cols = Environment.GetEnvironmentVariable("COLUMNS");
        if (int.TryParse(cols, out int envWidth) && envWidth > 0)
            return envWidth;

        return 0; // unknown -> render at Normal
    }
}
