namespace ClaudeWatch.Rendering;

/// <summary>
/// Renders the current model name (spec §4). "Model ?" when unknown. A trailing
/// context-window note such as "Opus 4.8 (1M context)" is stripped here — the window
/// size is surfaced by the context component instead — so the model stays terse.
/// </summary>
public sealed class ModelComponent : IStatusComponent
{
    public string? Render(StatusContext ctx)
    {
        var model = ctx.Status.Model;
        var text = string.IsNullOrWhiteSpace(model) ? "Model ?" : StripContextNote(model.Trim());
        return Ansi.Wrap(text, ctx.Settings.Colors.Model, ctx.Settings.UseColor);
    }

    /// <summary>
    /// Removes a trailing parenthetical that describes the context window, e.g.
    /// "Opus 4.8 (1M context)" -> "Opus 4.8", or "(200k)". Leaves other parentheticals
    /// (which aren't context notes) untouched.
    /// </summary>
    private static string StripContextNote(string model)
    {
        if (!model.EndsWith(")", StringComparison.Ordinal)) return model;
        int open = model.LastIndexOf('(');
        if (open <= 0) return model;

        var inside = model[(open + 1)..^1];
        return LooksLikeContextNote(inside) ? model[..open].TrimEnd() : model;
    }

    private static bool LooksLikeContextNote(string inside)
    {
        var lower = inside.ToLowerInvariant();
        if (lower.Contains("context") || lower.Contains("token")) return true;

        // A bare size token like "1m" / "200k".
        for (int i = 0; i + 1 < lower.Length; i++)
            if (char.IsDigit(lower[i]) && (lower[i + 1] == 'k' || lower[i + 1] == 'm'))
                return true;
        return false;
    }
}
