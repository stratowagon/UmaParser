# AGENTS.md

Instructions for AI coding agents working in this repository.

## Project overview

UmaParser is a Windows desktop app for analyzing **Uma Musume: Pretty Derby** race capture JSON files — primarily **Team Trials (TT)** scores, but also Champions Meet, Room Match, and Practice Room captures.

- **README.md** — user-facing usage and feature descriptions.
- **This file** — build steps, architecture, and conventions for agents.

The solution is the WinForms app, a WPF app that is replacing it one screen at a time, Core, and Core tests, plus a few dev-only console tools under `Tools/`. WinForms stays until WPF covers the same tabs. New UI work goes in the WPF project.

## Repository layout

```
UmaParser.sln
├── UmaParser/              WinForms UI (kept until WPF reaches parity)
│   ├── Form1*.cs           Main window split into partial classes by tab/concern
│   └── Ui/                 Theming and shared UI helpers
├── UmaParser.Wpf/          WPF UI (MVVM by hand). New UI work goes here.
│   ├── Mvvm/               ObservableObject and RelayCommand
│   ├── ViewModels/         MainViewModel, tab list, SkillsViewModel, TracksViewModel
│   ├── Views/              MainWindow, SkillsView, and TracksView
│   ├── Behaviors/          File drop forwarded to a command
│   ├── Converters/         CellBand to brush
│   └── Themes/             Cell band colors, same values as AppColors
├── UmaParser.Core/         All non-UI logic
│   ├── Import/             JSON capture decoding and batch loading
│   ├── Analysis/           Score/skill/track analysis engines
│   ├── Presentation/       Shared column lists, Skills and Tracks screens, status text
│   ├── MasterData/         Game master DB (master.mdb) + embedded fallback
│   ├── DataModel/          Deserialized API/capture shapes
│   └── ObjectModel/        App-level domain types (e.g. TeamTrialResult)
├── tests/
│   └── UmaParser.Core.Tests/  xUnit tests, scrubbed fixtures, and golden snapshots
└── Tools/                  Dev utilities (not shipped with the app)
    ├── ExportFallback/     Regenerates EmbeddedMasterFallback.cs from master.mdb
    ├── FixtureScrubber/    Copies race JSON to a new folder with trainer ids removed
    ├── SkillProbe/         Skill data inspection
    └── SympathyNpcScan/    One-off data scan
```

## Build and run

Requires **.NET 8 SDK** on Windows (WinForms and WPF).

```powershell
# Build the solution
dotnet build UmaParser.sln -c Release

# Run the tests (after the build)
dotnet test UmaParser.sln -c Release

# Run the WinForms app (from repo root). This is still the default startup project.
dotnet run --project UmaParser/UmaParser.csproj -c Release

# Run the WPF app. In Visual Studio: right-click UmaParser.Wpf, then "Set as Startup Project".
dotnet run --project UmaParser.Wpf/UmaParser.Wpf.csproj -c Release
```

A non-Windows build of the desktop projects needs `-p:EnableWindowsTargeting=true`. `UmaParser.Wpf` sets that property itself. The WinForms project still needs it passed in on Linux.

`tests/UmaParser.Core.Tests` is an xUnit project. It runs from Visual Studio Test Explorer and from `dotnet test`. Tests call `GameMasterService.InitializeEmbeddedOnly()` and use **EmbeddedMasterFallback** only — they must not read AppData or a live `master.mdb`.

Scrubbed fixtures live in `tests/UmaParser.Core.Tests/Fixtures/` (see `manifest.json` for provenance). To add more, scrub a **copy** first. The tool never modifies the folder you point it at, and it refuses to write into that same folder:

```powershell
dotnet run --project Tools/FixtureScrubber -- <source-folder> <dest-folder> --owner <YourTrainerName>
```

`--owner` is the in-game trainer name to replace with `Player`. Other trainer names in that file become `Trainer01`, `Trainer02`, and so on. Copy the scrubbed files into a fixtures group, update `Fixtures/manifest.json`, and re-run `dotnet test`. Golden snapshots are compared as JSON. To accept a new or changed snapshot, re-run with `UPDATE_SNAPSHOTS=1` and commit the `.verified.json` file (see the comment on `Snapshot.Match`). A corpus smoke test runs only when `UMAPARSER_CORPUS` points at a folder of captures; otherwise it is skipped.

After changes, run `dotnet build` and `dotnet test` and confirm both succeed.

Published executables land in `publish/`, `release/`, or `_publish-sc/` (all gitignored).

## Architecture rules

### Keep logic out of the UI

- **UmaParser.Core** owns parsing, analysis, master-data access, domain types, and the shared Skills screen (`UmaParser.Presentation`).
- **UmaParser.Wpf** owns the WPF shell. New screens go here and call Core. Do not reimplement a calculation in the view model.
- **UmaParser** owns the existing WinForms wiring. Leave it in place until WPF covers that tab. Do not add new tabs there.

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

Projects use **`UmaParser`** as the root namespace (e.g. `UmaParser.Analysis`, `UmaParser.Import`, `UmaParser.Ui`, `UmaParser.ViewModels`). Assembly names are `UmaParser`, `UmaParser.Wpf`, and `UmaParser.Core`. `GameMasterService` stays `internal`; `UmaParser.Wpf` is on the `InternalsVisibleTo` list, same as WinForms.

## Domain concepts agents should know

### Capture types

Imports come from tools like [HorseACT](https://github.com/ayaliz/horseACT) as `.json` files. Team Trials captures are the primary use case; other race modes are supported but must not be mixed with TT in one drop. Only `.json` files are imported. Any other extension is skipped as an unsupported file.

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
- WinForms colors go through `UmaParser/Ui/AppColors.cs` and `AppThemeApplier`. WPF maps `CellBand` through `UmaParser.Wpf/Themes/CellBandBrushes.xaml`. Do not add a third copy of those colors.
- Keep changes focused; no drive-by refactors or unrelated warning fixes.

## What to avoid

- Do not add markdown docs unless explicitly asked (README and this file are enough).
- Do not add NuGet packages without a clear need.
- Do not move `Tools/` projects into the solution unless asked — they are run ad hoc.
- Do not commit unscrubbed race captures. Fixtures under `tests/UmaParser.Core.Tests/Fixtures/` are already scrubbed; add new ones with `Tools/FixtureScrubber`.
- Do not edit `Form1.Designer.cs` by hand except through the WinForms designer pattern already in use.

## Commit guidance

- One logical change per commit when possible.
- If you regenerate `EmbeddedMasterFallback.cs`, mention the game/master version in the commit message.
- Tag releases follow existing pattern (`V1.0`, `V1.1`, `V1.1.1`, etc.).