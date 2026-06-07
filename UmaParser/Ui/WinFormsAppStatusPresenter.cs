using UmaBlobber.AppStatus;

namespace UmaBlobber.Ui;

internal sealed class WinFormsAppStatusPresenter(TextBox textBox) : IAppStatusPresenter
{
    public void SetMessage(string message)
    {
        if (textBox.InvokeRequired)
        {
            textBox.Invoke(SetMessage, message);
            return;
        }

        textBox.Text = message;
        ScrollToEnd();
    }

    public void AppendMessage(string message)
    {
        if (textBox.InvokeRequired)
        {
            textBox.Invoke(AppendMessage, message);
            return;
        }

        if (textBox.TextLength > 0)
        {
            textBox.AppendText(Environment.NewLine + message);
        }
        else
        {
            textBox.Text = message;
        }

        ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        textBox.SelectionStart = textBox.TextLength;
        textBox.SelectionLength = 0;
        textBox.ScrollToCaret();
    }
}