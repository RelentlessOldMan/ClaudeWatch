# ClaudeWatch 👁️

**A tiny, fast, colored HUD for [Claude Code](https://claude.com/claude-code).** It
answers four questions at a glance:

**What model am I using? · How full is my context? · Where am I working? · What is Claude doing?**

```text
Opus 4.8 │ Context (120k/1M) [████░░░░░░] 42% │ CodeCompass │ Edit
```

The context section is tinted by usage severity — **green** up to 50%, **orange** to 80%,
then **red** — so a filling context window is obvious at a glance.

## What it shows

`MODEL │ CONTEXT │ DIRECTORY │ ACTIVITY`

- **Model** — the model name Claude Code reports, with any trailing window note stripped
  (`Opus 4.8 (1M context)` → `Opus 4.8`). `Model ?` if unknown.
- **Context** — usage over the window size (`Context (120k/1M)`), a fill bar, and the
  rounded percentage — the whole section colored by severity. Shows
  `Context (?/…) [??????????] ?%` when usage is unavailable.
- **Directory** — the final component of the working directory (`?` if unknown).
- **Activity** — the tool Claude is currently running, or `Idle` when nothing is running.

## How it works

Claude Code invokes the configured status-line command on each refresh, passing a JSON
blob on **stdin** (model, workspace, transcript path, ...). ClaudeWatch:

1. Parses that JSON — including the `context_window` object, which is authoritative:
   `context_window_size` (the real 200k vs 1M window), `used_percentage` (matches Claude
   Code's own meter), and `total_input_tokens` (tokens currently in the window).
2. Reads the **tail** of the session transcript (a small, cheap read) for tool activity —
   the current/most-recent tool isn't in the stdin payload. On older Claude Code versions
   that predate `context_window`, the transcript also backfills the token usage, and the
   window falls back to a size token in the model name (`[1m]`, `(1M context)`, `(200k)`)
   or 200k.
3. Renders a single line to **stdout**. Nothing else is ever written to stdout;
   diagnostics go to `%TEMP%\claudewatch.log`.

It never makes network calls, spawns subprocesses, or scans the repo (spec §12), and it
degrades gracefully — any missing field becomes a placeholder rather than an error. Because
the window size comes straight from Claude Code, it stays correct and stable across
`/compact` and session restarts rather than guessing from current usage.

### Narrow terminals

The line compresses in stages as width shrinks, always keeping the percentage:

```text
Opus 4.8 │ Context (120k/1M) [████░░░░░░] 42% │ CodeCompass │ Edit   (normal)
Opus 4.8 │ Ctx (120k/1M) [██░░░] 42% │ CodeCompass │ Edit            (moderately narrow)
Opus 4.8 │ Ctx 42% │ CodeCompass │ Edit                              (narrow)
Opus 4.8 │ 42% │ CodeCompass │ Edit                                  (very narrow)
```

Color escape codes don't count toward width, so the tiers fit correctly.

## Configuration

All defaults are overridable via an optional JSON file, searched in this order:

1. the path in the `CLAUDEWATCH_CONFIG` environment variable,
2. `claudewatch.json` next to the executable,
3. `%USERPROFILE%\.claude\claudewatch.json`.

Copy [`claudewatch.example.json`](claudewatch.example.json) and edit what you want; every
key is optional. Colors accept names (`brightCyan`, `orange`, `green`, `gray`, ...) or raw
ANSI SGR params (`38;5;208`); `default`/`none`/`""` mean no color. Setting `NO_COLOR` in
the environment disables all color regardless of the file.

Key options: `contextBarWidth` (default 10), `useColor`, `useUnicode`, `showContextLabel`,
`showContextCapacity`, `showIdle`, `directoryMode` (`NameOnly`/`FullPath`),
`contextMedPercent` (50), `contextHighPercent` (80), and a `colors` block
(`model`, `directory`, `activeTool`, `idle`, `separator`, `contextLow`, `contextMed`,
`contextHigh`).

## Architecture

Data acquisition is kept separate from presentation (spec §10), and each field is an
independent, swappable component:

```
src/
  Program.cs                     entry point; reads stdin, writes stdout, guards everything
  Config/
    Settings.cs                  defaults, palette, thresholds, glyphs
    ConfigLoader.cs              optional JSON config -> Settings
  Model/ClaudeStatus.cs          the internal status model
  Acquisition/StatusReader.cs    stdin JSON + transcript tail -> ClaudeStatus
  Rendering/
    IStatusComponent.cs          Render(StatusContext) contract
    Ansi.cs                      color helper + ANSI-aware width measurement
    ModelComponent.cs
    ContextComponent.cs
    DirectoryComponent.cs
    ToolActivityComponent.cs
    StatusLineRenderer.cs        combines components; applies width tiers
  Diagnostics/Log.cs             best-effort error log (never touches stdout)
```

Adding a future indicator (cost, git, session time, ...) means implementing
`IStatusComponent` and registering it in `StatusLineRenderer` — existing components are
untouched.

## Build

Requires the .NET SDK and the Visual C++ toolset (for Native AOT linking).

```powershell
.\build.ps1
```

Produces a self-contained native executable at `dist\claudewatch.exe` (~1.6 MB, no .NET
runtime required, single-digit-millisecond startup).

## Install

Add to your Claude Code `settings.json`:

```json
"statusLine": {
  "type": "command",
  "command": "C:/Playground/ClaudeWatch/dist/claudewatch.exe"
}
```

## Manual test

```powershell
.\test\run-tests.ps1
```

renders a range of cases (severity colors, width tiers, missing data, `NO_COLOR`) showing
both the raw ANSI output and a color-stripped view.

## Contributing

This is a personal tool, published as-is — **issues and pull requests aren't accepted** (PRs auto-close). Fork it and make it your own. 👁️

## License

MIT — see [LICENSE](LICENSE). © 2026 RelentlessOldMan.
