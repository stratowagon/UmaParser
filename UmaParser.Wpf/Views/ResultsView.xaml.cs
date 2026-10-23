using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using UmaParser.Presentation;
using UmaParser.ViewModels;

namespace UmaParser.Views;

public partial class ResultsView : UserControl
{
    private ResultsViewModel? _viewModel;
    private Brush? _outlierBrush;
    private int _appliedRevision = -1;

    public ResultsView()
    {
        InitializeComponent();
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
        // Loaded so font inheritance and DPI are the ones the grid will paint with.
        if (IsLoaded)
        {
            ApplyColumns();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => ApplyColumns();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Columns changes on the same drop as GridRevision. Rebuild once, from the revision.
        if (e.PropertyName == nameof(ResultsViewModel.GridRevision) && IsLoaded)
        {
            ApplyColumns();
        }
    }

    /// <summary>
    /// One text column per file. Widths are pixels measured from the header and the
    /// cell text. An Auto width leaves DisplayValue unset, and the cells panel then
    /// realizes every column on the first layout. A pixel width is known up front, so
    /// column virtualization only builds the columns in view.
    /// </summary>
    private void ApplyColumns()
    {
        if (_viewModel == null)
        {
            ResultsGrid.ItemsSource = null;
            ResultsGrid.Columns.Clear();
            _appliedRevision = -1;
            return;
        }

        if (_appliedRevision == _viewModel.GridRevision)
        {
            return;
        }

        long started = Stopwatch.GetTimestamp();
        List<ResultsDisplayRow> rows = ResultsGridPresentation.Build(_viewModel.Rows, OutlierBrush());
        var baseStyle = (Style)FindResource("ResultsCellStyle");
        var measure = new TextWidthCache(ResultsGrid);

        // Drop the rows before swapping columns so each added column does not
        // realize a cell. The single ItemsSource assignment below is the layout.
        ResultsGrid.ItemsSource = null;
        ResultsGrid.Columns.Clear();

        for (int index = 0; index < _viewModel.Columns.Count; index++)
        {
            ResultsColumn column = _viewModel.Columns[index];
            ResultsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = HeaderBlock(column),
                Binding = new Binding($"Cells[{index}].Text") { Mode = BindingMode.OneWay },
                CellStyle = CellStyle(baseStyle, index),
                Width = new DataGridLength(ColumnWidth(measure, column.Header, rows, index)),
                CanUserSort = false,
            });
        }

        ResultsGrid.ItemsSource = rows;
        ResultsGrid.UnselectAll();
        _appliedRevision = _viewModel.GridRevision;

        double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Trace.WriteLine(
            $"UmaParser Results grid: {ResultsGrid.Columns.Count} columns, {rows.Count} rows, built in {elapsed:0.0} ms");
    }

    private Brush OutlierBrush()
    {
        if (_outlierBrush != null)
        {
            return _outlierBrush;
        }

        var brush = (Brush)FindResource("ResultsOutlierBrush");
        if (brush.CanFreeze && !brush.IsFrozen)
        {
            brush.Freeze();
        }

        _outlierBrush = brush;
        return brush;
    }

    private static TextBlock HeaderBlock(ResultsColumn column)
    {
        return new TextBlock
        {
            Text = column.Header,
            ToolTip = column.ToolTip.Length == 0 ? null : column.ToolTip,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    private static Style CellStyle(Style baseStyle, int index)
    {
        var style = new Style(typeof(DataGridCell), baseStyle);
        style.Setters.Add(new Setter(Control.BackgroundProperty, PaintBinding($"Cells[{index}].Background")));
        style.Setters.Add(new Setter(Control.ForegroundProperty, PaintBinding($"Cells[{index}].Foreground")));
        style.Setters.Add(new Setter(
            Control.FontWeightProperty,
            new Binding($"Cells[{index}].FontWeight") { Mode = BindingMode.OneWay }));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, PaintBinding($"Cells[{index}].BorderBrush")));
        style.Setters.Add(new Setter(
            Control.BorderThicknessProperty,
            new Binding($"Cells[{index}].BorderThickness") { Mode = BindingMode.OneWay }));
        return style;
    }

    /// <summary>A null brush leaves the theme value in place instead of painting null.</summary>
    private static Binding PaintBinding(string path) => new(path)
    {
        Mode = BindingMode.OneWay,
        TargetNullValue = DependencyProperty.UnsetValue,
    };

    private static double ColumnWidth(TextWidthCache measure, string header, List<ResultsDisplayRow> rows, int index)
    {
        double width = measure.Width(header, FontWeights.Normal) + ResultsGridPresentation.HeaderChrome;
        foreach (ResultsDisplayRow row in rows)
        {
            ResultsDisplayCell cell = row.Cells[index];
            width = Math.Max(width, measure.Width(cell.Text, cell.FontWeight) + ResultsGridPresentation.CellChrome);
        }

        return measure.SnapUp(Math.Max(width, measure.MinColumnWidth));
    }

    /// <summary>
    /// Width of a string in the grid's font. Cached for the build: a 400-file grid
    /// repeats the same score text, and the header text, across rows.
    /// </summary>
    private sealed class TextWidthCache
    {
        private readonly FontFamily _family;
        private readonly double _size;
        private readonly FontStyle _style;
        private readonly FontStretch _stretch;
        private readonly TextFormattingMode _formatting;
        private readonly TextRenderingMode _rendering;
        private readonly double _pixelsPerDip;
        private readonly Dictionary<(string Text, int Weight), double> _widths = new();

        public TextWidthCache(DataGrid grid)
        {
            _family = grid.FontFamily;
            _size = grid.FontSize;
            _style = grid.FontStyle;
            _stretch = grid.FontStretch;
            _formatting = TextOptions.GetTextFormattingMode(grid);
            _rendering = TextOptions.GetTextRenderingMode(grid);
            _pixelsPerDip = VisualTreeHelper.GetDpi(grid).PixelsPerDip;
            MinColumnWidth = grid.MinColumnWidth;
        }

        public double MinColumnWidth { get; }

        public double Width(string text, FontWeight weight)
        {
            var key = (text, weight.ToOpenTypeWeight());
            if (_widths.TryGetValue(key, out double cached))
            {
                return cached;
            }

            var block = new TextBlock
            {
                Text = text,
                FontFamily = _family,
                FontSize = _size,
                FontStyle = _style,
                FontWeight = weight,
                FontStretch = _stretch,
                TextWrapping = TextWrapping.NoWrap,
            };
            TextOptions.SetTextFormattingMode(block, _formatting);
            TextOptions.SetTextRenderingMode(block, _rendering);
            block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _widths[key] = block.DesiredSize.Width;
            return block.DesiredSize.Width;
        }

        public double SnapUp(double width)
        {
            if (_pixelsPerDip <= 0)
            {
                return Math.Ceiling(width);
            }

            // Exact device pixels stay put. Anything past that rounds up so a sub-pixel
            // measure does not clip the text. Pixel column widths do not shrink later.
            return Math.Ceiling((width * _pixelsPerDip) - 1e-4) / _pixelsPerDip;
        }
    }
}
