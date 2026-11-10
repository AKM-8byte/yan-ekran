using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using TelefonPaneli.App.Server;
using TelefonPaneli.Core.Media;
using TelefonPaneli.Core.Metrics;
using TelefonPaneli.Core.Security;
using TelefonPaneli.Core.Settings;

namespace TelefonPaneli.App.Tests;

public sealed class FakeMedia : IMediaController
{
    public MediaState? Current { get; set; }
    public event Action? Changed;
    public byte[]? Art { get; set; }
    public List<MediaAction> Actions { get; } = [];
    public List<double> Volumes { get; } = [];
    public Task<byte[]?> GetArtAsync() => Task.FromResult(Art);
    public Task SendAsync(MediaAction a) { lock (Actions) Actions.Add(a); return Task.CompletedTask; }
    public Task SetVolumeAsync(double v) { lock (Volumes) Volumes.Add(v); return Task.CompletedTask; }
    public void RaiseChanged() => Changed?.Invoke();
}

sealed class FixedSource(Dictionary<string, double?> values) : IMetricSource
{
    public string Name => "fixed";
    public void Collect(IDictionary<string, double?> into) { foreach (var (k, v) in values) into[k] = v; }
}

public sealed class WebServerTests : IAsyncLifetime
{
    readonly string _dir = Directory.CreateTempSubdirectory("tp-web-").FullName;
    string SettingsPath => Path.Combine(_dir, "settings.json");
    readonly List<WebApplication> _apps = [];

