namespace TelefonPaneli.Core.Layout;

public static class LayoutValidator
{
    public const int MaxPages = 10;
    public const int MaxTilesPerPage = 24;
    static readonly HashSet<string> Sizes = ["S", "M", "L"];

    /// <summary>Telefondan veya diskten gelen düzeni güvenli hale getirir. null → varsayılan düzen.</summary>
    public static LayoutModel Sanitize(LayoutModel? input)
    {
        if (input is null) return DefaultLayout.Create();

        var tileIds = new HashSet<string>(StringComparer.Ordinal);
        var pageIds = new HashSet<string>(StringComparer.Ordinal);
        var pages = new List<Page>();

        foreach (var page in (input.Pages ?? []).Where(p => p is not null).Take(MaxPages))
        {
            var tiles = new List<Tile>();
            foreach (var t in (page.Tiles ?? []).Where(t => t is not null))
            {
                if (tiles.Count == MaxTilesPerPage) break;
                if (t.Kind is null || !TileKinds.All.Contains(t.Kind)) continue;
                if (TileKinds.NeedsMetric(t.Kind) && string.IsNullOrWhiteSpace(t.Metric)) continue;
                var id = Unique(t.Id, tileIds, "t");
                var size = t.Size is not null && Sizes.Contains(t.Size) ? t.Size : "M";
                var metric = TileKinds.NeedsMetric(t.Kind) ? t.Metric!.Trim() : null;
                tiles.Add(new Tile(id, t.Kind, metric, size, Finite(t.Warn), Finite(t.Crit)));
            }
            var name = string.IsNullOrWhiteSpace(page.Name) ? $"Sayfa {pages.Count + 1}" : page.Name.Trim();
            if (name.Length > 30) name = name[..30];
            pages.Add(new Page(Unique(page.Id, pageIds, "p"), name, tiles));
        }

        if (pages.Count == 0) pages.Add(new Page(Unique(null, pageIds, "p"), "Sayfa 1", []));
        return new LayoutModel(1, pages);
    }

    static double? Finite(double? v) => v is { } d && double.IsFinite(d) ? d : null;

    static string Unique(string? id, HashSet<string> used, string prefix)
    {
        if (!string.IsNullOrWhiteSpace(id) && id.Length <= 64 && used.Add(id)) return id;
        string fresh;
        do fresh = prefix + "-" + Guid.NewGuid().ToString("N")[..8];
        while (!used.Add(fresh));
        return fresh;
    }
}
