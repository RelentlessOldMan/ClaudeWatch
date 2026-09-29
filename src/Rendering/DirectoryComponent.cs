using ClaudeWatch.Config;

namespace ClaudeWatch.Rendering;

/// <summary>
/// Renders the working directory (spec §6). By default shows only the final path
/// component to stay compact; the full path is retained in the model for future use.
/// "?" when the directory cannot be determined.
/// </summary>
public sealed class DirectoryComponent : IStatusComponent
{
    public string? Render(StatusContext ctx)
    {
        string text;
        if (ctx.Settings.DirectoryMode == DirectoryMode.FullPath)
            text = string.IsNullOrWhiteSpace(ctx.Status.WorkingDirectory) ? "?" : ctx.Status.WorkingDirectory!;
        else
            text = string.IsNullOrWhiteSpace(ctx.Status.WorkingDirectoryName) ? "?" : ctx.Status.WorkingDirectoryName!;

        return Ansi.Wrap(text, ctx.Settings.Colors.Directory, ctx.Settings.UseColor);
    }
}
