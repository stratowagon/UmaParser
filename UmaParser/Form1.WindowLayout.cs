using UmaParser.MasterData;
using UmaParser.Settings;

namespace UmaParser
{
    public partial class Form1
    {
        private void RestoreWindowLayout()
        {
            var settings = GameMasterSettings.Load();
            if (!WindowPlacement.TryReadSize(settings, out int width, out int height))
            {
                return;
            }

            StartPosition = FormStartPosition.Manual;
            Size = new Size(width, height);

            if (WindowPlacement.TryReadPosition(settings, out int x, out int y))
            {
                Rectangle area = Screen.FromRectangle(new Rectangle(x, y, width, height)).WorkingArea;
                (int clampedX, int clampedY) = WindowPlacement.ClampToWorkArea(
                    x,
                    y,
                    width,
                    height,
                    new ScreenRect(area.X, area.Y, area.Width, area.Height));
                Location = new Point(clampedX, clampedY);
            }

            if (WindowPlacement.IsMaximized(settings))
            {
                WindowState = FormWindowState.Maximized;
            }
        }

        private void SaveWindowLayout()
        {
            var settings = GameMasterSettings.Load();
            Rectangle bounds = WindowState == FormWindowState.Normal
                ? new Rectangle(Location, Size)
                : RestoreBounds;

            WindowPlacement.Write(
                settings,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                WindowState == FormWindowState.Maximized);
            settings.Save();
        }
    }
}
