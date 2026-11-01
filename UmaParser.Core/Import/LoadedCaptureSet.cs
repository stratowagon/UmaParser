namespace UmaParser.Import;

/// <summary>
/// Paths the window has been given. A later drop appends; it does not replace this list.
/// The same path is stored once, compared as a full path without regard to case, so dropping
/// a file that is already loaded does not count it twice. Import still decides which of
/// these paths are usable captures.
/// </summary>
public sealed class LoadedCaptureSet
{
    private readonly List<string> _paths = new();
    private readonly HashSet<string> _known = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Paths => _paths;

    public int Count => _paths.Count;

    /// <summary>
    /// Stores paths that are not already in the set. Blank entries are ignored.
    /// Returns how many paths were new.
    /// </summary>
    public int Add(IEnumerable<string> paths)
    {
        int added = 0;
        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string key = Normalize(path);
            if (!_known.Add(key))
            {
                continue;
            }

            _paths.Add(key);
            added++;
        }

        return added;
    }

    public void Clear()
    {
        _paths.Clear();
        _known.Clear();
    }

    /// <summary>Full path when the text is a path. An unusable string is kept as typed so it can be skipped once.</summary>
    public static string Normalize(string path)
    {
        string trimmed = path.Trim();
        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return trimmed;
        }
    }
}
