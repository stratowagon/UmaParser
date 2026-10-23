using System.Windows;
using System.Windows.Input;

namespace UmaParser.Behaviors;

/// <summary>
/// Forwards a file drop to an <see cref="ICommand"/>.
///
/// Drag-and-drop is a window event, so it cannot be a normal binding on its own.
/// This attached behavior handles that event in the view and passes only the file paths
/// to <c>MainViewModel.LoadFilesCommand</c>. A few lines in MainWindow code-behind would
/// also work. The behavior keeps that code-behind empty and leaves the import in the view model.
///
/// WinForms does the same hand-off in Form1_DragDrop: each dropped path goes to
/// CaptureImportService.TryImportPath. Folders are not opened.
/// </summary>
public static class FileDropBehavior
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command",
        typeof(ICommand),
        typeof(FileDropBehavior),
        new PropertyMetadata(null, OnCommandChanged));

    public static void SetCommand(UIElement element, ICommand? value) => element.SetValue(CommandProperty, value);

    public static ICommand? GetCommand(UIElement element) => (ICommand?)element.GetValue(CommandProperty);

    private static void OnCommandChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        element.AllowDrop = true;
        element.PreviewDragOver -= OnPreviewDragOver;
        element.PreviewDrop -= OnPreviewDrop;
        if (e.NewValue is ICommand)
        {
            element.PreviewDragOver += OnPreviewDragOver;
            element.PreviewDrop += OnPreviewDrop;
        }
    }

    private static void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        bool files = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = files ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = files;
    }

    private static void OnPreviewDrop(object sender, DragEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        ICommand? command = GetCommand(element);
        if (command == null || e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        if (command.CanExecute(files))
        {
            command.Execute(files);
        }

        e.Handled = true;
    }
}
