using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UmaParser.Import;

class Program
{
    static void Main(string[] args)
    {
        if (args.Any(a => a is "-h" or "--help" or "/?"))
        {
            Console.WriteLine("SympathyNpcScan - Count races with NPC Sympathy (skill_id 201631) in saved Team Trials JSONs");
            Console.WriteLine("Usage: SympathyNpcScan [optional-path-to-tt-saves-dir]");
            Console.WriteLine($"Default: {CapturePaths.DefaultTeamTrialsSaveFolder} (plus subfolders)");
            return;
        }

        string root = args.FirstOrDefault(a => !a.StartsWith("-"))
            ?? CapturePaths.DefaultTeamTrialsSaveFolder;

        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"Directory not found: {root}");
            Console.Error.WriteLine("Pass the root folder containing your saved TT *.json files as the first argument.");
            return;
        }

        var jsonFiles = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
        Console.WriteLine($"Scanning {jsonFiles.Length} JSON files under {root}...");

        int totalRaces = 0;
        int totalNpcs = 0;                    // all horses with npc_type == 0
        int totalSympathyNpcs = 0;            // npc_type==0 AND has skill 201631
        int racesWithAnyNpc = 0;
        int racesWithSympathyNpc = 0;         // at least one npc_type==0 with 201631
        int filesWithSuchRace = 0;

        // Per distance_type breakdowns (1=sprint, 2=mile, 3=medium, 4=long, 5=dirt)
        // from matching race_result.distance_type by round
        var typeRaces = new Dictionary<int, int>();
        var typeWithSympathy = new Dictionary<int, int>();
        var typeTotalNpcs = new Dictionary<int, int>();
        var typeSympathyNpcs = new Dictionary<int, int>();

        // For mob_id / race_instance analysis
        var racesByInstance = new Dictionary<int, List<string>>();  // ri_id -> list of "mob1,mob2,..." roster signatures (sorted)
        var mobToSkills = new Dictionary<int, HashSet<string>>();   // mob_id -> set of observed skill signatures "id1:lvl1,id2:lvl2,..."
        var mobToRiIds = new Dictionary<int, HashSet<int>>();       // mob_id -> set of race_instance_ids it appeared in
        var mobCounts = new Dictionary<int, int>();                 // total appearances of this mob_id (across all races)

        // Analysis for trained_chara_id as potential stable NPC identifier
        var trainedToMobs = new Dictionary<int, HashSet<int>>();           // trained_chara_id -> distinct mob_ids seen for it (among NPCs)
        var trainedToSkills = new Dictionary<int, HashSet<string>>();      // trained_chara_id -> distinct skill signatures
        var trainedToCharaCards = new Dictionary<int, HashSet<(int chara, int card)>>(); // trained -> (chara_id, card_id) pairs
        var trainedCounts = new Dictionary<int, int>();                    // how many times this trained_chara_id appeared as NPC

        // Pool behavior using trained_chara_id (stable profile with fixed skills)
        var racesByInstanceTrained = new Dictionary<int, List<string>>(); // ri_id -> list of sorted "trained1,trained2,..." roster sigs
        var trainedToDistTypes = new Dictionary<int, HashSet<int>>();     // trained -> which distance_types (1-5) it appeared in
        var trainedToDtCounts = new Dictionary<int, Dictionary<int, int>>(); // trained -> dt -> appearance count
        var trainedSympathy = new Dictionary<int, bool>();                // trained -> has skill 201631 (Sympathy)

        foreach (var file in jsonFiles)
        {
            try
            {
                string json = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(json);
                var rootEl = doc.RootElement;

                // Normalize: support both full responses (with "data" wrapper) and headerless captures (direct fields at root, as in saved TT JSONs)
                JsonElement payload = rootEl;
                if (rootEl.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
                {
                    payload = dataEl;
                }

                if (!payload.TryGetProperty("race_start_params_array", out var startArray)) continue;
                if (startArray.ValueKind != JsonValueKind.Array) continue;

                // Also load results to get distance_type per round (key for race type)
                var roundToDistType = new Dictionary<int, int>();
                if (payload.TryGetProperty("race_result_array", out var resultArray) && resultArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var res in resultArray.EnumerateArray())
                    {
                        if (res.TryGetProperty("round", out var rEl) && rEl.ValueKind == JsonValueKind.Number &&
                            res.TryGetProperty("distance_type", out var dtEl) && dtEl.ValueKind == JsonValueKind.Number)
                        {
                            roundToDistType[rEl.GetInt32()] = dtEl.GetInt32();
                        }
                    }
                }

                bool hasSuchRaceInFile = false;
                foreach (var raceStart in startArray.EnumerateArray())
                {
                    totalRaces++;

                    int round = 0;
                    if (raceStart.TryGetProperty("round", out var roundEl) && roundEl.ValueKind == JsonValueKind.Number)
                        round = roundEl.GetInt32();
                    int dt = roundToDistType.GetValueOrDefault(round, 0);

                    int riId = 0;
                    if (raceStart.TryGetProperty("race_instance_id", out var riEl) && riEl.ValueKind == JsonValueKind.Number)
                        riId = riEl.GetInt32();

                    if (!raceStart.TryGetProperty("race_horse_data_array", out var horsesEl) || horsesEl.ValueKind != JsonValueKind.Array) continue;

                    bool raceHasAnyNpc = false;
                    int npcsInRace = 0;
                    int sympathyNpcsInRace = 0;
                    var npcMobs = new List<int>();
                    var npcTraineds = new List<int>();

                    foreach (var horse in horsesEl.EnumerateArray())
                    {
                        if (horse.TryGetProperty("npc_type", out var npcTypeEl) &&
                            npcTypeEl.ValueKind == JsonValueKind.Number &&
                            npcTypeEl.GetInt32() == 0)
                        {
                            raceHasAnyNpc = true;
                            totalNpcs++;
                            npcsInRace++;

                            // Collect identifiers for NPC (mob_id vs trained_chara_id etc.)
                            int mob = 0;
                            if (horse.TryGetProperty("mob_id", out var mobEl) && mobEl.ValueKind == JsonValueKind.Number)
                                mob = mobEl.GetInt32();

                            int trained = 0;
                            if (horse.TryGetProperty("trained_chara_id", out var trainedEl) && trainedEl.ValueKind == JsonValueKind.Number)
                                trained = trainedEl.GetInt32();

                            int charaId = 0;
                            if (horse.TryGetProperty("chara_id", out var charaEl) && charaEl.ValueKind == JsonValueKind.Number)
                                charaId = charaEl.GetInt32();

                            int cardId = 0;
                            if (horse.TryGetProperty("card_id", out var cardEl) && cardEl.ValueKind == JsonValueKind.Number)
                                cardId = cardEl.GetInt32();

                            // Compute skill signature once for this NPC horse
                            string skillSig = "";
                            if (horse.TryGetProperty("skill_array", out var skillsForSig) && skillsForSig.ValueKind == JsonValueKind.Array)
                            {
                                var parts = new List<string>();
                                foreach (var sk in skillsForSig.EnumerateArray())
                                {
                                    if (sk.TryGetProperty("skill_id", out var sidEl) && sidEl.ValueKind == JsonValueKind.Number)
                                    {
                                        int sid = sidEl.GetInt32();
                                        int lvl = 0;
                                        if (sk.TryGetProperty("level", out var lEl) && lEl.ValueKind == JsonValueKind.Number)
                                            lvl = lEl.GetInt32();
                                        parts.Add($"{sid}:{lvl}");
                                    }
                                }
                                parts.Sort(StringComparer.Ordinal);
                                skillSig = string.Join(",", parts);
                            }

                            if (mob != 0)
                            {
                                npcMobs.Add(mob);

                                if (!mobToSkills.ContainsKey(mob))
                                    mobToSkills[mob] = new HashSet<string>();
                                mobToSkills[mob].Add(skillSig);

                                if (riId > 0)
                                {
                                    if (!mobToRiIds.ContainsKey(mob))
                                        mobToRiIds[mob] = new HashSet<int>();
                                    mobToRiIds[mob].Add(riId);
                                }

                                mobCounts[mob] = mobCounts.GetValueOrDefault(mob, 0) + 1;
                            }

                            // Track trained_chara_id as potential stable identifier for NPCs
                            if (trained != 0)
                            {
                                npcTraineds.Add(trained);

                                if (!trainedToMobs.ContainsKey(trained))
                                    trainedToMobs[trained] = new HashSet<int>();
                                if (mob != 0) trainedToMobs[trained].Add(mob);

                                if (!trainedToSkills.ContainsKey(trained))
                                    trainedToSkills[trained] = new HashSet<string>();
                                if (!string.IsNullOrEmpty(skillSig))
                                    trainedToSkills[trained].Add(skillSig);

                                if (!trainedToCharaCards.ContainsKey(trained))
                                    trainedToCharaCards[trained] = new HashSet<(int, int)>();
                                trainedToCharaCards[trained].Add((charaId, cardId));

                                trainedCounts[trained] = trainedCounts.GetValueOrDefault(trained, 0) + 1;

                                // Pool / race type patterns
                                if (dt > 0)
                                {
                                    if (!trainedToDistTypes.ContainsKey(trained))
                                        trainedToDistTypes[trained] = new HashSet<int>();
                                    trainedToDistTypes[trained].Add(dt);

                                    if (!trainedToDtCounts.ContainsKey(trained))
                                        trainedToDtCounts[trained] = new Dictionary<int, int>();
                                    trainedToDtCounts[trained][dt] = trainedToDtCounts[trained].GetValueOrDefault(dt, 0) + 1;
                                }

                                if (!string.IsNullOrEmpty(skillSig) && skillSig.Contains("201631"))
                                {
                                    trainedSympathy[trained] = true;
                                }
                                else if (!trainedSympathy.ContainsKey(trained))
                                {
                                    trainedSympathy[trained] = false;
                                }
                            }

                            bool hasSympathy = false;
                            if (horse.TryGetProperty("skill_array", out var skillsEl) && skillsEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var skill in skillsEl.EnumerateArray())
                                {
                                    if (skill.TryGetProperty("skill_id", out var sidEl) &&
                                        sidEl.ValueKind == JsonValueKind.Number &&
                                        sidEl.GetInt32() == 201631)
                                    {
                                        hasSympathy = true;
                                        break;
                                    }
                                }
                            }

                            if (hasSympathy)
                            {
                                totalSympathyNpcs++;
                                sympathyNpcsInRace++;
                            }
                        }
                    }

                    if (raceHasAnyNpc)
                    {
                        racesWithAnyNpc++;
                    }

                    if (sympathyNpcsInRace > 0)
                    {
                        racesWithSympathyNpc++;
                        hasSuchRaceInFile = true;
                    }

                    // Accumulate per distance_type stats
                    if (dt > 0)
                    {
                        typeRaces[dt] = typeRaces.GetValueOrDefault(dt, 0) + 1;
                        typeTotalNpcs[dt] = typeTotalNpcs.GetValueOrDefault(dt, 0) + npcsInRace;
                        typeSympathyNpcs[dt] = typeSympathyNpcs.GetValueOrDefault(dt, 0) + sympathyNpcsInRace;
                        if (sympathyNpcsInRace > 0)
                        {
                            typeWithSympathy[dt] = typeWithSympathy.GetValueOrDefault(dt, 0) + 1;
                        }
                    }

                    // Record the NPC roster (by mob_id) for this exact race instance
                    if (riId > 0 && npcMobs.Count > 0)
                    {
                        npcMobs.Sort();
                        string rosterSig = string.Join(",", npcMobs);
                        if (!racesByInstance.ContainsKey(riId))
                            racesByInstance[riId] = new List<string>();
                        racesByInstance[riId].Add(rosterSig);
                    }

                    // Record NPC roster by trained_chara_id (stable profile with fixed skills)
                    if (riId > 0 && npcTraineds.Count > 0)
                    {
                        var distinctTrained = npcTraineds.Distinct().OrderBy(x => x).ToList();
                        string rosterSig = string.Join(",", distinctTrained);
                        if (!racesByInstanceTrained.ContainsKey(riId))
                            racesByInstanceTrained[riId] = new List<string>();
                        racesByInstanceTrained[riId].Add(rosterSig);
                    }
                }
                if (hasSuchRaceInFile) filesWithSuchRace++;
            }
            catch
            {
                // skip invalid JSON or non-TT files
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Total races parsed: {totalRaces}");

        double anyNpcPct = totalRaces > 0 ? Math.Round(racesWithAnyNpc * 100.0 / totalRaces, 2) : 0;
        Console.WriteLine($"Races with at least one NPC (npc_type == 0): {racesWithAnyNpc} ({anyNpcPct}%)");

        double sympathyRacePct = totalRaces > 0 ? Math.Round(racesWithSympathyNpc * 100.0 / totalRaces, 2) : 0;
        Console.WriteLine($"Races with at least one NPC having Sympathy (skill_id == 201631): {racesWithSympathyNpc} ({sympathyRacePct}%)");
        Console.WriteLine($"Files containing at least one such race: {filesWithSuchRace}");

        double avgSympathyNpcsPerRace = totalRaces > 0 ? Math.Round((double)totalSympathyNpcs / totalRaces, 3) : 0;
        double avgNpcsPerRace = totalRaces > 0 ? Math.Round((double)totalNpcs / totalRaces, 3) : 0;
        double pctOfNpcsWithSympathy = totalNpcs > 0 ? Math.Round(totalSympathyNpcs * 100.0 / totalNpcs, 2) : 0;

        Console.WriteLine();
        Console.WriteLine($"Total NPC (npc_type==0) instances across all races: {totalNpcs}");
        Console.WriteLine($"Average NPCs per race: {avgNpcsPerRace}");
        Console.WriteLine($"Total Sympathy NPCs (npc_type==0 + skill 201631): {totalSympathyNpcs}");
        Console.WriteLine($"Average Sympathy NPCs per race: {avgSympathyNpcsPerRace}");
        Console.WriteLine($"Of all NPCs, {pctOfNpcsWithSympathy}% had Sympathy");

        // Per race type breakdown
        Console.WriteLine();
        Console.WriteLine("=== Breakdown by distance_type (from race_result.distance_type) ===");
        Console.WriteLine("1=Sprint | 2=Mile | 3=Medium | 4=Long | 5=Dirt");
        Console.WriteLine();

        for (int dt = 1; dt <= 5; dt++)
        {
            string name = dt switch
            {
                1 => "Sprint",
                2 => "Mile",
                3 => "Medium",
                4 => "Long",
                5 => "Dirt",
                _ => "?"
            };
            int tr = typeRaces.GetValueOrDefault(dt, 0);
            int tws = typeWithSympathy.GetValueOrDefault(dt, 0);
            int tn = typeTotalNpcs.GetValueOrDefault(dt, 0);
            int tsn = typeSympathyNpcs.GetValueOrDefault(dt, 0);

            double pctWith = tr > 0 ? Math.Round(tws * 100.0 / tr, 2) : 0;
            double avgSymPerRace = tr > 0 ? Math.Round((double)tsn / tr, 3) : 0;
            double avgNpcsPer = tr > 0 ? Math.Round((double)tn / tr, 2) : 0;
            double symAmongNpcs = tn > 0 ? Math.Round(tsn * 100.0 / tn, 2) : 0;

            Console.WriteLine($"{name,-8} (dt={dt}): {tr,4} races | {tws,4} races ({pctWith,5:0.00}%) w/ >=1 Sympathy NPC");
            Console.WriteLine($"         avg {avgSymPerRace:0.000} Sympathy NPCs/race | {avgNpcsPer:0.00} NPCs/race total | {symAmongNpcs,5:0.00}% of NPCs have Sympathy");
            Console.WriteLine();
        }

        // ==================== mob_id / race_instance_id analysis ====================
        Console.WriteLine();
        Console.WriteLine("=== mob_id analysis (NPC identity & skill consistency) ===");

        int uniqueRi = racesByInstance.Count;
        Console.WriteLine($"Unique race_instance_id values (exact race definitions): {uniqueRi}");

        int fixedRosters = 0;
        int variableRosters = 0;
        var rosterVariationCounts = new Dictionary<int, int>(); // #distinct rosters -> how many ri_ids

        foreach (var kv in racesByInstance)
        {
            var distinct = kv.Value.Distinct().Count();
            if (distinct == 1) fixedRosters++;
            else variableRosters++;

            rosterVariationCounts[distinct] = rosterVariationCounts.GetValueOrDefault(distinct, 0) + 1;
        }

        Console.WriteLine($"Race definitions with FIXED NPC set (same mob_ids every time): {fixedRosters}");
        Console.WriteLine($"Race definitions with VARIABLE NPC sets: {variableRosters}");

        if (rosterVariationCounts.Count > 0)
        {
            Console.WriteLine("Variation distribution (distinct NPC rosters per race def):");
            foreach (var kv in rosterVariationCounts.OrderBy(k => k.Key))
                Console.WriteLine($"  {kv.Key} different rosters: {kv.Value} race definitions");
        }

        int totalUniqueMobs = mobToSkills.Count;
        Console.WriteLine();
        Console.WriteLine($"Total unique mob_ids seen across all NPCs: {totalUniqueMobs}");

        int mobsInMultipleRi = 0;
        int mobsInOneRi = 0;
        foreach (var kv in mobToRiIds)
        {
            if (kv.Value.Count > 1) mobsInMultipleRi++;
            else mobsInOneRi++;
        }
        Console.WriteLine($"Mobs that appear in only one race definition (ri_id): {mobsInOneRi}");
        Console.WriteLine($"Mobs that appear across MULTIPLE different race definitions: {mobsInMultipleRi}");

        int consistentSkills = 0;
        int variableSkills = 0;
        var exampleVariableMobs = new List<int>();

        foreach (var kv in mobToSkills)
        {
            if (kv.Value.Count == 1) consistentSkills++;
            else
            {
                variableSkills++;
                if (exampleVariableMobs.Count < 5) exampleVariableMobs.Add(kv.Key);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Mobs with CONSISTENT skill set across all appearances: {consistentSkills}");
        Console.WriteLine($"Mobs with VARYING skill sets (different skills/levels in different races): {variableSkills}");

        if (exampleVariableMobs.Count > 0)
        {
            Console.WriteLine("Example mob_ids with varying skills (first few): " + string.Join(", ", exampleVariableMobs));
            // Optionally could dump the actual sigs, but keep summary for now
        }

        // Bonus: how many times do we see the same mob
        int totalNpcInstances = mobCounts.Values.Sum();
        double avgAppearances = totalUniqueMobs > 0 ? Math.Round((double)totalNpcInstances / totalUniqueMobs, 2) : 0;
        Console.WriteLine();
        Console.WriteLine($"Total NPC (npc_type=0) instances: {totalNpcInstances}");
        Console.WriteLine($"Average appearances per unique mob_id: {avgAppearances}");

        int veryCommon = mobCounts.Count(kv => kv.Value >= 10);
        Console.WriteLine($"mob_ids that appeared 10+ times: {veryCommon}");

        // ==================== trained_chara_id analysis ====================
        Console.WriteLine();
        Console.WriteLine("=== trained_chara_id analysis (potential stable NPC identifier) ===");

        int uniqueTrained = trainedCounts.Count;
        int totalTrainedAppearances = trainedCounts.Values.Sum();
        double avgPerTrained = uniqueTrained > 0 ? Math.Round((double)totalTrainedAppearances / uniqueTrained, 2) : 0;

        Console.WriteLine($"Unique trained_chara_id values among NPCs: {uniqueTrained}");
        Console.WriteLine($"Total NPC appearances counted via trained_chara_id: {totalTrainedAppearances}");
        Console.WriteLine($"Average appearances per unique trained_chara_id: {avgPerTrained}");

        // How many different mob_ids per trained_chara_id?
        int trainedWithOneMob = 0;
        int trainedWithMultipleMobs = 0;
        int maxMobsPerTrained = 0;
        int trainedWithOneSkill = 0;
        int trainedWithMultipleSkills = 0;
        int maxSkillsPerTrained = 0;
        int trainedWithOneVisual = 0;  // same (chara,card)
        int trainedWithMultipleVisuals = 0;

        foreach (var kv in trainedToMobs)
        {
            int mobCount = kv.Value.Count;
            if (mobCount == 1) trainedWithOneMob++;
            else trainedWithMultipleMobs++;
            if (mobCount > maxMobsPerTrained) maxMobsPerTrained = mobCount;
        }

        foreach (var kv in trainedToSkills)
        {
            int skCount = kv.Value.Count;
            if (skCount == 1) trainedWithOneSkill++;
            else trainedWithMultipleSkills++;
            if (skCount > maxSkillsPerTrained) maxSkillsPerTrained = skCount;
        }

        foreach (var kv in trainedToCharaCards)
        {
            int visCount = kv.Value.Count;
            if (visCount == 1) trainedWithOneVisual++;
            else trainedWithMultipleVisuals++;
        }

        Console.WriteLine();
        Console.WriteLine("Stability when grouped by trained_chara_id:");
        Console.WriteLine($"  trained_chara_id with only 1 mob_id: {trainedWithOneMob}");
        Console.WriteLine($"  trained_chara_id with multiple mob_ids: {trainedWithMultipleMobs} (max {maxMobsPerTrained} different mobs)");
        Console.WriteLine();
        Console.WriteLine($"  trained_chara_id with only 1 skill set: {trainedWithOneSkill}");
        Console.WriteLine($"  trained_chara_id with multiple skill sets: {trainedWithMultipleSkills} (max {maxSkillsPerTrained} different sets)");
        Console.WriteLine();
        Console.WriteLine($"  trained_chara_id with only 1 (chara_id, card_id) visual: {trainedWithOneVisual}");
        Console.WriteLine($"  trained_chara_id with multiple visuals: {trainedWithMultipleVisuals}");

        // Compare to mob_id: previously we had 0 consistent skills under mob_id
        // Here we can see if trained_chara_id gives better consistency for skills.
        Console.WriteLine();
        Console.WriteLine("Note: Under mob_id, 0/612 had consistent skills. Under trained_chara_id we see the numbers above.");
        Console.WriteLine("If trained_chara_id shows many with 1 skill set, it is a better stable key for the NPC's loadout.");

        // ==================== Pool behavior with trained_chara_id (stable profiles) ====================
        Console.WriteLine();
        Console.WriteLine("=== Pool behavior re-analyzed with trained_chara_id (fixed-skill profiles) ===");

        int uniqueRiT = racesByInstanceTrained.Count;
        Console.WriteLine($"Unique race_instance_id values: {uniqueRiT}");

        int fixedTrainedRosters = 0;
        int variableTrainedRosters = 0;
        var trainedRosterVariation = new Dictionary<int, int>(); // #distinct trained rosters -> count of ri_ids

        foreach (var kv in racesByInstanceTrained)
        {
            int distinct = kv.Value.Distinct().Count();
            if (distinct == 1) fixedTrainedRosters++;
            else variableTrainedRosters++;
            trainedRosterVariation[distinct] = trainedRosterVariation.GetValueOrDefault(distinct, 0) + 1;
        }

        Console.WriteLine($"Race definitions with FIXED set of trained_chara_id profiles: {fixedTrainedRosters}");
        Console.WriteLine($"Race definitions with VARIABLE trained profiles: {variableTrainedRosters}");

        if (trainedRosterVariation.Any())
        {
            Console.WriteLine("Variation in trained rosters per race definition:");
            foreach (var kv in trainedRosterVariation.OrderBy(k => k.Key))
                Console.WriteLine($"  {kv.Key} different trained rosters: {kv.Value} race definitions");
        }

        // Usage per race type (distance_type)
        Console.WriteLine();
        Console.WriteLine("Unique trained profiles used per race type:");
        var dtName = new Dictionary<int, string> { {1,"Sprint"}, {2,"Mile"}, {3,"Medium"}, {4,"Long"}, {5,"Dirt"} };
        var profilesPerDt = new Dictionary<int, HashSet<int>>();
        int[] totalAppearancesPerDt = new int[6];

        foreach (var kv in trainedToDistTypes)
        {
            foreach (int d in kv.Value)
            {
                if (!profilesPerDt.ContainsKey(d)) profilesPerDt[d] = new HashSet<int>();
                profilesPerDt[d].Add(kv.Key);
            }
        }
        foreach (var kv in trainedToDtCounts)
        {
            foreach (var dc in kv.Value)
            {
                totalAppearancesPerDt[dc.Key] += dc.Value;
            }
        }

        for (int d = 1; d <= 5; d++)
        {
            int profCount = profilesPerDt.ContainsKey(d) ? profilesPerDt[d].Count : 0;
            Console.WriteLine($"  {dtName[d]} (dt={d}): {profCount} unique profiles (out of {uniqueTrained}), {totalAppearancesPerDt[d]} total NPC slots");
        }

        // Specialization vs generalist
        int singleTypeProfiles = 0;
        int allTypeProfiles = 0;
        var onlyInDt = new Dictionary<int, int>(); // dt -> #profiles that ONLY appear in this type

        foreach (var kv in trainedToDistTypes)
        {
            int n = kv.Value.Count;
            if (n == 1)
            {
                singleTypeProfiles++;
                int onlyDt = kv.Value.First();
                onlyInDt[onlyDt] = onlyInDt.GetValueOrDefault(onlyDt, 0) + 1;
            }
            if (n == 5) allTypeProfiles++;
        }

        Console.WriteLine();
        Console.WriteLine($"Profiles specialized to exactly 1 race type: {singleTypeProfiles}");
        Console.WriteLine($"Profiles that appear in all 5 race types (generalists): {allTypeProfiles}");

        if (onlyInDt.Any())
        {
            Console.WriteLine("Specialist profiles (appear in only one type):");
            foreach (var kv in onlyInDt.OrderBy(k => k.Key))
                Console.WriteLine($"  Only {dtName[kv.Key]}: {kv.Value} profiles");
        }

        // Sympathy profile distribution
        int sympCount = trainedSympathy.Count(kv => kv.Value);
        Console.WriteLine();
        Console.WriteLine($"Profiles carrying Sympathy (skill 201631): {sympCount} / {uniqueTrained}");

        var sympPerDt = new Dictionary<int, int>();
        var sympSpecialists = new Dictionary<int, int>();
        int sympGeneralists = 0;

        foreach (var kv in trainedSympathy)
        {
            if (!kv.Value) continue;
            int t = kv.Key;
            if (trainedToDistTypes.ContainsKey(t))
            {
                var types = trainedToDistTypes[t];
                foreach (int d in types)
                    sympPerDt[d] = sympPerDt.GetValueOrDefault(d, 0) + 1;

                if (types.Count == 5) sympGeneralists++;
                if (types.Count == 1)
                {
                    int d = types.First();
                    sympSpecialists[d] = sympSpecialists.GetValueOrDefault(d, 0) + 1;
                }
            }
        }

        Console.WriteLine("Sympathy profiles per race type (count of profiles that appear in that type):");
        for (int d=1; d<=5; d++)
        {
            int c = sympPerDt.GetValueOrDefault(d, 0);
            Console.WriteLine($"  {dtName[d]}: {c}");
        }
        Console.WriteLine($"Sympathy profiles that are generalists (all 5 types): {sympGeneralists}");
        if (sympSpecialists.Any())
        {
            Console.WriteLine("Sympathy specialists (only one type):");
            foreach (var kv in sympSpecialists.OrderBy(k=>k.Key))
                Console.WriteLine($"  Only {dtName[kv.Key]}: {kv.Value}");
        }

        Console.WriteLine();
        Console.WriteLine("Interpretation notes:");
        Console.WriteLine("- If most race definitions now show FIXED or very low-variation trained rosters,");
        Console.WriteLine("  it suggests the game selects from type-specific pools of skill profiles (trained_chara_id).");
        Console.WriteLine("- Specialist profiles + sympathy specialists would support the idea that certain");
        Console.WriteLine("  skill sets (e.g. Sympathy) are preferentially used in races where they are useful.");

        // === Full list of TT NPC pools (the 50 trained_chara_id profiles and their fixed skill sets) ===
        Console.WriteLine();
        Console.WriteLine("=== Full list of TT NPC pools (trained_chara_id + fixed skill sets from your saves) ===");
        Console.WriteLine("These are the stable NPC profiles used in TT (50 total, 10 per type). Skills are fixed per profile.");
        Console.WriteLine("Format: trained=ID [SYMP if has 201631] types=dt(s) skills: Name1, Name2, ... (all level 1)");
        var byDt = new Dictionary<int, List<string>>();
        foreach (var kv in trainedToSkills.OrderBy(k => k.Key))
        {
            int t = kv.Key;
            var skillIds = (kv.Value.FirstOrDefault() ?? "").Split(',').Select(s => s.Trim().Split(':')[0]).Where(x => !string.IsNullOrEmpty(x)).Select(s => int.Parse(s)).ToList();
            var names = skillIds.Select(id => Skills.TryGetValue(id, out var name) ? name : $"Unknown({id})").ToList();
            string sks = string.Join(", ", names);
            bool sym = trainedSympathy.GetValueOrDefault(t, false);
            string syms = sym ? " [SYMP]" : "";
            var dts = trainedToDistTypes.ContainsKey(t) ? string.Join(",", trainedToDistTypes[t]) : "?";
            int dtKey = trainedToDistTypes.ContainsKey(t) && trainedToDistTypes[t].Count > 0 ? trainedToDistTypes[t].Min() : 0;
            if (!byDt.ContainsKey(dtKey)) byDt[dtKey] = new List<string>();
            byDt[dtKey].Add($"trained={t}{syms} types={dts} skills: {sks}");
        }
        foreach (var d in new[] {1,2,3,4,5})
        {
            if (!byDt.ContainsKey(d)) continue;
            string tnm = dtName.ContainsKey(d) ? dtName[d] : d.ToString();
            Console.WriteLine($"\n-- {tnm} pool ({byDt[d].Count} profiles) --");
            foreach (var line in byDt[d].OrderBy(x => x))
                Console.WriteLine("  " + line);
        }
        Console.WriteLine($"\nTotal unique TT NPC profiles: {trainedToSkills.Count}");

        // === Probability of >=2 Sympathy NPCs per race type ===
        // Assumption: for a race of a given type, 6 distinct profiles are chosen uniformly at random
        // from the 10 dedicated to that type (hypergeometric, without replacement).
        // This matches the observed use of many different combinations within each type's pool.
        Console.WriteLine();
        Console.WriteLine("=== Odds of 2+ Sympathy NPCs (for your scenario) ===");
        Console.WriteLine("Assumption: 6 distinct profiles chosen uniformly from the type's 10-profile pool.");
        Console.WriteLine("Hypergeometric: N=10, n=6, K=#Sympathy profiles in that type's pool.");
        Console.WriteLine("Your case: 3 on your team + 0 opponent → need X>=2 from the 6 NPCs for 5+ total.");
        Console.WriteLine();

        // Hardcoded from scan: K per dt (all profiles are type specialists)
        int[] K_sym = { 0, 3, 3, 4, 5, 3 }; // dt 1=Sprint ... 5=Dirt

        long TotalWays = Binom(10, 6); // 210

        for (int d = 1; d <= 5; d++)
        {
            int K = K_sym[d];
            long waysGE2 = 0;
            for (int x = 2; x <= Math.Min(6, K); x++)
            {
                waysGE2 += Binom(K, x) * Binom(10 - K, 6 - x);
            }
            double p = (double)waysGE2 / TotalWays;
            double eX = 6.0 * K / 10.0;

            Console.WriteLine($"{dtName[d],-8} (K={K} Sympathy profiles):");
            Console.WriteLine($"  P(X >= 2) = {p * 100:F2}%   ({waysGE2}/{TotalWays} = {waysGE2}/{TotalWays})");
            Console.WriteLine($"  E[X] = {eX:F2}   (expected # Sympathy NPCs in the 6)");
            Console.WriteLine();
        }
    }

    static long Binom(int n, int k)
    {
        if (k < 0 || k > n) return 0;
        if (k == 0 || k == n) return 1;
        long res = 1;
        for (int i = 1; i <= k; i++)
        {
            res = res * (n - k + i) / i;
        }
        return res;
    }

    private static readonly Dictionary<int, string> Skills = new()
    {
        { 10071, "Warning Shot!" },
        { 10081, "Xceleration" },
        { 10091, "Red Ace" },
        { 10111, "Focused Mind" },
        { 10141, "Corazón ☆ Ardiente" },
        { 10181, "Empress's Pride" },
        { 10241, "1st Place Kiss☆" },
        { 10271, "Feel the Burn!" },
        { 10321, "Introduction to Physiology" },
        { 10351, "V Is for Victory!" },
        { 10411, "Class Rep + Speed = Bakushin" },
        { 10451, "Clear Heart" },
        { 10521, "Super-Duper Stoked" },
        { 10561, "Luck Be with Me!" },
        { 10601, "I Can Win Sometimes, Right?" },
        { 10611, "Call Me King" },
        { 10621, "Ready, Go!" },
        { 100011, "Shooting Star" },
        { 100021, "The View from the Lead Is Mine!" },
        { 100031, "Sky-High Teio Step" },
        { 100041, "Red Shift/LP1211-M" },
        { 100051, "Lights of Vaudeville" },
        { 100061, "Triumphant Pulse" },
        { 100071, "Anchors Aweigh!" },
        { 100081, "Cut and Drive!" },
        { 100091, "Resplendent Red Ace" },
        { 100101, "Shooting for Victory!" },
        { 100111, "Where There's a Will, There's a Way" },
        { 100121, "You and Me! One-on-One!" },
        { 100131, "The Duty of Dignity Calls" },
        { 100141, "Victoria por plancha ☆" },
        { 100151, "This Dance Is for Vittoria!" },
        { 100161, "Shadow Break" },
        { 100171, "Behold Thine Emperor's Divine Might" },
        { 100181, "Blazing Pride" },
        { 100191, "OMG! (ﾟ∀ﾟ)  The Final Sprint! ☆" },
        { 100201, "Angling and Scheming" },
        { 100211, "White Lightning Comin' Through!" },
        { 100221, "Fairy Tale" },
        { 100231, "∴win Q.E.D." },
        { 100241, "Flashy☆Landing" },
        { 100251, "Chasing After You" },
        { 100261, "G00 1st. F∞;" },
        { 100271, "Let's Pump Some Iron!" },
        { 100281, "YUMMY☆SPEED!" },
        { 100301, "Blue Rose Closer" },
        { 100311, "All Charged! It's Go Time!" },
        { 100321, "U=ma2" },
        { 100331, "Shooting Star of Dioskouroi" },
        { 100341, "Now We're Cruisin'!" },
        { 100351, "Our Ticket to Win!" },
        { 100371, "Schwarzes Schwert" },
        { 100381, "#LookatCurren" },
        { 100391, "A Princess Must Seize Victory!" },
        { 100401, "KEEP IT REAL." },
        { 100411, "Genius x Bakushin = Victory" },
        { 100441, "Victory Belongs to Me—Strelitzia! ☆" },
        { 100451, "Pure Heart" },
        { 100461, "SPARKLY☆STARDOM" },
        { 100481, "Pop & Polish" },
        { 100501, "Nemesis" },
        { 100511, "Budding Blossom" },
        { 100521, "Super-Duper Climax" },
        { 100561, "I See Victory in My Future!" },
        { 100581, "I Never Goof Up!" },
        { 100591, "Moving Past, and Beyond" },
        { 100601, "Just a Little Farther!" },
        { 100611, "Prideful King" },
        { 100621, "Go, Go, Mun!" },
        { 100641, "Keep Pushing Ahead" },
        { 100671, "Eternal Encompassing Shine" },
        { 100681, "Victory Cheer!" },
        { 100691, "Ambition to Surpass the Sakura" },
        { 100711, "A Lifelong Dream, A Moment's Flight" },
        { 100721, "Peerless Dance of Flowering Flames" },
        { 100741, "Lovely Spring Breeze" },
        { 110011, "Dazzl'n ♪ Diver" },
        { 110031, "Certain Victory" },
        { 110041, "A Kiss for Courage" },
        { 110051, "Ravissant" },
        { 110061, "Festive Miracle" },
        { 110111, "Superior Heal" },
        { 110131, "Legacy of the Strong" },
        { 110141, "Condor's Fury" },
        { 110151, "Barcarole of Blessings" },
        { 110171, "Arrows Whistle, Shadows Disperse" },
        { 110181, "Eternal Moments" },
        { 110201, "Break It Down!" },
        { 110221, "Best Day Ever" },
        { 110231, "Presents from X" },
        { 110241, "Flowery☆Maneuver" },
        { 110261, "Operation Cacao" },
        { 110301, "Every Rose Has Its Fangs" },
        { 110371, "Guten Appetit ♪" },
        { 110381, "One True Color" },
        { 110401, "Dancing in the Leaves" },
        { 110451, "Give Mummy a Hug ♡" },
        { 110521, "114th Time's the Charm" },
        { 110561, "Bountiful Harvest" },
        { 110601, "Go☆Go☆Goal!" },
        { 110611, "Louder! Tracen Cheer!" },
        { 200011, "Right-Handed ◎" },
        { 200012, "Right-Handed ○" },
        { 200013, "Right-Handed ×" },
        { 200014, "Right-Handed Demon" },
        { 200021, "Left-Handed ◎" },
        { 200022, "Left-Handed ○" },
        { 200023, "Left-Handed ×" },
        { 200031, "Tokyo Racecourse ◎" },
        { 200032, "Tokyo Racecourse ○" },
        { 200033, "Tokyo Racecourse ×" },
        { 200041, "Nakayama Racecourse ◎" },
        { 200042, "Nakayama Racecourse ○" },
        { 200043, "Nakayama Racecourse ×" },
        { 200051, "Hanshin Racecourse ◎" },
        { 200052, "Hanshin Racecourse ○" },
        { 200053, "Hanshin Racecourse ×" },
        { 200061, "Kyoto Racecourse ◎" },
        { 200062, "Kyoto Racecourse ○" },
        { 200063, "Kyoto Racecourse ×" },
        { 200064, "Yodo Invicta" },
        { 200071, "Chukyo Racecourse ◎" },
        { 200072, "Chukyo Racecourse ○" },
        { 200073, "Chukyo Racecourse ×" },
        { 200081, "Sapporo Racecourse ◎" },
        { 200082, "Sapporo Racecourse ○" },
        { 200083, "Sapporo Racecourse ×" },
        { 200091, "Hakodate Racecourse ◎" },
        { 200092, "Hakodate Racecourse ○" },
        { 200093, "Hakodate Racecourse ×" },
        { 200101, "Fukushima Racecourse ◎" },
        { 200102, "Fukushima Racecourse ○" },
        { 200103, "Fukushima Racecourse ×" },
        { 200111, "Niigata Racecourse ◎" },
        { 200112, "Niigata Racecourse ○" },
        { 200113, "Niigata Racecourse ×" },
        { 200121, "Kokura Racecourse ◎" },
        { 200122, "Kokura Racecourse ○" },
        { 200123, "Kokura Racecourse ×" },
        { 200131, "Standard Distance ◎" },
        { 200132, "Standard Distance ○" },
        { 200133, "Standard Distance ×" },
        { 200141, "Non-Standard Distance ◎" },
        { 200142, "Non-Standard Distance ○" },
        { 200143, "Non-Standard Distance ×" },
        { 200151, "Firm Conditions ◎" },
        { 200152, "Firm Conditions ○" },
        { 200153, "Firm Conditions ×" },
        { 200154, "Firm Course Menace" },
        { 200161, "Wet Conditions ◎" },
        { 200162, "Wet Conditions ○" },
        { 200163, "Wet Conditions ×" },
        { 200171, "Spring Runner ◎" },
        { 200172, "Spring Runner ○" },
        { 200173, "Spring Runner ×" },
        { 200174, "Spring Spectacle" },
        { 200181, "Summer Runner ◎" },
        { 200182, "Summer Runner ○" },
        { 200183, "Summer Runner ×" },
        { 200191, "Fall Runner ◎" },
        { 200192, "Fall Runner ○" },
        { 200193, "Fall Runner ×" },
        { 200194, "Fall Frenzy" },
        { 200201, "Winter Runner ◎" },
        { 200202, "Winter Runner ○" },
        { 200203, "Winter Runner ×" },
        { 200211, "Sunny Days ◎" },
        { 200212, "Sunny Days ○" },
        { 200221, "Cloudy Days ◎" },
        { 200222, "Cloudy Days ○" },
        { 200231, "Rainy Days ◎" },
        { 200232, "Rainy Days ○" },
        { 200233, "Rainy Days ×" },
        { 200241, "Snowy Days ◎" },
        { 200242, "Snowy Days ○" },
        { 200251, "Inner Post Proficiency ◎" },
        { 200252, "Inner Post Proficiency ○" },
        { 200253, "Inner Post Averseness" },
        { 200261, "Outer Post Proficiency ◎" },
        { 200262, "Outer Post Proficiency ○" },
        { 200263, "Outer Post Averseness" },
        { 200271, "Maverick ◎" },
        { 200272, "Maverick ○" },
        { 200281, "Competitive Spirit ◎" },
        { 200282, "Competitive Spirit ○" },
        { 200283, "Wallflower" },
        { 200291, "Target in Sight ◎" },
        { 200292, "Target in Sight ○" },
        { 200301, "Long Shot ◎" },
        { 200302, "Long Shot ○" },
        { 200311, "G1 Averseness" },
        { 200321, "Paddock Fright" },
        { 200331, "Professor of Curvature" },
        { 200332, "Corner Adept ○" },
        { 200333, "Corner Adept ×" },
        { 200341, "Corner Connoisseur" },
        { 200342, "Corner Acceleration ○" },
        { 200343, "Corner Acceleration ×" },
        { 200351, "Swinging Maestro" },
        { 200352, "Corner Recovery ○" },
        { 200353, "Corner Recovery ×" },
        { 200361, "Beeline Burst" },
        { 200362, "Straightaway Adept" },
        { 200371, "Rushing Gale!" },
        { 200372, "Straightaway Acceleration" },
        { 200381, "Breath of Fresh Air" },
        { 200382, "Straightaway Recovery" },
        { 200391, "Ramp Revulsion" },
        { 200401, "Packphobia" },
        { 200411, "Defeatist" },
        { 200421, "Reckless" },
        { 200431, "Concentration" },
        { 200432, "Focus" },
        { 200433, "Gatekept" },
        { 200441, "Iron Will" },
        { 200442, "Lay Low" },
        { 200451, "Center Stage" },
        { 200452, "Prudent Positioning" },
        { 200461, "It's On!" },
        { 200462, "Ramp Up" },
        { 200471, "Indomitable" },
        { 200472, "Pace Strategy" },
        { 200481, "Unruffled" },
        { 200482, "Calm in a Crowd" },
        { 200491, "No Stopping Me!" },
        { 200492, "Nimble Navigator" },
        { 200501, "Lane Legerdemain" },
        { 200502, "Go with the Flow" },
        { 200511, "In Body and Mind" },
        { 200512, "Homestretch Haste" },
        { 200521, "Running Idle" },
        { 200531, "Taking the Lead" },
        { 200532, "Early Lead" },
        { 200541, "Escape Artist" },
        { 200542, "Fast-Paced" },
        { 200551, "Unrestrained" },
        { 200552, "Final Push" },
        { 200561, "Calm and Collected" },
        { 200562, "Stamina to Spare" },
        { 200571, "Race Planner" },
        { 200572, "Preferred Position" },
        { 200581, "Speed Star" },
        { 200582, "Prepared to Pass" },
        { 200591, "Fast & Furious" },
        { 200592, "Position Pilfer" },
        { 200601, "On Your Left!" },
        { 200602, "Slick Surge" },
        { 200611, "Rising Dragon" },
        { 200612, "Outer Swell" },
        { 200621, "Sleeping Lion" },
        { 200622, "Standing By" },
        { 200631, "Sturm und Drang" },
        { 200632, "Masterful Gambit" },
        { 200641, "Encroaching Shadow" },
        { 200642, "Straightaway Spurt" },
        { 200651, "Turbo Sprint" },
        { 200652, "Sprinting Gear" },
        { 200662, "Wait-and-See" },
        { 200671, "Blinding Flash" },
        { 200672, "Gap Closer" },
        { 200681, "Mile Maven" },
        { 200682, "Productive Plan" },
        { 200691, "Keen Eye" },
        { 200692, "Watchful Eye" },
        { 200701, "Furious Feat" },
        { 200702, "Updrafters" },
        { 200711, "Trackblazer" },
        { 200712, "Rosy Outlook" },
        { 200721, "Killer Tunes" },
        { 200722, "Up-Tempo" },
        { 200731, "Unyielding" },
        { 200732, "Steadfast" },
        { 200741, "Cooldown" },
        { 200742, "Deep Breaths" },
        { 200751, "Innate Experience" },
        { 200752, "Inside Scoop" },
        { 200761, "Adrenaline Rush" },
        { 200762, "Extra Tank" },
        { 200771, "Trick (Front)" },
        { 200772, "Tantalizing Trick" },
        { 200781, "Trick (Rear)" },
        { 200791, "Frenzied Front Runners" },
        { 200801, "Frenzied Pace Chasers" },
        { 200811, "Frenzied Late Surgers" },
        { 200821, "Frenzied End Closers" },
        { 200831, "Subdued Front Runners" },
        { 200841, "Flustered Front Runners" },
        { 200851, "Hesitant Front Runners" },
        { 200861, "Subdued Pace Chasers" },
        { 200871, "Flustered Pace Chasers" },
        { 200881, "Hesitant Pace Chasers" },
        { 200891, "Subdued Late Surgers" },
        { 200901, "Flustered Late Surgers" },
        { 200911, "Hesitant Late Surgers" },
        { 200921, "Subdued End Closers" },
        { 200931, "Flustered End Closers" },
        { 200941, "Hesitant End Closers" },
        { 200951, "Oi Racecourse ◎" },
        { 200952, "Oi Racecourse ○" },
        { 200953, "Oi Racecourse ×" },
        { 200961, "Sprint Straightaways ◎" },
        { 200962, "Sprint Straightaways ○" },
        { 200971, "Sprint Corners ◎" },
        { 200972, "Sprint Corners ○" },
        { 200981, "Staggering Lead" },
        { 200982, "Huge Lead" },
        { 200991, "Plan X" },
        { 200992, "Countermeasure" },
        { 201001, "Perfect Prep!" },
        { 201002, "Meticulous Measures" },
        { 201011, "Adored by All" },
        { 201012, "Intimidate" },
        { 201021, "You've Got No Shot" },
        { 201022, "Stop Right There!" },
        { 201031, "Mile Straightaways ◎" },
        { 201032, "Mile Straightaways ○" },
        { 201041, "Mile Corners ◎" },
        { 201042, "Mile Corners ○" },
        { 201051, "Changing Gears" },
        { 201052, "Shifting Gears" },
        { 201061, "Step on the Gas!" },
        { 201062, "Acceleration" },
        { 201071, "Big-Sisterly" },
        { 201072, "Unyielding Spirit" },
        { 201081, "Greed for Speed" },
        { 201082, "Speed Eater" },
        { 201091, "Battle Formation" },
        { 201092, "Opening Gambit" },
        { 201101, "Medium Straightaways ◎" },
        { 201102, "Medium Straightaways ○" },
        { 201103, "Flash Forward" },
        { 201111, "Medium Corners ◎" },
        { 201112, "Medium Corners ○" },
        { 201121, "Clairvoyance" },
        { 201122, "Hawkeye" },
        { 201131, "Lightning Step" },
        { 201132, "Thunderbolt Step" },
        { 201141, "Miraculous Step" },
        { 201142, "Soft Step" },
        { 201151, "Dominator" },
        { 201152, "Tether" },
        { 201161, "Mystifying Murmur" },
        { 201162, "Murmur" },
        { 201171, "Long Straightaways ◎" },
        { 201172, "Long Straightaways ○" },
        { 201173, "Blast Forward" },
        { 201181, "Long Corners ◎" },
        { 201182, "Long Corners ○" },
        { 201191, "Vanguard Spirit" },
        { 201192, "Keeping the Lead" },
        { 201201, "VIP Pass" },
        { 201202, "Passing Pro" },
        { 201211, "Overwhelming Pressure" },
        { 201212, "Pressure" },
        { 201221, "Stamina Siphon" },
        { 201222, "Stamina Eater" },
        { 201231, "Illusionist" },
        { 201232, "Smoke Screen" },
        { 201241, "Front Runner Straightaways ◎" },
        { 201242, "Front Runner Straightaways ○" },
        { 201251, "Front Runner Corners ◎" },
        { 201252, "Front Runner Corners ○" },
        { 201261, "Sixth Sense" },
        { 201262, "Dodging Danger" },
        { 201271, "Top Runner" },
        { 201272, "Leader's Pride" },
        { 201281, "Restless" },
        { 201282, "Moxie" },
        { 201291, "Reignition" },
        { 201292, "Second Wind" },
        { 201302, "Restart" },
        { 201311, "Pace Chaser Straightaways ◎" },
        { 201312, "Pace Chaser Straightaways ○" },
        { 201321, "Pace Chaser Corners ◎" },
        { 201322, "Pace Chaser Corners ○" },
        { 201331, "Technician" },
        { 201332, "Shrewd Step" },
        { 201341, "Determined Descent" },
        { 201342, "Straight Descent" },
        { 201351, "Gourmand" },
        { 201352, "Hydrate" },
        { 201361, "Shatterproof" },
        { 201362, "Tactical Tweak" },
        { 201371, "Dazzling Disorientation" },
        { 201372, "Disorient" },
        { 201381, "Late Surger Straightaways ◎" },
        { 201382, "Late Surger Straightaways ○" },
        { 201391, "Late Surger Corners ◎" },
        { 201392, "Late Surger Corners ○" },
        { 201401, "Hard Worker" },
        { 201402, "Fighter" },
        { 201411, "15,000,000 CC" },
        { 201412, "1,500,000 CC" },
        { 201421, "Relax" },
        { 201422, "A Small Breather" },
        { 201431, "The Bigger Picture" },
        { 201432, "Studious" },
        { 201441, "All-Seeing Eyes" },
        { 201442, "Sharp Gaze" },
        { 201451, "End Closer Straightaways ◎" },
        { 201452, "End Closer Straightaways ○" },
        { 201461, "End Closer Corners ◎" },
        { 201462, "End Closer Corners ○" },
        { 201471, "The Coast Is Clear!" },
        { 201472, "I Can See Right Through You" },
        { 201481, "Go-Home Specialist" },
        { 201482, "After-School Stroll" },
        { 201491, "Serenity" },
        { 201492, "Levelheaded" },
        { 201501, "Crusader" },
        { 201502, "Strategist" },
        { 201511, "Petrifying Gaze" },
        { 201512, "Intense Gaze" },
        { 201521, "Front Runner Savvy ◎" },
        { 201522, "Front Runner Savvy ○" },
        { 201531, "Pace Chaser Savvy ◎" },
        { 201532, "Pace Chaser Savvy ○" },
        { 201541, "Late Surger Savvy ◎" },
        { 201542, "Late Surger Savvy ○" },
        { 201551, "End Closer Savvy ◎" },
        { 201552, "End Closer Savvy ○" },
        { 201561, "Super Lucky Seven" },
        { 201562, "Lucky Seven" },
        { 201571, "Triple 7s" },
        { 201581, "Highlander" },
        { 201591, "Uma Stan" },
        { 201592, "Superstan" },
        { 201601, "Groundwork" },
        { 201611, "Tail Held High" },
        { 201612, "Tail Nine" },
        { 201621, "Shake It Out" },
        { 201631, "Sympathy" },
        { 201641, "Lone Wolf" },
        { 201651, "Slipstream" },
        { 201661, "Playtime's Over!" },
        { 201662, "See Ya Later!" },
        { 201671, "Trending in the Charts!" },
        { 201672, "Top Pick" },
        { 201681, "Lead the Charge!" },
        { 201682, "Forward, March!" },
        { 201691, "Lie in Wait" },
        { 201692, "Be Still" },
        { 201701, "Come What May" },
        { 201702, "All I've Got" },
        { 201801, "♡ 3D Nail Art" },
        { 201901, "Neck and Neck" },
        { 201902, "Head-On" },
        { 202001, "Master of the Sands" },
        { 202002, "Familiar Ground" },
        { 202011, "Headliner" },
        { 202012, "Feature Act" },
        { 202021, "Daring Strike" },
        { 202022, "Early Start" },
        { 202031, "Nothing Ventured" },
        { 202032, "Risky Business" },
        { 202041, "In High Spirits" },
        { 202042, "Light as a Feather" },
        { 202051, "Runaway" },
        { 202061, "Best in Japan" },
        { 202071, "Of Calm Mind" },
        { 202072, "Free-Spirited" },
        { 202081, "From the Brink" },
        { 202082, "Take the Chance" },
        { 202091, "Burning Soul" },
        { 202092, "Fighting Spirit" },
        { 202101, "Elated" },
        { 202102, "Eager" },
        { 202111, "Full of Vigor" },
        { 202112, "Pumped" },
        { 202121, "Dauntless" },
        { 202122, "Fearless" },
        { 202131, "Wild Wind" },
        { 202132, "With All My Soul" },
        { 202141, "You're Not the Boss of Me!" },
        { 202151, "Keep Going!" },
        { 202152, "Full Throttle" },
        { 202161, "Restraint" },
        { 210011, "Burning Spirit SPD" },
        { 210012, "Ignited Spirit SPD" },
        { 210021, "Burning Spirit STA" },
        { 210022, "Ignited Spirit STA" },
        { 210031, "Burning Spirit PWR" },
        { 210032, "Ignited Spirit PWR" },
        { 210041, "Burning Spirit GUTS" },
        { 210042, "Ignited Spirit GUTS" },
        { 210051, "Burning Spirit WIT" },
        { 210052, "Ignited Spirit WIT" },
        { 210061, "Radiant Star" },
        { 210062, "Glittering Star" },
        { 300011, "Unquenched Thirst" },
        { 300021, "Unchanging" },
        { 300031, "Towards the Scenery I Seek" },
        { 300041, "Creeping Anxiety" },
        { 300051, "Blatant Fear" },
        { 300061, "Dream Run" },
        { 300071, "Show Me What Lies Beyond!" },
        { 300081, "Hoiya! Have a Good Run!" },
        { 300091, "As a Friend and Rival" },
        { 300101, "Cheers of a Fellow Dreamer" },
        { 300111, "Chin Up, Derby Umamusume!" },
        { 300121, "For the Team" },
        { 900011, "Shooting Star" },
        { 900021, "The View from the Lead Is Mine!" },
        { 900031, "Sky-High Teio Step" },
        { 900041, "Red Shift/LP1211-M" },
        { 900051, "Lights of Vaudeville" },
        { 900061, "Triumphant Pulse" },
        { 900071, "Anchors Aweigh!" },
        { 900081, "Cut and Drive!" },
        { 900091, "Resplendent Red Ace" },
        { 900101, "Shooting for Victory!" },
        { 900111, "Where There's a Will, There's a Way" },
        { 900121, "You and Me! One-on-One!" },
        { 900131, "The Duty of Dignity Calls" },
        { 900141, "Victoria por plancha ☆" },
        { 900151, "This Dance Is for Vittoria!" },
        { 900161, "Shadow Break" },
        { 900171, "Behold Thine Emperor's Divine Might" },
        { 900181, "Blazing Pride" },
        { 900191, "OMG! (ﾟ∀ﾟ)  The Final Sprint! ☆" },
        { 900201, "Angling and Scheming" },
        { 900211, "White Lightning Comin' Through!" },
        { 900221, "Fairy Tale" },
        { 900231, "∴win Q.E.D." },
        { 900241, "Flashy☆Landing" },
        { 900251, "Chasing After You" },
        { 900261, "G00 1st. F∞;" },
        { 900271, "Let's Pump Some Iron!" },
        { 900281, "YUMMY☆SPEED!" },
        { 900301, "Blue Rose Closer" },
        { 900311, "All Charged! It's Go Time!" },
        { 900321, "U=ma2" },
        { 900331, "Shooting Star of Dioskouroi" },
        { 900341, "Now We're Cruisin'!" },
        { 900351, "Our Ticket to Win!" },
        { 900371, "Schwarzes Schwert" },
        { 900381, "#LookatCurren" },
        { 900391, "A Princess Must Seize Victory!" },
        { 900401, "KEEP IT REAL." },
        { 900411, "Genius x Bakushin = Victory" },
        { 900441, "Victory Belongs to Me—Strelitzia! ☆" },
        { 900451, "Pure Heart" },
        { 900461, "SPARKLY☆STARDOM" },
        { 900481, "Pop & Polish" },
        { 900501, "Nemesis" },
        { 900511, "Budding Blossom" },
        { 900521, "Super-Duper Climax" },
        { 900561, "I See Victory in My Future!" },
        { 900581, "I Never Goof Up!" },
        { 900591, "Moving Past, and Beyond" },
        { 900601, "Just a Little Farther!" },
        { 900611, "Prideful King" },
        { 900621, "Go, Go, Mun!" },
        { 900641, "Keep Pushing Ahead" },
        { 900671, "Eternal Encompassing Shine" },
        { 900681, "Victory Cheer!" },
        { 900691, "Ambition to Surpass the Sakura" },
        { 900711, "A Lifelong Dream, A Moment's Flight" },
        { 900721, "Peerless Dance of Flowering Flames" },
        { 900741, "Lovely Spring Breeze" },
        { 910011, "Dazzl'n ♪ Diver" },
        { 910031, "Certain Victory" },
        { 910041, "A Kiss for Courage" },
        { 910051, "Ravissant" },
        { 910061, "Festive Miracle" },
        { 910111, "Superior Heal" },
        { 910131, "Legacy of the Strong" },
        { 910141, "Condor's Fury" },
        { 910151, "Barcarole of Blessings" },
        { 910171, "Arrows Whistle, Shadows Disperse" },
        { 910181, "Eternal Moments" },
        { 910201, "Break It Down!" },
        { 910221, "Best day ever" },
        { 910231, "Presents from X" },
        { 910241, "Flowery☆Maneuver" },
        { 910261, "Operation Cacao" },
        { 910301, "Every Rose Has Its Fangs" },
        { 910371, "Guten Appetit ♪" },
        { 910381, "One True Color" },
        { 910401, "Dancing in the Leaves" },
        { 910451, "Give Mummy a Hug ♡" },
        { 910521, "114th Time's the Charm" },
        { 910561, "Bountiful Harvest" },
        { 910601, "Go☆Go☆Goal!" },
        { 910611, "Louder! Tracen Cheer!" },
        { 1000011, "Carnival Bonus" },
        { 1000012, "Carnival Bonus" },
        { 1100011, "Feelin' a Bit Silly" },
    };

}
