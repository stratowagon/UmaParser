using UmaParser.Presentation;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

public sealed class ScoreHeatTests
{
    [Fact]
    public void Endpoints_and_the_middle_use_the_light_mode_anchors()
    {
        Assert.Equal(ScoreHeatColor.Bad, ScoreHeat.ColorFor(0, 0, 100));
        Assert.Equal(ScoreHeatColor.Good, ScoreHeat.ColorFor(100, 0, 100));
        Assert.Equal(ScoreHeatColor.Neutral, ScoreHeat.ColorFor(50, 0, 100));
        Assert.Equal(ScoreHeatColor.Neutral, ScoreHeat.ColorFor(7, 7, 7));
    }

    [Fact]
    public void Values_between_the_anchors_flatten_toward_white()
    {
        Assert.Equal(new ScoreHeatColor(253, 217, 218), ScoreHeat.ColorFor(25, 0, 100));
        Assert.Equal(new ScoreHeatColor(216, 238, 222), ScoreHeat.ColorFor(75, 0, 100));
    }

    [Fact]
    public void Dark_endpoints_use_the_dark_anchors()
    {
        ScoreHeatPalette dark = ScoreHeat.Palette(ColorTheme.Dark);
        Assert.Equal(dark.Bad, ScoreHeat.ColorFor(0, 0, 100, ColorTheme.Dark));
        Assert.Equal(dark.Good, ScoreHeat.ColorFor(100, 0, 100, ColorTheme.Dark));
        Assert.Equal(dark.Neutral, ScoreHeat.ColorFor(50, 0, 100, ColorTheme.Dark));
        Assert.Equal(dark.Neutral, ScoreHeat.ColorFor(7, 7, 7, ColorTheme.Dark));
        Assert.Equal(new ScoreHeatColor(60, 38, 39), ScoreHeat.ColorFor(25, 0, 100, ColorTheme.Dark));
        Assert.Equal(new ScoreHeatColor(36, 56, 42), ScoreHeat.ColorFor(75, 0, 100, ColorTheme.Dark));
    }

    [Theory]
    [InlineData(ColorTheme.Light)]
    [InlineData(ColorTheme.Dark)]
    public void Every_blend_keeps_body_text_contrast(ColorTheme theme)
    {
        ScoreHeatColor text = ScoreHeat.Palette(theme).Text;
        for (int value = 0; value <= 100; value += 5)
        {
            ScoreHeatColor fill = ScoreHeat.ColorFor(value, 0, 100, theme);
            Assert.True(ColorContrast.Ratio(fill, text) >= ColorContrast.BodyText, $"value {value} theme {theme}");
        }
    }

    [Fact]
    public void An_empty_score_list_has_no_range()
    {
        Assert.False(ScoreHeat.TryGetRange(Array.Empty<int>(), out _, out _));
        Assert.True(ScoreHeat.TryGetRange([3, 9, 3], out double min, out double max));
        Assert.Equal(3, min);
        Assert.Equal(9, max);
    }
}
