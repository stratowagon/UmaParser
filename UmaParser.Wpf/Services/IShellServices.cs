using UmaParser.ViewModels;

namespace UmaParser.Services;

/// <summary>
/// Window dialogs the view model can call without referencing a control.
/// Tests pass a fake. The WPF window passes <see cref="WpfShellServices"/>.
/// </summary>
internal interface IShellServices
{
    /// <summary>Returns the chosen master.mdb path, or null when the user cancels.</summary>
    string? PickMasterDatabase(string initialDirectory);

    void ShowInformation(string title, string message);

    void ShowWarning(string title, string message);

    void ShowError(string title, string message);

    void OpenFolder(string folder);

    void ShowOptions(OptionsViewModel viewModel);

    void ActivateOptions();
}
