namespace ClaudeWatch.Rendering;

/// <summary>
/// A single independently-renderable piece of the status line (spec §10).
/// New indicators (cost, git, session time, ...) are added by implementing this
/// and registering it with the renderer — existing components stay untouched.
/// </summary>
public interface IStatusComponent
{
    /// <summary>
    /// Renders this component, or returns null to omit it entirely (e.g. an empty
    /// idle state when idle display is disabled). Implementations must not throw;
    /// a failing component omits itself rather than breaking the line (spec §13).
    /// </summary>
    string? Render(StatusContext ctx);
}
