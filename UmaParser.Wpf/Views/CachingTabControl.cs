using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Threading;
using UmaParser.ViewModels;

namespace UmaParser.Views;

/// <summary>
/// Tab control that builds each page once and keeps it in the visual tree.
/// The stock control puts every page through one content presenter, so a tab
/// switch throws the view away and expands its DataTemplate again. That rebuilds
/// the DataGrid. Here the presenter is replaced with a panel: the selected page
/// is Visible and the others stay Collapsed.
/// </summary>
public sealed class CachingTabControl : TabControl
{
    private const string ContentHostPart = "PART_SelectedContentHost";

    private readonly Grid _panel = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        Focusable = false,
    };

    private readonly Dictionary<object, FrameworkElement> _views = new(ReferenceEqualityComparer.Instance);
    private bool _hostInstalled;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _hostInstalled = InstallHost();
        if (_hostInstalled)
        {
            ShowSelected();
        }
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        if (_hostInstalled)
        {
            ShowSelected();
        }
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        if (_hostInstalled)
        {
            ShowSelected();
        }
    }

    /// <summary>
    /// Takes the stock content site out of the template and puts <see cref="_panel"/>
    /// in its place. The presenter is unbound so a later selection does not build a
    /// second copy of the page.
    /// </summary>
    private bool InstallHost()
    {
        if (GetTemplateChild(ContentHostPart) is not ContentPresenter presenter)
        {
            Trace.WriteLine("UmaParser tab host: PART_SelectedContentHost was not found.");
            return false;
        }

        if (presenter.Parent is not Decorator parent)
        {
            Trace.WriteLine("UmaParser tab host: the content presenter is not inside a decorator.");
            return false;
        }

        // The theme binds this presenter's margin to TabControl.Padding (2).
        _panel.SetBinding(MarginProperty, new Binding(nameof(Padding)) { Source = this });
        _panel.SnapsToDevicePixels = presenter.SnapsToDevicePixels;
        parent.Child = _panel;

        BindingOperations.ClearAllBindings(presenter);
        presenter.Content = null;
        presenter.ContentTemplate = null;
        presenter.ContentTemplateSelector = null;
        presenter.Visibility = Visibility.Collapsed;

        if (NameScope.GetNameScope(this) is NameScope scope && scope.FindName(ContentHostPart) != null)
        {
            scope.UnregisterName(ContentHostPart);
        }

        return true;
    }

    private void ShowSelected()
    {
        object? content = SelectedItem is ScreenTab tab ? tab.Content : null;
        if (content == null)
        {
            CollapseAll();
            return;
        }

        long started = Stopwatch.GetTimestamp();
        bool created = false;
        if (!_views.TryGetValue(content, out FrameworkElement? view))
        {
            view = CreateView(content);
            view.DataContext = content;
            view.Visibility = Visibility.Collapsed;
            _views.Add(content, view);
            _panel.Children.Add(view);
            created = true;
        }

        if (!created && view.Visibility == Visibility.Visible)
        {
            return;
        }

        foreach (UIElement child in _panel.Children)
        {
            child.Visibility = ReferenceEquals(child, view) ? Visibility.Visible : Visibility.Collapsed;
        }

        string name = content.GetType().Name;
        string verb = created ? "created" : "shown";
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                Trace.WriteLine($"UmaParser tab {name} {verb} in {elapsed:0.0} ms (including layout)");
            },
            DispatcherPriority.Loaded);
    }

    private void CollapseAll()
    {
        foreach (UIElement child in _panel.Children)
        {
            child.Visibility = Visibility.Collapsed;
        }
    }

    private FrameworkElement CreateView(object content)
    {
        var key = new DataTemplateKey(content.GetType());
        if (TryFindResource(key) is not DataTemplate template)
        {
            throw new InvalidOperationException($"No DataTemplate for {content.GetType().Name}.");
        }

        if (template.LoadContent() is not FrameworkElement view)
        {
            throw new InvalidOperationException($"The DataTemplate for {content.GetType().Name} did not create a view.");
        }

        view.HorizontalAlignment = HorizontalAlignment.Stretch;
        view.VerticalAlignment = VerticalAlignment.Stretch;
        return view;
    }
}
