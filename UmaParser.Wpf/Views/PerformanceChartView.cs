using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using UmaParser.Analysis;

namespace UmaParser.Views;

/// <summary>
/// Stacked base-score bars for the current roster, with the percentile spread under each bar
/// and a dashed team-average line. Brushes come from the light/dark theme dictionaries.
/// </summary>
public sealed class PerformanceChartView : FrameworkElement
{
    private const double LabelWidth = 200;
    private const double RowHeight = 56;
    private const double AxisHeight = 28;
    private const double RightPad = 18;
    private const double BarTop = 10;
    private const double BarHeight = 16;
    private const double LegendRowHeight = 20;

    private readonly ToolTip _tip = new()
    {
        StaysOpen = true,
        Placement = PlacementMode.Mouse,
        HorizontalOffset = 14,
        VerticalOffset = 18,
    };

    private readonly TextBlock _tipText = new()
    {
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 440,
    };

    private double _legendHeight = LegendRowHeight;
    private int _hover = -1;

    public PerformanceChartView()
    {
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 380,
            Content = _tipText,
        };
        _tip.Content = scroll;
    }

    public static readonly DependencyProperty ChartProperty = DependencyProperty.Register(
        nameof(Chart),
        typeof(PerformanceChart),
        typeof(PerformanceChartView),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((PerformanceChartView)d).OnChartChanged()));

    public PerformanceChart? Chart
    {
        get => (PerformanceChart?)GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    protected override Size MeasureOverride(Size available)
    {
        PerformanceChart? chart = Chart;
        if (chart == null || !chart.Available || chart.Rows.Count == 0)
        {
            return new Size(0, 0);
        }

        double width = double.IsInfinity(available.Width) ? 720 : Math.Max(360, available.Width);
        _legendHeight = MeasureLegend(width);
        return new Size(width, _legendHeight + AxisHeight + chart.Rows.Count * RowHeight + 8);
    }

    protected override void OnRender(DrawingContext dc)
    {
        PerformanceChart? chart = Chart;
        if (chart == null || !chart.Available || chart.Rows.Count == 0 || ActualWidth <= 1)
        {
            return;
        }

        Palette palette = Palette.Capture(this);
        double legend = DrawLegend(dc, palette, ActualWidth);
        if (Math.Abs(legend - _legendHeight) > 1)
        {
            _legendHeight = legend;
            Dispatcher.BeginInvoke(new Action(InvalidateMeasure));
        }

        (double axisMin, double axisMax, double step) = Axis(chart);
        double plotTop = _legendHeight;
        double rowsTop = plotTop + AxisHeight;
        DrawAxis(dc, palette, axisMin, axisMax, step, plotTop);
        for (int i = 0; i < chart.Rows.Count; i++)
        {
            DrawRow(dc, palette, chart.Rows[i], rowsTop + i * RowHeight, axisMin, axisMax, i == _hover, i % 2 == 1);
        }

        DrawTeamLine(dc, palette, chart.TeamAverage, axisMin, axisMax, rowsTop, chart.Rows.Count * RowHeight);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int index = HitRow(e.GetPosition(this));
        if (index == _hover)
        {
            return;
        }

        _hover = index;
        _tip.IsOpen = false;
        if (index >= 0 && Chart != null && index < Chart.Rows.Count)
        {
            _tipText.Text = Chart.Rows[index].Tooltip;
            _tip.PlacementTarget = this;
            _tip.IsOpen = true;
        }

        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        _tip.IsOpen = false;
        InvalidateVisual();
    }

    private void OnChartChanged()
    {
        _hover = -1;
        _tip.IsOpen = false;
    }

    private int HitRow(Point point)
    {
        PerformanceChart? chart = Chart;
        if (chart == null)
        {
            return -1;
        }

        double top = _legendHeight + AxisHeight;
        if (point.Y < top)
        {
            return -1;
        }

        int index = (int)((point.Y - top) / RowHeight);
        return index >= 0 && index < chart.Rows.Count ? index : -1;
    }

    private double MeasureLegend(double width) => DrawLegend(null, null, width);

    private double DrawLegend(DrawingContext? dc, Palette? palette, double width)
    {
        var items = new List<(PerformanceScoreSource? Source, string Label, LegendMark Mark)>();
        foreach (PerformanceScoreSource source in PerformanceScoreSources.StackOrder)
        {
            items.Add((source, PerformanceScoreSources.Label(source), LegendMark.Swatch));
        }

        items.Add((null, "P25–P75, median, min/max", LegendMark.Spread));
        items.Add((null, "P90", LegendMark.P90));
        items.Add((null, "Team average", LegendMark.Team));

        double x = 0;
        double y = 0;
        double maxWidth = Math.Max(160, width);
        foreach ((PerformanceScoreSource? source, string label, LegendMark mark) in items)
        {
            FormattedText text = LabelText(label, 12, palette?.Text);
            double itemWidth = 16 + text.Width + 14;
            if (x > 0 && x + itemWidth > maxWidth)
            {
                x = 0;
                y += LegendRowHeight;
            }

            if (dc != null && palette != null)
            {
                DrawMark(dc, palette, mark, source, x, y + 3);
                dc.DrawText(text, new Point(x + 16, y + 1));
            }

            x += itemWidth;
        }

        return y + LegendRowHeight;
    }

    private void DrawMark(DrawingContext dc, Palette palette, LegendMark mark, PerformanceScoreSource? source, double x, double y)
    {
        switch (mark)
        {
            case LegendMark.Swatch:
                Brush fill = source.HasValue ? palette.Segment(source.Value) : palette.Muted;
                dc.DrawRectangle(fill, palette.SegmentEdge, new Rect(x, y, 12, 12));
                break;
            case LegendMark.Spread:
                dc.DrawLine(palette.SpreadPen, new Point(x, y + 6), new Point(x + 12, y + 6));
                dc.DrawEllipse(palette.Spread, null, new Point(x, y + 6), 2.2, 2.2);
                dc.DrawEllipse(palette.Spread, null, new Point(x + 12, y + 6), 2.2, 2.2);
                dc.DrawLine(palette.MedianPen, new Point(x + 6, y + 2), new Point(x + 6, y + 10));
                break;
            case LegendMark.P90:
                DrawDiamond(dc, palette.P90, x + 6, y + 6, 5);
                break;
            case LegendMark.Team:
                dc.DrawLine(palette.TeamPen, new Point(x, y + 6), new Point(x + 12, y + 6));
                break;
        }
    }

    private void DrawAxis(DrawingContext dc, Palette palette, double axisMin, double axisMax, double step, double top)
    {
        double baseline = top + AxisHeight - 1;
        dc.DrawLine(palette.AxisPen, new Point(LabelWidth, baseline), new Point(ActualWidth - 4, baseline));
        if (step <= 0)
        {
            return;
        }

        for (double tick = axisMin; tick <= axisMax + step * 0.01; tick += step)
        {
            double x = X(tick, axisMin, axisMax);
            dc.DrawLine(palette.AxisPen, new Point(x, baseline), new Point(x, baseline - 5));
            FormattedText label = LabelText(tick.ToString("N0", CultureInfo.CurrentCulture), 11, palette.Muted);
            double left = x - label.Width / 2;
            left = Math.Max(LabelWidth, Math.Min(left, ActualWidth - label.Width - 2));
            dc.DrawText(label, new Point(left, top + 2));
        }
    }

    private void DrawRow(
        DrawingContext dc,
        Palette palette,
        PerformanceUmaRow row,
        double top,
        double axisMin,
        double axisMax,
        bool hover,
        bool alt)
    {
        if (hover)
        {
            dc.DrawRectangle(palette.Hover, null, new Rect(0, top, ActualWidth, RowHeight));
        }
        else if (alt)
        {
            dc.DrawRectangle(palette.AltRow, null, new Rect(0, top, ActualWidth, RowHeight));
        }

        FormattedText name = LabelText(row.Name, 13, palette.Text, row.IsAce ? FontWeights.SemiBold : FontWeights.Normal);
        FormattedText average = LabelText(row.Average.ToString("N0", CultureInfo.CurrentCulture), 13, palette.Text, FontWeights.SemiBold);
        FormattedText subtitle = LabelText(row.Subtitle, 11, palette.Muted);
        double nameWidth = LabelWidth - average.Width - 20;
        name.MaxTextWidth = Math.Max(24, nameWidth);
        name.Trimming = TextTrimming.CharacterEllipsis;
        subtitle.MaxTextWidth = LabelWidth - 12;
        subtitle.Trimming = TextTrimming.CharacterEllipsis;
        dc.DrawText(name, new Point(8, top + 6));
        dc.DrawText(average, new Point(LabelWidth - average.Width - 8, top + 6));
        dc.DrawText(subtitle, new Point(8, top + 26));

        double barY = top + BarTop;
        double positive = 0;
        foreach (PerformanceSegment segment in row.Segments)
        {
            if (segment.Average <= 0)
            {
                continue;
            }

            double x0 = X(positive, axisMin, axisMax);
            double x1 = X(positive + segment.Average, axisMin, axisMax);
            positive += segment.Average;
            if (x1 - x0 < 0.75)
            {
                continue;
            }

            dc.DrawRectangle(palette.Segment(segment.Source), palette.SegmentEdge, new Rect(x0, barY, x1 - x0, BarHeight));
        }

        double negative = 0;
        foreach (PerformanceSegment segment in row.Segments)
        {
            if (segment.Average >= 0)
            {
                continue;
            }

            double x0 = X(negative + segment.Average, axisMin, axisMax);
            double x1 = X(negative, axisMin, axisMax);
            negative += segment.Average;
            if (x1 - x0 < 0.75)
            {
                continue;
            }

            dc.DrawRectangle(palette.Deduction, palette.SegmentEdge, new Rect(x0, barY, x1 - x0, BarHeight));
        }

        double spreadY = top + BarTop + BarHeight + 12;
        double minX = X(row.Min, axisMin, axisMax);
        double maxX = X(row.Max, axisMin, axisMax);
        double p25 = X(row.P25, axisMin, axisMax);
        double p75 = X(row.P75, axisMin, axisMax);
        dc.DrawLine(palette.SpreadPen, new Point(p25, spreadY), new Point(p75, spreadY));
        dc.DrawEllipse(palette.Spread, null, new Point(minX, spreadY), 2.6, 2.6);
        dc.DrawEllipse(palette.Spread, null, new Point(maxX, spreadY), 2.6, 2.6);
        double medianX = X(row.Median, axisMin, axisMax);
        dc.DrawLine(palette.MedianPen, new Point(medianX, spreadY - 6), new Point(medianX, spreadY + 6));
        DrawDiamond(dc, palette.P90, X(row.P90, axisMin, axisMax), spreadY - 1, 5);
    }

    private void DrawTeamLine(DrawingContext dc, Palette palette, double teamAverage, double axisMin, double axisMax, double top, double height)
    {
        if (teamAverage < axisMin || teamAverage > axisMax)
        {
            return;
        }

        double x = X(teamAverage, axisMin, axisMax);
        dc.DrawLine(palette.TeamPen, new Point(x, top), new Point(x, top + height));
    }

    private static void DrawDiamond(DrawingContext dc, Brush brush, double cx, double cy, double radius)
    {
        var diamond = new StreamGeometry();
        using (StreamGeometryContext ctx = diamond.Open())
        {
            ctx.BeginFigure(new Point(cx, cy - radius), true, true);
            ctx.LineTo(new Point(cx + radius, cy), true, false);
            ctx.LineTo(new Point(cx, cy + radius), true, false);
            ctx.LineTo(new Point(cx - radius, cy), true, false);
        }

        diamond.Freeze();
        dc.DrawGeometry(brush, null, diamond);
    }

    private double X(double value, double axisMin, double axisMax)
    {
        double span = axisMax - axisMin;
        if (span <= 0)
        {
            span = 1;
        }

        double plot = Math.Max(1, ActualWidth - LabelWidth - RightPad);
        return LabelWidth + (value - axisMin) / span * plot;
    }

    private FormattedText LabelText(string value, double size, Brush? brush, FontWeight? weight = null)
    {
        FontFamily family = TextElement.GetFontFamily(this) ?? SystemFonts.MessageFontFamily;
        return new FormattedText(
            value,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(family, FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal),
            size,
            brush ?? Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    private static (double Min, double Max, double Step) Axis(PerformanceChart chart)
    {
        double dataMin = 0;
        double dataMax = chart.TeamAverage;
        foreach (PerformanceUmaRow row in chart.Rows)
        {
            dataMin = Math.Min(dataMin, row.Min);
            dataMax = Math.Max(dataMax, Math.Max(row.Max, Math.Max(row.P90, row.Average)));
            double positive = 0;
            double negative = 0;
            foreach (PerformanceSegment segment in row.Segments)
            {
                if (segment.Average >= 0)
                {
                    positive += segment.Average;
                }
                else
                {
                    negative += segment.Average;
                }
            }

            dataMax = Math.Max(dataMax, positive);
            dataMin = Math.Min(dataMin, negative);
        }

        if (dataMax <= dataMin)
        {
            dataMax = dataMin + 1000;
        }

        double step = NiceStep((dataMax - dataMin) / 4.0);
        double min = Math.Floor(dataMin / step) * step;
        double max = Math.Ceiling(dataMax / step) * step;
        if (max <= min)
        {
            max = min + step;
        }

        if (dataMax > max - step * 0.08)
        {
            max += step;
        }

        return (min, max, step);
    }

    private static double NiceStep(double raw)
    {
        if (raw <= 0 || double.IsNaN(raw) || double.IsInfinity(raw))
        {
            return 1;
        }

        double exp = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double fraction = raw / exp;
        double nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
        return nice * exp;
    }

    private enum LegendMark
    {
        Swatch,
        Spread,
        P90,
        Team,
    }

    private sealed class Palette
    {
        private readonly Dictionary<PerformanceScoreSource, Brush> _segments;

        private Palette(Dictionary<PerformanceScoreSource, Brush> segments)
        {
            _segments = segments;
        }

        public required Brush Text { get; init; }
        public required Brush Muted { get; init; }
        public required Brush Hover { get; init; }
        public required Brush AltRow { get; init; }
        public required Brush Spread { get; init; }
        public required Brush P90 { get; init; }
        public required Brush Deduction { get; init; }
        public required Pen SegmentEdge { get; init; }
        public required Pen TeamPen { get; init; }
        public required Pen SpreadPen { get; init; }
        public required Pen MedianPen { get; init; }
        public required Pen AxisPen { get; init; }

        public Brush Segment(PerformanceScoreSource source) => _segments[source];

        public static Palette Capture(FrameworkElement owner)
        {
            Brush text = Solid(owner, "Window.Foreground");
            Brush muted = Solid(owner, "Muted.Foreground");
            Brush grid = Solid(owner, "Grid.GridLine");
            Brush spread = Solid(owner, "Performance.Spread");
            Brush median = Solid(owner, "Performance.Median");
            Brush team = Solid(owner, "Performance.TeamAverage");
            var segments = new Dictionary<PerformanceScoreSource, Brush>
            {
                [PerformanceScoreSource.FinishAndMargin] = Solid(owner, "Performance.Finish"),
                [PerformanceScoreSource.SkillWhite] = Solid(owner, "Performance.SkillWhite"),
                [PerformanceScoreSource.SkillGold] = Solid(owner, "Performance.SkillGold"),
                [PerformanceScoreSource.SkillUnique] = Solid(owner, "Performance.SkillUnique"),
                [PerformanceScoreSource.StrongStart] = Solid(owner, "Performance.StrongStart"),
                [PerformanceScoreSource.StyleAndExtras] = Solid(owner, "Performance.StyleExtras"),
            };
            return new Palette(segments)
            {
                Text = text,
                Muted = muted,
                Hover = Solid(owner, "Performance.Hover"),
                AltRow = Solid(owner, "Grid.AlternatingRowBackground"),
                Spread = spread,
                P90 = Solid(owner, "Performance.P90"),
                Deduction = Solid(owner, "Performance.Deduction"),
                SegmentEdge = Pen(grid, 1),
                TeamPen = Dash(team, 1.4),
                SpreadPen = Pen(spread, 2),
                MedianPen = Pen(median, 2),
                AxisPen = Pen(grid, 1),
            };
        }

        private static SolidColorBrush Solid(FrameworkElement owner, string key)
        {
            if (owner.TryFindResource(key) is not SolidColorBrush brush)
            {
                throw new InvalidOperationException($"Theme brush '{key}' is missing.");
            }

            var copy = new SolidColorBrush(brush.Color);
            copy.Freeze();
            return copy;
        }

        private static Pen Pen(Brush brush, double thickness)
        {
            var pen = new Pen(brush, thickness);
            pen.Freeze();
            return pen;
        }

        private static Pen Dash(Brush brush, double thickness)
        {
            var pen = new Pen(brush, thickness) { DashStyle = DashStyles.Dash };
            pen.Freeze();
            return pen;
        }
    }
}
