using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using UmaParser.Analysis;
using UmaParser.Presentation;
using UmaParser.ViewModels;

namespace UmaParser.Views;

public partial class AnalysisView : UserControl
{
    private AnalysisViewModel? _viewModel;

    public AnalysisView()
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

        _viewModel = e.NewValue as AnalysisViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyColumns();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // VisibleColumns changes on the same drop as GridRevision. Rebuild once.
        if (e.PropertyName == nameof(AnalysisViewModel.GridRevision))
        {
            ApplyColumns();
        }
    }

    /// <summary>
    /// Builds DataGrid columns from the view model's visible column list, in that order.
    /// A DataGrid column is not a visual child, so its visibility cannot be bound.
    /// Which columns exist is decided in Core. This method only creates them.
    /// </summary>
    private void ApplyColumns()
    {
        long started = Stopwatch.GetTimestamp();
        AnalysisGrid.Columns.Clear();
        if (_viewModel == null)
        {
            return;
        }

        foreach (GridColumnDefinition<UmaAnalysisRow> column in _viewModel.VisibleColumns)
        {
            AnalysisColumnBinding binding = AnalysisColumnBinding.For(column.Id);
            AnalysisGrid.Columns.Add(new DataGridTextColumn
            {
                Header = new TextBlock { Text = column.Header, ToolTip = column.Tooltip },
                Binding = new Binding(binding.DisplayPath),
                CellStyle = (Style)FindResource(binding.CellStyleKey),
                Width = DataGridLength.Auto,
                CanUserSort = false,
            });
        }

        AnalysisGrid.UnselectAll();
        double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Trace.WriteLine(
            $"UmaParser Team Analysis grid: {AnalysisGrid.Columns.Count} columns, {_viewModel.Rows.Count} rows, built in {elapsed:0.0} ms");
    }

    private readonly record struct AnalysisColumnBinding(string DisplayPath, string CellStyleKey)
    {
        public static AnalysisColumnBinding For(string columnId)
        {
            if (columnId == GridColumnIds.Analysis.Uma)
            {
                return new(nameof(AnalysisGridRow.Uma), "AnalysisUmaCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.Style)
            {
                return new(nameof(AnalysisGridRow.Style), "AnalysisStyleCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.Avg)
            {
                return new(nameof(AnalysisGridRow.Avg), "AnalysisCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.Skills)
            {
                return new(nameof(AnalysisGridRow.Skills), "AnalysisCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.SkillCv)
            {
                return new(nameof(AnalysisGridRow.SkillCv), "AnalysisCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.SkillRatio)
            {
                return new(nameof(AnalysisGridRow.SkillRatio), "AnalysisCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.Other)
            {
                return new(nameof(AnalysisGridRow.Other), "AnalysisCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.OtherCv)
            {
                return new(nameof(AnalysisGridRow.OtherCv), "AnalysisCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.AvgPlace)
            {
                return new(nameof(AnalysisGridRow.AvgPlace), "AnalysisCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.Trimmed)
            {
                return new(nameof(AnalysisGridRow.Trimmed), "AnalysisCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.Cv)
            {
                return new(nameof(AnalysisGridRow.Cv), "AnalysisCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.AdjCv)
            {
                return new(nameof(AnalysisGridRow.AdjCv), "AnalysisCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.Gap)
            {
                return new(nameof(AnalysisGridRow.Gap), "AnalysisCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.Ceiling)
            {
                return new(nameof(AnalysisGridRow.Ceiling), "AnalysisCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.Floor)
            {
                return new(nameof(AnalysisGridRow.Floor), "AnalysisCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.Spread)
            {
                return new(nameof(AnalysisGridRow.Spread), "AnalysisCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.Retrain)
            {
                return new(nameof(AnalysisGridRow.Retrain), "AnalysisRetrainCellStyle");
            }

            if (columnId == GridColumnIds.Analysis.AceDelta)
            {
                return new(nameof(AnalysisGridRow.AceDelta), "AnalysisAceDeltaCellStyle");
            }

            return new(nameof(AnalysisGridRow.Uma), "AnalysisCellStyle");
        }
    }
}
