namespace UmaBlobber.MasterData;

/// <summary>
/// <c>text_data.category</c> values in <c>master.mdb</c> (SQLite).
/// Enum names describe how UmaParser uses each category, not in-game "card" UI terminology.
/// </summary>
internal enum MasterTextCategory
{
    /// <summary>Short uma name without variation title (category 6). Index = chara id.</summary>
    CharaShortName = 6,

    /// <summary>Skill names (category 47). Index = skill id.</summary>
    SkillName = 47,

    /// <summary>Team Trials score bucket labels (category 140).</summary>
    TeamTrialsScoreType = 140,

    /// <summary>Team Trials score bucket descriptions (category 141).</summary>
    TeamTrialsScoreDesc = 141,

    /// <summary>Race track short names, e.g. "Chukyo", "Tokyo" (category 35). Used via race_course_set.race_track_id.</summary>
    RaceTrackName = 35,

    /// <summary>Parent factor / spark skill names (category 147).</summary>
    FactorSkillName = 147,

    /// <summary>Team Trials score bonus type labels (category 148).</summary>
    ScoreBonusType = 148,
}