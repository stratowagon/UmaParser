namespace UmaParser.ViewModels;

/// <summary>
/// One page of the main window.
/// <see cref="Title"/> is the tab header. <see cref="Content"/> is that page's view model.
/// MainWindow maps the view model type to a view with a DataTemplate.
/// A later Results tab is another entry in <see cref="MainViewModel"/>'s tab list,
/// plus a view and a DataTemplate. Put it above Skills so the order matches WinForms:
/// Results, Team Analysis, Skills, Tracks.
/// </summary>
public sealed class ScreenTab
{
    public required string Title { get; init; }
    public required object Content { get; init; }
}
