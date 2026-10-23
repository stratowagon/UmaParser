using UmaParser.Settings;

namespace UmaParser.Presentation;

public sealed class GridColumnDefinition<TRow>
{
    public required string Id { get; init; }
    public required string Header { get; init; }
    public required string Tooltip { get; init; }
    public required string Description { get; init; }
    public required Type ValueType { get; init; }
    public bool IsLocked { get; init; }
    public required Func<TRow, object?> GetValue { get; init; }
    public Func<GridColumnContext, bool>? IsAvailable { get; init; }
    public Func<GridColumnContext, string?>? UnavailableReason { get; init; }

    public bool CheckAvailable(GridColumnContext context) =>
        IsAvailable?.Invoke(context) ?? true;

    public string? GetUnavailableReason(GridColumnContext context) =>
        CheckAvailable(context) ? null : UnavailableReason?.Invoke(context);
}

public static class GridColumnIds
{
    public static class Analysis
    {
        public const string Uma = "analysis.uma";
        public const string Style = "analysis.style";
        public const string Avg = "analysis.avg";
        public const string Skills = "analysis.skills";
        public const string SkillCv = "analysis.skill-cv";
        public const string SkillRatio = "analysis.skill-ratio";
        public const string Other = "analysis.other";
        public const string AvgPlace = "analysis.avg-place";
        public const string OtherCv = "analysis.other-cv";
        public const string Trimmed = "analysis.trimmed";
        public const string Cv = "analysis.cv";
        public const string AdjCv = "analysis.adj-cv";
        public const string Gap = "analysis.gap";
        public const string Ceiling = "analysis.ceiling";
        public const string Floor = "analysis.floor";
        public const string Spread = "analysis.spread";
        public const string Retrain = "analysis.retrain";
        public const string AceDelta = "analysis.ace-delta";
    }

    public static class Skills
    {
        public const string Skill = "skills.skill";
        public const string PtsRace = "skills.pts-race";
        public const string Activations = "skills.activations";
        public const string PerRacePct = "skills.per-race-pct";
        public const string ProcsPerRacePct = "skills.procs-per-race-pct";
        public const string WitDelta = "skills.wit-delta";
    }

    public static class Tracks
    {
        public const string Track = "tracks.track";
        public const string Distance = "tracks.distance";
        public const string Races = "tracks.races";
        public const string AvgPlace = "tracks.avg-place";
        public const string PctFirst = "tracks.pct-first";
        public const string PctTop3 = "tracks.pct-top3";
        public const string PctTop5 = "tracks.pct-top5";
        public const string SpurtRate = "tracks.spurt-rate";
        public const string Survival = "tracks.survival";
        public const string AvgSkillPts = "tracks.avg-skill-pts";
    }
}

public static class GridKeys
{
    public const string Analysis = "Analysis";
    public const string Skills = "Skills";
    public const string Tracks = "Tracks";
}
