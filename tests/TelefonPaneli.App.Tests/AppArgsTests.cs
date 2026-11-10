using System.Net;
using TelefonPaneli.App;

namespace TelefonPaneli.App.Tests;

public class AppArgsTests
{
    [Fact]
    public void No_args_is_normal_mode() => Assert.Equal(new AppArgs(false, null), AppArgs.Parse([]));

    [Fact]
    public void Fifa_flag_is_case_insensitive() => Assert.True(AppArgs.Parse(["--FIFA"]).Fifa);

    [Fact]
    public void After_pid_is_parsed() => Assert.Equal(new AppArgs(true, 4242), AppArgs.Parse(["--fifa", "--after", "4242"]));

    [Fact]
    public void Garbage_is_ignored() => Assert.Equal(new AppArgs(false, null), AppArgs.Parse(["--after", "abc", "x"]));

    [Fact]
    public void Restart_args_round_trip()
    {
        Assert.Equal("--fifa --after 7", AppArgs.RestartArguments(fifa: true, currentPid: 7));
        Assert.Equal("--after 7", AppArgs.RestartArguments(fifa: false, currentPid: 7));
    }

    [Fact]
    public void Lan_ip_prefers_private_address_with_gateway()
    {
        var candidates = new[]
        {
            new LanCandidate(IPAddress.Parse("169.254.10.2"), HasGateway: false),
            new LanCandidate(IPAddress.Parse("172.22.0.1"), HasGateway: false),   // Hyper-V/WSL sanal adaptör
            new LanCandidate(IPAddress.Parse("192.168.1.34"), HasGateway: true),
        };
        Assert.Equal(IPAddress.Parse("192.168.1.34"), LanAddress.Pick(candidates));
    }

    [Fact]
    public void Lan_ip_falls_back_to_any_private_then_loopback()
    {
        Assert.Equal(IPAddress.Parse("10.0.0.5"), LanAddress.Pick([new LanCandidate(IPAddress.Parse("10.0.0.5"), false)]));
        Assert.Equal(IPAddress.Loopback, LanAddress.Pick([]));
    }
}
