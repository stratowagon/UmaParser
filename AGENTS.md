# AGENTS.md

Instructions for AI coding agents working in this repository.

## Project overview

UmaParser is a Windows desktop app for analyzing **Uma Musume: Pretty Derby** race capture JSON files — primarily **Team Trials (TT)** scores, but also Champions Meet, Room Match, and Practice Room captures.

- **README.md** — user-facing usage and feature descriptions.
- **This file** — build steps, architecture, and conventions for agents.

The solution is the WPF app, the Blazor WebAssembly shell, Core, Core.Desktop, and Core tests, plus a few dev-only console tools under `Tools/`. `UmaParser.Wpf` is the desktop UI. `UmaParser.Web` is the browser shell and references Core only. `UmaParser.Core` owns parsing, analysis, the settings model, and the screen models both shells display. `UmaParser.Core.Desktop` owns Sqlite `master.mdb` reads, AppData paths, path-based import, and the Windows theme reader.

## Repository layout

```
UmaParser.sln
├── UmaParser.Wpf/          WPF UI (MVVM by hand)
│   ├── Mvvm/               ObservableObject and RelayCommand
│   ├── ViewModels/         MainViewModel, tab list, Results, Team Analysis, Performance, Skills, Tracks, Options
│   ├── Views/              MainWindow, Options, and one view per tab
│   ├── Behaviors/          File drop forwarded to a command
│   ├── Converters/         CellBand to brush
│   ├── Services/           Shell dialogs and window monitors
│   └── Themes/             Light, dark, and System chrome; cell-band brushes
├── UmaParser.Web/          Blazor WebAssembly shell (references Core only)
│   ├── Components/         Skills and Tracks grids, plus placeholder tabs
│   ├── Services/           In-browser capture set (name + text + last modified)
│   └── wwwroot/            Static host page, base href, and .nojekyll
├── UmaParser.Core/         Browser-safe logic (no Sqlite, no Registry, no AppData requirement)
│   ├── Import/             JSON capture decoding from text (CaptureImportService.TryImport)
│   ├── Analysis/           Score/skill/track analysis engines
│   ├── Presentation/       Shared column lists and the Results, Team Analysis, Skills, and Tracks screens
│   ├── MasterData/         Catalog, embedded fallback, and GameMasterBootstrap
│   ├── DataModel/          Deserialized API/capture shapes
│   ├── ObjectModel/        App-level domain types (e.g. TeamTrialResult)
│   └── Settings/           Column preferences and window placement (in-memory settings model)
├── UmaParser.Core.Desktop/ Desktop I/O used by WPF (references Core)
│   ├── MasterData/         master.mdb via Sqlite, AppData paths, settings.json, cache file
│   ├── Import/             Path drops (CaptureFileImport) and the Documents capture folder
│   ├── Presentation/       Windows registry app theme (RegistryWindowsAppThemeReader)
│   └── Analysis/           Settings-file adapters for the Performance sort and sample thresholds
├── tests/
│   └── UmaParser.Core.Tests/  xUnit tests, scrubbed fixtures, and golden snapshots
└── Tools/                  Dev utilities (not shipped with the app)
    ├── ExportFallback/     Regenerates EmbeddedMasterFallback.cs from master.mdb
    ├── FixtureScrubber/    Copies race JSON to a new folder with trainer ids removed
    ├── SkillProbe/         Skill data inspection
    └── SympathyNpcScan/    One-off data scan
```

## Build and run

Requires **.NET 8 SDK** on Windows (WPF).

```powershell
# Build the solution
dotnet build UmaParser.sln -c Release

# Run the tests (after the build)
dotnet test UmaParser.sln -c Release

# Run the WPF app (from repo root). This is the startup project.
dotnet run --project UmaParser.Wpf/UmaParser.Wpf.csproj -c Release

# Run the web shell (from repo root). No Node install. The console prints the localhost URL.
dotnet run --project UmaParser.Web
```

