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

    private void SystemTheme_Click(object sender, RoutedEventArgs e) => SetMode(ColorMode.System);

    private void LightTheme_Click(object sender, RoutedEventArgs e) => SetMode(ColorMode.Light);

    private void DarkTheme_Click(object sender, RoutedEventArgs e) => SetMode(ColorMode.Dark);

    private void SetMode(ColorMode mode)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SetColorMode(mode);
        }

        // MenuItem toggles IsChecked locally before Click. Put the checks back from the preference.
        SyncThemeChecks();
    }

    private void SyncThemeChecks()
    {
        SystemThemeItem.IsChecked = AppTheme.Preference == ColorMode.System;
        LightThemeItem.IsChecked = AppTheme.Preference == ColorMode.Light;
        DarkThemeItem.IsChecked = AppTheme.Preference == ColorMode.Dark;
    }
}
