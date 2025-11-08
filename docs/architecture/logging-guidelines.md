# Logging Guidelines

**Audience**: Engineering team  
**Updated**: 2025-11-08

---

## Console Theme

- The console logger now centralises formatting through `LogFormatting`.
- ANSI markup is auto-disabled when the host cannot render colour, output is redirected, or `POKESHARP_LOG_PLAIN=1`.
- Timestamps use `[HH:mm:ss.fff]`, a padded colour-coded level token, optional scope, the hashed category column, and finally the message. Plain mode keeps the alignment using ASCII fallbacks.

| Level | Token | Colour | Message Colour |
|-------|-------|--------|----------------|
| Trace | `[TRACE]` | `grey53` | `grey35 dim` |
| Debug | `[DEBUG]` | `steelblue1` | `lightsteelblue1` |
| Info  | `[INFO ]` | `skyblue3` | `silver dim` |
| Warn  | `[WARN ]` | `gold1 bold` | `gold1` |
| Error | `[ERROR]` | `tomato bold` | `tomato` |
| Crit  | `[CRIT ]` | `magenta1 bold` | `magenta1` |

Categories are hashed into a stable palette (`cyan1`, `deepskyblue1`, `mediumorchid`, `springgreen1`, `gold1`, `lightsteelblue`, `dodgerblue3`, `mediumvioletred`, `turquoise2`, `plum1`).

### Exceptions

- Exceptions render on separate lines (`Exception:` and optional `StackTrace:`).  
- Stack traces appear when the log request is `Debug` or higher.

### Accent Glyphs

Several template helpers now prefix messages with glyphs for quick scanning. When ANSI is disabled the glyphs fall back to short tags (`[asset]`, `[perf]`, etc.).

| Accent | Glyph | Colour | Usage |
|--------|-------|--------|-------|
| Initialization | `▶` | `skyblue1` | Startup progress, system/component init |
| Asset | `A` | `aqua` | Asset pipeline life-cycle |
| Map | `M` | `springgreen1` | Map loading, terrain statistics |
| Performance | `P` | `plum1` | Frame/system timing, throttled warnings |
| Memory | `MEM` | `lightsteelblue1` | GC snapshots and memory pressure |
| Render | `R` | `mediumorchid1` | Rendering passes and sprite counts |
| Entity | `E` | `gold1` | Entity/template lifecycle diagnostics |
| Input | `I` | `deepskyblue3` | Control hints, input state changes |
| Workflow | `WF` | `steelblue1` | Batch jobs, generation steps |
| System | `SYS` | `orange3` | Availability checks, dependency issues |

## Using Log Templates vs Source Generators

- **`LogTemplates`**: Use for human-facing or presentation-critical messages where colour and glyphs help scanning (system availability, scripting warnings, performance summaries).
- **`LogMessages` (`LoggerMessage` source generators)**: Keep for tight loops or telemetry that must remain allocation-free (movement traces, per-frame stats).
- When adding new template methods, funnel them through `LogFormatting.FormatTemplate` to get automatic plain-text fallbacks.

### New Helpers

- `LogSystemUnavailable`, `LogSystemDependencyMissing`: for dependency/state issues.
- `LogEntityMissingComponent`, `LogEntityNotFound`, `LogEntityOperationInvalid`: for scripting APIs.
- `LogTemplateMissing`, `LogTemplateCompilerMissing`: for template/tooling failures.

## File Logging

- `FileLogger` strips Spectre markup before writing. Composite loggers no longer emit `[color]` tags to disk.
- Log rotation behaviour is unchanged (10 MB per file, retains 10 files).

## Environment Verification

1. Run any executable (e.g., `dotnet run --project PokeSharp.Game`) inside Windows Terminal to confirm coloured output.
2. Pipe to a file (`dotnet run ... > out.log`) to confirm plain fallback.
3. Toggle plain mode via `set POKESHARP_LOG_PLAIN=1` and verify the console matches file output.

## Authoring Checklist

- Prefer the strongest template that matches the scenario before writing raw `LogWarning(...)`.
- Escape external input via `LogTemplates.EscapeMarkup` (already used internally).
- Avoid sharing markup strings directly; wrap them in template helpers.
- When in doubt, render locally with `dotnet test --logger "console;verbosity=detailed"` to confirm colours align with expectations.
- Verify plain-mode replacements when introducing new glyphs by checking `PlainGlyphMap` in `LogFormatting`.

