using UmaParser.Analysis;
using UmaParser.Presentation;
using UmaParser.Settings;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

public sealed class SkillsColumnLayoutTests
{
    [Fact]
    public void Team_trial_with_metadata_shows_every_skills_column_and_keeps_the_name_first()
    {
        var context = new GridColumnContext { IsTeamTrial = true, HasSkillLotMetadata = true };
        IReadOnlyList<GridColumnDefinition<SkillActivationRow>> columns =
            GridColumnLayout.ResolveEffectiveColumns(SkillsColumnSet.All, new GridColumnGridPreferences(), context);

        Assert.Equal(
            [
                GridColumnIds.Skills.Skill,
                GridColumnIds.Skills.PtsRace,
                GridColumnIds.Skills.Activations,
                GridColumnIds.Skills.PerRacePct,
                GridColumnIds.Skills.ProcsPerRacePct,
                GridColumnIds.Skills.WitDelta,
            ],
            columns.Select(column => column.Id));
        Assert.Equal(
            ["Skill", "Pts/race", "Activations", "≥1/race %", "Procs/race %", "Δ wit"],
            columns.Select(column => column.Header));
    }

    [Fact]
    public void Points_per_race_is_team_trials_only_and_wit_delta_needs_lot_metadata()
    {
        var context = new GridColumnContext { IsTeamTrial = false, HasSkillLotMetadata = false };
        IReadOnlyList<GridColumnDefinition<SkillActivationRow>> columns =
            GridColumnLayout.ResolveEffectiveColumns(SkillsColumnSet.All, new GridColumnGridPreferences(), context);

        Assert.DoesNotContain(columns, column => column.Id == GridColumnIds.Skills.PtsRace);
        Assert.DoesNotContain(columns, column => column.Id == GridColumnIds.Skills.WitDelta);
        Assert.Equal(GridColumnIds.Skills.Skill, columns[0].Id);
    }

    [Fact]
    public void A_hidden_preference_removes_a_customizable_column_but_not_the_skill_name()
    {
        var prefs = new GridColumnGridPreferences
        {
            Hidden = [GridColumnIds.Skills.Activations, GridColumnIds.Skills.Skill],
        };
        var context = new GridColumnContext { IsTeamTrial = true, HasSkillLotMetadata = true };
        IReadOnlyList<GridColumnDefinition<SkillActivationRow>> columns =
            GridColumnLayout.ResolveEffectiveColumns(SkillsColumnSet.All, prefs, context);

        Assert.Equal(GridColumnIds.Skills.Skill, columns[0].Id);
        Assert.DoesNotContain(columns, column => column.Id == GridColumnIds.Skills.Activations);
        Assert.Contains(columns, column => column.Id == GridColumnIds.Skills.PtsRace);
    }
}
