using System.Reflection;
using System.Windows.Forms;

namespace UmaParser.Ui;

internal static class DataGridViewOptimizations
{
    /// <summary>
    /// Enables double-buffered painting to reduce flicker and improve scroll performance.
    /// </summary>
    public static void EnableDoubleBuffering(DataGridView grid)
    {
        typeof(DataGridView).InvokeMember(
            "DoubleBuffered",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetProperty,
            null,
            grid,
            [true]);
    }
}