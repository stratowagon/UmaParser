using UmaParser.MasterData;
using UmaParser.Ui;

namespace UmaParser
{
    public partial class Form1
    {
        private void InitializeViewMenu()
        {
            string active = AppColorMode.Normalize(GameMasterSettings.Load().ColorMode);
            UpdateViewMenuChecks(active);
        }

        private void UpdateViewMenuChecks(string active)
        {
            viewLightMenuItem.Checked = active == AppColorMode.Light;
            viewDarkMenuItem.Checked = active == AppColorMode.Dark;
        }

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