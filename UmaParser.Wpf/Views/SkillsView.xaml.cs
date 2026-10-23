using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using UmaParser.Presentation;
using UmaParser.ViewModels;

namespace UmaParser.Views;

public partial class SkillsView : UserControl
{
    private SkillsViewModel? _viewModel;

    public SkillsView()
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

        _viewModel = e.NewValue as SkillsViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyColumnsAndSort();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SkillsViewModel.GridRevision) or nameof(SkillsViewModel.VisibleColumns))
        {
            ApplyColumnsAndSort();
        }
    }

    /// <summary>
    /// Builds DataGrid columns from the view model's visible column list.
    /// A DataGrid column is not a visual child, so binding its Visibility to
    /// "show Pts/race only for Team Trials" does not work. Creating the columns
    /// here is view wiring: the decision of which columns exist is in Core.
    /// </summary>
    private void ApplyColumnsAndSort()
    {
        SkillsGrid.Columns.Clear();
        if (_viewModel == null)
        {
            return;
        }

        var nameStyle = (Style)FindResource("SkillNameCellStyle");
        var metricStyle = (Style)FindResource("SkillMetricCellStyle");
        // WinForms prefers Pts/race, then Activations, even if the user reordered the columns.
        string? pointsSortPath = null;
        string? activationsSortPath = null;

        foreach (var column in _viewModel.VisibleColumns)
        {
            SkillColumnBinding binding = SkillColumnBinding.For(column.Id);
            if (column.Id == GridColumnIds.Skills.PtsRace)
            {
                pointsSortPath = binding.SortPath;
            }
            else if (column.Id == GridColumnIds.Skills.Activations)
            {
                activationsSortPath = binding.SortPath;
            }

            var gridColumn = new DataGridTextColumn
            {
                Header = new TextBlock { Text = column.Header, ToolTip = column.Tooltip },
                Binding = new Binding(binding.DisplayPath),
                SortMemberPath = binding.SortPath,
                CellStyle = binding.UseNameBand ? nameStyle : metricStyle,
                Width = DataGridLength.Auto,
            };
            SkillsGrid.Columns.Add(gridColumn);
        }

        string? defaultSortPath = pointsSortPath ?? activationsSortPath;

        if (CollectionViewSource.GetDefaultView(SkillsGrid.ItemsSource) is not ListCollectionView view)
        {
            return;
        }

        view.CustomSort = null;
        view.SortDescriptions.Clear();
        if (defaultSortPath != null)
        {
            view.SortDescriptions.Add(new SortDescription(defaultSortPath, ListSortDirection.Descending));
        }

        foreach (DataGridColumn column in SkillsGrid.Columns)
        {
            column.SortDirection = column.SortMemberPath == defaultSortPath
                ? ListSortDirection.Descending
                : null;
        }
    }

    /// <summary>
    /// Header click. Most columns use the grid's own sort. Δ wit uses
    /// <see cref="SkillWitDeltaSort"/> so a blank cell sorts last when ascending,
    /// matching the WinForms SortCompare handler.
    /// </summary>
    private void SkillsGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (CollectionViewSource.GetDefaultView(SkillsGrid.ItemsSource) is not ListCollectionView view)
        {
            return;
        }

        ListSortDirection next = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        if (e.Column.SortMemberPath == nameof(SkillRowViewModel.WitDeltaPercent))
        {
            e.Handled = true;
            view.SortDescriptions.Clear();
            view.CustomSort = new WitDeltaRowComparer(next);
            foreach (DataGridColumn column in SkillsGrid.Columns)
            {
                column.SortDirection = null;
            }

            e.Column.SortDirection = next;
            return;
        }

        if (view.CustomSort != null)
        {
            view.CustomSort = null;
        }
    }

    private readonly record struct SkillColumnBinding(string DisplayPath, string SortPath, bool UseNameBand)
    {
        public static SkillColumnBinding For(string columnId)
        {
            if (columnId == GridColumnIds.Skills.Skill)
            {
                return new(nameof(SkillRowViewModel.SkillName), nameof(SkillRowViewModel.SkillName), true);
            }

            if (columnId == GridColumnIds.Skills.PtsRace)
            {
                return new(nameof(SkillRowViewModel.PointsPerRaceText), nameof(SkillRowViewModel.PointsPerRace), false);
            }

            if (columnId == GridColumnIds.Skills.Activations)
            {
                return new(nameof(SkillRowViewModel.ActivationCount), nameof(SkillRowViewModel.ActivationCount), false);
            }

            if (columnId == GridColumnIds.Skills.PerRacePct)
            {
                return new(nameof(SkillRowViewModel.PerRacePercentText), nameof(SkillRowViewModel.PerRacePercent), false);
            }

            if (columnId == GridColumnIds.Skills.ProcsPerRacePct)
            {
                return new(nameof(SkillRowViewModel.ProcsPerRacePercentText), nameof(SkillRowViewModel.ProcsPerRacePercent), false);
            }

            if (columnId == GridColumnIds.Skills.WitDelta)
            {
                return new(nameof(SkillRowViewModel.WitDeltaText), nameof(SkillRowViewModel.WitDeltaPercent), false);
            }

            return new(nameof(SkillRowViewModel.SkillName), nameof(SkillRowViewModel.SkillName), false);
        }
    }

    /// <summary>ListCollectionView.CustomSort needs the non-generic comparer.</summary>
    private sealed class WitDeltaRowComparer : IComparer
    {
        private readonly int _direction;

        public WitDeltaRowComparer(ListSortDirection direction)
        {
            _direction = direction == ListSortDirection.Descending ? -1 : 1;
        }

        public int Compare(object? x, object? y)
        {
            double? left = (x as SkillRowViewModel)?.WitDeltaPercent;
            double? right = (y as SkillRowViewModel)?.WitDeltaPercent;
            return _direction * SkillWitDeltaSort.Compare(left, right);
        }
    }
}
