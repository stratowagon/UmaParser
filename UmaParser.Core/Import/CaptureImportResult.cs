using UmaBlobber.DataModel.ResponseData;

namespace UmaBlobber.Import;

public sealed class CaptureImportResult
{
    public required string FileName { get; init; }
    public ImportStatus Status { get; init; }
    public UmaApiResponse? Response { get; init; }
    public string? ResponseTypeName { get; init; }
    public string? ErrorMessage { get; init; }

    public bool IsTeamTrial =>
        Status == ImportStatus.Success && Response is ObjectModel.TeamTrialResult;
}