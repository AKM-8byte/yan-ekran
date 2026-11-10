using System.Text.Json;
using TelefonPaneli.Core.Layout;
using TelefonPaneli.Core.Media;
using TelefonPaneli.Core.Metrics;

namespace TelefonPaneli.Core.Protocol;

/// <summary>Telefondan gelen mesaj. Alanların hepsi isteğe bağlı; geçersiz olanlar yok sayılır.</summary>
public sealed record ClientMessage(string Type, MediaAction? Action, double? Value, LayoutModel? Layout);

public static class Messages
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Snapshot(Snapshot s) => JsonSerializer.Serialize(new
    {
        type = "snapshot",
        mode = s.Mode,
        gpuAsleep = s.GpuAsleep,
        fifaWarning = s.FifaWarning,
        driverStopFailed = s.DriverStopFailed,
        metrics = s.Metrics.ToDictionary(kv => kv.Key, kv => kv.Value is { } v && double.IsFinite(v) ? Math.Round(v, 2) : (double?)null),
    }, Json);

    public static string Media(MediaState? state) => JsonSerializer.Serialize(new { type = "media", state }, Json);

    public static string Layout(LayoutModel layout) => JsonSerializer.Serialize(new { type = "layout", layout }, Json);

    /// <summary>Telefon mesajını ayrıştırır. Geçersizse null.</summary>
    public static ClientMessage? Parse(ReadOnlySpan<byte> utf8)
    {
        try
        {
            using var doc = JsonDocument.Parse(utf8.ToArray());
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String)
                return null;
            var type = t.GetString()!;
            switch (type)
            {
                case "media":
                    var action = root.TryGetProperty("action", out var a) && a.ValueKind == JsonValueKind.String
                        ? a.GetString() switch { "playPause" => MediaAction.PlayPause, "next" => MediaAction.Next, "prev" => MediaAction.Prev, _ => (MediaAction?)null }
                        : null;
                    return action is null ? null : new ClientMessage(type, action, null, null);
                case "volume":
                    return root.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number && double.IsFinite(v.GetDouble())
                        ? new ClientMessage(type, null, Math.Clamp(v.GetDouble(), 0, 1), null)
                        : null;
                case "saveLayout":
                    if (!root.TryGetProperty("layout", out var l) || l.ValueKind != JsonValueKind.Object) return null;
                    return new ClientMessage(type, null, null, l.Deserialize<LayoutModel>(Json));
                default:
                    return null;
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
