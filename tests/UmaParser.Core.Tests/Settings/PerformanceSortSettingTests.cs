using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.MasterData;
using Xunit;

namespace UmaParser.Core.Tests.Settings;

public sealed class PerformanceSortSettingTests
{
    [Theory]
    [InlineData(null, PerformanceSortKey.Average)]
    [InlineData("", PerformanceSortKey.Average)]
    [InlineData("   ", PerformanceSortKey.Average)]
    [InlineData("nope", PerformanceSortKey.Average)]
    [InlineData("Average", PerformanceSortKey.Average)]
    [InlineData("median", PerformanceSortKey.Median)]
    [InlineData("Max", PerformanceSortKey.Max)]
    [InlineData("p75", PerformanceSortKey.P75)]
    [InlineData("P90", PerformanceSortKey.P90)]
    public void Missing_or_unknown_sort_is_average_and_known_names_round_trip(string? saved, PerformanceSortKey expected)
    {
        Assert.Equal(expected, PerformanceSortSetting.Parse(saved));
        if (expected != PerformanceSortKey.Average || saved == "Average")
        {
            Assert.Equal(expected, PerformanceSortSetting.Parse(PerformanceSortSetting.Format(expected)));
        }
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Missing_direction_is_ascending(bool? saved, bool ascending)
    {
        Assert.Equal(ascending, PerformanceSortSetting.AscendingOrDefault(saved));
    }

    [Fact]
    public void A_saved_sort_reloads_and_leaves_the_other_settings_keys_alone()
    {
        using var file = new TempSettingsFile();
        var seeded = new GameMasterSettings
        {
            CustomMasterDbPath = "D:/games/master.mdb",
            WindowX = 15,
            WindowY = 25,
            WindowWidth = 900,
            WindowHeight = 640,
            WindowState = "Maximized",
            ColorMode = "Dark",
        };
        seeded.Save(file.FilePath);

        (PerformanceSortKey missingKey, bool missingAscending) = PerformanceSortPreferences.Load(file.FilePath);
        Assert.Equal(PerformanceSortKey.Average, missingKey);
        Assert.True(missingAscending);

        PerformanceSortPreferences.Save(file.FilePath, PerformanceSortKey.P90, ascending: false);

        GameMasterSettings saved = GameMasterSettings.Load(file.FilePath);
        Assert.Equal("P90", saved.PerformanceSort);
        Assert.False(saved.PerformanceSortAscending);
        Assert.Equal("Dark", saved.ColorMode);
        Assert.Equal("D:/games/master.mdb", saved.CustomMasterDbPath);
        Assert.Equal(15, saved.WindowX);
        Assert.Equal(25, saved.WindowY);
        Assert.Equal(900, saved.WindowWidth);
        Assert.Equal(640, saved.WindowHeight);
        Assert.Equal("Maximized", saved.WindowState);

        (PerformanceSortKey key, bool ascending) = PerformanceSortPreferences.Load(file.FilePath);
        Assert.Equal(PerformanceSortKey.P90, key);
        Assert.False(ascending);

        PerformanceSortPreferences.Save(file.FilePath, PerformanceSortKey.Median, ascending: true);
        (PerformanceSortKey median, bool up) = PerformanceSortPreferences.Load(file.FilePath);
        Assert.Equal(PerformanceSortKey.Median, median);
        Assert.True(up);
        Assert.Equal("Dark", GameMasterSettings.Load(file.FilePath).ColorMode);
    }

    [Fact]
    public void An_unknown_saved_sort_falls_back_without_erasing_the_file()
    {
        using var file = new TempSettingsFile();
        var seeded = new GameMasterSettings
        {
            ColorMode = "Light",
            PerformanceSort = "NotAMetric",
            PerformanceSortAscending = null,
        };
        seeded.Save(file.FilePath);

        (PerformanceSortKey key, bool ascending) = PerformanceSortPreferences.Load(file.FilePath);
        Assert.Equal(PerformanceSortKey.Average, key);
        Assert.True(ascending);
        Assert.Equal("Light", GameMasterSettings.Load(file.FilePath).ColorMode);
        Assert.Equal("NotAMetric", GameMasterSettings.Load(file.FilePath).PerformanceSort);
    }
}
