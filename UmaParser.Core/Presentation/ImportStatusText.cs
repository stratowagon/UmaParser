using UmaParser.Import;

namespace UmaParser.Presentation;

/// <summary>
/// Short status line for a drop: how many files loaded, how many were skipped, and the batch kind.
/// The WinForms status text is longer because it also describes the Results tab.
/// </summary>
public static class ImportStatusText
{
    public static string Format(TeamTrialBatch batch)
    {
        if (!batch.HasAnyData)
        {
            return batch.SkippedFileCount > 0
                ? $"No supported race files loaded ({batch.SkippedFileCount} file(s) skipped)."
                : "No supported race files loaded.";
        }

        string skipped = batch.SkippedFileCount > 0
            ? $" ({batch.SkippedFileCount} file(s) skipped)"
            : "";

        return batch.Kind switch
        {
            TeamTrialBatchKind.RosterStats =>
                $"Roster stats — {batch.Trials.Count} team trial file(s) loaded.{skipped}",
            TeamTrialBatchKind.RosterMismatch =>
                $"Roster mismatch — {batch.Trials.Count} team trial file(s) loaded.{skipped}",
            TeamTrialBatchKind.MixedOrNonTT => FormatMixed(batch, skipped),
            _ => $"No supported race files loaded.{skipped}",
        };
    }

    private static string FormatMixed(TeamTrialBatch batch, string skipped)
    {
        string message = $"Single-race — {batch.SingleRaceImports.Count} file(s) loaded";
        if (batch.Trials.Count > 0)
        {
            message += $" ({batch.Trials.Count} team trial file(s) mixed in)";
        }

        return message + "." + skipped;
    }
}
