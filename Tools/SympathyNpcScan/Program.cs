using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

class Program
{
    static void Main(string[] args)
    {
        if (args.Any(a => a is "-h" or "--help" or "/?"))
        {
            Console.WriteLine("SympathyNpcScan - Count races with NPC Sympathy (skill_id 201631) in saved Team Trials JSONs");
            Console.WriteLine("Usage: SympathyNpcScan [optional-path-to-tt-saves-dir]");
            Console.WriteLine(@"Default: C:\Users\strat\Documents\Saved races\Team Trials (plus subfolders)");
            return;
        }

        string root = args.FirstOrDefault(a => !a.StartsWith("-"))
            ?? @"C:\Users\strat\Documents\Saved races\Team Trials";

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

        // Also report if trained_chara_id + visual is even more stable.
        int trainedStableIdentity = 0; // those with 1 mob + 1 skill + 1 visual? (rough)
        // For simplicity, just note the counts.

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
}