    public FakeMedia Media { get; } = new();
    PairingService _pairing = null!;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var a in _apps) await a.DisposeAsync();
        Directory.Delete(_dir, true);
    }

    async Task<TestServer> StartAsync(bool enforceLocal = true, Dictionary<string, double?>? metrics = null)
    {
        var store = new SettingsStore(SettingsPath);
        var q = new Queue<int>([1234, 5678, 9012]);
        _pairing = new PairingService(store, () => q.Count > 1 ? q.Dequeue() : q.Peek());
        var hub = new MetricHub(
            [new FixedSource(metrics ?? new() { ["cpu.load"] = 34, ["fan.0"] = null, ["gpu.temp"] = double.NaN })],
            () => new HubFlags("normal", false, false));
        var app = WebServer.Build(
            new WebServerOptions(0, hub, store, _pairing, Media, TimeSpan.FromMilliseconds(50), enforceLocal),
            b => b.WebHost.UseTestServer());
        _apps.Add(app);
        await app.StartAsync();
        return app.GetTestServer();
    }

    async Task<string> PairAsync(TestServer server)
    {
        var res = await server.CreateClient().PostAsJsonAsync("/api/pair", new { code = _pairing.CurrentCode, deviceName = "Test" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    static Task<WebSocket> ConnectAsync(TestServer server, string? token) =>
        server.CreateWebSocketClient().ConnectAsync(new Uri(server.BaseAddress, "ws" + (token is null ? "" : "?token=" + token)), CancellationToken.None);

    static async Task<JsonElement> ReceiveAsync(WebSocket ws, int timeoutMs = 3000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        var buf = new byte[64 * 1024];
        using var ms = new MemoryStream();
        WebSocketReceiveResult r;
        do
        {
            r = await ws.ReceiveAsync(buf, cts.Token);
            if (r.MessageType == WebSocketMessageType.Close) throw new WebSocketException("closed:" + (int?)r.CloseStatus);
            ms.Write(buf, 0, r.Count);
        } while (!r.EndOfMessage);
        return JsonDocument.Parse(ms.ToArray()).RootElement.Clone();
    }

    static async Task<JsonElement> ReceiveTypeAsync(WebSocket ws, string type)
    {
        for (var i = 0; i < 50; i++)
        {
            var m = await ReceiveAsync(ws);
            if (m.GetProperty("type").GetString() == type) return m;
        }
        throw new TimeoutException(type);
    }

    static Task SendAsync(WebSocket ws, string json) =>
        ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);

    static async Task WaitUntil(Func<bool> cond)
    {
        for (var i = 0; i < 100 && !cond(); i++) await Task.Delay(20);
        Assert.True(cond());
    }

    [Fact]
    public async Task Pair_with_wrong_code_is_401()
    {
        var server = await StartAsync();
        var res = await server.CreateClient().PostAsJsonAsync("/api/pair", new { code = "0000", deviceName = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Pair_locked_is_429()
    {
        var server = await StartAsync();
        var c = server.CreateClient();
        HttpResponseMessage res = null!;
        for (var i = 0; i < 5; i++) res = await c.PostAsJsonAsync("/api/pair", new { code = "0000", deviceName = "x" });
        Assert.Equal((HttpStatusCode)429, res.StatusCode);
    }

    [Fact]
    public async Task Ws_without_token_is_401()
    {
        var server = await StartAsync();
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(server, null));
        Assert.Contains("401", ex.Message);
        ex = await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(server, "yanlis"));
        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task Ws_receives_layout_then_snapshots()
    {
        var server = await StartAsync();
        var ws = await ConnectAsync(server, await PairAsync(server));
        var first = await ReceiveAsync(ws);
        Assert.Equal("layout", first.GetProperty("type").GetString());
        Assert.Equal(3, first.GetProperty("layout").GetProperty("pages").GetArrayLength());
        var snap = await ReceiveTypeAsync(ws, "snapshot");
        Assert.Equal(34, snap.GetProperty("metrics").GetProperty("cpu.load").GetDouble());
        Assert.Equal("normal", snap.GetProperty("mode").GetString());
        Assert.False(snap.GetProperty("gpuAsleep").GetBoolean());
        Assert.False(snap.GetProperty("fifaWarning").GetBoolean());
    }

    [Fact]
    public async Task Missing_metric_is_serialized_as_null()
    {
        var server = await StartAsync();
        var ws = await ConnectAsync(server, await PairAsync(server));
        var m = (await ReceiveTypeAsync(ws, "snapshot")).GetProperty("metrics");
        Assert.Equal(JsonValueKind.Null, m.GetProperty("fan.0").ValueKind);
        Assert.Equal(JsonValueKind.Null, m.GetProperty("gpu.temp").ValueKind); // NaN → null
    }

    [Fact]
    public async Task Media_null_is_sent_as_state_null_and_commands_do_not_throw()
    {
        var server = await StartAsync();
        var ws = await ConnectAsync(server, await PairAsync(server));
        var media = await ReceiveTypeAsync(ws, "media");
        Assert.Equal(JsonValueKind.Null, media.GetProperty("state").ValueKind);
        await SendAsync(ws, """{"type":"media","action":"next"}""");
        await ReceiveTypeAsync(ws, "snapshot"); // bağlantı hâlâ açık
    }

    [Fact]
    public async Task Media_change_is_pushed()
    {
        var server = await StartAsync();
        var ws = await ConnectAsync(server, await PairAsync(server));
        await ReceiveTypeAsync(ws, "media");
        Media.Current = new MediaState("Spotify", "Blinding Lights", "The Weeknd", true, 94000, 200000, 3, 0.6);
        Media.RaiseChanged();
        var m = await ReceiveTypeAsync(ws, "media");
        var s = m.GetProperty("state");
        Assert.Equal("Blinding Lights", s.GetProperty("title").GetString());
        Assert.Equal(3, s.GetProperty("artVersion").GetInt32());
        Assert.Equal(0.6, s.GetProperty("volume").GetDouble());
    }

    [Fact]
    public async Task Media_action_reaches_controller()
    {
        var server = await StartAsync();
        var ws = await ConnectAsync(server, await PairAsync(server));
        await SendAsync(ws, """{"type":"media","action":"playPause"}""");
        await SendAsync(ws, """{"type":"media","action":"prev"}""");
        await WaitUntil(() => { lock (Media.Actions) return Media.Actions.Count == 2; });
        Assert.Equal([MediaAction.PlayPause, MediaAction.Prev], Media.Actions);
    }

    [Fact]
    public async Task Volume_is_clamped()
    {
        var server = await StartAsync();
        var ws = await ConnectAsync(server, await PairAsync(server));
        await SendAsync(ws, """{"type":"volume","value":1.7}""");
        await SendAsync(ws, """{"type":"volume","value":-2}""");
        await WaitUntil(() => { lock (Media.Volumes) return Media.Volumes.Count == 2; });
        Assert.Equal([1.0, 0.0], Media.Volumes);
    }

    [Fact]
    public async Task SaveLayout_is_sanitized_persisted_and_broadcast_to_other_clients()
    {
        var server = await StartAsync();
        var token = await PairAsync(server);
        var a = await ConnectAsync(server, token);
        var b = await ConnectAsync(server, token);
        await ReceiveTypeAsync(b, "layout");
        await SendAsync(a, """{"type":"saveLayout","layout":{"version":1,"pages":[{"id":"p","name":"Tek","tiles":[{"id":"t","kind":"metric","metric":"gpu.temp","size":"XXL"},{"id":"z","kind":"evil"}]}]}}""");
        var l = (await ReceiveTypeAsync(b, "layout")).GetProperty("layout");
        Assert.Equal("Tek", l.GetProperty("pages")[0].GetProperty("name").GetString());
        var tiles = l.GetProperty("pages")[0].GetProperty("tiles");
        Assert.Equal(1, tiles.GetArrayLength());
        Assert.Equal("M", tiles[0].GetProperty("size").GetString());
        Assert.Equal("Tek", new SettingsStore(SettingsPath).Load().Layout.Pages.Single().Name);
    }

    [Fact]
    public async Task Garbage_message_keeps_socket_open()
    {
        var server = await StartAsync();
        var ws = await ConnectAsync(server, await PairAsync(server));
        await SendAsync(ws, "{ bu json değil");
        await SendAsync(ws, """{"type":"bilinmeyen"}""");
        await SendAsync(ws, """{"type":"saveLayout","layout":"metin"}""");
        await SendAsync(ws, """{"type":"media","action":"patlat"}""");
        await ReceiveTypeAsync(ws, "snapshot");
        await ReceiveTypeAsync(ws, "snapshot");
        Assert.Equal(WebSocketState.Open, ws.State);
        Assert.Empty(Media.Actions);
    }

    [Fact]
    public async Task Token_still_valid_after_host_restart()
    {
        var first = await StartAsync();
        var token = await PairAsync(first);
        await _apps[0].StopAsync();
        var second = await StartAsync();
        var ws = await ConnectAsync(second, token);
        Assert.Equal("layout", (await ReceiveAsync(ws)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task ForgetAll_closes_open_socket_with_4401()
    {
        var server = await StartAsync();
        var ws = await ConnectAsync(server, await PairAsync(server));
        await ReceiveAsync(ws);
        _pairing.ForgetAll();
        var ex = await Assert.ThrowsAsync<WebSocketException>(async () => { while (true) await ReceiveAsync(ws); });
        Assert.Equal("closed:4401", ex.Message);
    }

    [Fact]
    public async Task Non_local_ip_is_403_when_enforced()
    {
        var server = await StartAsync();
        var ctx = await server.SendAsync(c =>
        {
            c.Request.Path = "/index.html";
            c.Connection.RemoteIpAddress = IPAddress.Parse("8.8.8.8");
        });
        Assert.Equal(403, ctx.Response.StatusCode);
        var ok = await server.SendAsync(c =>
        {
            c.Request.Method = "POST";
            c.Request.Path = "/api/pair";
            c.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.30");
        });
        Assert.NotEqual(403, ok.Response.StatusCode);
    }

    [Fact]
    public async Task Art_requires_token_and_returns_404_when_no_media()
    {
        var server = await StartAsync();
        var c = server.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/art")).StatusCode);
        var token = await PairAsync(server);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/art?token=" + token)).StatusCode);
        Media.Art = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2];
        var res = await c.GetAsync("/art?token=" + token + "&v=1");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("image/png", res.Content.Headers.ContentType!.MediaType);
    }
}
