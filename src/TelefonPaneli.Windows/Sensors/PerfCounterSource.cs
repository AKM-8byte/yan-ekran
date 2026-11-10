using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using TelefonPaneli.Core.Metrics;

namespace TelefonPaneli.Windows.Sensors;

/// <summary>Sürücü gerektirmeyen Windows sayaçları: CPU kullanımı, RAM, ağ hızı.</summary>
public sealed class PerfCounterSource : IMetricSource, IDisposable
{
    const double GB = 1024d * 1024 * 1024;
    const double MB = 1024d * 1024;

    readonly PerformanceCounter? _cpu;
    long _lastRx, _lastTx;
    long _lastTicks;

    public PerfCounterSource()
    {
        try
        {
            _cpu = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total", readOnly: true);
            _cpu.NextValue();
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            _cpu = null;
        }
    }

    public string Name => "perf";

    public void Collect(IDictionary<string, double?> into)
    {
        into[MetricIds.CpuLoad] = _cpu is null ? null : Math.Clamp(_cpu.NextValue(), 0, 100);

        var mem = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (GlobalMemoryStatusEx(ref mem))
        {
            into[MetricIds.RamTotal] = mem.TotalPhys / GB;
            into[MetricIds.RamUsed] = (mem.TotalPhys - mem.AvailPhys) / GB;
        }
        else
        {
            into[MetricIds.RamTotal] = null;
            into[MetricIds.RamUsed] = null;
        }

        var (rx, tx) = NetworkTotals();
        var now = Stopwatch.GetTimestamp();
        if (_lastTicks != 0 && rx >= _lastRx && tx >= _lastTx)
        {
            var secs = Stopwatch.GetElapsedTime(_lastTicks, now).TotalSeconds;
            into[MetricIds.NetDown] = secs > 0 ? (rx - _lastRx) / MB / secs : 0;
            into[MetricIds.NetUp] = secs > 0 ? (tx - _lastTx) / MB / secs : 0;
        }
        else
        {
            into[MetricIds.NetDown] = null;
            into[MetricIds.NetUp] = null;
        }
        (_lastRx, _lastTx, _lastTicks) = (rx, tx, now);
    }

    static (long Rx, long Tx) NetworkTotals()
    {
        long rx = 0, tx = 0;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
            try
            {
                var s = nic.GetIPStatistics();
                rx += s.BytesReceived;
                tx += s.BytesSent;
            }
            catch (NetworkInformationException) { }
        }
        return (rx, tx);
    }

    public void Dispose() => _cpu?.Dispose();

    [StructLayout(LayoutKind.Sequential)]
    struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
