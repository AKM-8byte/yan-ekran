using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TelefonPaneli.Core.Layout;
using TelefonPaneli.Core.Media;
using TelefonPaneli.Core.Metrics;
using TelefonPaneli.Core.Protocol;
using TelefonPaneli.Core.Security;
using TelefonPaneli.Core.Settings;

namespace TelefonPaneli.App.Server;

public sealed record WebServerOptions(
    int Port,
    MetricHub Hub,
    SettingsStore Store,
    PairingService Pairing,
    IMediaController Media,
    TimeSpan Interval,
    bool EnforceLocalNetwork);

public sealed record PairRequest(string? Code, string? DeviceName);

public static class WebServer
{
    public const WebSocketCloseStatus Unpaired = (WebSocketCloseStatus)4401;

    public static WebApplication Build(WebServerOptions o, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
        });
        builder.WebHost.UseUrls($"http://0.0.0.0:{o.Port}");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        configure?.Invoke(builder);

        var app = builder.Build();
        var clients = new ClientRegistry();

        if (o.EnforceLocalNetwork)
        {
            app.Use(async (ctx, next) =>
            {
                var ip = ctx.Connection.RemoteIpAddress;
                if (ip is not null && !NetworkGuard.IsLocal(ip))
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                await next();
            });
        }

        // DNS rebinding koruması: yalnızca IP adresi veya localhost ile gelen istekler.
        app.Use(async (ctx, next) =>
        {
            if (!IsAllowedHost(ctx.Request.Host.Host))
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            await next();
        });

        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) });
        app.UseDefaultFiles();
        if (Directory.Exists(app.Environment.WebRootPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = "no-cache",
            });
        }

        app.MapPost("/api/pair", (PairRequest req) =>
        {
            var r = o.Pairing.TryPair(req.Code, req.DeviceName);
            return r.Status switch
            {
                PairStatus.Ok => Results.Ok(new { token = r.Token }),
                PairStatus.Locked => Results.StatusCode(StatusCodes.Status429TooManyRequests),
                PairStatus.Closed => Results.StatusCode(StatusCodes.Status403Forbidden),
                _ => Results.Unauthorized(),
            };
        });

        app.MapGet("/art", async (HttpContext ctx) =>
        {
            if (!o.Pairing.IsValid(ctx.Request.Query["token"])) return Results.Unauthorized();
            var bytes = await o.Media.GetArtAsync();
            if (bytes is null || bytes.Length == 0) return Results.NotFound();
            ctx.Response.Headers.CacheControl = "private, max-age=3600";
            return Results.File(bytes, SniffImageType(bytes));
        });

        app.Map("/ws", async (HttpContext ctx) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
            using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            if (!o.Pairing.IsValid(ctx.Request.Query["token"]))
            {
                // El sıkışmada 401 dönülürse tarayıcı bunu 1006 olarak görür ve sonsuza dek yeniden dener.
                // Kabul edip 4401 ile kapatmak telefonu eşleştirme ekranına döndürür.
                await ws.CloseOutputAsync(Unpaired, "eşleşme yok", ctx.RequestAborted);
                return;
            }
            var client = new Client(ws);
            clients.Add(client);
            try
            {
                client.Enqueue(Messages.Layout(o.Store.Load().Layout));
                client.Enqueue(Messages.Media(o.Media.Current));
                var sending = client.RunSenderAsync(ctx.RequestAborted);
                await ReceiveLoopAsync(client, o, clients, ctx.RequestAborted);
                client.Complete();
                await sending;
            }
            finally
            {
                clients.Remove(client);
            }
        });

        // Yayın döngüsü ve olay bağlantıları
        var lifetime = app.Lifetime;
        void OnMediaChanged() => clients.Broadcast(Messages.Media(o.Media.Current));
        void OnForgotten() => clients.CloseAll(Unpaired, "eşleşme silindi");

        lifetime.ApplicationStarted.Register(() =>
        {
            o.Media.Changed += OnMediaChanged;
            o.Pairing.DevicesForgotten += OnForgotten;
            _ = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(o.Interval);
                try
                {
                    while (await timer.WaitForNextTickAsync(lifetime.ApplicationStopping))
                    {
                        if (clients.Count == 0) continue;
                        string json;
                        try { json = Messages.Snapshot(o.Hub.Collect()); }
                        catch (Exception ex)
                        {
                            app.Logger.LogWarning(ex, "Anlık görüntü oluşturulamadı");
                            continue;
                        }
                        clients.BroadcastSnapshot(json);
                    }
                }
                catch (OperationCanceledException) { }
            });
        });
        lifetime.ApplicationStopping.Register(() =>
        {
            o.Media.Changed -= OnMediaChanged;
            o.Pairing.DevicesForgotten -= OnForgotten;
            clients.CloseAll(WebSocketCloseStatus.EndpointUnavailable, "kapanıyor");
        });

        return app;
    }

    static async Task ReceiveLoopAsync(Client client, WebServerOptions o, ClientRegistry clients, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        while (client.Socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult r;
            try { r = await client.Socket.ReceiveAsync(buffer, ct); }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException) { return; }

            if (r.MessageType == WebSocketMessageType.Close) return;
            if (message.Length + r.Count > 256 * 1024) { message.SetLength(0); continue; } // aşırı büyük mesajı at
            message.Write(buffer, 0, r.Count);
            if (!r.EndOfMessage) continue;

            var msg = Messages.Parse(message.ToArray());
            message.SetLength(0);
            if (msg is null) continue;

            try
            {
                switch (msg.Type)
                {
                    case "media":
                        await o.Media.SendAsync(msg.Action!.Value);
                        break;
                    case "volume":
                        await o.Media.SetVolumeAsync(msg.Value!.Value);
                        break;
                    case "saveLayout":
                        var clean = LayoutValidator.Sanitize(msg.Layout);
                        o.Store.Update(s => s with { Layout = clean });
                        clients.Broadcast(Messages.Layout(clean));
                        break;
                }
            }
            catch (Exception)
            {
                // Medya uygulaması kapanmış olabilir; telefon bağlantısını düşürmeyiz.
            }
        }
    }

    internal static bool IsAllowedHost(string host)
    {
        if (string.IsNullOrEmpty(host)) return false;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return System.Net.IPAddress.TryParse(host.Trim('[', ']'), out _);
    }

    static string SniffImageType(byte[] b) =>
        b.Length > 3 && b[0] == 0x89 && b[1] == 0x50 ? "image/png" :
        b.Length > 2 && b[0] == 0xFF && b[1] == 0xD8 ? "image/jpeg" :
        b.Length > 11 && b[8] == (byte)'W' && b[9] == (byte)'E' ? "image/webp" :
        b.Length > 3 && b[0] == (byte)'G' && b[1] == (byte)'I' ? "image/gif" :
        b.Length > 1 && b[0] == (byte)'B' && b[1] == (byte)'M' ? "image/bmp" :
        "application/octet-stream";

    sealed class ClientRegistry
    {
        readonly ConcurrentDictionary<Client, byte> _all = new();
        public int Count => _all.Count;
        public void Add(Client c) => _all[c] = 0;
        public void Remove(Client c) => _all.TryRemove(c, out _);
        public void Broadcast(string json) { foreach (var c in _all.Keys) c.Enqueue(json); }
        public void BroadcastSnapshot(string json) { foreach (var c in _all.Keys) c.SetSnapshot(json); }
        public void CloseAll(WebSocketCloseStatus status, string reason) { foreach (var c in _all.Keys) c.RequestClose(status, reason); }
    }

    /// <summary>
    /// İstemci başına tek gönderici. Kontrol mesajları (layout, media) sırayla gider;
    /// anlık görüntüde yalnızca en sonuncusu tutulur, böylece yavaş bir telefon diğerlerini bekletmez.
    /// </summary>
    sealed class Client(WebSocket socket)
    {
        public WebSocket Socket => socket;
        readonly ConcurrentQueue<string> _control = new();
        readonly SemaphoreSlim _signal = new(0);
        string? _snapshot;
        (WebSocketCloseStatus Status, string Reason)? _close;
        volatile bool _done;

        public void Enqueue(string json)
        {
            if (_control.Count > 64) _control.TryDequeue(out _);
            _control.Enqueue(json);
            Signal();
        }

        public void SetSnapshot(string json) { Volatile.Write(ref _snapshot, json); Signal(); }

        public void RequestClose(WebSocketCloseStatus status, string reason) { _close = (status, reason); Signal(); }

        public void Complete() { _done = true; Signal(); }

        void Signal() { if (_signal.CurrentCount == 0) _signal.Release(); }

        public async Task RunSenderAsync(CancellationToken ct)
        {
            try
            {
                while (!_done && socket.State == WebSocketState.Open)
                {
                    await _signal.WaitAsync(ct);
                    if (_close is { } c)
                    {
                        await socket.CloseOutputAsync(c.Status, c.Reason, ct);
                        return;
                    }
                    while (_control.TryDequeue(out var m)) await SendAsync(m, ct);
                    if (Interlocked.Exchange(ref _snapshot, null) is { } s) await SendAsync(s, ct);
                }
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException) { }
        }

        Task SendAsync(string json, CancellationToken ct) =>
            socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
    }
}
