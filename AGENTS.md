# AGENTS.md

Instructions for AI coding agents working in this repository.

## Project overview

UmaParser is a Windows desktop app for analyzing **Uma Musume: Pretty Derby** race capture JSON files — primarily **Team Trials (TT)** scores, but also Champions Meet, Room Match, and Practice Room captures.

- **README.md** — user-facing usage and feature descriptions.
- **This file** — build steps, architecture, and conventions for agents.

The solution is small: two main projects plus a few dev-only console tools under `Tools/`.

## Repository layout

```
UmaParser.sln
├── UmaParser/              WinForms UI (drag-and-drop, tabs, grids)
│   ├── Form1*.cs           Main window split into partial classes by tab/concern
│   └── Ui/                 Theming and shared UI helpers
├── UmaParser.Core/         All non-UI logic
│   ├── Import/             JSON capture decoding and batch loading
│   ├── Analysis/           Score/skill/track analysis engines
│   ├── MasterData/         Game master DB (master.mdb) + embedded fallback
│   ├── DataModel/          Deserialized API/capture shapes
│   └── ObjectModel/        App-level domain types (e.g. TeamTrialResult)
└── Tools/                  Dev utilities (not shipped with the app)
    ├── ExportFallback/     Regenerates EmbeddedMasterFallback.cs from master.mdb
    ├── SkillProbe/         Skill data inspection
    └── SympathyNpcScan/    One-off data scan
```

## Build and run

Requires **.NET 8 SDK** on Windows (WinForms target).

```powershell
# Build the solution
dotnet build UmaParser.sln -c Release

# Run the app (from repo root)
dotnet run --project UmaParser/UmaParser.csproj -c Release
```

There is **no automated test project** today. After changes, at minimum run `dotnet build` and confirm zero errors.

Published executables land in `publish/`, `release/`, or `_publish-sc/` (all gitignored).

## Architecture rules

### Keep logic out of the UI

- **UmaParser.Core** owns parsing, analysis, master-data access, and domain types.
- **UmaParser** owns WinForms wiring: event handlers, grid binding, tab state, theming.
- When adding a feature, put computation in Core and call it from a `Form1.*.cs` partial.

### Partial-class organization

`Form1` is split by concern — follow the existing pattern:

| File | Responsibility |
|------|----------------|
| `Form1.cs` | Constructor, drag-drop, shared grid helpers |
| `Form1.TeamTrials.cs` | Results tab, roster grid |
| `Form1.Analysis.cs` | Team Analysis tab |
| `Form1.Skills.cs` | Skills tab |
| `Form1.Tracks.cs` | Tracks tab |
| `Form1.MasterData.cs` | Master DB menu / settings |
| `Form1.View.cs` | Tab visibility and empty states |
| `Form1.Status.cs` | Status bar |
| `Form1.WindowLayout.cs` | Window size/position persistence |
| `Form1.Designer.cs` | Designer-generated controls only |

Do **not** grow `Form1.cs` with tab-specific logic; add or extend the matching partial.

### Namespace

Both projects use **`UmaParser`** as the root namespace (e.g. `UmaParser.Analysis`, `UmaParser.Import`, `UmaParser.Ui`). Assembly names are `UmaParser` and `UmaParser.Core`.

## Domain concepts agents should know

### Capture types

Imports come from tools like [HorseACT](https://github.com/ayaliz/horseACT) as `.json` files. Team Trials captures are the primary use case; other race modes are supported but must not be mixed with TT in one drop.

### Roster consistency (Team Trials)

Team Analysis requires all 15 umas to be **consistent** across every dropped file:

1. Present in every file
2. Same running style in every file
3. Same team assignment in every file (slot changes within a team are OK)

If rosters differ, the Results tab still works (with outlier highlighting) but Team Analysis is disabled.

### Master data

Skill names, character names, and track names come from the game's `master.mdb` SQLite database when available (auto-detected from Steam install path, or user-selected via menu). If no DB is found, the app uses **`EmbeddedMasterFallback.cs`** — a generated snapshot checked into the repo.

To refresh embedded fallback data (requires a local `master.mdb`):

```powershell
dotnet run --project Tools/ExportFallback/ExportFallback.csproj
```

This overwrites `UmaParser.Core/MasterData/EmbeddedMasterFallback.cs`. Commit that file when game data changes.

**Never commit `*.mdb` files** — they are gitignored.

## Code style

- C# with **nullable reference types** enabled.
- Prefer `sealed` classes and `internal` visibility in Core unless the type is part of a public surface.
- Use `file-scoped namespaces` where surrounding files already do.
- Match naming in the folder you are editing (`GameMasterService`, `CaptureImportService`, etc.).
- UI colors go through `UmaParser/Ui/AppColors.cs` and `AppThemeApplier` — avoid hard-coded theme colors in new code.
- Keep changes focused; no drive-by refactors or unrelated warning fixes.

## What to avoid

- Do not add markdown docs unless explicitly asked (README and this file are enough).
- Do not add NuGet packages without a clear need.
- Do not move `Tools/` projects into the solution unless asked — they are run ad hoc.
- Do not assume test fixtures exist; sample JSON captures are not in the repo.
- Do not edit `Form1.Designer.cs` by hand except through the WinForms designer pattern already in use.

## Commit guidance

- One logical change per commit when possible.
- If you regenerate `EmbeddedMasterFallback.cs`, mention the game/master version in the commit message.
- Tag releases follow existing pattern (`V1.0`, `V1.1`, `V1.1.1`, etc.).