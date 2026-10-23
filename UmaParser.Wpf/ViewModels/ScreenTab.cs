namespace UmaParser.ViewModels;

/// <summary>
/// One page of the main window.
/// <see cref="Title"/> is the tab header. <see cref="Content"/> is that page's view model.
/// MainWindow maps the view model type to a view with a DataTemplate.
/// Tab order matches WinForms: Results, Team Analysis, Skills, Tracks.
/// </summary>
public sealed class ScreenTab
{
    public required string Title { get; init; }
    public required object Content { get; init; }
}
