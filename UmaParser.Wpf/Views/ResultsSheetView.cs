using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using UmaParser.Presentation;

namespace UmaParser.Views;

/// <summary>
/// Results grid drawn as one element. A DataGrid builds a cell control per visible column,
/// and a page scroll does that for every newly revealed column across every row. This paints
/// the visible window with cached text and moves the window when the offset changes.
/// The name column is not frozen: WinForms does not freeze it either.
/// </summary>
public sealed class ResultsSheetView : FrameworkElement
{
    public const double HeaderHeight = 40;

    /// <summary>Aero2 column-header padding, left plus right (5 + 5).</summary>
    private const double HeaderChrome = 10;

    /// <summary>Text inset 2+2, cell border 1+1, and the vertical grid line.</summary>
    private const double CellChrome = 7;

    private const double MinColumnWidth = 20;
    private const double ResizeSlop = 5;

    private static readonly Pen GridLinePen = FreezePen(Brushes.Black, 1);
    private static readonly Pen TeamLinePen = FreezePen(SystemColors.ControlDarkBrush, 2);
    private static readonly Pen FocusPen = FreezePen(Brushes.Black, 1);
    private static readonly Pen HeaderLinePen = FreezePen(SystemColors.ControlDarkBrush, 1);
    private static readonly SolidColorBrush OutlierBrush = FreezeBrush(Color.FromRgb(0xFF, 0xFF, 0xC8));
    private static readonly LinearGradientBrush HeaderBrush = FreezeHeaderBrush();
    private static readonly Dictionary<ScoreHeatColor, SolidColorBrush> HeatBrushes = new();

    private readonly Dictionary<(string Text, ResultsTextWeight Weight, GlyphBrush Brush), FormattedText> _text = new();
    private readonly HashSet<int> _selected = new();

    private ResultsSheetModel _model = ResultsSheetModel.Empty;
    private double[] _widths = [];
    private double[] _preferred = [];
    private double[] _prefix = [0];
    private double _rowHeight = 20;
    private double _horizontalOffset;
    private double _verticalOffset;
    private double _pixelsPerDip = 1;
    private int _currentRow = -1;
    private int _currentColumn = -1;
    private int _anchor = -1;
    private int _resizeColumn = -1;
    private int _tipColumn = -2;
    private double _resizeStartX;
    private double _resizeStartWidth;
    private bool _selecting;
    private bool _traceNextPaint;
    private string _fontKey = "";

