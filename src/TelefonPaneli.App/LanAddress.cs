using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TelefonPaneli.Core.Security;

namespace TelefonPaneli.App;

public sealed record LanCandidate(IPAddress Address, bool HasGateway);

/// <summary>Telefonun bağlanacağı yerel IPv4 adresini seçer.</summary>
public static class LanAddress
{
    public static IPAddress Pick(IEnumerable<LanCandidate> candidates)
    {
        var list = candidates
            .Where(c => c.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(c.Address))
            .Where(c => NetworkGuard.IsLocal(c.Address) && !c.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
            .ToList();
        return list.FirstOrDefault(c => c.HasGateway)?.Address ?? list.FirstOrDefault()?.Address ?? IPAddress.Loopback;
    }

    public static IPAddress Current()
    {
        var candidates = new List<LanCandidate>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            var props = nic.GetIPProperties();
            var hasGateway = props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
            foreach (var u in props.UnicastAddresses) candidates.Add(new LanCandidate(u.Address, hasGateway));
        }
        return Pick(candidates);
    }
}
