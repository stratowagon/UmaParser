using System.ComponentModel;
using System.Windows;
using UmaParser.Presentation;
using UmaParser.Services;
using UmaParser.Themes;
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
        SyncThemeChecks();
        Loaded += (_, _) => WindowChromeTheme.Apply(this);
        AppTheme.Changed += OnThemeChanged;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        MainWindowPlacement.Save(this);
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        AppTheme.Changed -= OnThemeChanged;
        base.OnClosed(e);
    }

    private void OnThemeChanged(object? sender, EventArgs e) => SyncThemeChecks();

    private void LightTheme_Click(object sender, RoutedEventArgs e) => SetTheme(ColorTheme.Light);

    private void DarkTheme_Click(object sender, RoutedEventArgs e) => SetTheme(ColorTheme.Dark);

    private void SetTheme(ColorTheme theme)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SetColorTheme(theme);
        }

        // MenuItem toggles IsChecked locally before Click. Put the checks back from the theme.
        SyncThemeChecks();
    }

    private void SyncThemeChecks()
    {
        LightThemeItem.IsChecked = !AppTheme.IsDark;
        DarkThemeItem.IsChecked = AppTheme.IsDark;
    }
}
