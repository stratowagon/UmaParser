using UmaParser.AppStatus;
using UmaParser.Presentation;

namespace UmaParser
{
    public partial class Form1
    {
        private static readonly string ResultsWelcomeMessage = ResultsScreen.Welcome;

        private IAppStatusPresenter _status = null!;

        private void InitializeStatusUi()
        {
            _status = new Ui.WinFormsAppStatusPresenter(statusTextBox);
        }

        private void SetStatus(string message) => _status.SetMessage(message);

        private void AppendStatus(string message) => _status.AppendMessage(message);
    }
}