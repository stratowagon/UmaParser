using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.MasterData;
using Xunit;

namespace UmaParser.Core.Tests.Settings;

public sealed class PerformanceSampleThresholdTests
{
    [Fact]
    public void Missing_keys_use_20_and_10_and_a_bad_pair_falls_back()
    {
        Assert.Equal(PerformanceSampleThresholds.Default, PerformanceSampleThresholds.FromSaved(null, null));
        Assert.Equal(new PerformanceSampleThresholds(30, 10), PerformanceSampleThresholds.FromSaved(30, null));
        Assert.Equal(new PerformanceSampleThresholds(20, 5), PerformanceSampleThresholds.FromSaved(null, 5));
        Assert.Equal(PerformanceSampleThresholds.Default, PerformanceSampleThresholds.FromSaved(5, 10));
        Assert.Equal(PerformanceSampleThresholds.Default, PerformanceSampleThresholds.FromSaved(20, 20));
        Assert.Equal(PerformanceSampleThresholds.Default, PerformanceSampleThresholds.FromSaved(0, 10));
        Assert.Equal(PerformanceSampleThresholds.Default, PerformanceSampleThresholds.FromSaved(20, 0));
    }

    [Fact]
    public void Blank_options_text_uses_the_default_and_a_red_line_at_or_above_yellow_is_rejected()
    {
        Assert.True(PerformanceSampleThresholds.TryParse(" ", null, out PerformanceSampleThresholds blank, out string blankError));
        Assert.Equal(PerformanceSampleThresholds.Default, blank);
        Assert.Equal("", blankError);

        Assert.True(PerformanceSampleThresholds.TryParse("15", "4", out PerformanceSampleThresholds custom, out _));
        Assert.Equal(new PerformanceSampleThresholds(15, 4), custom);

        Assert.False(PerformanceSampleThresholds.TryParse("10", "10", out _, out string same));
        Assert.Contains("lower than the yellow", same);
        Assert.False(PerformanceSampleThresholds.TryParse("nope", "10", out _, out string text));
        Assert.Contains("Yellow", text);
        Assert.False(PerformanceSampleThresholds.TryParse("20", "0", out _, out string zero));
        Assert.Contains("Red", zero);
    }

    [Fact]
    public void A_saved_pair_reloads_and_leaves_color_mode_and_sort_alone()
    {
        using var file = new TempSettingsFile();
        var seeded = new GameMasterSettings
        {
            ColorMode = "Light",
            PerformanceSort = "Median",
            PerformanceSortAscending = true,
            WindowX = 15,
        };
        seeded.Save(file.FilePath);

        Assert.Equal(PerformanceSampleThresholds.Default, PerformanceSamplePreferences.Load(file.FilePath));

        GameMasterSettings settings = GameMasterSettings.Load(file.FilePath);
        settings.PerformanceSampleSoftThreshold = 40;
        settings.PerformanceSampleHardThreshold = 12;
        settings.Save(file.FilePath);

        Assert.Equal(new PerformanceSampleThresholds(40, 12), PerformanceSamplePreferences.Load(file.FilePath));
        GameMasterSettings saved = GameMasterSettings.Load(file.FilePath);
        Assert.Equal("Light", saved.ColorMode);
        Assert.Equal("Median", saved.PerformanceSort);
        Assert.True(saved.PerformanceSortAscending);
        Assert.Equal(15, saved.WindowX);

        settings = GameMasterSettings.Load(file.FilePath);
        settings.PerformanceSampleSoftThreshold = 8;
        settings.PerformanceSampleHardThreshold = 8;
        settings.Save(file.FilePath);
        Assert.Equal(PerformanceSampleThresholds.Default, PerformanceSamplePreferences.Load(file.FilePath));
        Assert.Equal("Light", GameMasterSettings.Load(file.FilePath).ColorMode);
    }
}
