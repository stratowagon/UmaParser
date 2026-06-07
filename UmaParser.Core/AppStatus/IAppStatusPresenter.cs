namespace UmaBlobber.AppStatus;

/// <summary>
/// Application status / log line for user-facing messages (imports, master data, analysis summary).
/// </summary>
public interface IAppStatusPresenter
{
    void SetMessage(string message);

    void AppendMessage(string message);
}