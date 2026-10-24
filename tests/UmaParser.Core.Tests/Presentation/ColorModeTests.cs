using UmaParser.Presentation;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

public sealed class ColorModeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("System")]
    [InlineData("nope")]
    public void Unsaved_or_unknown_is_system(string? saved)
    {
        Assert.Equal(ColorMode.System, ColorModeSetting.Parse(saved));
    }

    [Fact]
    public void Light_and_dark_round_trip()
    {
        Assert.Equal(ColorMode.Light, ColorModeSetting.Parse("Light"));
        Assert.Equal(ColorModeSetting.Light, ColorModeSetting.Format(ColorMode.Light));
        Assert.Equal(ColorMode.Dark, ColorModeSetting.Parse("Dark"));
        Assert.Equal(ColorModeSetting.Dark, ColorModeSetting.Format(ColorMode.Dark));
        Assert.Equal(ColorModeSetting.System, ColorModeSetting.Format(ColorMode.System));
    }

    [Theory]
    [InlineData(0, ColorTheme.Dark)]
    [InlineData(1, ColorTheme.Light)]
    public void System_follows_the_windows_reader(int appsUseLightTheme, ColorTheme expected)
    {
        Assert.Equal(expected, SystemColorTheme.Resolve(ColorMode.System, new FakeWindowsTheme(appsUseLightTheme)));
    }

    [Fact]
    public void Missing_windows_value_is_light()
    {
        Assert.True(SystemColorTheme.IsLight(null));
        Assert.Equal(ColorTheme.Light, SystemColorTheme.Resolve(ColorMode.System, new FakeWindowsTheme(null)));
    }

    [Fact]
    public void Saved_light_and_dark_ignore_windows()
    {
        var windowsDark = new FakeWindowsTheme(0);
        var windowsLight = new FakeWindowsTheme(1);
        Assert.Equal(ColorTheme.Light, SystemColorTheme.Resolve(ColorMode.Light, windowsDark));
        Assert.Equal(ColorTheme.Dark, SystemColorTheme.Resolve(ColorMode.Dark, windowsLight));
    }

    private sealed class FakeWindowsTheme : IWindowsAppThemeReader
    {
        public FakeWindowsTheme(int? appsUseLightTheme) => AppsUseLightTheme = appsUseLightTheme;

        public int? AppsUseLightTheme { get; }
    }
}
