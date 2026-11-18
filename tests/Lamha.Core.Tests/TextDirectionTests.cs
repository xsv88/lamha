using Lamha.Core.Strip;

namespace Lamha.Core.Tests;

public class TextDirectionTests
{
    [Theory]
    [InlineData("نهاية عهد جنسن في إنفيديا", true)]
    [InlineData("Basha Podcast | بودكاست", false)]
    [InlineData("123 مرحبا", true)]          // Digits are neutral; first letter decides.
    [InlineData("I Built the MOST Overkill", false)]
    [InlineData("", false)]
    public void IsRtl_FirstStrongLetterDecides(string text, bool rtl) => Assert.Equal(rtl, TextDirection.IsRtl(text));
}
