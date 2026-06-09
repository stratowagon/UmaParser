using UmaBlobber.DataModel.ResponseData;

namespace UmaBlobber.Import;

public sealed class CaptureImportResult
{
    public required string FileName { get; init; }
    public ImportStatus Status { get; init; }
    public UmaApiResponse? Response { get; init; }
    public string? ResponseTypeName { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// True for classic Team Trials multi-race responses.
    /// </summary>
    public bool IsTeamTrial =>
        Status == ImportStatus.Success && Response is ObjectModel.TeamTrialResult;

    /// <summary>
    /// True for single-race captures (Champions Meeting, Room Match, Practice Room, etc.).
    /// These have a different JSON shape (often with k__BackingField names) and contain exactly one race.
    /// </summary>
    public bool IsSingleRace { get; init; }

    /// <summary>
    /// When IsSingleRace is true, this is the (lightly normalized) JSON text.
    /// Callers can use it to extract SimDataBase64 / horse lists / conditions without a full heavyweight DTO
    /// for the very large capture files (which often embed full career histories).
    /// </summary>
    public string? SingleRaceNormalizedJson { get; init; }

    /// <summary>
    /// Convenience: the SimDataBase64 (or equivalent) value extracted for single-race captures.
    /// This is the gzipped+base64 race simulation blob that RaceScenarioParser understands.
    /// </summary>
    public string? SingleRaceSimDataBase64 { get; init; }

    /// <summary>
    /// Best-effort label for the kind of single race (e.g. "Champions", "RoomMatch", "Practice").
    /// </summary>
    public string? SingleRaceType { get; init; }
}