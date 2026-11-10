namespace TelefonPaneli.Core.Layout;

public sealed record LayoutModel(int Version, List<Page> Pages);

public sealed record Page(string Id, string Name, List<Tile> Tiles);

/// <summary>Kind: metric | chart | mediaStrip | mediaFull. Size: S (1×1) | M (2×1) | L (2×2).</summary>
public sealed record Tile(string Id, string Kind, string? Metric, string Size, double? Warn, double? Crit);

public static class TileKinds
{
    public const string Metric = "metric";
    public const string Chart = "chart";
    public const string MediaStrip = "mediaStrip";
    public const string MediaFull = "mediaFull";
    public static readonly IReadOnlySet<string> All = new HashSet<string> { Metric, Chart, MediaStrip, MediaFull };
    public static bool NeedsMetric(string kind) => kind is Metric or Chart;
}

public static class DefaultLayout
{
    public static LayoutModel Create()
    {
        Tile Temp(string id, string metric, string size) => new(id, TileKinds.Metric, metric, size, 70, 85);
        Tile M(string id, string metric, string size = "S") => new(id, TileKinds.Metric, metric, size, null, null);

        return new LayoutModel(1,
        [
            new Page("oyun", "Oyun",
            [
                Temp("t-cpu", "cpu.temp", "S"),
                Temp("t-gpu", "gpu.temp", "S"),
                M("t-fps", "fps", "L"),
                M("t-ram", "ram.used"),
                M("t-vram", "gpu.vram.used"),
                new Tile("t-strip", TileKinds.MediaStrip, null, "M", null, null),
            ]),
            new Page("muzik", "Müzik",
            [
                new Tile("t-media", TileKinds.MediaFull, null, "L", null, null),
            ]),
            new Page("detay", "Detay",
            [
                M("t-cpuclk", "cpu.clock"),
                M("t-gpupow", "gpu.power"),
                M("t-fan", "fan.0"),
                new Tile("t-disk", TileKinds.Metric, "disk.0.temp", "S", 60, 70),
                M("t-down", "net.down"),
                new Tile("t-ping", TileKinds.Metric, "ping", "S", 60, 120),
                new Tile("t-chart", TileKinds.Chart, "cpu.load", "M", null, null),
            ]),
        ]);
    }
}
