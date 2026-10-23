using System.Drawing;
using System.Windows.Forms;
using UmaParser.Analysis;

namespace UmaParser.Ui;

/// <summary>
/// Paints a <see cref="CellBand"/> onto a grid cell using <see cref="AppColors"/>.
/// Gold and unique use a slightly smaller selection lighten than the severity bands,
/// matching the previous CellFormatting handlers.
/// </summary>
internal static class CellBandStyling
{
    public static void Apply(DataGridViewCellFormattingEventArgs e, CellBand band)
    {
        if (band == CellBand.None || e.CellStyle == null)
        {
            return;
        }

        Color back = AppColors.BackFor(band);
        Color fore = AppColors.ForeFor(band);
        float lighten = band is CellBand.Gold or CellBand.Unique ? 0.25f : 0.28f;
        e.CellStyle.BackColor = back;
        e.CellStyle.ForeColor = fore;
        e.CellStyle.SelectionBackColor = ControlPaint.Light(back, lighten);
        e.CellStyle.SelectionForeColor = fore;
    }
}
