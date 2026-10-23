using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using UmaParser.ViewModels;
using UmaParser.Views;

namespace UmaParser.Services;

/// <summary>Win32 file dialog, message boxes, and the modeless Options window.</summary>
internal sealed class WpfShellServices : IShellServices
{
    private readonly Func<Window?> _owner;
    private OptionsWindow? _options;

    public WpfShellServices(Func<Window?> owner)
    {
        _owner = owner;
    }

    public string? PickMasterDatabase(string initialDirectory)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select master.mdb",
            Filter = "Master database (master.mdb)|master.mdb|All files (*.*)|*.*",
            FileName = "master.mdb",
            InitialDirectory = initialDirectory,
        };

        return dialog.ShowDialog(_owner()) == true ? dialog.FileName : null;
    }

    public void ShowInformation(string title, string message) =>
        MessageBox.Show(_owner(), message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowWarning(string title, string message) =>
        MessageBox.Show(_owner(), message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowError(string title, string message) =>
        MessageBox.Show(_owner(), message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void OpenFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        try
        {
            Process.Start("explorer.exe", folder);
        }
        catch (Exception ex)
        {
            ShowError("Master data", ex.Message);
        }
    }

    public void ShowOptions(OptionsViewModel viewModel)
    {
        var window = new OptionsWindow(viewModel)
        {
            Owner = _owner(),
        };
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_options, window))
            {
                _options = null;
            }

            viewModel.NotifyClosed();
        };
        _options = window;
        window.Show();
    }

    public void ActivateOptions()
    {
        if (_options == null)
        {
            return;
        }

        if (_options.WindowState == WindowState.Minimized)
        {
            _options.WindowState = WindowState.Normal;
        }

        _options.Activate();
    }
}
