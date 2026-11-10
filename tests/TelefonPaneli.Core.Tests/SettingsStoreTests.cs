using TelefonPaneli.Core.Layout;
using TelefonPaneli.Core.Settings;

namespace TelefonPaneli.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("tp-").FullName;
    string Path => System.IO.Path.Combine(_dir, "settings.json");
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void Load_missing_file_returns_defaults()
    {
        var s = new SettingsStore(Path).Load();
        Assert.Equal(3, s.Layout.Pages.Count);
        Assert.Empty(s.Devices);
        Assert.Equal(["FIFA23.exe", "FIFA23_Trial.exe"], s.FifaProcessNames);
    }

    [Fact]
    public void Save_then_Load_roundtrips()
    {
        var store = new SettingsStore(Path);
        var s = store.Load();
        var layout = new LayoutModel(1, [new Page("p1", "Tek", [new Tile("t1", "metric", "gpu.temp", "L", 70, 85)])]);
        store.Save(s with { Layout = layout, Devices = [new DeviceToken("d1", "Telefon", "abc", DateTimeOffset.UnixEpoch)] });
        var back = new SettingsStore(Path).Load();
        Assert.Equal("Tek", back.Layout.Pages.Single().Name);
        Assert.Equal("gpu.temp", back.Layout.Pages[0].Tiles[0].Metric);
        Assert.Equal(85, back.Layout.Pages[0].Tiles[0].Crit);
        Assert.Equal("abc", back.Devices.Single().TokenHash);
    }

    [Fact]
    public void Save_uses_camelCase()
    {
        var store = new SettingsStore(Path);
        store.Save(store.Load());
        Assert.Contains("\"pages\"", File.ReadAllText(Path));
    }

    [Fact]
    public void Load_corrupt_json_returns_defaults_and_keeps_backup()
    {
        File.WriteAllText(Path, "{ bozuk");
        var s = new SettingsStore(Path).Load();
        Assert.Equal(3, s.Layout.Pages.Count);
        Assert.True(File.Exists(Path + ".bak"));
        Assert.Equal("{ bozuk", File.ReadAllText(Path + ".bak"));
    }

    [Fact]
    public void Load_sanitizes_layout_from_disk()
    {
        File.WriteAllText(Path, """{"layout":{"version":1,"pages":[{"id":"p","name":"A","tiles":[{"id":"t","kind":"metric","metric":"cpu.temp","size":"HUGE"}]}]}}""");
        var s = new SettingsStore(Path).Load();
        Assert.Equal("M", s.Layout.Pages[0].Tiles[0].Size);
        Assert.NotNull(s.Devices);
        Assert.NotEmpty(s.FifaProcessNames);
    }

    [Fact]
    public void Save_creates_missing_directory()
    {
        var nested = System.IO.Path.Combine(_dir, "a", "b", "settings.json");
        var store = new SettingsStore(nested);
        store.Save(store.Load());
        Assert.True(File.Exists(nested));
    }
}
