namespace TelefonPaneli.Core.Media;

public sealed record MediaState(string App, string Title, string Artist, bool IsPlaying, long PositionMs, long DurationMs, int ArtVersion, double? Volume);

public enum MediaAction { PlayPause, Next, Prev }

/// <summary>Şu an çalan medya ve kontrolü. Windows tarafında SMTC ile uygulanır.</summary>
public interface IMediaController
{
    MediaState? Current { get; }
    event Action? Changed;
    Task<byte[]?> GetArtAsync();
    Task SendAsync(MediaAction action);
    Task SetVolumeAsync(double value);
}
