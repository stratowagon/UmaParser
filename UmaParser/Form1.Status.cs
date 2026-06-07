using UmaBlobber.AppStatus;

namespace UmaBlobber
{
    public partial class Form1
    {
        private static readonly string ResultsWelcomeMessage =
            "Drop one or more Team Trials JSON files anywhere on this window to load results." +
            Environment.NewLine + Environment.NewLine +
            "Legacy .bin captures are also accepted. Use the Results, Analysis, and Skills tabs after loading.";

        private IAppStatusPresenter _status = null!;

        private void InitializeStatusUi()
        {
            _status = new Ui.WinFormsAppStatusPresenter(statusTextBox);
        }

        private void SetStatus(string message) => _status.SetMessage(message);

        private void AppendStatus(string message) => _status.AppendMessage(message);
    }
}