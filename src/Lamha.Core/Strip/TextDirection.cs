namespace Lamha.Core.Strip;

/// <summary>Decides if a line should read right-to-left: the first strong letter wins (like most chat apps).</summary>
public static class TextDirection
{
    public static bool IsRtl(string text)
    {
        foreach (char c in text)
        {
            if (c is >= '֐' and <= 'ࣿ' or >= 'יִ' and <= '﷿' or >= 'ﹰ' and <= '﻿') return true;
            if (char.IsLetter(c)) return false;
        }
        return false;
    }
}