`UmaParser.Wpf` sets `EnableWindowsTargeting` itself, so a non-Windows build of the desktop project does not need an extra property. The Cloud Agent commands below still pass `-p:EnableWindowsTargeting=true`.

`tests/UmaParser.Core.Tests` is an xUnit project. It runs from Visual Studio Test Explorer and from `dotnet test`. Tests boot through the embedded catalog (`GameMasterBootstrap.UseEmbeddedCatalog` / `GameMasterService.InitializeEmbeddedOnly()`) and use **EmbeddedMasterFallback** only — they must not read AppData or a live `master.mdb`, except the desktop master-path tests, which build a temporary database.

Scrubbed fixtures live in `tests/UmaParser.Core.Tests/Fixtures/` (see `manifest.json` for provenance). To add more, scrub a **copy** first. The tool never modifies the folder you point it at, and it refuses to write into that same folder:

```powershell
dotnet run --project Tools/FixtureScrubber -- <source-folder> <dest-folder> --owner <YourTrainerName>
```

`--owner` is the in-game trainer name to replace with `Player`. Other trainer names in that file become `Trainer01`, `Trainer02`, and so on. Copy the scrubbed files into a fixtures group, update `Fixtures/manifest.json`, and re-run `dotnet test`. Golden snapshots are compared as JSON. To accept a new or changed snapshot, re-run with `UPDATE_SNAPSHOTS=1` and commit the `.verified.json` file (see the comment on `Snapshot.Match`). A corpus smoke test runs only when `UMAPARSER_CORPUS` points at a folder of captures; otherwise it is skipped.

After changes, run `dotnet build` and `dotnet test` and confirm both succeed.

Published executables land in `publish/`, `release/`, or `_publish-sc/` (all gitignored).

## Architecture rules

### Keep logic out of the UI

- **UmaParser.Core** owns parsing, analysis, the in-memory master-data catalog, domain types, the settings model, and the shared screen models (`UmaParser.Presentation`). It must not reference Sqlite, the Windows Registry, or AppData / LocalLow / Documents. A web host calls `GameMasterBootstrap.UseEmbeddedCatalog` (or `TryUseCacheJson`) and `CaptureImportService.TryImport`.
- **UmaParser.Core.Desktop** owns live `master.mdb` loading, the settings file, path-based capture import, and the registry theme reader. WPF references Core and Desktop. Do not move a shared calculation here.
- **UmaParser.Wpf** owns the WPF shell. New desktop screens go here and call Core. Do not reimplement a calculation in the view model. Desktop-only I/O goes through Desktop (`DesktopGameMaster`, `GameMasterSettingsStore`, `CaptureFileImport`).
- **UmaParser.Web** owns the Blazor WebAssembly shell. It references Core only. Startup calls `GameMasterBootstrap.UseEmbeddedCatalog`. The status line uses `GameMasterBootstrap.StatusLine`. Captures go through `CaptureImportService.TryImport` (file name, JSON text, and last-modified time). Skills and Tracks render `SkillsScreen` and `TracksScreen`. It does not read `master.mdb`. `wwwroot/index.html` keeps `<base href="/" />` for local run. `.github/workflows/deploy-web.yml` rewrites that tag to `/UmaParser/` when it publishes to GitHub Pages.

### WPF organization

The window is MVVM by hand. Follow the existing folders:

| Area | Responsibility |
|------|----------------|
| `Views/MainWindow` | Shell, menus, tabs, drag-drop, window placement |
| `Views/` | One view per tab (Results, Team Analysis, Performance, Skills, Tracks) plus Options |
| `ViewModels/` | `MainViewModel`, one view model per tab, and Options |
| `Themes/` | Light, dark, and System appearance |
| `Services/` | File dialogs and monitor rectangles |
| `Mvvm/` | `ObservableObject` and `RelayCommand` |

Tab order is Results, Team Analysis, Performance, Skills, Tracks. A new screen is a view plus a view model that calls Core.

### Namespace

