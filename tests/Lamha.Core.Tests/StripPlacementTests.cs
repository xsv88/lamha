using Lamha.App.Strip;
using Lamha.App.Tray;

namespace Lamha.Core.Tests;

public class StripPlacementTests
{
    private static readonly PxRect BottomBar = new(0, 1032, 1920, 48);
    private static readonly PxSize Strip = new(140, 40);

    [Fact]
    public void StripPlacement_BottomTaskbar_LeftOfTray()
    {
        var tray = new PxRect(1600, 1032, 320, 48);
        var r = StripPlacement.Place(BottomBar, tray, Strip, offsetFromRight: null, scale: 1)!.Value;
        Assert.Equal(1600 - 8, r.Right);
        Assert.Equal(1032 + 4, r.Y); // Centered: (48 - 40) / 2.
    }

    [Fact]
    public void StripPlacement_NoTrayRect_FallbackOffset()
    {
        var r = StripPlacement.Place(BottomBar, null, Strip, null, scale: 1.5)!.Value;
        Assert.Equal(1920 - 260 * 1.5, r.Right);
    }

    [Fact]
    public void StripPlacement_SavedOffset_Wins()
    {
        var tray = new PxRect(1600, 1032, 320, 48);
        var r = StripPlacement.Place(BottomBar, tray, Strip, offsetFromRight: 700, scale: 1)!.Value;
        Assert.Equal(1920 - 700, r.Right);
    }

    [Fact]
    public void StripPlacement_OffsetBeyondTaskbar_Clamped()
    {
        var r = StripPlacement.Place(BottomBar, null, Strip, offsetFromRight: 5000, scale: 1)!.Value;
        Assert.Equal(0, r.X);
    }

    [Fact]
    public void StripPlacement_TaskbarTop_CenteredInTaskbar()
    {
        var top = new PxRect(0, 0, 1920, 48);
        Assert.Equal(4, StripPlacement.Place(top, null, Strip, null, 1)!.Value.Y);
    }

    [Fact]
    public void StripPlacement_VerticalTaskbar_Null() =>
        Assert.Null(StripPlacement.Place(new PxRect(0, 0, 48, 1080), null, Strip, null, 1));
}
