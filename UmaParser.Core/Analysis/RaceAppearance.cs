using UmaParser.DataModel.RaceScenario;
using UmaParser.DataModel.ResponseData;

namespace UmaParser.Analysis;

/// <summary>
/// One race in a trial where a specific roster uma participated.
/// </summary>
public readonly record struct RaceAppearance(
    RaceResult Result,
    RaceScenarioData Simulation,
    RaceHorseData Horse);