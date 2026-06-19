namespace UmaParser.Ui;

internal static class ResultsGridHeaders
{
    public const int CompactHeaderHeight = 36;
    private const int MaxLineLength = 12;

    /// <summary>
    /// Compact two-line column label for a capture filename.
    /// Uses a HorseACT-style date/time only when the basename clearly matches that pattern;
    /// otherwise wraps the actual filename. Full filename should be shown via
    /// <see cref="DataGridViewColumn.ToolTipText"/>.
    /// </summary>
    public static string FormatCompact(string fileName)
    {
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        return TryFormatHorseActTimestamp(baseName, out string formatted)
            ? formatted
            : WrapFileName(baseName);
    }

    public static void ApplyCompactHeaderLayout(DataGridView grid)
    {
        grid.ColumnHeadersHeight = CompactHeaderHeight;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
    }

    public static void ApplyDefaultHeaderLayout(DataGridView grid)
    {
        grid.ColumnHeadersHeight = 23;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
    }

    private static bool TryFormatHorseActTimestamp(string baseName, out string formatted)
    {
        formatted = string.Empty;

        ReadOnlySpan<char> span = baseName.AsSpan();
        if (span.StartsWith("TT-", StringComparison.OrdinalIgnoreCase))
        {
            span = span[3..];
        }

        if (span.Length < 13 || span[8] != '_')
        {
            return false;
        }

        ReadOnlySpan<char> date = span[..8];
        if (!AllDigits(date))
        {
            return false;
        }

        int timeEnd = 9;
        while (timeEnd < span.Length && char.IsDigit(span[timeEnd]) && timeEnd < 15)
        {
            timeEnd++;
        }

        int timeLength = timeEnd - 9;
        if (timeLength < 4)
        {
            return false;
        }

        ReadOnlySpan<char> remainder = span[timeEnd..];
        if (!remainder.IsEmpty)
        {
            if (remainder[0] != '_' || !AllDigits(remainder[1..]))
            {
                return false;
            }
        }

        formatted = $"{date[4..6]}/{date[6..8]}{Environment.NewLine}{span[9..11]}:{span[11..13]}";
        return true;
    }

    private static string WrapFileName(string baseName)
    {
        if (string.IsNullOrEmpty(baseName))
        {
            return baseName;
        }

        if (baseName.Length <= MaxLineLength)
        {
            return baseName;
        }

        int target = baseName.Length / 2;
        int bestBreak = -1;
        int bestDistance = int.MaxValue;

        for (int i = 1; i < baseName.Length; i++)
        {
            if (baseName[i] is not ('_' or '-' or '.' or ' '))
            {
                continue;
            }

            int distance = Math.Abs(i - target);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestBreak = i;
            }
        }

        if (bestBreak > 0)
        {
            string line1 = TrimSeparators(baseName[..bestBreak]);
            string line2 = TrimSeparators(baseName[(bestBreak + 1)..]);
            if (line1.Length > 0 && line2.Length > 0)
            {
                return $"{Truncate(line1)}{Environment.NewLine}{Truncate(line2)}";
            }
        }

        int mid = baseName.Length / 2;
        return $"{Truncate(baseName[..mid])}{Environment.NewLine}{Truncate(baseName[mid..])}";
    }

    private static bool AllDigits(ReadOnlySpan<char> span)
    {
        foreach (char c in span)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static string TrimSeparators(string value)
    {
        return value.Trim('_', '-', '.', ' ');
    }

    private static string Truncate(string value)
    {
        return value.Length <= MaxLineLength
            ? value
            : value[..(MaxLineLength - 1)] + "…";
    }
}