using ClaudeWatch.Config;

namespace ClaudeWatch.Rendering;

/// <summary>
/// Renders the working directory (spec §6). By default shows only final path
/// components to stay compact: "project : current" when Claude has cd'd away from the
/// directory it was launched in, otherwise just the one name. Narrow tiers drop the
/// project part. "?" when the directory cannot be determined.
/// </summary>
public sealed class DirectoryComponent : IStatusComponent
{
    public string? Render(StatusContext ctx)
    {
        var st = ctx.Status;
        string text;
        if (ctx.Settings.DirectoryMode == DirectoryMode.FullPath)
        {
            text = string.IsNullOrWhiteSpace(st.WorkingDirectory) ? "?" : st.WorkingDirectory!;
        }
        else
        {
            text = string.IsNullOrWhiteSpace(st.WorkingDirectoryName) ? "?" : st.WorkingDirectoryName!;
            bool roomy = ctx.Level is DetailLevel.Normal or DetailLevel.ModeratelyNarrow;
            if (roomy && !string.IsNullOrWhiteSpace(st.ProjectDirectoryName)
                      && !SamePath(st.ProjectDirectory, st.WorkingDirectory))
                text = $"{st.ProjectDirectoryName} : {text}";
        }

        return Ansi.Wrap(text, ctx.Settings.Colors.Directory, ctx.Settings.UseColor);
    }

    private static bool SamePath(string? a, string? b)
    {
        if (a is null || b is null) return true; // nothing to contrast with
        static string Norm(string p) => p.Replace('\\', '/').TrimEnd('/');
        return string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);
    }
}
