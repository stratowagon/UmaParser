using System.Drawing;
using System.Windows.Forms;

namespace UmaParser.Ui;

internal static class AppThemeApplier
{
    public static void Apply(Form form)
    {
        bool dark = AppColors.IsDark;

        ApplyWindow(form);
        WindowsChromeTheme.ApplyTitleBar(form, dark);

        foreach (Control control in form.Controls)
        {
            ApplyControlTree(control, dark);
        }

        RefreshThemedGrids(form);
    }

    private static void ApplyWindow(Form form)
    {
        form.BackColor = AppColors.WindowBack;
        form.ForeColor = AppColors.WindowFore;
    }

    private static void ApplyControlTree(Control control, bool dark)
    {
        switch (control)
        {
            case MenuStrip menuStrip:
                ApplyMenuStrip(menuStrip, dark);
                break;
            case TabControl tabControl:
                ApplyTabControl(tabControl, dark);
                break;
            case TabPage tabPage:
                ApplyTabPage(tabPage);
                break;
            case DataGridView grid:
                ApplyDataGridView(grid, dark);
                break;
            case ComboBox comboBox:
                ApplyComboBox(comboBox, dark);
                break;
            case TextBox textBox:
                ApplyTextBox(textBox, dark);
                break;
            case Label label:
                ApplyLabel(label);
                break;
            case SplitContainer splitContainer:
                ApplySplitContainer(splitContainer);
                break;
            case Panel panel:
                ApplyPanel(panel);
                break;
            default:
                control.BackColor = AppColors.WindowBack;
                control.ForeColor = AppColors.WindowFore;
                break;
        }

        IEnumerable<Control> children = control is SplitContainer split
            ? split.Panel1.Controls.Cast<Control>().Concat(split.Panel2.Controls.Cast<Control>())
            : control.Controls.Cast<Control>();

        foreach (Control child in children)
        {
            ApplyControlTree(child, dark);
        }
    }

    private static void ApplyMenuStrip(MenuStrip menuStrip, bool dark)
    {
        menuStrip.RenderMode = ToolStripRenderMode.Professional;
        menuStrip.Renderer = new ToolStripProfessionalRenderer(new MenuColorTable());
        menuStrip.BackColor = AppColors.MenuBack;
        menuStrip.ForeColor = AppColors.MenuFore;
        WindowsChromeTheme.ApplyNativeControlTheme(menuStrip, dark);

        ApplyToolStripItems(menuStrip.Items);
    }

    private static void ApplyToolStripItems(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.BackColor = AppColors.MenuBack;
            item.ForeColor = AppColors.MenuFore;

            if (item is ToolStripMenuItem menuItem)
            {
                ApplyToolStripItems(menuItem.DropDownItems);
            }
        }
    }

    private static void ApplyTabControl(TabControl tabControl, bool dark)
    {
        tabControl.BackColor = AppColors.WindowBack;
        tabControl.ForeColor = AppColors.WindowFore;

        foreach (TabPage page in tabControl.TabPages)
        {
            ApplyTabPage(page);
        }

        tabControl.Invalidate();
    }

    private static void ApplyTabPage(TabPage tabPage)
    {
        tabPage.UseVisualStyleBackColor = false;
        tabPage.BackColor = AppColors.WindowBack;
        tabPage.ForeColor = AppColors.WindowFore;
        tabPage.Padding = Padding.Empty;
    }

    private static void ApplyPanel(Panel panel)
    {
        panel.BackColor = AppColors.WindowBack;
        panel.ForeColor = AppColors.WindowFore;
    }

    private static void ApplyLabel(Label label)
    {
        label.BackColor = AppColors.WindowBack;
        label.ForeColor = IsMutedLabel(label) ? AppColors.MutedFore : AppColors.WindowFore;
    }

    private static bool IsMutedLabel(Label label) =>
        label.ForeColor == SystemColors.GrayText
        || label.Name is "labelResultsWelcome" or "labelAnalysisMessage";

    private static void ApplyTextBox(TextBox textBox, bool dark)
    {
        textBox.BackColor = AppColors.InputBack;
        textBox.ForeColor = AppColors.InputFore;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        WindowsChromeTheme.ApplyNativeControlTheme(textBox, dark);
    }

    private static void ApplyComboBox(ComboBox comboBox, bool dark)
    {
        comboBox.BackColor = AppColors.InputBack;
        comboBox.ForeColor = AppColors.InputFore;
        comboBox.FlatStyle = FlatStyle.Flat;
        WindowsChromeTheme.ApplyNativeControlTheme(comboBox, dark);
    }

    private static void ApplySplitContainer(SplitContainer splitContainer)
    {
        splitContainer.BackColor = AppColors.SplitterBack;
        splitContainer.Panel1.BackColor = AppColors.WindowBack;
        splitContainer.Panel2.BackColor = AppColors.WindowBack;
        splitContainer.ForeColor = AppColors.WindowFore;
        WindowsChromeTheme.ApplyNativeControlTheme(splitContainer, AppColors.IsDark);
    }

    private static void ApplyDataGridView(DataGridView grid, bool dark)
    {
        grid.BorderStyle = BorderStyle.None;
        grid.BackgroundColor = AppColors.GridBack;
        grid.GridColor = AppColors.GridLine;
        grid.EnableHeadersVisualStyles = false;

        grid.DefaultCellStyle.BackColor = AppColors.GridBack;
        grid.DefaultCellStyle.ForeColor = AppColors.GridFore;
        grid.DefaultCellStyle.SelectionBackColor = AppColors.GridSelectionBack;
        grid.DefaultCellStyle.SelectionForeColor = AppColors.GridSelectionFore;

        grid.ColumnHeadersDefaultCellStyle.BackColor = AppColors.GridHeaderBack;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = AppColors.GridHeaderFore;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = AppColors.GridHeaderBack;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = AppColors.GridHeaderFore;

        grid.RowHeadersDefaultCellStyle.BackColor = AppColors.GridHeaderBack;
        grid.RowHeadersDefaultCellStyle.ForeColor = AppColors.GridHeaderFore;

        WindowsChromeTheme.ApplyNativeControlTheme(grid, dark);
    }

    private static void RefreshThemedGrids(Control root)
    {
        if (root is DataGridView grid)
        {
            grid.Invalidate();
        }

        foreach (Control child in root.Controls)
        {
            RefreshThemedGrids(child);
        }
    }

    private sealed class MenuColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => AppColors.MenuBack;
        public override Color MenuStripGradientEnd => AppColors.MenuBack;
        public override Color ToolStripDropDownBackground => AppColors.MenuBack;
        public override Color ImageMarginGradientBegin => AppColors.MenuBack;
        public override Color ImageMarginGradientMiddle => AppColors.MenuBack;
        public override Color ImageMarginGradientEnd => AppColors.MenuBack;
        public override Color MenuItemBorder => AppColors.GridLine;
        public override Color MenuItemSelected => AppColors.GridSelectionBack;
        public override Color MenuItemSelectedGradientBegin => AppColors.GridSelectionBack;
        public override Color MenuItemSelectedGradientEnd => AppColors.GridSelectionBack;
        public override Color MenuBorder => AppColors.GridLine;
    }
}