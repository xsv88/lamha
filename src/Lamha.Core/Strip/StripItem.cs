namespace Lamha.Core.Strip;

/// <summary>What one block on the taskbar strip shows. NextEvent comes in a later phase.</summary>
public enum StripItem { NextPrayer, NowPlaying, CpuRam, HijriDate, Battery }

/// <summary>Text color of a strip block. Matches the prayer card colors.</summary>
public enum StripTone { Normal, Blue, Yellow, Red }

/// <summary>
/// Two short lines, like the Windows clock. A now-playing block is drawn as a colored "pill"
/// with the cover: <see cref="Pill"/> is its background, <see cref="Image"/> the cover pixels (BGRA).
/// </summary>
public sealed record StripText(string Line1, string Line2, StripTone Tone = StripTone.Normal)
{
    public Lamha.Core.Media.Rgb? Pill { get; init; }
    public byte[]? Image { get; init; }
    public int ImageSize { get; init; }
    public bool Playing { get; init; }
}

/// <summary>Localized words used on the strip, e.g. English "in"/"min" or Arabic "بعد"/"د".</summary>
public sealed record StripWords(string In, string Iqama, string Min, string Hour, string MinuteShort);

public sealed record StripSettings
{
    public const int MaxItems = 3;

    public bool Enabled { get; init; } = true;
    public IReadOnlyList<StripItem> Items { get; init; } = [StripItem.NextPrayer];

    /// <summary>Distance from the taskbar's right edge, in pixels, after the user drags it. Null = automatic.</summary>
    public double? OffsetFromRightPx { get; init; }

    /// <summary>
    /// Content equality. Record equality compares the Items list by reference, so two copies
    /// with the same items would look "changed" and restart services for nothing.
    /// </summary>
    public bool SameAs(StripSettings other) =>
        Enabled == other.Enabled && OffsetFromRightPx == other.OffsetFromRightPx && Items.SequenceEqual(other.Items);

    /// <summary>1 to 3 distinct items in the chosen order.</summary>
    public StripSettings Normalized()
    {
        var items = (Items ?? []).Distinct().Take(MaxItems).ToList();
        return this with { Items = items.Count > 0 ? items : [StripItem.NextPrayer] };
    }
}
