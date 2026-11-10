using TelefonPaneli.Core.Layout;

namespace TelefonPaneli.Core.Tests;

public class LayoutTests
{
    static Tile T(string id, string kind = "metric", string? metric = "cpu.temp", string size = "S") => new(id, kind, metric, size, null, null);
    static LayoutModel One(params Tile[] tiles) => new(1, [new Page("p1", "Oyun", tiles.ToList())]);

    [Fact]
    public void Sanitize_null_returns_default() => Assert.Equal(3, LayoutValidator.Sanitize(null).Pages.Count);

    [Fact]
    public void Default_layout_matches_spec_pages()
    {
        var d = DefaultLayout.Create();
        Assert.Equal(["Oyun", "Müzik", "Detay"], d.Pages.Select(p => p.Name));
        Assert.Contains(d.Pages[0].Tiles, t => t.Metric == "fps" && t.Size == "L");
        Assert.Contains(d.Pages[0].Tiles, t => t.Kind == "mediaStrip");
        Assert.Equal("mediaFull", Assert.Single(d.Pages[1].Tiles).Kind);
        var cpu = d.Pages[0].Tiles.First(t => t.Metric == "cpu.temp");
        Assert.Equal((70d, 85d), (cpu.Warn!.Value, cpu.Crit!.Value));
        var ping = d.Pages[2].Tiles.First(t => t.Metric == "ping");
        Assert.Equal((60d, 120d), (ping.Warn!.Value, ping.Crit!.Value));
        Assert.Contains(d.Pages[2].Tiles, t => t.Kind == "chart" && t.Metric == "cpu.load" && t.Size == "M");
    }

    [Fact]
    public void Sanitize_drops_tiles_with_unknown_kind()
    {
        var r = LayoutValidator.Sanitize(One(T("a"), T("b", kind: "hack")));
        Assert.Equal(["a"], r.Pages[0].Tiles.Select(t => t.Id));
    }

    [Fact]
    public void Sanitize_metric_tile_without_metric_is_dropped()
    {
        var r = LayoutValidator.Sanitize(One(T("a", metric: null), T("b", kind: "chart", metric: ""), T("c", kind: "mediaStrip", metric: null)));
        Assert.Equal(["c"], r.Pages[0].Tiles.Select(t => t.Id));
    }

    [Fact]
    public void Sanitize_invalid_size_becomes_M() =>
        Assert.Equal("M", LayoutValidator.Sanitize(One(T("a", size: "XXL"))).Pages[0].Tiles[0].Size);

    [Fact]
    public void Sanitize_zero_pages_gets_one_empty_page()
    {
        var r = LayoutValidator.Sanitize(new LayoutModel(1, []));
        var p = Assert.Single(r.Pages);
        Assert.Equal("Sayfa 1", p.Name);
        Assert.Empty(p.Tiles);
    }

    [Fact]
    public void Sanitize_duplicate_tile_ids_are_regenerated()
    {
        var r = LayoutValidator.Sanitize(new LayoutModel(1, [new Page("p1", "A", [T("x")]), new Page("p1", "B", [T("x"), T("")])]));
        var ids = r.Pages.SelectMany(p => p.Tiles).Select(t => t.Id).ToList();
        Assert.Equal(3, ids.Distinct().Count());
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(2, r.Pages.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void Sanitize_caps_at_10_pages_and_24_tiles_per_page()
    {
        var pages = Enumerable.Range(0, 15).Select(i => new Page("p" + i, "S" + i, Enumerable.Range(0, 40).Select(j => T($"t{i}-{j}")).ToList())).ToList();
        var r = LayoutValidator.Sanitize(new LayoutModel(1, pages));
        Assert.Equal(10, r.Pages.Count);
        Assert.All(r.Pages, p => Assert.Equal(24, p.Tiles.Count));
    }

    [Fact]
    public void Sanitize_null_lists_and_names_are_repaired()
    {
        var r = LayoutValidator.Sanitize(new LayoutModel(1, [new Page(null!, null!, null!)]));
        Assert.Equal("Sayfa 1", r.Pages[0].Name);
        Assert.Empty(r.Pages[0].Tiles);
    }
}
