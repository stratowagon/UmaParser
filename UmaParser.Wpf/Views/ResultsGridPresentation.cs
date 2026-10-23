using System.Windows;
using System.Windows.Media;
using UmaParser.Presentation;

namespace UmaParser.Views;

/// <summary>
/// One Results row for the DataGrid. Brushes are chosen once, when the drop is applied,
/// so a cell does not run a converter or a trigger while it is realized.
/// </summary>
internal sealed class ResultsDisplayRow
{
    public required IList<ResultsDisplayCell> Cells { get; init; }
}

/// <summary>
/// One Results cell. A null brush means "leave the DataGrid default": the binding
/// uses <see cref="DependencyProperty.UnsetValue"/> so selection and grid lines stay
/// on the theme style.
/// </summary>
internal sealed class ResultsDisplayCell
{
    public required string Text { get; init; }
    public Brush? Background { get; init; }
    public Brush? Foreground { get; init; }
    public FontWeight FontWeight { get; init; }
    public Brush? BorderBrush { get; init; }
    public Thickness BorderThickness { get; init; }
}

/// <summary>
/// Frozen heat-map brushes, one per distinct <see cref="ScoreHeatColor"/>.
/// </summary>
internal static class ScoreHeatBrushes
{
    private static readonly Dictionary<ScoreHeatColor, SolidColorBrush> Cache = new();

    public static SolidColorBrush For(ScoreHeatColor color)
    {
        if (!Cache.TryGetValue(color, out SolidColorBrush? brush))
        {
            brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
            brush.Freeze();
            Cache.Add(color, brush);
        }

        return brush;
    }
}

/// <summary>
/// Builds the display rows for one Results grid. Paint order matches the old cell
/// triggers: heat, then outlier, then the total row. A bold flag still wins the
/// font weight, which is the order those triggers ran in.
/// </summary>
internal static class ResultsGridPresentation
{
    /// <summary>Aero2 column-header padding, left plus right (5 + 5).</summary>
    public const double HeaderChrome = 10;

    /// <summary>TextBlock margin 2+2, cell border 1+1, and the vertical grid line.</summary>
    public const double CellChrome = 7;

    public static List<ResultsDisplayRow> Build(IReadOnlyList<ResultsGridRow> rows, Brush outlierBrush)
    {
        var display = new List<ResultsDisplayRow>(rows.Count);
        foreach (ResultsGridRow row in rows)
        {
            var cells = new List<ResultsDisplayCell>(row.Cells.Count);
            foreach (ResultsCell cell in row.Cells)
            {
                cells.Add(Paint(cell, row.IsFooter, row.IsTeamBoundary, outlierBrush));
            }

            display.Add(new ResultsDisplayRow { Cells = cells });
        }

        return display;
    }

    private static ResultsDisplayCell Paint(ResultsCell cell, bool isFooter, bool isTeamBoundary, Brush outlierBrush)
    {
        Brush? background = cell.Heat is ScoreHeatColor heat ? ScoreHeatBrushes.For(heat) : null;
        Brush? foreground = null;
        FontWeight weight = FontWeights.Normal;

        if (cell.Outlier)
        {
            background = outlierBrush;
            foreground = Brushes.Black;
        }

        if (isFooter)
        {
            background = SystemColors.ControlBrush;
            foreground = SystemColors.ControlTextBrush;
            weight = FontWeights.SemiBold;
        }

        if (cell.Bold)
        {
            weight = FontWeights.Bold;
        }

        return new ResultsDisplayCell
        {
            Text = cell.Text,
            Background = background,
            Foreground = foreground,
            FontWeight = weight,
            BorderBrush = isTeamBoundary ? SystemColors.ControlDarkBrush : null,
            BorderThickness = isTeamBoundary ? new Thickness(0, 2, 0, 0) : new Thickness(1),
        };
    }
}
