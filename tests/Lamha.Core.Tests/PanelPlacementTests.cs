using Lamha.App.Tray;

namespace Lamha.Core.Tests;

public class PanelPlacementTests
{
    // 1920x1080 monitor, 48 px taskbar.
    private static readonly PxSize Panel = new(360, 500);

    [Fact]
    public void PanelPlacement_TaskbarBottom_AboveIcon()
    {
        var work = new PxRect(0, 0, 1920, 1032);
        var icon = new PxRect(1700, 1040, 32, 32);
        var r = PanelPlacement.Place(icon, work, Panel);

        Assert.Equal(1032 - 12, r.Bottom);
        Assert.Equal(1716, r.X + r.Width / 2); // Centered on icon...
    }

    [Fact]
    public void PanelPlacement_TaskbarTop_BelowIcon()
    {
        var work = new PxRect(0, 48, 1920, 1032);
        var icon = new PxRect(1700, 8, 32, 32);
        Assert.Equal(48 + 12, PanelPlacement.Place(icon, work, Panel).Y);
    }

    [Fact]
    public void PanelPlacement_TaskbarLeft_RightOfIcon()
    {
        var work = new PxRect(48, 0, 1872, 1080);
        var icon = new PxRect(8, 900, 32, 32);
        Assert.Equal(48 + 12, PanelPlacement.Place(icon, work, Panel).X);
    }

    [Fact]
    public void PanelPlacement_TaskbarRight_LeftOfIcon()
    {
        var work = new PxRect(0, 0, 1872, 1080);
        var icon = new PxRect(1880, 900, 32, 32);
        Assert.Equal(1872 - 12, PanelPlacement.Place(icon, work, Panel).Right);
    }

    [Fact]
    public void PanelPlacement_NearScreenEdge_StaysInsideWorkArea()
    {
        var work = new PxRect(0, 0, 1920, 1032);
        var icon = new PxRect(1900, 1040, 20, 32); // Icon at far right corner.
        var r = PanelPlacement.Place(icon, work, Panel);

        Assert.True(r.Right <= work.Right - 12);
        Assert.True(r.X >= work.X + 12);
    }

    [Fact]
    public void PanelPlacement_SecondMonitorNegativeCoords_StaysOnThatMonitor()
    {
        var work = new PxRect(-2560, 0, 2560, 1392); // Monitor to the left of the main one.
        var icon = new PxRect(-200, 1400, 32, 32);
        var r = PanelPlacement.Place(icon, work, Panel);

        Assert.True(r.X >= -2560 && r.Right <= 0);
        Assert.Equal(1392 - 12, r.Bottom);
    }

    [Fact]
    public void PanelPlacement_PanelTallerThanScreen_ClampedToTop()
    {
        var work = new PxRect(0, 0, 1280, 400);
        var icon = new PxRect(1200, 410, 32, 32);
        Assert.Equal(12, PanelPlacement.Place(icon, work, Panel).Y);
    }
}
