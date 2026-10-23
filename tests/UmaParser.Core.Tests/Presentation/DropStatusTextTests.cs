using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using UmaParser.Presentation;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

public sealed class DropStatusTextTests
{
    [Fact]
    public void Empty_and_skipped_drops_keep_the_WinForms_sentences()
    {
        Assert.Equal("No supported race files loaded.", DropStatusText.NoneLoaded(0));
        Assert.Equal(
            "No supported race files loaded (2 file(s) skipped).",
            DropStatusText.NoneLoaded(2));
        Assert.Equal("4 team trial file(s) loaded.", DropStatusText.RosterLoaded(4, 0));
        Assert.Equal(
            "4 team trial file(s) loaded. (1 other file(s) skipped.)",
            DropStatusText.RosterLoaded(4, 1));
        Assert.Equal(
            "Roster mismatch — compare columns to find outlier files. Analysis skipped.",
            DropStatusText.RosterMismatch(0));
        Assert.Equal(
            "Roster mismatch — compare columns to find outlier files. Analysis skipped. (3 other file(s) skipped.)",
            DropStatusText.RosterMismatch(3));
    }

    [Fact]
    public void Single_race_and_mixed_drops_count_parsed_blobs_and_local_umas()
    {
        TeamTrialBatch single = TrialSets.Build("Single-CM12");
        NonTeamTrialLoad load = NonTeamTrialLoad.Collect(single.SingleRaceImports);
        string text = DropStatusText.Mixed(single, load);

        Assert.Contains($"{single.SingleRaceImports.Count} single-race capture(s) loaded", text);
        Assert.Contains(
            $"Parsed {load.ParsedSimulationCount}/{single.SingleRaceImports.Count} sim blobs ({load.TotalHorseSlots} total horse slots). Found {load.Identities.Count} unique local player uma(s).",
            text);
        Assert.Contains("Results + Analysis are for uniform TT rosters only. Skills/Tracks support non-uniform data.", text);
        Assert.DoesNotContain("not recommended", text);

        TeamTrialBatch team = TrialSets.Build("TT-K0");
        var mixed = new TeamTrialBatch
        {
            Kind = TeamTrialBatchKind.MixedOrNonTT,
            Trials = team.Trials,
            SingleRaceImports = single.SingleRaceImports,
            SkippedFileCount = 2,
        };
        string mixedText = DropStatusText.Mixed(mixed, load);
        Assert.Contains($" (incl. {team.Trials.Count} TT)", mixedText);
        Assert.Contains($" (incl. {single.SingleRaceImports.Count} CM/Room/Practice)", mixedText);
        Assert.Contains("(2 other file(s) skipped)", mixedText);
        Assert.Contains("Mixing Team Trials with CM/Room/Practice is not recommended", mixedText);
    }
}
