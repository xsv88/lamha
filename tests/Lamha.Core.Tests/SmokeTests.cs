using Lamha.Core.Prayer;

namespace Lamha.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Core_Assembly_Loads()
    {
        Assert.NotNull(typeof(PrayerName).Assembly);
    }
}
