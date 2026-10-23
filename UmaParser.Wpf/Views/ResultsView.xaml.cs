using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using UmaParser.ViewModels;

namespace UmaParser.Views;

public partial class ResultsView : UserControl
{
    private ResultsViewModel? _viewModel;
    private int _appliedRevision = -1;
    private bool _syncingScroll;

    public ResultsView()
    {
        InitializeComponent();
        Sheet.ScrollMetricsChanged += (_, _) => SyncScrollBars();
        HorizontalScroll.ValueChanged += (_, e) => OnScrollBar(e.NewValue, horizontal: true);
        VerticalScroll.ValueChanged += (_, e) => OnScrollBar(e.NewValue, horizontal: false);
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as ResultsViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        // The tab host sets DataContext before the view is in the tree. Wait for
        // Loaded so font inheritance and DPI are the ones the sheet will paint with.
        if (IsLoaded)
        {
            ApplySheet();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => ApplySheet();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Columns changes on the same drop as GridRevision. Rebuild once, from the revision.
        if (e.PropertyName == nameof(ResultsViewModel.GridRevision) && IsLoaded)
        {
            ApplySheet();
            return;
        }

        // Column options do not change this sheet. Paint it again without moving the scroll bars.
        if (e.PropertyName == nameof(ResultsViewModel.RepaintRequest) && IsLoaded)
        {
            Sheet.InvalidateVisual();
        }
    }

    private void ApplySheet()
    {
        if (_viewModel == null)
        {
            Sheet.SetModel(ResultsSheetModel.Empty);
            _appliedRevision = -1;
            return;
        }

        if (_appliedRevision == _viewModel.GridRevision)
        {
            return;
        }

        Sheet.SetModel(ResultsSheetBuilder.Build(_viewModel.Columns, _viewModel.Rows));
        _appliedRevision = _viewModel.GridRevision;
        SyncScrollBars();
    }

    private void OnScrollBar(double value, bool horizontal)
    {
        if (_syncingScroll)
        {
            return;
        }

        if (horizontal)
        {
            Sheet.HorizontalOffset = value;
        }
        else
        {
            Sheet.VerticalOffset = value;
        }
    }

    private void SyncScrollBars()
    {
        _syncingScroll = true;
        Sync(HorizontalScroll, Sheet.HorizontalOffset, Sheet.ExtentWidth, Sheet.ViewportWidth, smallChange: 16);
        Sync(VerticalScroll, Sheet.VerticalOffset, Sheet.ExtentHeight, Sheet.ViewportHeight, smallChange: Math.Max(1, Sheet.RowHeight));
        _syncingScroll = false;
    }

    private static void Sync(ScrollBar bar, double offset, double extent, double viewport, double smallChange)
    {
        double max = Math.Max(0, extent - viewport);
        bar.Minimum = 0;
        bar.ViewportSize = Math.Max(0, viewport);
        bar.Maximum = max;
        bar.LargeChange = Math.Max(1, viewport);
        bar.SmallChange = smallChange;
        bar.Value = Math.Min(Math.Max(0, offset), max);
        bar.Visibility = max <= 0.5 ? Visibility.Collapsed : Visibility.Visible;
    }
}
