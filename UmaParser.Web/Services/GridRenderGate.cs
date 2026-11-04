using UmaParser.Presentation;

namespace UmaParser.Web;

/// <summary>
/// Lets a grid skip its render tree while a drop is still parsing.
/// Progress text re-renders the page; the table renders when its rows, columns, or theme change.
/// </summary>
internal struct GridRenderGate
{
    private object? _rows;
    private object? _columns;
    private ColorTheme _theme;
    private bool _seen;

    public bool NeedsRender(object? rows, object? columns, ColorTheme theme) =>
        !_seen
        || !ReferenceEquals(_rows, rows)
        || !ReferenceEquals(_columns, columns)
        || _theme != theme;

    public void MarkRendered(object? rows, object? columns, ColorTheme theme)
    {
        _seen = true;
        _rows = rows;
        _columns = columns;
        _theme = theme;
    }
}
