using UmaParser.Analysis;
using UmaParser.Presentation;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

public sealed class ThemePaletteTests
{
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
    public void Light_bands_use_soft_fills()
    {
        Assert.Equal(new RgbColor(183, 225, 184), CellBandPalette.Background(CellBand.Stable, ColorTheme.Light));
        Assert.Equal(new RgbColor(255, 216, 168), CellBandPalette.Background(CellBand.NeedsWork, ColorTheme.Light));
        Assert.Equal(new RgbColor(246, 193, 193), CellBandPalette.Background(CellBand.Critical, ColorTheme.Light));
        Assert.Equal(new RgbColor(255, 233, 160), CellBandPalette.Background(CellBand.Gold, ColorTheme.Light));
        Assert.Equal(new RgbColor(215, 198, 239), CellBandPalette.Background(CellBand.Unique, ColorTheme.Light));
        foreach (CellBand band in Enum.GetValues<CellBand>())
        {
            if (band == CellBand.None)
            {
                continue;
            }

            Assert.Equal(RgbColor.Black, CellBandPalette.Foreground(band, ColorTheme.Light));
        }
    }

    [Fact]
    public void Dark_bands_keep_the_same_hues()
    {
        Assert.Equal(new RgbColor(46, 125, 50), CellBandPalette.Background(CellBand.Stable, ColorTheme.Dark));
        Assert.Equal(new RgbColor(255, 179, 0), CellBandPalette.Background(CellBand.NeedsWork, ColorTheme.Dark));
        Assert.Equal(new RgbColor(198, 40, 40), CellBandPalette.Background(CellBand.Critical, ColorTheme.Dark));
        Assert.Equal(new RgbColor(255, 193, 7), CellBandPalette.Background(CellBand.Gold, ColorTheme.Dark));
        Assert.Equal(new RgbColor(94, 84, 128), CellBandPalette.Background(CellBand.Unique, ColorTheme.Dark));
        Assert.Equal(RgbColor.White, CellBandPalette.Foreground(CellBand.Stable, ColorTheme.Dark));
        Assert.Equal(RgbColor.Black, CellBandPalette.Foreground(CellBand.NeedsWork, ColorTheme.Dark));
        Assert.Equal(RgbColor.White, CellBandPalette.Foreground(CellBand.Critical, ColorTheme.Dark));
        Assert.Equal(RgbColor.Black, CellBandPalette.Foreground(CellBand.Gold, ColorTheme.Dark));
        Assert.Equal(RgbColor.White, CellBandPalette.Foreground(CellBand.Unique, ColorTheme.Dark));
        Assert.Equal(new RgbColor(241, 241, 241), CellBandPalette.Foreground(CellBand.None, ColorTheme.Dark));
        Assert.Equal(RgbColor.Black, CellBandPalette.Foreground(CellBand.None, ColorTheme.Light));
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
