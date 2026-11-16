namespace Lamha.Core.Media;

public readonly record struct Rgb(byte R, byte G, byte B);

/// <summary>
/// Picks the main color of an album cover and makes sure white text stays readable on it.
/// </summary>
public static class AlbumColor
{
    /// <summary>Used when there is no cover or no usable color.</summary>
    public static readonly Rgb Neutral = new(60, 60, 70);

    /// <summary>
    /// Most common color in a BGRA image. Colors are grouped into 4-bit buckets per channel;
    /// near-white, near-black and transparent pixels are ignored.
    /// </summary>
    public static Rgb Dominant(ReadOnlySpan<byte> bgra, int width, int height)
    {
        int count = Math.Min(width * height, bgra.Length / 4);
        if (count <= 0) return Neutral;

        var buckets = new Dictionary<int, (int Hits, long R, long G, long B)>();
        for (int i = 0; i < count; i++)
        {
            byte b = bgra[i * 4], g = bgra[i * 4 + 1], r = bgra[i * 4 + 2], a = bgra[i * 4 + 3];
            if (a < 128) continue;
            int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            if (min > 235 || max < 20) continue; // Near-white or near-black.

            int key = (r >> 4) << 8 | (g >> 4) << 4 | (b >> 4);
            buckets.TryGetValue(key, out var e);
            buckets[key] = (e.Hits + 1, e.R + r, e.G + g, e.B + b);
        }

        if (buckets.Count == 0) return Neutral;
        var best = buckets.Values.MaxBy(v => v.Hits);
        return new Rgb((byte)(best.R / best.Hits), (byte)(best.G / best.Hits), (byte)(best.B / best.Hits));
    }

    /// <summary>Darkens the color in small steps until white text has at least <paramref name="minRatio"/> contrast (WCAG).</summary>
    public static Rgb EnsureContrastWithWhite(Rgb c, double minRatio = 4.5)
    {
        var white = new Rgb(255, 255, 255);
        var (h, s, l) = ToHsl(c);
        var current = c;
        while (ContrastRatio(current, white) < minRatio && l > 0)
        {
            l = Math.Max(0, l - 0.02);
            current = FromHsl(h, s, l);
        }
        return current;
    }

    /// <summary>WCAG contrast ratio between two colors (1 to 21).</summary>
    public static double ContrastRatio(Rgb a, Rgb b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(Rgb c)
    {
        static double Ch(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
    }

    private static (double H, double S, double L) ToHsl(Rgb c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2, d = max - min;
        if (d == 0) return (0, 0, l);
        double s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h / 6, s, l);
    }

    private static Rgb FromHsl(double h, double s, double l)
    {
        if (s == 0) { byte v = (byte)Math.Round(l * 255); return new Rgb(v, v, v); }
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
        static double Hue(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            return t < 1.0 / 6 ? p + (q - p) * 6 * t : t < 0.5 ? q : t < 2.0 / 3 ? p + (q - p) * (2.0 / 3 - t) * 6 : p;
        }
        return new Rgb((byte)Math.Round(Hue(p, q, h + 1.0 / 3) * 255),
                       (byte)Math.Round(Hue(p, q, h) * 255),
                       (byte)Math.Round(Hue(p, q, h - 1.0 / 3) * 255));
    }
}
