namespace TelefonPaneli.Core.Metrics;

/// <summary>Metrik kimlikleri. Web tarafı (format.js) aynı dizeleri kullanır.</summary>
public static class MetricIds
{
    public const string CpuTemp = "cpu.temp";
    public const string CpuLoad = "cpu.load";
    public const string CpuClock = "cpu.clock";
    public const string GpuTemp = "gpu.temp";
    public const string GpuLoad = "gpu.load";
    public const string GpuPower = "gpu.power";
    public const string GpuClock = "gpu.clock";
    public const string GpuVramUsed = "gpu.vram.used";
    public const string GpuVramTotal = "gpu.vram.total";
    public const string RamUsed = "ram.used";
    public const string RamTotal = "ram.total";
    public const string Fps = "fps";
    public const string FpsLow1 = "fps.low1";
    public const string NetDown = "net.down";
    public const string NetUp = "net.up";
    public const string Ping = "ping";

    public const string FanPrefix = "fan.";
    public const string DiskPrefix = "disk.";

    public static string Fan(int index) => FanPrefix + index;
    public static string DiskTemp(int index) => $"{DiskPrefix}{index}.temp";

    /// <summary>Sürücü (PawnIO) gerektiren metrikler: FIFA modunda okunmaz.</summary>
    public static bool RequiresDriver(string id) =>
        id == CpuTemp || id == CpuClock ||
        id.StartsWith(FanPrefix, StringComparison.Ordinal) ||
        id.StartsWith(DiskPrefix, StringComparison.Ordinal);
}
