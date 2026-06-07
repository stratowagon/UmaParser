namespace UmaBlobber.MasterData;

internal sealed class GameMasterCatalog
{
    private readonly Dictionary<MasterTextCategory, Dictionary<int, string>> _sections = new();
    private readonly Dictionary<int, SkillMasterEntry> _skills = new();

    public IReadOnlyDictionary<MasterTextCategory, int> SectionCounts =>
        _sections.ToDictionary(kv => kv.Key, kv => kv.Value.Count);

    public void SetSection(MasterTextCategory category, Dictionary<int, string> entries)
    {
        _sections[category] = entries;
    }

    public void MergeSection(MasterTextCategory category, IEnumerable<KeyValuePair<int, string>> entries)
    {
        if (!_sections.TryGetValue(category, out var map))
        {
            map = new Dictionary<int, string>();
            _sections[category] = map;
        }

        foreach (var (key, value) in entries)
        {
            if (!string.IsNullOrEmpty(value))
            {
                map[key] = value;
            }
        }
    }

    public bool TryGet(MasterTextCategory category, int index, out string text)
    {
        text = string.Empty;
        return _sections.TryGetValue(category, out var map) && map.TryGetValue(index, out text!);
    }

    public string Format(MasterTextCategory category, int index, string missingPrefix = "#")
    {
        return TryGet(category, index, out var text) ? text : $"{missingPrefix}{index}";
    }

    public string FormatCharaShortName(int charaId) => Format(MasterTextCategory.CharaShortName, charaId);

    public string FormatSkillName(int skillId)
    {
        if (_skills.TryGetValue(skillId, out var entry) && !string.IsNullOrEmpty(entry.Name))
        {
            return entry.Name;
        }

        return Format(MasterTextCategory.SkillName, skillId);
    }

    public void SetSkillEntries(IReadOnlyDictionary<int, SkillMasterEntry> entries)
    {
        _skills.Clear();
        MergeSkillEntries(entries);
    }

    public void MergeSkillEntries(IEnumerable<KeyValuePair<int, SkillMasterEntry>> entries)
    {
        foreach (var (skillId, entry) in entries)
        {
            _skills[skillId] = entry;
            if (!string.IsNullOrEmpty(entry.Name))
            {
                MergeSection(MasterTextCategory.SkillName, [new KeyValuePair<int, string>(skillId, entry.Name)]);
            }
        }
    }

    public void MergeSkillActivateLot(IReadOnlyDictionary<int, SkillActivateLotKind> lots)
    {
        foreach (var (skillId, lot) in lots)
        {
            if (_skills.TryGetValue(skillId, out var existing))
            {
                _skills[skillId] = existing with { ActivateLot = lot };
            }
            else
            {
                string name = TryGet(MasterTextCategory.SkillName, skillId, out var text) ? text : string.Empty;
                _skills[skillId] = new SkillMasterEntry(name, lot);
            }
        }
    }

    public SkillActivateLotKind GetSkillActivateLot(int skillId) =>
        _skills.TryGetValue(skillId, out var entry)
            ? entry.ActivateLot
            : SkillActivateLotKind.Unknown;

    public int SkillActivateLotCount =>
        _skills.Values.Count(e => e.ActivateLot != SkillActivateLotKind.Unknown);

    public void ImportFromCache(MasterCacheFile cache)
    {
        _sections.Clear();
        _skills.Clear();

        foreach (var (name, entries) in cache.Sections)
        {
            if (Enum.TryParse<MasterTextCategory>(name, ignoreCase: false, out var category))
            {
                SetSection(category, new Dictionary<int, string>(entries));
            }
        }

        if (cache.SkillEntries != null && cache.SkillEntries.Count > 0)
        {
            foreach (var (key, cached) in cache.SkillEntries)
            {
                if (!int.TryParse(key, out int skillId))
                {
                    continue;
                }

                var lot = Enum.TryParse<SkillActivateLotKind>(cached.ActivateLot, ignoreCase: false, out var parsed)
                    ? parsed
                    : SkillActivateLotKind.Unknown;
                MergeSkillEntries([new KeyValuePair<int, SkillMasterEntry>(skillId, new SkillMasterEntry(cached.Name, lot))]);
            }
        }
        else if (cache.SkillActivateLot != null)
        {
            var lots = new Dictionary<int, SkillActivateLotKind>();
            foreach (var (key, value) in cache.SkillActivateLot)
            {
                if (int.TryParse(key, out int skillId)
                    && Enum.TryParse<SkillActivateLotKind>(value, ignoreCase: false, out var kind))
                {
                    lots[skillId] = kind;
                }
            }

            MergeSkillActivateLot(lots);
        }
    }

    public MasterCacheFile ExportToCache(string? sourcePath, DateTime? sourceLastWriteUtc)
    {
        var cache = new MasterCacheFile
        {
            SourcePath = sourcePath,
            SourceLastWriteUtc = sourceLastWriteUtc,
            ExportedUtc = DateTime.UtcNow,
        };

        foreach (var (category, map) in _sections)
        {
            cache.Sections[category.ToString()] = new Dictionary<int, string>(map);
        }

        cache.SkillEntries = _skills.ToDictionary(
            kv => kv.Key.ToString(),
            kv => new CachedSkillEntry
            {
                Name = kv.Value.Name,
                ActivateLot = kv.Value.ActivateLot.ToString(),
            });

        return cache;
    }
}