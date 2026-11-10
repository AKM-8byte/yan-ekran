using System.Security.Principal;
using TelefonPaneli.Core.Metrics;
using TelefonPaneli.Windows.Sensors;

namespace TelefonPaneli.App.Tests;

/// <summary>Yalnızca gerçek makinede, TP_HARDWARE=1 ortam değişkeniyle çalışır.</summary>
public sealed class HardwareFactAttribute : FactAttribute
{
    public HardwareFactAttribute(bool needsAdmin = false)
    {
        if (Environment.GetEnvironmentVariable("TP_HARDWARE") != "1") Skip = "TP_HARDWARE=1 değil";
        else if (needsAdmin && !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            Skip = "yönetici yetkisi gerekli";
    }
}

[Trait("Category", "Hardware")]
public class HardwareSmokeTests
{
    static Dictionary<string, double?> Collect(IMetricSource s)
    {
        var d = new Dictionary<string, double?>();
        s.Collect(d);
        return d;
    }

    [HardwareFact]
    public async Task PerfCounters_report_cpu_ram_and_net()
    {
        using var src = new PerfCounterSource();
        Collect(src);
        await Task.Delay(1100);
        var d = Collect(src);
        Assert.InRange(d[MetricIds.CpuLoad]!.Value, 0, 100);
        Assert.True(d[MetricIds.RamTotal] > 4);
        Assert.InRange(d[MetricIds.RamUsed]!.Value, 0.1, d[MetricIds.RamTotal]!.Value);
        Assert.True(d[MetricIds.NetDown] >= 0);
        Assert.True(d[MetricIds.NetUp] >= 0);
    }

    [HardwareFact]
    public async Task Ping_writes_key()
    {
        using var src = new PingSource("1.1.1.1", TimeSpan.FromMilliseconds(500));
        await Task.Delay(1500);
        Assert.True(Collect(src).ContainsKey(MetricIds.Ping));
    }

    [HardwareFact]
    public void Gpu_gate_finds_nvidia_adapter()
    {
        using var gate = new GpuActivityGate();
        Assert.NotNull(gate.NvidiaLuid);
        _ = gate.IsAwake; // exception atmamalı
    }

    [HardwareFact]
    public void Nvml_source_reports_vram_total_when_awake()
    {
        using var gate = new GpuActivityGate(forceAwake: true);
        using var src = new NvmlSource(gate);
        var d = Collect(src);
        Assert.True(d[MetricIds.GpuVramTotal] > 1);
        Assert.InRange(d[MetricIds.GpuTemp]!.Value, 20, 110);
    }

    [HardwareFact]
    public void Nvml_source_writes_nulls_when_asleep()
    {
        using var gate = new GpuActivityGate(forceAsleep: true);
        using var src = new NvmlSource(gate);
        var d = Collect(src);
        Assert.Null(d[MetricIds.GpuTemp]);
        Assert.Null(d[MetricIds.GpuLoad]);
    }

    [HardwareFact(needsAdmin: true)]
    public void Lhm_reads_cpu_temp_in_normal_mode()
    {
        using var src = new LhmSource();
        var d = Collect(src);
        Assert.InRange(d[MetricIds.CpuTemp]!.Value, 20, 110);
    }
}
