using UmaParser.DataModel.ResponseData;
using UmaParser.ObjectModel;

namespace UmaParser.Analysis;

/// <summary>One combo-box entry: a trained character and the label the Results roster already uses.</summary>
public readonly record struct NamedRosterUma(int TrainedCharaId, string Name);

/// <summary>
/// The Skills and Tracks uma lists for a consistent Team Trials roster.
/// Order is the trial's race roster (distance, then team member), with that trial's roster names.
/// </summary>
public static class RosterListing
{
    public static List<NamedRosterUma> FromTrial(TeamTrialResult trial)
    {
        var entries = new List<NamedRosterUma>();
        IReadOnlyList<string> names = trial.RosterNames;
        int index = 0;
        foreach (RaceHorseData uma in trial.RaceRoster.Values)
        {
            entries.Add(new NamedRosterUma(uma.TrainedCharaId, names[index]));
            index++;
        }

        return entries;
    }
}
