namespace UmaParser.MasterData;

/// <summary>
/// Compressed race course / track information resolved via the master joins.
/// Surface (Ground) is kept internally for uniqueness even if the UI often omits it.
/// </summary>
public sealed record RaceCourseInfo(
    int InstanceId,      // race_instance.id - the key that appears in capture data
    string Name,         // short name from text_data 35, e.g. "Chukyo"
    int Distance,
    int Ground,          // 1=Turf, 2=Dirt (typically)
    int Turn,            // 1=Right, 2=Left (typically)
    int InOut            // 1=Inner, 2=Outer (typically)
);
