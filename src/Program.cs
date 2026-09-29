using System.Text;
using ClaudeWatch.Acquisition;
using ClaudeWatch.Config;
using ClaudeWatch.Diagnostics;
using ClaudeWatch.Model;
using ClaudeWatch.Rendering;

// ClaudeWatch — a tiny status line for Claude Code.
// Reads the status JSON Claude Code provides on stdin and writes a single formatted
// status line to stdout. stdout must contain ONLY the status line (spec §13); all
// diagnostics go to a log file. Nothing here is allowed to throw out of the process
// in a way that would disrupt Claude Code.

try
{
    // Ensure Unicode block glyphs (█ ░ │) survive to the terminal.
    try { Console.OutputEncoding = Encoding.UTF8; } catch { /* redirected/unsupported — ignore */ }

    var settings = ConfigLoader.Load();

    string stdin = string.Empty;
    try { stdin = Console.In.ReadToEnd(); }
    catch (Exception e) { Log.Error("Failed to read stdin", e); }

    ClaudeStatus status;
    try
    {
        status = StatusReader.Read(stdin, settings);
    }
    catch (Exception e)
    {
        // StatusReader is defensive, but never let acquisition break rendering.
        Log.Error("StatusReader threw", e);
        status = new ClaudeStatus();
    }

    string line = new StatusLineRenderer(settings).Render(status);
    Console.Out.Write(line);
}
catch (Exception e)
{
    // Last-resort guard: log and emit nothing rather than garbage (spec §13).
    Log.Error("Fatal error rendering status line", e);
}
