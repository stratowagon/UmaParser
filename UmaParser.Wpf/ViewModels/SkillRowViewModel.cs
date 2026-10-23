using UmaParser.Analysis;
using UmaParser.Presentation;

namespace UmaParser.ViewModels;

/// <summary>
/// One Skills grid row. The numbers come from <see cref="SkillActivationRow"/>.
/// Text properties match the WinForms cell format. Sort properties stay numeric
/// so a header click sorts 10 after 9, not after 1.
/// </summary>
public sealed class SkillRowViewModel
{
    public SkillRowViewModel(SkillActivationRow row)
    {
        SkillName = row.SkillName;
        PointsPerRace = row.PointsPerRace;
        PointsPerRaceText = SkillCellText.PointsPerRace(row.PointsPerRace);
        ActivationCount = row.ActivationCount;
        PerRacePercent = row.PerRaceActivationRatePercent;
        PerRacePercentText = SkillCellText.OneDecimal(row.PerRaceActivationRatePercent);
        ProcsPerRacePercent = row.ActivationRatePercent;
        ProcsPerRacePercentText = SkillCellText.OneDecimal(row.ActivationRatePercent);
        WitDeltaPercent = row.WitDeltaPercent;
        WitDeltaText = SkillCellText.FormatWitDelta(row);
        NameBand = CellBands.ForSkillName(row);
        MetricBand = CellBands.ForSkillMetric(row);
    }

    public string SkillName { get; }
    public double PointsPerRace { get; }
    public string PointsPerRaceText { get; }
    public int ActivationCount { get; }
    public double PerRacePercent { get; }
    public string PerRacePercentText { get; }
    public double ProcsPerRacePercent { get; }
    public string ProcsPerRacePercentText { get; }
    public double? WitDeltaPercent { get; }
    public string WitDeltaText { get; }
    public CellBand NameBand { get; }
    public CellBand MetricBand { get; }
}
