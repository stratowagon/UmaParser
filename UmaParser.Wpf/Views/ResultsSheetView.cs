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

    private readonly Dictionary<(string Text, ResultsTextWeight Weight, GlyphBrush Brush), FormattedText> _text = new();
    private ResultsSheetPaint? _paint;
    private readonly ResultsInteraction _pointer = new();

    private ResultsSheetModel _model = ResultsSheetModel.Empty;
    private Window? _window;
    private double[] _widths = [];
    private double[] _preferred = [];
    private double[] _prefix = [0];
    private double _rowHeight = 20;
    private double _horizontalOffset;
    private double _verticalOffset;
    private double _pixelsPerDip = 1;
    private int _tipColumn = -2;
    private double _resizeStartX;
    private double _resizeStartWidth;
    private bool _traceNextPaint;
    private string _fontKey = "";

    public ResultsSheetView()
    {
        Focusable = true;
        FocusVisualStyle = null;
        SnapsToDevicePixels = true;
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopy, CanCopy));
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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
        ReleasePointer();
        _pointer.Clear();
        _model = model;
        _horizontalOffset = 0;
        _verticalOffset = 0;
        _tipColumn = -2;
        ToolTip = null;
        Cursor = null;
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

    /// <summary>
    /// Theme change. Copies new heat colors onto the cells already on screen, drops the text
    /// cache (each entry holds the brush it was drawn with), and keeps the scroll position.
    /// </summary>
    internal void RefreshAppearance(IReadOnlyList<ResultsGridRow> rows)
    {
        CopyHeat(rows);
        _paint = ResultsSheetPaint.Capture();
        _text.Clear();
        InvalidateVisual();
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

        dc.DrawRectangle(Paint.Backdrop, null, new Rect(0, 0, width, height));
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
        ResultsSheetHit hit = Hit(point);
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool resetWidth = _pointer.Press(hit, e.ClickCount, shift, control, point.X, point.Y);
        if (resetWidth && hit.ResizeColumn >= 0 && hit.ResizeColumn < _preferred.Length)
        {
            _widths[hit.ResizeColumn] = _preferred[hit.ResizeColumn];
            RebuildPrefix();
            ClampOffsets();
            ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (_pointer.Drag == ResultsDrag.Resizing && hit.ResizeColumn >= 0 && hit.ResizeColumn < _widths.Length)
        {
            _resizeStartX = point.X;
            _resizeStartWidth = _widths[hit.ResizeColumn];
        }

        // Capture waits until the pointer actually moves. A click must not hold the mouse.
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        Point point = e.GetPosition(this);
        ResultsSheetHit hit = Hit(point);
        bool wasCapturing = _pointer.CaptureHeld;
        _pointer.Move(hit, e.LeftButton == MouseButtonState.Pressed, point.X, point.Y);
        if (!wasCapturing && _pointer.CaptureHeld && !CaptureMouse())
        {
            _pointer.NoteCaptureFailed();
        }

        if (_pointer.Drag == ResultsDrag.Resizing
            && _pointer.CaptureHeld
            && _pointer.ResizeColumn >= 0
            && _pointer.ResizeColumn < _widths.Length)
        {
            _widths[_pointer.ResizeColumn] = Math.Max(MinColumnWidth, _resizeStartWidth + (point.X - _resizeStartX));
            RebuildPrefix();
            ClampOffsets();
            _traceNextPaint = true;
            InvalidateVisual();
            ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (_pointer.Drag == ResultsDrag.Selecting)
        {
            InvalidateVisual();
        }

        Cursor = _pointer.Drag == ResultsDrag.Resizing || hit.IsResize ? Cursors.SizeWE : null;
        UpdateTooltip(hit);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        ReleasePointer();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        _pointer.Cancel();
        Cursor = null;
        base.OnLostMouseCapture(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (_tipColumn != -1)
        {
            _tipColumn = -1;
            ToolTip = null;
        }

        // Leave is not delivered for a captured drag. Without capture, drop the gesture
        // and the resize cursor so the tabs and scrollbar see the pointer again.
        if (!IsMouseCaptured)
        {
            Cursor = null;
            if (_pointer.Pressed)
            {
                _pointer.Release();
            }
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
            case Key.Escape:
                ReleasePointer();
                e.Handled = true;
                break;
            case Key.Up:
                _pointer.MoveRow(-1, shift, _model.Rows.Count);
                InvalidateVisual();
                e.Handled = true;
                break;
            case Key.Down:
                _pointer.MoveRow(1, shift, _model.Rows.Count);
                InvalidateVisual();
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
                _pointer.SelectAll(_model.Rows.Count);
                InvalidateVisual();
                e.Handled = true;
                break;
            default:
                base.OnKeyDown(e);
                break;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Window? window = Window.GetWindow(this);
        if (!ReferenceEquals(window, _window))
        {
            if (_window != null)
            {
                _window.Deactivated -= OnWindowDeactivated;
            }

            _window = window;
            if (_window != null)
            {
                _window.Deactivated += OnWindowDeactivated;
            }
        }

        if (_widths.Length != _model.Columns.Count)
        {
            EnsureMeasured(force: true);
            InvalidateMeasure();
            InvalidateVisual();
            ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_window != null)
        {
            _window.Deactivated -= OnWindowDeactivated;
            _window = null;
        }

        ReleasePointer();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => ReleasePointer();

    /// <summary>Mouse up, Escape, deactivate, or a new sheet. Lets the rest of the window take clicks.</summary>
    private void ReleasePointer()
    {
        _pointer.Release();
        Cursor = null;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void DrawHeader(DrawingContext dc, ColumnWindow window, double width)
    {
        dc.DrawRectangle(Paint.Header, null, new Rect(0, 0, width, HeaderHeight));
        double yLine = HeaderHeight - 0.5;
        dc.DrawLine(Paint.GridLine, new Point(0, yLine), new Point(width, yLine));
        for (int column = window.First; column < window.LastExclusive; column++)
        {
            double left = _prefix[column] - _horizontalOffset;
            double columnWidth = _widths[column];
            var rect = new Rect(left, 0, columnWidth, HeaderHeight);
            dc.PushClip(new RectangleGeometry(rect));
            FormattedText header = Text(_model.Columns[column].Header, ResultsTextWeight.Normal, GlyphBrush.Header);
            double textX = left + Math.Max(2, (columnWidth - header.Width) / 2);
            double textY = Math.Max(0, (HeaderHeight - header.Height) / 2);
            dc.DrawText(header, new Point(textX, textY));
            dc.Pop();
            double edge = left + columnWidth - 0.5;
            dc.DrawLine(Paint.GridLine, new Point(edge, 2), new Point(edge, HeaderHeight - 2));
        }
    }

    private void DrawRow(DrawingContext dc, int rowIndex, ColumnWindow window)
    {
        ResultsSheetRow row = _model.Rows[rowIndex];
        double top = HeaderHeight + (rowIndex * _rowHeight) - _verticalOffset;
        bool selected = _pointer.IsSelected(rowIndex);
        bool active = IsKeyboardFocusWithin;
        for (int column = window.First; column < window.LastExclusive; column++)
        {
            ResultsSheetCell cell = row.Cells[column];
            double left = _prefix[column] - _horizontalOffset;
            double columnWidth = _widths[column];
            var rect = new Rect(left, top, columnWidth, _rowHeight);
            ResultsRowChrome chrome = ResultsRowPaint.Chrome(selected, active);
            dc.DrawRectangle(Fill(row, cell, chrome), null, rect);
            dc.PushClip(new RectangleGeometry(rect));
            if (cell.Text.Length > 0)
            {
                FormattedText text = Text(cell.Text, cell.Weight, GlyphFor(cell, chrome));
                double textY = top + (row.TeamBoundary ? 2 : 1);
                dc.DrawText(text, new Point(left + 2, textY));
            }

            dc.Pop();

            dc.DrawLine(Paint.GridLine, new Point(left + columnWidth - 0.5, top), new Point(left + columnWidth - 0.5, top + _rowHeight));
            dc.DrawLine(Paint.GridLine, new Point(left, top + _rowHeight - 0.5), new Point(left + columnWidth, top + _rowHeight - 0.5));
            if (row.TeamBoundary)
            {
                double lineY = top + 1;
                dc.DrawLine(Paint.TeamLine, new Point(left, lineY), new Point(left + columnWidth, lineY));
            }
        }
    }

    /// <summary>
    /// A selected row uses a filled highlight over the heat map. Unselected cells keep
    /// their content fill. Highlight text is the matching foreground so the score stays readable.
    /// </summary>
    private Brush Fill(ResultsSheetRow row, ResultsSheetCell cell, ResultsRowChrome chrome)
    {
        switch (chrome)
        {
            case ResultsRowChrome.ActiveSelection:
                return Paint.Selection;
            case ResultsRowChrome.InactiveSelection:
                return Paint.InactiveSelection;
        }

        if (row.Footer)
        {
            return Paint.Footer;
        }

        if (cell.Outlier)
        {
            return Paint.Outlier;
        }

        if (cell.Heat is ScoreHeatColor heat)
        {
            return Paint.Heat(heat);
        }

        return Paint.Row;
    }

    private static GlyphBrush GlyphFor(ResultsSheetCell cell, ResultsRowChrome chrome)
    {
        switch (chrome)
        {
            case ResultsRowChrome.ActiveSelection:
                return GlyphBrush.Highlight;
            case ResultsRowChrome.InactiveSelection:
                return GlyphBrush.InactiveHighlight;
        }

        return cell.Tone switch
        {
            ResultsTextTone.Outlier => GlyphBrush.Outlier,
            ResultsTextTone.Footer => GlyphBrush.Header,
            ResultsTextTone.Theme when cell.Heat != null => GlyphBrush.Heat,
            ResultsTextTone.Theme => GlyphBrush.Body,
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
            Text("Mg", ResultsTextWeight.Normal, GlyphBrush.Body).Height,
            Math.Max(
                Text("Mg", ResultsTextWeight.SemiBold, GlyphBrush.Body).Height,
                Text("Mg", ResultsTextWeight.Bold, GlyphBrush.Body).Height));
        // Room for a 1px grid line, plus the 2px team line on boundary rows.
        _rowHeight = Math.Ceiling(sample + 3);

        for (int column = 0; column < count; column++)
        {
            double width = Text(_model.Columns[column].Header, ResultsTextWeight.Normal, GlyphBrush.Header).Width + HeaderChrome;
            foreach (ResultsSheetRow row in _model.Rows)
            {
                ResultsSheetCell cell = row.Cells[column];
                width = Math.Max(width, Text(cell.Text, cell.Weight, GlyphFor(cell, ResultsRowChrome.Content)).Width + CellChrome);
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

    private Brush BrushFor(GlyphBrush brush) => brush switch
    {
        GlyphBrush.Body => Paint.BodyText,
        GlyphBrush.Heat => Paint.HeatText,
        GlyphBrush.Header => Paint.HeaderText,
        GlyphBrush.Outlier => Paint.OutlierText,
        GlyphBrush.Highlight => Paint.SelectionText,
        GlyphBrush.InactiveHighlight => Paint.InactiveSelectionText,
        _ => throw new InvalidOperationException($"Results text brush {brush} is not drawn."),
    };

    /// <summary>Which frozen brush a string is drawn with. The cache key, so a null brush cannot get in.</summary>
    private enum GlyphBrush
    {
        Body,
        Heat,
        Header,
        Outlier,
        Highlight,
        InactiveHighlight,
    };

    private static FontWeight FontWeightFor(ResultsTextWeight weight) => weight switch
    {
        ResultsTextWeight.Bold => FontWeights.Bold,
        ResultsTextWeight.SemiBold => FontWeights.SemiBold,
        _ => FontWeights.Normal,
    };

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

    private ResultsSheetHit Hit(Point point) =>
        ResultsSheetHitTest.Hit(
            point.X,
            point.Y,
            HeaderHeight,
            _rowHeight,
            _verticalOffset,
            _model.Rows.Count,
            _prefix,
            _horizontalOffset,
            ResizeSlop);

    private void UpdateTooltip(ResultsSheetHit hit)
    {
        int column = hit.InHeader ? hit.Column : -1;
        if (column == _tipColumn)
        {
            return;
        }

        _tipColumn = column;
        string tip = column >= 0 ? _model.Columns[column].ToolTip : "";
        ToolTip = tip.Length == 0 ? null : tip;
    }

    private void MoveColumn(int delta)
    {
        int previous = _pointer.CurrentColumn;
        _pointer.MoveColumn(delta, _model.Columns.Count);
        int next = _pointer.CurrentColumn;
        if (next < 0 || next >= _prefix.Length - 1)
        {
            return;
        }

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
        else if (next != previous)
        {
            InvalidateVisual();
        }
    }

    private void CanCopy(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _pointer.SelectedCount > 0 && _model.Columns.Count > 0;
        e.Handled = true;
    }

    private void OnCopy(object sender, ExecutedRoutedEventArgs e)
    {
        if (_pointer.SelectedCount == 0 || _model.Columns.Count == 0)
        {
            return;
        }

        var lines = new List<string>(_pointer.SelectedCount);
        foreach (int row in _pointer.SelectedRows)
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

    private ResultsSheetPaint Paint => _paint ??= ResultsSheetPaint.Capture();

    private void CopyHeat(IReadOnlyList<ResultsGridRow> rows)
    {
        int rowCount = Math.Min(rows.Count, _model.Rows.Count);
        for (int row = 0; row < rowCount; row++)
        {
            IList<ResultsCell> source = rows[row].Cells;
            IReadOnlyList<ResultsSheetCell> dest = _model.Rows[row].Cells;
            int count = Math.Min(source.Count, dest.Count);
            for (int column = 0; column < count; column++)
            {
                dest[column].Heat = source[column].Heat;
            }
        }
    }
}
