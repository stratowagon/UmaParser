using UmaBlobber.MasterData;

namespace UmaBlobber
{
    public partial class Form1
    {
        private const int MinWindowWidth = 640;
        private const int MinWindowHeight = 480;

        private void RestoreWindowLayout()
        {
            var settings = GameMasterSettings.Load();
            if (settings.WindowWidth is not int width || settings.WindowHeight is not int height
                || width < MinWindowWidth || height < MinWindowHeight)
            {
                return;
            }

            StartPosition = FormStartPosition.Manual;
            Size = new Size(width, height);

            if (settings.WindowX is int x && settings.WindowY is int y)
            {
                Location = ClampToVisibleScreen(new Point(x, y), Size);
            }

            if (settings.WindowState == nameof(FormWindowState.Maximized))
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

            settings.WindowX = bounds.X;
            settings.WindowY = bounds.Y;
            settings.WindowWidth = bounds.Width;
            settings.WindowHeight = bounds.Height;
            settings.WindowState = WindowState == FormWindowState.Maximized
                ? nameof(FormWindowState.Maximized)
                : nameof(FormWindowState.Normal);

            settings.Save();
        }

        private static Point ClampToVisibleScreen(Point location, Size size)
        {
            var rect = new Rectangle(location, size);
            Screen screen = Screen.FromRectangle(rect);
            Rectangle area = screen.WorkingArea;

            int x = location.X;
            int y = location.Y;

            if (size.Width <= area.Width)
            {
                x = Math.Clamp(x, area.Left, area.Right - size.Width);
            }
            else
            {
                x = area.Left;
            }

            if (size.Height <= area.Height)
            {
                y = Math.Clamp(y, area.Top, area.Bottom - size.Height);
            }
            else
            {
                y = area.Top;
            }

            return new Point(x, y);
        }
    }
}