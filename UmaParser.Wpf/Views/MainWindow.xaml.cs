using System.ComponentModel;
using System.Windows;
using UmaParser.Services;
using UmaParser.ViewModels;

namespace UmaParser.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // The view model is created here, after App.OnStartup has initialized master data.
        // Bindings on this window read MainViewModel. The file-drop behavior calls LoadFilesCommand.
        DataContext = new MainViewModel(new WpfShellServices(() => this));
        MainWindowPlacement.Restore(this);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        MainWindowPlacement.Save(this);
        base.OnClosing(e);
    }
}
