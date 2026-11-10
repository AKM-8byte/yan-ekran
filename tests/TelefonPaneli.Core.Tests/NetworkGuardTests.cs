using System.Net;
using TelefonPaneli.Core.Security;

namespace TelefonPaneli.Core.Tests;

public class NetworkGuardTests
{
    [Theory]
    [InlineData("192.168.1.20", true)]
    [InlineData("10.0.0.5", true)]
    [InlineData("172.20.1.1", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.255", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("127.0.0.1", true)]
    [InlineData("169.254.3.4", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("::1", true)]
    [InlineData("fe80::1", true)]
    [InlineData("fd12:3456::1", true)]
    [InlineData("::ffff:192.168.1.5", true)]
    [InlineData("::ffff:8.8.8.8", false)]
    [InlineData("2001:4860::1", false)]
    public void IsLocal(string ip, bool expected) => Assert.Equal(expected, NetworkGuard.IsLocal(IPAddress.Parse(ip)));
}
