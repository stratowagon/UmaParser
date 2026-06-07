using System.Drawing;
using System.Windows.Forms;

namespace UmaBlobber.Ui;

/// <summary>Caches a bold variant of a grid's default cell font for ace name highlighting.</summary>
internal sealed class AceNameFontCache : IDisposable
{
    private Font? _boldFont;
    private Font? _sourceFont;

    public Font GetBoldFont(DataGridView grid)
    {
        Font regular = grid.DefaultCellStyle.Font ?? grid.Font;
        if (_boldFont != null && ReferenceEquals(_sourceFont, regular))
        {
            return _boldFont;
        }

        _boldFont?.Dispose();
        _sourceFont = regular;
        _boldFont = new Font(regular, FontStyle.Bold);
        return _boldFont;
    }

    public void Dispose()
    {
        _boldFont?.Dispose();
        _boldFont = null;
        _sourceFont = null;
    }
}