using System.Runtime.InteropServices;
using TelefonPaneli.Core.Metrics;

namespace TelefonPaneli.Windows.Sensors;

/// <summary>
/// NVIDIA GPU değerlerini NVIDIA sürücüsüyle gelen nvml.dll üzerinden okur. Ek sürücü yüklemez,
/// bu yüzden FIFA modunda da açıktır. GPU uykudayken (gate) hiç dokunmaz ve null yazar.
/// </summary>
public sealed class NvmlSource : IMetricSource, IDisposable
{
    const double MiB = 1024d * 1024;
    static readonly string[] Keys =
    [
        MetricIds.GpuTemp, MetricIds.GpuLoad, MetricIds.GpuPower, MetricIds.GpuClock, MetricIds.GpuVramUsed, MetricIds.GpuVramTotal,
    ];

    readonly GpuActivityGate _gate;
    bool _initialized;
    bool _unavailable;
    IntPtr _device;

    public NvmlSource(GpuActivityGate gate) => _gate = gate;

    public string Name => "nvml";

    public void Collect(IDictionary<string, double?> into)
    {
        foreach (var k in Keys) into[k] = null;
        if (_unavailable || !_gate.HasNvidia || !_gate.IsAwake) return;
        if (!EnsureInit()) return;

        if (nvmlDeviceGetTemperature(_device, 0, out var temp) == 0) into[MetricIds.GpuTemp] = temp;
        if (nvmlDeviceGetUtilizationRates(_device, out var util) == 0) into[MetricIds.GpuLoad] = util.Gpu;
        if (nvmlDeviceGetPowerUsage(_device, out var mw) == 0) into[MetricIds.GpuPower] = mw / 1000.0;
        if (nvmlDeviceGetClockInfo(_device, 0, out var mhz) == 0) into[MetricIds.GpuClock] = mhz;
        if (nvmlDeviceGetMemoryInfo(_device, out var mem) == 0)
        {
            into[MetricIds.GpuVramUsed] = mem.Used / MiB / 1024;
            into[MetricIds.GpuVramTotal] = mem.Total / MiB / 1024;
        }
    }

    bool EnsureInit()
    {
        if (_initialized) return true;
        try
        {
            if (nvmlInit_v2() != 0 || nvmlDeviceGetHandleByIndex_v2(0, out _device) != 0)
            {
                _unavailable = true;
                return false;
            }
            _initialized = true;
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            _unavailable = true;
            return false;
        }
    }

    public void Dispose()
    {
        if (_initialized) nvmlShutdown();
        _initialized = false;
    }

    [StructLayout(LayoutKind.Sequential)] struct Utilization { public uint Gpu, Memory; }
    [StructLayout(LayoutKind.Sequential)] struct Memory { public ulong Total, Free, Used; }

    [DllImport("nvml.dll")] static extern int nvmlInit_v2();
    [DllImport("nvml.dll")] static extern int nvmlShutdown();
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetTemperature(IntPtr device, int sensor, out uint temp);
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out Utilization util);
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetPowerUsage(IntPtr device, out uint milliwatts);
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetClockInfo(IntPtr device, int type, out uint mhz);
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetMemoryInfo(IntPtr device, out Memory memory);
}
