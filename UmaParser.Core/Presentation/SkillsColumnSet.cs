using UmaParser.Analysis;
using UmaParser.Settings;

namespace UmaParser.Presentation;

public static class SkillsColumnSet
{
    public static IReadOnlyList<GridColumnDefinition<SkillActivationRow>> All { get; } = Build();

    private static IReadOnlyList<GridColumnDefinition<SkillActivationRow>> Build()
    {
        return
        [
            Col(GridColumnIds.Skills.Skill, "Skill",
                "Skill name.",
                "Skill name. The name column is always shown first and uses color coding by skill type (white/gold/unique).",
                typeof(string), true, r => r.SkillName),
            Col(GridColumnIds.Skills.PtsRace, "Pts/race",
                "Average skill points per race.",
                "Observed Team Trial skill points per race. Only shown when Team Trial captures are loaded.",
                typeof(double), false, r => r.PointsPerRace,
                ctx => ctx.IsTeamTrial,
                _ => "Only shown for Team Trial captures."),
            Col(GridColumnIds.Skills.Activations, "Activations",
                "Total activations across all races.",
                "Total times this skill activated across all races for the selected uma.",
                typeof(int), false, r => r.ActivationCount),
            Col(GridColumnIds.Skills.PerRacePct, "≥1/race %",
                "Percent of races where the skill activated at least once.",
                "Percent of races where the skill activated at least once.",
                typeof(double), false, r => r.PerRaceActivationRatePercent),
            Col(GridColumnIds.Skills.ProcsPerRacePct, "Procs/race %",
                "Total activations ÷ races. Can exceed 100% for skills with low cooldowns.",
                "Total activations ÷ races. Can exceed 100% for skills with low cooldowns.",
                typeof(double), false, r => r.ActivationRatePercent),
            Col(GridColumnIds.Skills.WitDelta, "Δ wit",
                "≥1/race % minus expected wit rate for this uma.",
                "Difference between observed ≥1/race % and expected wit activation rate. Shows — for non-wit-gated skills.",
                typeof(double), false,
                r => r.WitDeltaPercent.HasValue ? r.WitDeltaPercent.Value : DBNull.Value,
                ctx => ctx.HasSkillLotMetadata,
                _ => "Only shown when skill lot metadata is available for the current uma."),
        ];
    }

    private static GridColumnDefinition<SkillActivationRow> Col(
        string id,
        string header,
        string tooltip,
        string description,
        Type valueType,
        bool isLocked,
        Func<SkillActivationRow, object?> getValue,
        Func<GridColumnContext, bool>? isAvailable = null,
        Func<GridColumnContext, string?>? unavailableReason = null) =>
        new()
        {
            Id = id,
            Header = header,
            Tooltip = tooltip,
            Description = description,
            ValueType = valueType,
            IsLocked = isLocked,
            GetValue = getValue,
            IsAvailable = isAvailable,
            UnavailableReason = unavailableReason,
        };
}
