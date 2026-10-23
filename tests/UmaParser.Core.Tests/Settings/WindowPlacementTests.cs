using UmaParser.MasterData;
using UmaParser.Settings;
using Xunit;

namespace UmaParser.Core.Tests.Settings;

public sealed class WindowPlacementTests
{
    [Fact]
    public void A_saved_size_below_the_minimum_is_ignored()
    {
        var settings = new GameMasterSettings { WindowWidth = 639, WindowHeight = 480, WindowX = 10, WindowY = 10 };

        Assert.False(WindowPlacement.TryReadSize(settings, out _, out _));

        settings.WindowWidth = 640;
        settings.WindowHeight = 479;
        Assert.False(WindowPlacement.TryReadSize(settings, out _, out _));

        settings.WindowHeight = null;
        settings.WindowWidth = 800;
        Assert.False(WindowPlacement.TryReadSize(settings, out _, out _));
    }

    [Fact]
    public void The_minimum_size_and_a_missing_position_round_trip()
    {
        var settings = new GameMasterSettings();
        WindowPlacement.Write(settings, 12, 24, WindowPlacement.MinWidth, WindowPlacement.MinHeight, maximized: false);

        Assert.True(WindowPlacement.TryReadSize(settings, out int width, out int height));
        Assert.Equal(WindowPlacement.MinWidth, width);
        Assert.Equal(WindowPlacement.MinHeight, height);
        Assert.True(WindowPlacement.TryReadPosition(settings, out int x, out int y));
        Assert.Equal(12, x);
        Assert.Equal(24, y);
        Assert.False(WindowPlacement.IsMaximized(settings));
        Assert.Equal(WindowPlacement.Normal, settings.WindowState);
    }

    [Fact]
    public void Only_the_maximized_word_restores_maximized()
    {
        var settings = new GameMasterSettings();
        WindowPlacement.Write(settings, 0, 0, 800, 600, maximized: true);

        Assert.True(WindowPlacement.IsMaximized(settings));
        Assert.Equal("Maximized", settings.WindowState);

        settings.WindowState = "maximized";
        Assert.False(WindowPlacement.IsMaximized(settings));
        settings.WindowState = null;
        Assert.False(WindowPlacement.IsMaximized(settings));
    }

    [Fact]
    public void A_window_inside_the_work_area_stays_put()
    {
        var area = new ScreenRect(100, 50, 800, 600);

        (int x, int y) = WindowPlacement.ClampToWorkArea(120, 80, 640, 480, area);

        Assert.Equal((120, 80), (x, y));
    }

    [Fact]
    public void A_window_past_the_edge_slides_back_inside()
    {
        var area = new ScreenRect(100, 50, 800, 600);

        (int x, int y) = WindowPlacement.ClampToWorkArea(400, 400, 640, 480, area);

        Assert.Equal(100 + 800 - 640, x);
        Assert.Equal(50 + 600 - 480, y);
    }

    [Fact]
    public void A_window_larger_than_the_work_area_is_pinned_to_its_top_left()
    {
        var area = new ScreenRect(20, 30, 500, 400);

        (int x, int y) = WindowPlacement.ClampToWorkArea(200, 200, 800, 700, area);

        Assert.Equal((20, 30), (x, y));
    }

    [Fact]
    public void The_monitor_with_the_larger_overlap_gets_the_window()
    {
        MonitorArea[] monitors =
        [
            new(new ScreenRect(0, 0, 1000, 800), new ScreenRect(0, 0, 1000, 760)),
            new(new ScreenRect(1000, 0, 1000, 800), new ScreenRect(1000, 40, 1000, 760)),
        ];

        // 300px on the left monitor, 340px on the right. The right work area starts at y=40.
        (int x, int y) = WindowPlacement.ClampToVisible(700, 10, 640, 480, monitors);

        Assert.Equal(1000, x);
        Assert.Equal(40, y);
    }

    [Fact]
    public void A_window_off_every_monitor_moves_onto_the_nearest_one()
    {
        MonitorArea[] monitors =
        [
            new(new ScreenRect(0, 0, 800, 600), new ScreenRect(0, 0, 800, 600)),
            new(new ScreenRect(2000, 0, 800, 600), new ScreenRect(2000, 0, 800, 600)),
        ];

        (int leftX, int leftY) = WindowPlacement.ClampToVisible(-700, 100, 640, 480, monitors);
        Assert.Equal((0, 100), (leftX, leftY));

        (int rightX, int rightY) = WindowPlacement.ClampToVisible(3000, 100, 640, 480, monitors);
        Assert.Equal(2000 + 800 - 640, rightX);
        Assert.Equal(100, rightY);
    }

    [Fact]
    public void No_monitors_leaves_the_saved_point_alone()
    {
        (int x, int y) = WindowPlacement.ClampToVisible(-40, -50, 800, 600, Array.Empty<MonitorArea>());

        Assert.Equal((-40, -50), (x, y));
    }
}
