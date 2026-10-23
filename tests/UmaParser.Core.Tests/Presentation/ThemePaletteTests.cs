using UmaParser.Analysis;
using UmaParser.Presentation;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

public sealed class ThemePaletteTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Light")]
    [InlineData("System")]
    public void Anything_but_dark_is_the_light_theme(string? saved)
    {
        Assert.Equal(ColorTheme.Light, ColorThemeSetting.Parse(saved));
        Assert.Equal(ColorThemeSetting.Light, ColorThemeSetting.Format(ColorTheme.Light));
    }

    [Fact]
    public void Dark_round_trips_the_settings_word()
    {
        Assert.Equal(ColorTheme.Dark, ColorThemeSetting.Parse("Dark"));
        Assert.Equal("Dark", ColorThemeSetting.Format(ColorTheme.Dark));
    }

    [Theory]
    [InlineData(ColorTheme.Light)]
    [InlineData(ColorTheme.Dark)]
    public void Cell_bands_keep_text_contrast(ColorTheme theme)
    {
        foreach (CellBand band in Enum.GetValues<CellBand>())
        {
            if (band == CellBand.None)
            {
                continue;
            }

            RgbColor fill = CellBandPalette.Background(band, theme);
            RgbColor text = CellBandPalette.Foreground(band, theme);
            Assert.True(text.ContrastWith(fill) >= ColorContrast.BodyText, $"{band} {theme}");
        }
    }

    [Fact]
    public void Light_bands_keep_the_winforms_fills()
    {
        Assert.Equal(new RgbColor(46, 125, 50), CellBandPalette.Background(CellBand.Stable, ColorTheme.Light));
        Assert.Equal(new RgbColor(180, 134, 11), CellBandPalette.Background(CellBand.NeedsWork, ColorTheme.Light));
        Assert.Equal(new RgbColor(183, 28, 28), CellBandPalette.Background(CellBand.Critical, ColorTheme.Light));
        Assert.Equal(new RgbColor(255, 215, 0), CellBandPalette.Background(CellBand.Gold, ColorTheme.Light));
        Assert.Equal(new RgbColor(176, 166, 210), CellBandPalette.Background(CellBand.Unique, ColorTheme.Light));
        Assert.Equal(RgbColor.Black, CellBandPalette.Foreground(CellBand.NeedsWork, ColorTheme.Light));
        Assert.Equal(RgbColor.White, CellBandPalette.Foreground(CellBand.Stable, ColorTheme.Light));
    }

    [Theory]
    [InlineData(ColorTheme.Light)]
    [InlineData(ColorTheme.Dark)]
    public void Outlier_text_contrasts_with_the_fill(ColorTheme theme)
    {
        Assert.True(ResultsMarks.Text(theme).ContrastWith(ResultsMarks.Fill(theme)) >= ColorContrast.BodyText);
    }

    [Fact]
    public void Light_outliers_stay_black_on_yellow()
    {
        Assert.Equal(new RgbColor(255, 255, 200), ResultsMarks.Fill(ColorTheme.Light));
        Assert.Equal(RgbColor.Black, ResultsMarks.Text(ColorTheme.Light));
        Assert.Equal(new RgbColor(90, 80, 30), ResultsMarks.Fill(ColorTheme.Dark));
        Assert.Equal(RgbColor.White, ResultsMarks.Text(ColorTheme.Dark));
    }
}
