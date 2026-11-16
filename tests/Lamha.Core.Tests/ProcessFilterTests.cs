using Lamha.Core.SystemInfo;

namespace Lamha.Core.Tests;

public class ProcessFilterTests
{
    [Theory]
    [InlineData("svchost")]
    [InlineData("dwm")]
    [InlineData("csrss")]
    [InlineData("explorer")]
    [InlineData("Memory Compression")]
    [InlineData("Lamha")]
    [InlineData("SVCHOST")]
    public void IsClosable_SystemProcess_False(string name) => Assert.False(ProcessFilter.IsClosable(name));

    [Theory]
    [InlineData("chrome")]
    [InlineData("Spotify")]
    [InlineData("Code")]
    public void IsClosable_UserApp_True(string name) => Assert.True(ProcessFilter.IsClosable(name));
}
