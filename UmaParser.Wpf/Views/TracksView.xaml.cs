using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using UmaParser.Presentation;
using UmaParser.ViewModels;

namespace UmaParser.Views;

public partial class TracksView : UserControl
{
    private TracksViewModel? _viewModel;

    public TracksView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as TracksViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyColumnsAndSort();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // VisibleColumns changes on the same drop as GridRevision. Rebuild once.
        if (e.PropertyName == nameof(TracksViewModel.GridRevision))
        {
            ApplyColumnsAndSort();
        }
    }

    /// <summary>
    /// Builds DataGrid columns from the view model's visible column list, then sorts by Track
    /// ascending. A DataGrid column is not a visual child, so its Visibility cannot be bound.
    /// Which columns exist is decided in Core. This method only creates them.
    /// Rebinding an uma resets the sort, which is what WinForms does in BindTracksGrid.
    /// </summary>
    private void ApplyColumnsAndSort()
    {
        long started = Stopwatch.GetTimestamp();
        TracksGrid.Columns.Clear();
        if (_viewModel == null)
        {
            return;
        }

        var plainStyle = (Style)FindResource("TrackCellStyle");
        string? trackSortPath = null;

        foreach (var column in _viewModel.VisibleColumns)
        {
            TrackColumnBinding binding = TrackColumnBinding.For(column.Id);
            if (column.Id == GridColumnIds.Tracks.Track)
            {
                trackSortPath = binding.SortPath;
            }

            var gridColumn = new DataGridTextColumn
            {
                Header = new TextBlock { Text = column.Header, ToolTip = column.Tooltip },
                Binding = new Binding(binding.DisplayPath),
                SortMemberPath = binding.SortPath,
                CellStyle = binding.CellStyleKey == null
                    ? plainStyle
                    : (Style)FindResource(binding.CellStyleKey),
                Width = DataGridLength.Auto,
            };
            TracksGrid.Columns.Add(gridColumn);
        }

        if (CollectionViewSource.GetDefaultView(TracksGrid.ItemsSource) is not ListCollectionView view)
        {
            return;
        }

        view.SortDescriptions.Clear();
        view.CustomSort = trackSortPath == null
            ? null
            : new TracksRowComparer(trackSortPath, descending: false);

        foreach (DataGridColumn column in TracksGrid.Columns)
        {
            column.SortDirection = column.SortMemberPath == trackSortPath
                ? ListSortDirection.Ascending
                : null;
        }

        TracksGrid.UnselectAll();
        double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Trace.WriteLine(
            $"UmaParser Tracks grid: {TracksGrid.Columns.Count} columns, {_viewModel.Rows.Count} rows, built in {elapsed:0.0} ms");
    }

    /// <summary>
    /// Header click. Every column uses <see cref="TracksGridSort"/> so the total row stays
    /// at the bottom. The grid's own sort would move that row with the numbers.
    /// </summary>
    private void TracksGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (CollectionViewSource.GetDefaultView(TracksGrid.ItemsSource) is not ListCollectionView view)
        {
            return;
        }

        e.Handled = true;
        // Null (first click) and descending both become ascending. Ascending becomes descending.
        bool descending = e.Column.SortDirection == ListSortDirection.Ascending;

        view.SortDescriptions.Clear();
        view.CustomSort = new TracksRowComparer(e.Column.SortMemberPath ?? "", descending);
        foreach (DataGridColumn column in TracksGrid.Columns)
        {
            column.SortDirection = null;
        }

        e.Column.SortDirection = descending ? ListSortDirection.Descending : ListSortDirection.Ascending;
    }

    private readonly record struct TrackColumnBinding(string DisplayPath, string SortPath, string? CellStyleKey)
    {
        public static TrackColumnBinding For(string columnId)
        {
            if (columnId == GridColumnIds.Tracks.Track)
            {
                return new(nameof(TracksGridRow.Track), nameof(TracksGridRow.Track), null);
            }

            if (columnId == GridColumnIds.Tracks.Distance)
            {
                return new(nameof(TracksGridRow.DistanceText), nameof(TracksGridRow.Distance), null);
            }

            if (columnId == GridColumnIds.Tracks.Races)
            {
                return new(nameof(TracksGridRow.Races), nameof(TracksGridRow.Races), null);
            }

            if (columnId == GridColumnIds.Tracks.AvgPlace)
            {
                return new(nameof(TracksGridRow.AvgPlaceText), nameof(TracksGridRow.AvgPlace), null);
            }

            if (columnId == GridColumnIds.Tracks.PctFirst)
            {
                return new(nameof(TracksGridRow.PctFirstText), nameof(TracksGridRow.PctFirst), null);
            }

            if (columnId == GridColumnIds.Tracks.PctTop3)
            {
                return new(nameof(TracksGridRow.PctTop3Text), nameof(TracksGridRow.PctTop3), null);
            }

            if (columnId == GridColumnIds.Tracks.PctTop5)
            {
                return new(nameof(TracksGridRow.PctTop5Text), nameof(TracksGridRow.PctTop5), null);
            }

            if (columnId == GridColumnIds.Tracks.SpurtRate)
            {
                return new(nameof(TracksGridRow.SpurtRateText), nameof(TracksGridRow.SpurtRate), "TrackSpurtCellStyle");
            }

            if (columnId == GridColumnIds.Tracks.Survival)
            {
                return new(nameof(TracksGridRow.SurvivalText), nameof(TracksGridRow.Survival), "TrackSurvivalCellStyle");
            }

            if (columnId == GridColumnIds.Tracks.AvgSkillPts)
            {
                return new(nameof(TracksGridRow.AvgSkillPointsText), nameof(TracksGridRow.AvgSkillPoints), null);
            }

            return new(nameof(TracksGridRow.Track), nameof(TracksGridRow.Track), null);
        }
    }

    /// <summary>ListCollectionView.CustomSort needs the non-generic comparer.</summary>
    private sealed class TracksRowComparer : IComparer
    {
        private readonly string _sortMember;
        private readonly bool _descending;

        public TracksRowComparer(string sortMember, bool descending)
        {
            _sortMember = sortMember;
            _descending = descending;
        }

        public int Compare(object? x, object? y)
        {
            if (x is not TracksGridRow left || y is not TracksGridRow right)
            {
                return 0;
            }

            return TracksGridSort.Compare(left, right, _sortMember, _descending);
        }
    }
}
