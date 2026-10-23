using UmaParser.Import;

namespace UmaParser.Presentation;

/// <summary>
/// Status-bar text for a drop. The sentences and the punctuation match the WinForms form.
/// A uniform Team Trials drop replaces this with <see cref="AnalysisCellText.Summary"/>
/// once the analysis report exists. The shorter <see cref="ImportStatusText"/> line is a
/// different summary and is not what the Results tab shows.
/// </summary>
public static class DropStatusText
{
    public static string For(TeamTrialBatch batch, NonTeamTrialLoad? singleRaceLoad = null)
    {
        return batch.Kind switch
        {
            TeamTrialBatchKind.RosterStats => RosterLoaded(batch.Trials.Count, batch.SkippedFileCount),
            TeamTrialBatchKind.RosterMismatch => RosterMismatch(batch.SkippedFileCount),
            TeamTrialBatchKind.MixedOrNonTT => Mixed(
                batch,
                singleRaceLoad ?? NonTeamTrialLoad.Collect(batch.SingleRaceImports)),
            _ => NoneLoaded(batch.SkippedFileCount),
        };
    }

    public static string NoneLoaded(int skippedFileCount) =>
        skippedFileCount > 0
            ? $"No supported race files loaded ({skippedFileCount} file(s) skipped)."
            : "No supported race files loaded.";

    public static string RosterLoaded(int trialCount, int skippedFileCount)
    {
        string primary = $"{trialCount} team trial file(s) loaded.";
        return skippedFileCount <= 0
            ? primary
            : $"{primary} ({skippedFileCount} other file(s) skipped.)";
    }

    public static string RosterMismatch(int skippedFileCount)
    {
        const string primary = "Roster mismatch — compare columns to find outlier files. Analysis skipped.";
        return skippedFileCount <= 0
            ? primary
            : $"{primary} ({skippedFileCount} other file(s) skipped.)";
    }

    public static string Mixed(TeamTrialBatch batch, NonTeamTrialLoad load)
    {
        int ttCount = batch.Trials.Count;
        int singleCount = batch.SingleRaceImports.Count;
        string what = singleCount > 0 ? "single-race capture(s)" : "races";
        string message = $"{ttCount + singleCount} {what} loaded";
        if (ttCount > 0)
        {
            message += $" (incl. {ttCount} TT)";
        }

        if (singleCount > 0)
        {
            message += $" (incl. {singleCount} CM/Room/Practice)";
        }

        if (batch.SkippedFileCount > 0)
        {
            message += $" ({batch.SkippedFileCount} other file(s) skipped)";
        }

        if (singleCount > 0)
        {
            message += $". Parsed {load.ParsedSimulationCount}/{singleCount} sim blobs ({load.TotalHorseSlots} total horse slots). Found {load.Identities.Count} unique local player uma(s).";
        }

        if (ttCount > 0 && singleCount > 0)
        {
            message += "  (Note: Mixing Team Trials with CM/Room/Practice is not recommended — the two use cases have very different team structures and the app treats them separately.)";
        }

        return message + "  Results + Analysis are for uniform TT rosters only. Skills/Tracks support non-uniform data.";
    }
}
