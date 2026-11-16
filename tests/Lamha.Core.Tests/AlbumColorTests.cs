using Lamha.Core.Media;

namespace Lamha.Core.Tests;

public class AlbumColorTests
{
    private static byte[] Solid(int w, int h, byte r, byte g, byte b, byte a = 255)
    {
        var px = new byte[w * h * 4];
        for (int i = 0; i < px.Length; i += 4) { px[i] = b; px[i + 1] = g; px[i + 2] = r; px[i + 3] = a; }
        return px;
    }

    [Fact]
    public void AlbumColor_SolidRed_ReturnsRedBucket()
    {
        var c = AlbumColor.Dominant(Solid(8, 8, 220, 30, 30), 8, 8);
        Assert.True(c.R > 180 && c.G < 60 && c.B < 60, $"got {c}");
    }

    [Fact]
    public void AlbumColor_NoImage_ReturnsNeutral() =>
        Assert.Equal(AlbumColor.Neutral, AlbumColor.Dominant([], 0, 0));

    [Fact]
    public void AlbumColor_AllWhite_ReturnsNeutral() =>
        Assert.Equal(AlbumColor.Neutral, AlbumColor.Dominant(Solid(4, 4, 255, 255, 255), 4, 4));

    [Fact]
    public void AlbumColor_Transparent_ReturnsNeutral() =>
        Assert.Equal(AlbumColor.Neutral, AlbumColor.Dominant(Solid(4, 4, 200, 0, 0, a: 0), 4, 4));

    [Fact]
    public void AlbumColor_MostlyBlueWithRedSpot_ReturnsBlue()
    {
        var px = Solid(10, 10, 30, 60, 200);
        px[0] = 0; px[1] = 0; px[2] = 255; // One red pixel.
        var c = AlbumColor.Dominant(px, 10, 10);
        Assert.True(c.B > c.R, $"got {c}");
    }

    [Fact]
    public void EnsureContrast_LightYellow_DarkenedToAtLeast4_5()
    {
        var c = AlbumColor.EnsureContrastWithWhite(new Rgb(250, 230, 120));
        Assert.True(AlbumColor.ContrastRatio(c, new Rgb(255, 255, 255)) >= 4.5);
    }

    [Fact]
    public void EnsureContrast_AlreadyDark_Unchanged()
    {
        var dark = new Rgb(40, 30, 90);
        Assert.Equal(dark, AlbumColor.EnsureContrastWithWhite(dark));
    }

    [Fact]
    public void ContrastRatio_BlackWhite_Is21() =>
        Assert.Equal(21.0, AlbumColor.ContrastRatio(new Rgb(0, 0, 0), new Rgb(255, 255, 255)), precision: 2);
}
