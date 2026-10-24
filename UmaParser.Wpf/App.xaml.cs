using System.Windows;
using UmaParser.MasterData;
using UmaParser.Themes;

namespace UmaParser;

/// <summary>
/// Startup. Master data is loaded before the window opens, the same way WinForms
/// calls GameMasterService.Current.Initialize() in Program.Main: a live master.mdb
/// when one is configured, otherwise the embedded fallback.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppTheme.Initialize();
        GameMasterService.Current.Initialize();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppTheme.Stop();
        base.OnExit(e);
    }
}