Projects use **`UmaParser`** as the root namespace (e.g. `UmaParser.Analysis`, `UmaParser.Import`, `UmaParser.Presentation`, `UmaParser.ViewModels`). Assembly names are `UmaParser.Wpf`, `UmaParser.Web`, `UmaParser.Core`, and `UmaParser.Core.Desktop`. `GameMasterService` and `DesktopGameMaster` stay `internal`. `UmaParser.Core.Desktop`, `UmaParser.Wpf`, `SkillProbe`, and `UmaParser.Core.Tests` are on Core's `InternalsVisibleTo` list. Desktop exposes its internals to WPF, SkillProbe, and the test assembly. The public web entry point is `GameMasterBootstrap`. The web project is not on that list.

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

Open `master.mdb` read-only only at startup or on explicit refresh/browse, read once, and close it immediately. Never hold it open, and never write to it or its folder; the game writes to it during updates.

## Code style

- C# with **nullable reference types** enabled.
- Prefer `sealed` classes and `internal` visibility in Core unless the type is part of a public surface.
- Use `file-scoped namespaces` where surrounding files already do.
- Match naming in the folder you are editing (`GameMasterService`, `CaptureImportService`, etc.).
- Cell-band colors live in `UmaParser.Core/Presentation/CellBandPalette.cs`. WPF maps `CellBand` through `UmaParser.Wpf/Themes/CellBandBrushSource.cs`. The web shell calls `CellBandPalette` for those cells. Window chrome colors live in `UmaParser.Wpf/Themes/Light.xaml` and `Dark.xaml`. The web shell's matching chrome is `UmaParser.Web/wwwroot/css/app.css`. Do not add a third copy of those colors.
- Keep changes focused; no drive-by refactors or unrelated warning fixes.

## What to avoid

- Do not add markdown docs unless explicitly asked (README and this file are enough).
- Do not add NuGet packages without a clear need.
- Do not move `Tools/` projects into the solution unless asked — they are run ad hoc.
- Do not commit unscrubbed race captures. Fixtures under `tests/UmaParser.Core.Tests/Fixtures/` are already scrubbed; add new ones with `Tools/FixtureScrubber`.
- Do not hold `master.mdb` open or write to it or its folder. Open it read-only only at startup or on explicit refresh/browse, read once, and close it immediately. The game writes to it during updates.

## Commit guidance

- One logical change per commit when possible.
- If you regenerate `EmbeddedMasterFallback.cs`, mention the game/master version in the commit message.
- Tag releases follow existing pattern (`V1.0`, `V1.1`, `V1.1.1`, etc.).

## Cursor Cloud specific instructions

Cloud Agent Builds preinstall the .NET 8 SDK and restore `UmaParser.sln` from `.cursor/environment.json` (`.cursor/install.sh`). Use the `dotnet` already on `PATH` from that Build. Install the SDK only when `dotnet` is missing or is not major version 8. Do not use Ubuntu’s `dotnet-sdk-8.0` apt package: it omits `Microsoft.NET.Sdk.WindowsDesktop`, and the WPF project then fails at `Microsoft.NET.Sdk.WindowsDesktop.targets`. When a reinstall is required, use Microsoft’s script (`https://dot.net/v1/dotnet-install.sh --channel 8.0`) into `/usr/share/dotnet` and symlink `dotnet` into `/usr/local/bin`, which is what the install script does. App hosts look for the runtime in that directory, so `dotnet test` does not need a shell profile.

The Cloud Agent VM is Ubuntu. `UmaParser.Wpf` still compiles here; it targets `Microsoft.WindowsDesktop.App` and does not launch (there is no Linux desktop runtime). `UmaParser.Web` builds and runs here with `dotnet run --project UmaParser.Web` (no `EnableWindowsTargeting`). `dotnet test` is the check that import and analysis work. It uses embedded master data only. With no `master.mdb`, startup reports built-in names. The corpus smoke test stays skipped unless `UMAPARSER_CORPUS` is set.

```powershell
dotnet build UmaParser.sln -c Release -p:EnableWindowsTargeting=true
dotnet test UmaParser.sln -c Release -p:EnableWindowsTargeting=true
```
