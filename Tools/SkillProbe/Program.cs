using System.Text.Json;
using UmaBlobber.Analysis;
using UmaBlobber.DataModel.RaceScenario;
using UmaBlobber.DataModel.ResponseData;
using UmaBlobber.MasterData;
using UmaBlobber.ObjectModel;

string path = args.Length > 0
    ? args[0]
    : @"C:\Users\strat\Documents\Saved races\Team Trials\TT-20260603_104707_088.json";

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

var skillEvents = appearance.Simulation.Events
    .Where(e => e.Type == SimulateEventType.Skill && e.Params.Count >= 2)
    .ToList();

Console.WriteLine($"Total skill events in race: {skillEvents.Count}");
foreach (var g in skillEvents.GroupBy(e => e.Params[0]).OrderBy(g => g.Key))
{
    Console.WriteLine($"  param0={g.Key}: {g.Count()} events, ids={string.Join(",", g.Select(e => e.Params[1]).Distinct().Take(6))}");
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