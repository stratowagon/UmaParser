using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UmaParser.Ui;

/// <summary>
/// TabControl that paints its own chrome so dark mode does not leave native white
/// tab gutters, tab halos, or content borders.
/// </summary>
internal sealed class ThemedTabControl : TabControl
{
    private const int WmPaint = 0x000F;

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? pszSubAppName, string? pszSubIdList);

    public ThemedTabControl()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DrawMode = TabDrawMode.OwnerDrawFixed;
        Appearance = TabAppearance.FlatButtons;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyNativeTheme();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= TabPages.Count)
        {
            return;
        }

        TabPage page = TabPages[e.Index];
        bool selected = e.Index == SelectedIndex;
        Color back = selected ? AppColors.WindowBack : AppColors.TabInactiveBack;
        Color fore = AppColors.WindowFore;

        // Cover the native 3D tab halo by slightly overpainting each tab bounds.
        Rectangle bounds = e.Bounds;
        bounds.Inflate(2, 2);

        using (var backBrush = new SolidBrush(back))
        {
            e.Graphics.FillRectangle(backBrush, bounds);
        }

        if (selected)
        {
            using var seamBrush = new SolidBrush(AppColors.WindowBack);
            e.Graphics.FillRectangle(seamBrush, e.Bounds.Left, e.Bounds.Bottom - 2, e.Bounds.Width, 3);
        }

        TextRenderer.DrawText(
            e.Graphics,
            page.Text,
            page.Font,
            e.Bounds,
            fore,
            TextFormatFlags.HorizontalCenter
                | TextFormatFlags.VerticalCenter
                | TextFormatFlags.SingleLine);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WmPaint)
        {
            PaintChromeOverlay();
        }
    }

    private void PaintChromeOverlay()
    {
        if (!IsHandleCreated || TabCount == 0)
        {
            return;
        }

        using var graphics = Graphics.FromHwnd(Handle);
        PaintChromeOverlay(graphics);
    }

    private void PaintChromeOverlay(Graphics graphics)
    {
        Rectangle client = ClientRectangle;
        Rectangle display = DisplayRectangle;

        using Region frame = new Region(client);
        frame.Exclude(display);
        for (int i = 0; i < TabCount; i++)
        {
            frame.Exclude(GetTabRect(i));
        }

        using (var frameBrush = new SolidBrush(AppColors.WindowBack))
        {
            graphics.FillRegion(frameBrush, frame);
        }

        if (TabCount > 0)
        {
            int tabBottom = GetTabRect(0).Bottom;
            Rectangle gutter = new Rectangle(
                GetTabRect(TabCount - 1).Right,
                client.Top,
                client.Right - GetTabRect(TabCount - 1).Right,
                tabBottom - client.Top);

            if (gutter.Width > 0 && gutter.Height > 0)
            {
                using var gutterBrush = new SolidBrush(AppColors.TabInactiveBack);
                graphics.FillRectangle(gutterBrush, gutter);
            }
        }
    }

    private void ApplyNativeTheme()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        // Clear the native tab theme so our owner-drawn chrome is authoritative.
        SetWindowTheme(Handle, string.Empty, string.Empty);
    }
}