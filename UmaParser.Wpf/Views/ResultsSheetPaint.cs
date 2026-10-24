using System.Windows;
using System.Windows.Media;
using UmaParser.Presentation;

namespace UmaParser.Views;

/// <summary>
/// Frozen brushes for one paint of the Results sheet. Built when the theme changes.
/// <see cref="FormattedText"/> keeps the brush it was given, so the sheet drops its text cache
/// together with this object.
/// </summary>
internal sealed class ResultsSheetPaint
{
    private readonly Dictionary<ScoreHeatColor, SolidColorBrush> _heat = new();

    private ResultsSheetPaint(
        Brush backdrop,
        Brush header,
        Brush footer,
        Brush row,
        Brush selection,
        Brush inactiveSelection,
        Brush outlier,
        Pen gridLine,
        Pen teamLine,
        Brush bodyText,
        Brush headerText,
        Brush heatText,
        Brush outlierText,
        Brush selectionText,
        Brush inactiveSelectionText)
    {
        Backdrop = backdrop;
        Header = header;
        Footer = footer;
        Row = row;
        Selection = selection;
        InactiveSelection = inactiveSelection;
        Outlier = outlier;
        GridLine = gridLine;
        TeamLine = teamLine;
        BodyText = bodyText;
        HeaderText = headerText;
        HeatText = heatText;
        OutlierText = outlierText;
        SelectionText = selectionText;
        InactiveSelectionText = inactiveSelectionText;
    }

    public Brush Backdrop { get; }
    public Brush Header { get; }
    public Brush Footer { get; }
    public Brush Row { get; }
    public Brush Selection { get; }
    public Brush InactiveSelection { get; }
    public Brush Outlier { get; }
    public Pen GridLine { get; }
    public Pen TeamLine { get; }
    public Brush BodyText { get; }
    public Brush HeaderText { get; }
    public Brush HeatText { get; }
    public Brush OutlierText { get; }
    public Brush SelectionText { get; }
    public Brush InactiveSelectionText { get; }

    public static ResultsSheetPaint Capture()
    {
        ColorTheme theme = Themes.AppTheme.Current;
        ScoreHeatColor heatText = ScoreHeat.Palette(theme).Text;
        RgbColor outlier = ResultsMarks.Fill(theme);
        RgbColor outlierText = ResultsMarks.Text(theme);
        Brush gridLine = BrushOf("Grid.GridLine");
        return new ResultsSheetPaint(
            BrushOf("Grid.Background"),
            BrushOf("Grid.HeaderBackground"),
            BrushOf("Grid.HeaderBackground"),
            BrushOf("Grid.RowBackground"),
            BrushOf("Grid.SelectionBackground"),
            BrushOf("Grid.InactiveSelectionBackground"),
            Freeze(Color.FromRgb(outlier.R, outlier.G, outlier.B)),
            Pen(gridLine, 1),
            Pen(gridLine, 2),
            BrushOf("Grid.Foreground"),
            BrushOf("Grid.HeaderForeground"),
            Freeze(Color.FromRgb(heatText.R, heatText.G, heatText.B)),
            Freeze(Color.FromRgb(outlierText.R, outlierText.G, outlierText.B)),
            BrushOf("Grid.SelectionForeground"),
            BrushOf("Grid.InactiveSelectionForeground"));
    }

    public Brush Heat(ScoreHeatColor color)
    {
        if (!_heat.TryGetValue(color, out SolidColorBrush? brush))
        {
            brush = Freeze(Color.FromRgb(color.R, color.G, color.B));
            _heat[color] = brush;
        }

        return brush;
    }

    private static SolidColorBrush BrushOf(string key)
    {
        if (Application.Current?.TryFindResource(key) is not SolidColorBrush brush)
        {
            throw new InvalidOperationException($"Theme brush '{key}' is missing.");
        }

        return Freeze(brush.Color);
    }

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen Pen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        pen.Freeze();
        return pen;
    }
}
