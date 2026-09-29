using ClaudeWatch.Config;
using ClaudeWatch.Model;

namespace ClaudeWatch.Rendering;

/// <summary>
/// How much detail a component should render, driven by available terminal width
/// (spec §9). Only some components vary across levels; those that don't simply
/// ignore it.
/// </summary>
public enum DetailLevel
{
    Normal,             // Context [████████░░░░░░░░░░░░] 42%
    ModeratelyNarrow,   // Ctx [████░░░░░░] 42%
    Narrow,             // Ctx 42%
    VeryNarrow,         // 42%
}

/// <summary>
/// Everything a component needs to render: the status data and the settings.
/// Passed to each component so presentation stays decoupled from acquisition (spec §10).
/// </summary>
public sealed record StatusContext(ClaudeStatus Status, Settings Settings, DetailLevel Level);