    public ResultsSheetView()
    {
        Focusable = true;
        FocusVisualStyle = null;
        SnapsToDevicePixels = true;
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopy, CanCopy));
        Loaded += OnLoaded;
    }

    public event EventHandler? ScrollMetricsChanged;

    public double RowHeight => _rowHeight;

    public double ExtentWidth => _prefix.Length == 0 ? 0 : _prefix[^1];

    /// <summary>Height of the rows. The header stays put, matching a DataGrid.</summary>
    public double ExtentHeight => _rowHeight * _model.Rows.Count;

    public double ViewportWidth => Math.Max(0, ActualWidth);

    public double ViewportHeight => Math.Max(0, ActualHeight - HeaderHeight);

    public double HorizontalOffset
    {
        get => _horizontalOffset;
        set => SetOffset(ref _horizontalOffset, value, Math.Max(0, ExtentWidth - ViewportWidth));
    }

    public double VerticalOffset
    {
        get => _verticalOffset;
        set => SetOffset(ref _verticalOffset, value, Math.Max(0, ExtentHeight - ViewportHeight));
    }

    internal void SetModel(ResultsSheetModel model)
    {
        long started = Stopwatch.GetTimestamp();
        _model = model;
        _horizontalOffset = 0;
        _verticalOffset = 0;
        _selected.Clear();
        _currentRow = -1;
        _currentColumn = -1;
        _anchor = -1;
        _tipColumn = -2;
        ToolTip = null;
        _text.Clear();
        EnsureMeasured(force: true);
        _traceNextPaint = true;
        InvalidateMeasure();
        InvalidateVisual();
        ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
        double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Trace.WriteLine(
            $"UmaParser Results sheet: {model.Columns.Count} columns, {model.Rows.Count} rows, built in {elapsed:0.0} ms");
    }

    protected override Size MeasureOverride(Size available)
    {
        EnsureMeasured(force: false);
        double height = HeaderHeight + ExtentHeight;
        double width = double.IsPositiveInfinity(available.Width) ? Math.Max(1, ExtentWidth) : Math.Max(0, available.Width);
        return new Size(width, height);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ClampOffsets();
        ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _text.Clear();
        EnsureMeasured(force: true);
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        long started = Stopwatch.GetTimestamp();
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        dc.DrawRectangle(SystemColors.ControlBrush, null, new Rect(0, 0, width, height));
        ColumnWindow window = ResultsSheetViewport.VisibleRange(_widths, _horizontalOffset, width);
        int firstRow = FirstVisibleRow();
        int lastRow = LastVisibleRow(height);

        dc.PushClip(new RectangleGeometry(new Rect(0, HeaderHeight, width, Math.Max(0, height - HeaderHeight))));
        for (int row = firstRow; row < lastRow; row++)
        {
            DrawRow(dc, row, window);
        }

        dc.Pop();
        DrawHeader(dc, window, width);

        if (_traceNextPaint)
        {
            _traceNextPaint = false;
            double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Trace.WriteLine(
                $"UmaParser Results paint: columns {window.First}-{window.LastExclusive} ({window.Count} columns), rows {firstRow}-{lastRow}, {elapsed:0.0} ms");
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Focus();
        Point point = e.GetPosition(this);
        if (point.Y <= HeaderHeight)
        {
            int boundary = BoundaryAt(point.X);
            if (boundary >= 0 && e.ClickCount == 2)
            {
                _widths[boundary] = _preferred[boundary];
                _resizeColumn = -1;
                if (IsMouseCaptured)
                {
                    ReleaseMouseCapture();
                }

                RebuildPrefix();
                ClampOffsets();
                InvalidateVisual();
                ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (boundary >= 0)
            {
                _resizeColumn = boundary;
                _resizeStartX = point.X;
                _resizeStartWidth = _widths[boundary];
                _selecting = false;
                CaptureMouse();
            }

            e.Handled = true;
            return;
        }

        int row = RowAt(point.Y);
        int column = ColumnAt(point.X);
        if (row < 0)
        {
            return;
        }

        _currentRow = row;
        if (column >= 0)
        {
            _currentColumn = column;
        }

        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (control && !shift)
        {
            if (!_selected.Add(row))
            {
                _selected.Remove(row);
            }

            _anchor = row;
            _selecting = false;
        }
        else if (shift)
        {
            SelectRange(_anchor < 0 ? row : _anchor, row);
            _selecting = false;
        }
        else
        {
            _selected.Clear();
            _selected.Add(row);
            _anchor = row;
            _selecting = true;
            CaptureMouse();
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        Point point = e.GetPosition(this);
        if (_resizeColumn >= 0 && _resizeColumn < _widths.Length)
        {
            _widths[_resizeColumn] = Math.Max(MinColumnWidth, _resizeStartWidth + (point.X - _resizeStartX));
            RebuildPrefix();
            ClampOffsets();
            _traceNextPaint = true;
            InvalidateVisual();
            ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_selecting && e.LeftButton == MouseButtonState.Pressed)
        {
            int row = RowAt(point.Y);
            if (row >= 0)
            {
                _currentRow = row;
                SelectRange(_anchor < 0 ? row : _anchor, row);
                InvalidateVisual();
            }

            return;
        }

        Cursor = point.Y <= HeaderHeight && BoundaryAt(point.X) >= 0 ? Cursors.SizeWE : null;
        UpdateTooltip(point);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        EndDrag();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        EndDrag();
        base.OnLostMouseCapture(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (_tipColumn != -1)
        {
            _tipColumn = -1;
            ToolTip = null;
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        double step = Math.Sign(e.Delta) * 48;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            HorizontalOffset -= step;
            e.Handled = true;
            return;
        }

        if (ExtentHeight > ViewportHeight)
        {
            VerticalOffset -= step;
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_model.Rows.Count == 0 || _model.Columns.Count == 0)
        {
            base.OnKeyDown(e);
            return;
        }

        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        switch (e.Key)
        {
            case Key.Up:
                MoveRow(-1, shift);
                e.Handled = true;
                break;
            case Key.Down:
                MoveRow(1, shift);
                e.Handled = true;
                break;
            case Key.Left:
                MoveColumn(-1);
                e.Handled = true;
                break;
            case Key.Right:
                MoveColumn(1);
                e.Handled = true;
                break;
            case Key.A when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                SelectAllRows();
                e.Handled = true;
                break;
            default:
                base.OnKeyDown(e);
                break;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_widths.Length != _model.Columns.Count)
        {
            EnsureMeasured(force: true);
            InvalidateMeasure();
            InvalidateVisual();
            ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DrawHeader(DrawingContext dc, ColumnWindow window, double width)
    {
        dc.DrawRectangle(HeaderBrush, null, new Rect(0, 0, width, HeaderHeight));
        double yLine = HeaderHeight - 0.5;
        dc.DrawLine(HeaderLinePen, new Point(0, yLine), new Point(width, yLine));
        for (int column = window.First; column < window.LastExclusive; column++)
        {
            double left = _prefix[column] - _horizontalOffset;
            double columnWidth = _widths[column];
            var rect = new Rect(left, 0, columnWidth, HeaderHeight);
            dc.PushClip(new RectangleGeometry(rect));
            FormattedText header = Text(_model.Columns[column].Header, ResultsTextWeight.Normal, GlyphBrush.ControlText);
            double textX = left + Math.Max(2, (columnWidth - header.Width) / 2);
            double textY = Math.Max(0, (HeaderHeight - header.Height) / 2);
            dc.DrawText(header, new Point(textX, textY));
            dc.Pop();
            double edge = left + columnWidth - 0.5;
            dc.DrawLine(HeaderLinePen, new Point(edge, 2), new Point(edge, HeaderHeight - 2));
        }
    }

    private void DrawRow(DrawingContext dc, int rowIndex, ColumnWindow window)
    {
        ResultsSheetRow row = _model.Rows[rowIndex];
        double top = HeaderHeight + (rowIndex * _rowHeight) - _verticalOffset;
        bool selected = _selected.Contains(rowIndex);
        bool active = IsKeyboardFocusWithin;
        for (int column = window.First; column < window.LastExclusive; column++)
        {
            ResultsSheetCell cell = row.Cells[column];
            double left = _prefix[column] - _horizontalOffset;
            double columnWidth = _widths[column];
            var rect = new Rect(left, top, columnWidth, _rowHeight);
            bool ownFill = row.Footer || cell.Outlier || cell.Heat is not null;
            Brush fill = Fill(row, cell, selected, active);
            dc.DrawRectangle(fill, null, rect);
            dc.PushClip(new RectangleGeometry(rect));
            if (cell.Text.Length > 0)
            {
                FormattedText text = Text(cell.Text, cell.Weight, GlyphFor(cell, selected, ownFill, active));
                double textY = top + (row.TeamBoundary ? 2 : 1);
                dc.DrawText(text, new Point(left + 2, textY));
            }

            dc.Pop();

            dc.DrawLine(GridLinePen, new Point(left + columnWidth - 0.5, top), new Point(left + columnWidth - 0.5, top + _rowHeight));
            dc.DrawLine(GridLinePen, new Point(left, top + _rowHeight - 0.5), new Point(left + columnWidth, top + _rowHeight - 0.5));
            if (row.TeamBoundary)
            {
                double lineY = top + 1;
                dc.DrawLine(TeamLinePen, new Point(left, lineY), new Point(left + columnWidth, lineY));
            }

            if (active && rowIndex == _currentRow && column == _currentColumn)
            {
                dc.DrawRectangle(null, FocusPen, new Rect(left + 1, top + 1, Math.Max(0, columnWidth - 2), Math.Max(0, _rowHeight - 2)));
            }
        }
    }

    private Brush Fill(ResultsSheetRow row, ResultsSheetCell cell, bool selected, bool active)
    {
        if (row.Footer)
        {
            return SystemColors.ControlBrush;
        }

        if (cell.Outlier)
        {
            return OutlierBrush;
        }

        if (cell.Heat is ScoreHeatColor heat)
        {
            return HeatBrush(heat);
        }

        if (selected)
        {
            return active
                ? SystemColors.HighlightBrush
                : SystemColors.InactiveSelectionHighlightBrush;
        }

        return SystemColors.WindowBrush;
    }

    private static GlyphBrush GlyphFor(ResultsSheetCell cell, bool selected, bool ownFill, bool active)
    {
        if (selected && !ownFill)
        {
            return active ? GlyphBrush.Highlight : GlyphBrush.InactiveHighlight;
        }

        return cell.Tone switch
        {
            ResultsTextTone.Black => GlyphBrush.Black,
            ResultsTextTone.Footer => GlyphBrush.ControlText,
            ResultsTextTone.Theme => GlyphBrush.ControlText,
            _ => throw new InvalidOperationException($"Results text tone {cell.Tone} is not drawn."),
        };
    }

    private void EnsureMeasured(bool force)
    {
        _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (_pixelsPerDip <= 0)
        {
            _pixelsPerDip = 1;
        }

        string fontKey = $"{TextElement.GetFontFamily(this)?.Source}|{TextElement.GetFontSize(this)}|{TextElement.GetFontStyle(this)}|{TextElement.GetFontStretch(this)}|{_pixelsPerDip}";
        if (!force && _widths.Length == _model.Columns.Count && fontKey == _fontKey)
        {
            return;
        }

        _fontKey = fontKey;
        _text.Clear();
        int count = _model.Columns.Count;
        _widths = new double[count];
        _preferred = new double[count];
        if (count == 0)
        {
            _rowHeight = 20;
            _prefix = [0];
            return;
        }

        double sample = Math.Max(
            Text("Mg", ResultsTextWeight.Normal, GlyphBrush.ControlText).Height,
            Math.Max(
                Text("Mg", ResultsTextWeight.SemiBold, GlyphBrush.ControlText).Height,
                Text("Mg", ResultsTextWeight.Bold, GlyphBrush.ControlText).Height));
        // Room for a 1px grid line, plus the 2px team line on boundary rows.
        _rowHeight = Math.Ceiling(sample + 3);

        for (int column = 0; column < count; column++)
        {
            double width = Text(_model.Columns[column].Header, ResultsTextWeight.Normal, GlyphBrush.ControlText).Width + HeaderChrome;
            foreach (ResultsSheetRow row in _model.Rows)
            {
                ResultsSheetCell cell = row.Cells[column];
                width = Math.Max(width, Text(cell.Text, cell.Weight, GlyphFor(cell, selected: false, ownFill: true, active: true)).Width + CellChrome);
            }

            width = Snap(Math.Max(width, MinColumnWidth));
            _preferred[column] = width;
            _widths[column] = width;
        }

        RebuildPrefix();
    }

    private void RebuildPrefix()
    {
        _prefix = new double[_widths.Length + 1];
        for (int i = 0; i < _widths.Length; i++)
        {
            _prefix[i + 1] = _prefix[i] + _widths[i];
        }
    }

    private FormattedText Text(string text, ResultsTextWeight weight, GlyphBrush brush)
    {
        var key = (text, weight, brush);
        if (_text.TryGetValue(key, out FormattedText? cached))
        {
            return cached;
        }

        FontFamily family = TextElement.GetFontFamily(this) ?? SystemFonts.MessageFontFamily;
        double fontSize = TextElement.GetFontSize(this);
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection,
            new Typeface(family, TextElement.GetFontStyle(this), FontWeightFor(weight), TextElement.GetFontStretch(this)),
            fontSize > 0 && !double.IsNaN(fontSize) ? fontSize : SystemFonts.MessageFontSize,
            BrushFor(brush),
            _pixelsPerDip);
        _text[key] = formatted;
        return formatted;
    }

    private static Brush BrushFor(GlyphBrush brush) => brush switch
    {
        GlyphBrush.Black => Brushes.Black,
        GlyphBrush.Highlight => SystemColors.HighlightTextBrush,
        GlyphBrush.InactiveHighlight => SystemColors.InactiveSelectionHighlightTextBrush,
        GlyphBrush.ControlText => SystemColors.ControlTextBrush,
        _ => throw new InvalidOperationException($"Results text brush {brush} is not drawn."),
    };

    /// <summary>Which frozen brush a string is drawn with. The cache key, so a null brush cannot get in.</summary>
    private enum GlyphBrush
    {
        ControlText,
        Black,
        Highlight,
        InactiveHighlight,
    };

    private static FontWeight FontWeightFor(ResultsTextWeight weight) => weight switch
    {
        ResultsTextWeight.Bold => FontWeights.Bold,
        ResultsTextWeight.SemiBold => FontWeights.SemiBold,
        _ => FontWeights.Normal,
    };

    private static SolidColorBrush HeatBrush(ScoreHeatColor color)
    {
        if (!HeatBrushes.TryGetValue(color, out SolidColorBrush? brush))
        {
            brush = FreezeBrush(Color.FromRgb(color.R, color.G, color.B));
            HeatBrushes[color] = brush;
        }

        return brush;
    }

    private void SetOffset(ref double field, double value, double max)
    {
        double clamped = Math.Clamp(value, 0, max);
        if (Math.Abs(clamped - field) < 0.1)
        {
            return;
        }

        field = clamped;
        _traceNextPaint = true;
        InvalidateVisual();
        ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClampOffsets()
    {
        _horizontalOffset = Math.Clamp(_horizontalOffset, 0, Math.Max(0, ExtentWidth - ViewportWidth));
        _verticalOffset = Math.Clamp(_verticalOffset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
    }

    private int FirstVisibleRow()
    {
        if (_rowHeight <= 0 || _model.Rows.Count == 0)
        {
            return 0;
        }

        return Math.Clamp((int)(_verticalOffset / _rowHeight), 0, _model.Rows.Count);
    }

    private int LastVisibleRow(double height)
    {
        if (_rowHeight <= 0 || _model.Rows.Count == 0)
        {
            return 0;
        }

        double bottom = _verticalOffset + Math.Max(0, height - HeaderHeight);
        int last = (int)Math.Ceiling(bottom / _rowHeight);
        return Math.Clamp(last, 0, _model.Rows.Count);
    }

    private int RowAt(double viewY)
    {
        if (_rowHeight <= 0 || viewY < HeaderHeight)
        {
            return -1;
        }

        int row = (int)((viewY - HeaderHeight + _verticalOffset) / _rowHeight);
        return row >= 0 && row < _model.Rows.Count ? row : -1;
    }

    private int ColumnAt(double viewX)
    {
        if (_widths.Length == 0)
        {
            return -1;
        }

        double x = viewX + _horizontalOffset;
        int lo = 0;
        int hi = _widths.Length - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (x < _prefix[mid])
            {
                hi = mid - 1;
            }
            else if (x >= _prefix[mid + 1])
            {
                lo = mid + 1;
            }
            else
            {
                return mid;
            }
        }

        return -1;
    }

    private int BoundaryAt(double viewX)
    {
        double x = viewX + _horizontalOffset;
        int found = -1;
        double best = ResizeSlop;
        for (int column = 0; column < _widths.Length; column++)
        {
            double distance = Math.Abs(x - _prefix[column + 1]);
            if (distance <= best)
            {
                best = distance;
                found = column;
            }
        }

        return found;
    }

    private void UpdateTooltip(Point point)
    {
        int column = point.Y <= HeaderHeight ? ColumnAt(point.X) : -1;
        if (column == _tipColumn)
        {
            return;
        }

        _tipColumn = column;
        string tip = column >= 0 ? _model.Columns[column].ToolTip : "";
        ToolTip = tip.Length == 0 ? null : tip;
    }

    private void MoveRow(int delta, bool extend)
    {
        int count = _model.Rows.Count;
        int current = _currentRow < 0 ? (delta > 0 ? -1 : count) : _currentRow;
        int next = Math.Clamp(current + delta, 0, count - 1);
        _currentRow = next;
        if (_currentColumn < 0)
        {
            _currentColumn = 0;
        }

        if (extend)
        {
            SelectRange(_anchor < 0 ? next : _anchor, next);
        }
        else
        {
            _selected.Clear();
            _selected.Add(next);
            _anchor = next;
        }

        InvalidateVisual();
    }

    private void MoveColumn(int delta)
    {
        int next = Math.Clamp((_currentColumn < 0 ? 0 : _currentColumn) + delta, 0, _model.Columns.Count - 1);
        _currentColumn = next;
        double left = _prefix[next];
        double right = _prefix[next + 1];
        if (left < _horizontalOffset)
        {
            HorizontalOffset = left;
        }
        else if (right > _horizontalOffset + ViewportWidth)
        {
            HorizontalOffset = right - ViewportWidth;
        }
        else
        {
            InvalidateVisual();
        }
    }

    private void SelectAllRows()
    {
        _selected.Clear();
        for (int row = 0; row < _model.Rows.Count; row++)
        {
            _selected.Add(row);
        }

        _anchor = 0;
        InvalidateVisual();
    }

    private void SelectRange(int from, int to)
    {
        _selected.Clear();
        int start = Math.Min(from, to);
        int end = Math.Max(from, to);
        for (int row = start; row <= end; row++)
        {
            _selected.Add(row);
        }
    }

    private void EndDrag()
    {
        _resizeColumn = -1;
        _selecting = false;
    }

    private void CanCopy(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _selected.Count > 0 && _model.Columns.Count > 0;
        e.Handled = true;
    }

    private void OnCopy(object sender, ExecutedRoutedEventArgs e)
    {
        if (_selected.Count == 0 || _model.Columns.Count == 0)
        {
            return;
        }

        var lines = new List<string>(_selected.Count);
        foreach (int row in _selected.Order())
        {
            var fields = new string[_model.Columns.Count];
            for (int column = 0; column < fields.Length; column++)
            {
                fields[column] = _model.Rows[row].Cells[column].Text;
            }

            lines.Add(string.Join('\t', fields));
        }

        Clipboard.SetText(string.Join("\r\n", lines));
        e.Handled = true;
    }

    private double Snap(double width)
    {
        return Math.Ceiling((width * _pixelsPerDip) - 1e-4) / _pixelsPerDip;
    }

    private static SolidColorBrush FreezeBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen FreezePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }

    private static LinearGradientBrush FreezeHeaderBrush()
    {
        var brush = new LinearGradientBrush(
            Color.FromRgb(0xF6, 0xF6, 0xF6),
            Color.FromRgb(0xE1, 0xE1, 0xE1),
            new Point(0, 0),
            new Point(0, 1));
        brush.Freeze();
        return brush;
    }
}
