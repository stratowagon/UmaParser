using UmaParser.MasterData;
using UmaParser.Ui;

namespace UmaParser
{
    public partial class Form1
    {
        private ToolStripMenuItem? _viewSystemMenuItem;

        private void InitializeViewMenu()
        {
            _viewSystemMenuItem = new ToolStripMenuItem("System")
            {
                Name = "viewSystemMenuItem",
            };
            _viewSystemMenuItem.Click += ViewSystemMenuItem_Click;
            viewToolStripMenuItem.DropDownItems.Insert(0, _viewSystemMenuItem);

            string active = AppColorMode.Normalize(GameMasterSettings.Load().ColorMode);
            UpdateViewMenuChecks(active);
        }

        private void UpdateViewMenuChecks(string active)
        {
            if (_viewSystemMenuItem != null)
            {
                _viewSystemMenuItem.Checked = active == AppColorMode.System;
            }

            viewLightMenuItem.Checked = active == AppColorMode.Light;
            viewDarkMenuItem.Checked = active == AppColorMode.Dark;
        }

        private void ViewSystemMenuItem_Click(object? sender, EventArgs e) =>
            ApplyViewColorMode(AppColorMode.System);

        private void ViewLightMenuItem_Click(object? sender, EventArgs e) =>
            ApplyViewColorMode(AppColorMode.Light);

        private void ViewDarkMenuItem_Click(object? sender, EventArgs e) =>
            ApplyViewColorMode(AppColorMode.Dark);

        private void ApplyViewColorMode(string mode)
        {
            string active = AppColorMode.Normalize(GameMasterSettings.Load().ColorMode);
            UpdateViewMenuChecks(mode);

            if (active == mode)
            {
                return;
            }

            AppColorMode.SaveAndRestart(mode);
        }
    }
}