using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using UmaParser.Converters;
using UmaParser.Presentation;
using UmaParser.ViewModels;

namespace UmaParser.Views;

public partial class ResultsView : UserControl
{
    private readonly ScoreHeatBrushConverter _heatBrush = new();
    private ResultsViewModel? _viewModel;

    public ResultsView()
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

        _viewModel = e.NewValue as ResultsViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyColumns();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ResultsViewModel.GridRevision) or nameof(ResultsViewModel.Columns))
        {
            ApplyColumns();
        }
    }

    /// <summary>
    /// One text column per file. WinForms does not sort this grid, so these columns are not sortable.
    /// The total row stays last because nothing reorders the rows.
    /// </summary>
    private void ApplyColumns()
    {
        ResultsGrid.Columns.Clear();
        if (_viewModel == null)
        {
            return;
        }

        var baseStyle = (Style)FindResource("ResultsCellStyle");
        var outlierBrush = (Brush)FindResource("ResultsOutlierBrush");
        for (int index = 0; index < _viewModel.Columns.Count; index++)
        {
            ResultsColumn column = _viewModel.Columns[index];
            ResultsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = new TextBlock
                {
                    Text = column.Header,
                    ToolTip = column.ToolTip.Length == 0 ? null : column.ToolTip,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                },
                Binding = new Binding($"Cells[{index}].Text"),
                CellStyle = BuildCellStyle(baseStyle, outlierBrush, index),
                Width = DataGridLength.Auto,
                CanUserSort = false,
            });
        }

        ResultsGrid.UnselectAll();
    }

    private Style BuildCellStyle(Style baseStyle, Brush outlierBrush, int index)
    {
        var style = new Style(typeof(DataGridCell), baseStyle);
        style.Setters.Add(new Setter(
            Control.BackgroundProperty,
            new Binding($"Cells[{index}].Heat") { Converter = _heatBrush }));

        var outlier = new DataTrigger
        {
            Binding = new Binding($"Cells[{index}].Outlier"),
            Value = true,
        };
        outlier.Setters.Add(new Setter(Control.BackgroundProperty, outlierBrush));
        outlier.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));
        style.Triggers.Add(outlier);

        var footer = new DataTrigger
        {
            Binding = new Binding(nameof(ResultsGridRow.IsFooter)),
            Value = true,
        };
        footer.Setters.Add(new Setter(Control.BackgroundProperty, SystemColors.ControlBrush));
        footer.Setters.Add(new Setter(Control.ForegroundProperty, SystemColors.ControlTextBrush));
        footer.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        style.Triggers.Add(footer);

        var bold = new DataTrigger
        {
            Binding = new Binding($"Cells[{index}].Bold"),
            Value = true,
        };
        bold.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        style.Triggers.Add(bold);

        var boundary = new DataTrigger
        {
            Binding = new Binding(nameof(ResultsGridRow.IsTeamBoundary)),
            Value = true,
        };
        boundary.Setters.Add(new Setter(Control.BorderBrushProperty, SystemColors.ControlDarkBrush));
        boundary.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 2, 0, 0)));
        style.Triggers.Add(boundary);
        return style;
    }
}
