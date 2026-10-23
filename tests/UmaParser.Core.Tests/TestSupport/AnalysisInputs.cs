using UmaParser.Analysis;
using UmaParser.ObjectModel;

namespace UmaParser.Core.Tests.TestSupport;

/// <summary>
/// Forwards to <see cref="UmaAnalysisEngine.Analyze(IReadOnlyList{TeamTrialResult}, TeamTrialResult?)"/>.
/// The layout trial is the last one in the list, which is the newest file once <see cref="TrialSets"/>
/// has ordered the batch.
/// </summary>
internal static class AnalysisInputs
{
    public static UmaAnalysisReport Analyze(IReadOnlyList<TeamTrialResult> trials) =>
        UmaAnalysisEngine.Analyze(trials);

    public static UmaAnalysisReport Analyze(IReadOnlyList<TeamTrialResult> trials, TeamTrialResult layout) =>
        UmaAnalysisEngine.Analyze(trials, layout);
}
