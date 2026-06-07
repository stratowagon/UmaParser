using UmaBlobber.DataModel.RaceScenario;
using UmaBlobber.DataModel.ResponseData;

namespace UmaBlobber.Analysis;

/// <summary>
/// One race in a trial where a specific roster uma participated.
/// </summary>
public readonly record struct RaceAppearance(
    RaceResult Result,
    RaceScenarioData Simulation,
    RaceHorseData Horse);