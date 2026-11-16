namespace Lamha.Core.Strip;

/// <summary>What one block on the taskbar strip shows. Battery and NextEvent come in later phases.</summary>
public enum StripItem { NextPrayer, NowPlaying, CpuRam, HijriDate }

/// <summary>Text color of a strip block. Matches the prayer card colors.</summary>
public enum StripTone { Normal, Blue, Yellow, Red }

/// <summary>Two short lines, like the Windows clock.</summary>
public sealed record StripText(string Line1, string Line2, StripTone Tone = StripTone.Normal);

public sealed record StripSettings
{
    public const int MaxItems = 3;

    public bool Enabled { get; init; } = true;
    public IReadOnlyList<StripItem> Items { get; init; } = [StripItem.NextPrayer];

    /// <summary>Distance from the taskbar's right edge, in pixels, after the user drags it. Null = automatic.</summary>
    public double? OffsetFromRightPx { get; init; }

    /// <summary>1 to 3 distinct items in the chosen order.</summary>
    public StripSettings Normalized()
    {
        var items = (Items ?? []).Distinct().Take(MaxItems).ToList();
        return this with { Items = items.Count > 0 ? items : [StripItem.NextPrayer] };
    }
}
