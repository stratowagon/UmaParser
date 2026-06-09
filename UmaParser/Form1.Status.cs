using UmaBlobber.AppStatus;

namespace UmaBlobber
{
    public partial class Form1
    {
        private static readonly string ResultsWelcomeMessage =
            "Drop one or more race capture JSON files (Team Trials, Champions Meeting, Room Match, or Practice Room) anywhere on this window to load results." +
            Environment.NewLine + Environment.NewLine +
            "The Team Analysis tab is only available if all files are Team Trials with the exact same team." + Environment.NewLine +
            "Mixed Team Trials teams can still show Skills and Track points analysis for any umas that are present in all files.";

        private IAppStatusPresenter _status = null!;

        private void InitializeStatusUi()
        {
            _status = new Ui.WinFormsAppStatusPresenter(statusTextBox);
        }

        private void SetStatus(string message) => _status.SetMessage(message);

        private void AppendStatus(string message) => _status.AppendMessage(message);
    }
}