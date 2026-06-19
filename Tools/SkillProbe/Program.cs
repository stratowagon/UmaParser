using System.Text.Json;
using UmaParser.Analysis;
using UmaParser.DataModel.RaceScenario;
using UmaParser.DataModel.ResponseData;
using UmaParser.Import;
using UmaParser.MasterData;
using UmaParser.ObjectModel;

string path = args.Length > 0
    ? args[0]
    : Directory.EnumerateFiles(CapturePaths.DefaultTeamTrialsSaveFolder, "*.json", SearchOption.AllDirectories)
        .OrderByDescending(File.GetLastWriteTimeUtc)
        .FirstOrDefault()
    ?? string.Empty;

if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
{
    Console.WriteLine("Usage: SkillProbe <path-to-tt-json>");
    Console.WriteLine($"Default folder: {CapturePaths.DefaultTeamTrialsSaveFolder}");
    return 1;
}

GameMasterService.Current.Initialize();

var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
options.Converters.Add(new UmaApiResponseConverter());
var resp = JsonSerializer.Deserialize<UmaApiResponse>(File.ReadAllText(path), options);
if (resp is not TeamTrialResult tt || tt.Data == null)
{
    Console.WriteLine("Not a team trial result.");
    return 1;
}

var horse = tt.RaceRoster.Values.First();
var appearance = tt.GetAppearances(horse.TrainedCharaId).First();
int gate = appearance.Horse.FrameOrder;

Console.WriteLine($"Uma chara={horse.CharaId} trained={horse.TrainedCharaId} frame_order={gate}");
Console.WriteLine($"Skills on card: {appearance.Horse.SkillArray.Count}");

var skillEvents = appearance.Simulation.SkillEvents.ToList();

Console.WriteLine($"Total skill events in race: {skillEvents.Count}");
foreach (var g in skillEvents.GroupBy(e => e.HorseIndex).OrderBy(g => g.Key))
{
    Console.WriteLine($"  horseIndex={g.Key}: {g.Count()} events, ids={string.Join(",", g.Select(e => e.SkillId).Distinct().Take(6))}");
}

var report = SkillActivationAnalyzer.Analyze([tt], horse.TrainedCharaId, "probe");
int matched = report.Rows.Sum(r => r.ActivationCount);
Console.WriteLine();
Console.WriteLine($"Analyzer: {report.RaceCount} race(s), {report.Rows.Count} skills, {matched} total activations counted");
Console.WriteLine("Top 5 by rate:");
foreach (var row in report.Rows.OrderByDescending(r => r.ActivationRatePercent).Take(5))
{
    Console.WriteLine($"  {row.ActivationRatePercent,5:0.#}%  {row.SkillName} ({row.SkillId})");
}

return 0;