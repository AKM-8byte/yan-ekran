using TelefonPaneli.Core.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace TelefonPaneli.Windows.Media;

/// <summary>
/// Windows'un medya oturumlarını (System Media Transport Controls) izler. Spotify uygulaması ve
/// Chrome/Edge'deki YouTube Music buraya kayıt olur.
/// </summary>
public sealed class SmtcMediaSource : IMediaController, IDisposable
{
    readonly AppVolume _volume;
    readonly object _gate = new();
    GlobalSystemMediaTransportControlsSessionManager? _manager;
    GlobalSystemMediaTransportControlsSession? _session;
    string _trackKey = "";
    int _artVersion;
    byte[]? _art;

    SmtcMediaSource(AppVolume volume) => _volume = volume;

    public static async Task<SmtcMediaSource> CreateAsync(AppVolume volume)
    {
        var src = new SmtcMediaSource(volume);
        try
        {
            src._manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            src._manager.CurrentSessionChanged += (_, _) => src.Rebind();
            src._manager.SessionsChanged += (_, _) => src.Rebind();
            src.Rebind();
        }
        catch (Exception) { /* SMTC yoksa medya her zaman boş görünür */ }
        return src;
    }

    public event Action? Changed;

    public MediaState? Current
    {
        get
        {
            GlobalSystemMediaTransportControlsSession? s;
            lock (_gate) s = _session;
            if (s is null) return null;
            try
            {
                var props = s.TryGetMediaPropertiesAsync().AsTask().GetAwaiter().GetResult();
                if (props is null || (string.IsNullOrWhiteSpace(props.Title) && string.IsNullOrWhiteSpace(props.Artist))) return null;
                var playback = s.GetPlaybackInfo();
                var timeline = s.GetTimelineProperties();
                var playing = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                var pos = timeline.Position;
                if (playing && timeline.LastUpdatedTime.Year > 2000)
                    pos += DateTimeOffset.Now - timeline.LastUpdatedTime;
                var duration = timeline.EndTime - timeline.StartTime;
                if (duration > TimeSpan.Zero && pos > duration) pos = duration;
                if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;

                return new MediaState(
                    MediaApps.AppName(s.SourceAppUserModelId),
                    props.Title ?? "",
                    string.IsNullOrWhiteSpace(props.Artist) ? props.AlbumArtist ?? "" : props.Artist,
                    playing,
                    (long)pos.TotalMilliseconds,
                    (long)Math.Max(0, duration.TotalMilliseconds),
                    _artVersion,
                    _volume.Get(s.SourceAppUserModelId));
            }
            catch (Exception) { return null; }
        }
    }

    public Task<byte[]?> GetArtAsync() => Task.FromResult(Volatile.Read(ref _art));

    public async Task SendAsync(MediaAction action)
    {
        GlobalSystemMediaTransportControlsSession? s;
        lock (_gate) s = _session;
        if (s is null) return;
        try
        {
            _ = action switch
            {
                MediaAction.PlayPause => await s.TryTogglePlayPauseAsync(),
                MediaAction.Next => await s.TrySkipNextAsync(),
                MediaAction.Prev => await s.TrySkipPreviousAsync(),
                _ => false,
            };
        }
        catch (Exception) { }
    }

    public Task SetVolumeAsync(double value)
    {
        GlobalSystemMediaTransportControlsSession? s;
        lock (_gate) s = _session;
        if (s is not null) _volume.Set(s.SourceAppUserModelId, value);
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    void Rebind()
    {
        if (_manager is null) return;
        GlobalSystemMediaTransportControlsSession? next;
        try { next = PickSession(_manager); }
        catch (Exception) { next = null; }

        lock (_gate)
        {
            if (!ReferenceEquals(next, _session))
            {
                if (_session is not null)
                {
                    _session.MediaPropertiesChanged -= OnProps;
                    _session.PlaybackInfoChanged -= OnOther;
                    _session.TimelinePropertiesChanged -= OnOther;
                }
                _session = next;
                if (_session is not null)
                {
                    _session.MediaPropertiesChanged += OnProps;
                    _session.PlaybackInfoChanged += OnOther;
                    _session.TimelinePropertiesChanged += OnOther;
                }
            }
        }
        _ = RefreshArtAsync();
        Changed?.Invoke();
    }

    static GlobalSystemMediaTransportControlsSession? PickSession(GlobalSystemMediaTransportControlsSessionManager m)
    {
        var current = m.GetCurrentSession();
        if (current is not null) return current;
        var all = m.GetSessions();
        return all.FirstOrDefault(s => s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
               ?? all.FirstOrDefault();
    }

    void OnProps(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e)
    {
        _ = RefreshArtAsync();
        Changed?.Invoke();
    }

    void OnOther(GlobalSystemMediaTransportControlsSession s, object e) => Changed?.Invoke();

    async Task RefreshArtAsync()
    {
        GlobalSystemMediaTransportControlsSession? s;
        lock (_gate) s = _session;
        if (s is null) { SetArt("", null); return; }
        try
        {
            var props = await s.TryGetMediaPropertiesAsync();
            // Chrome/Spotify önce başlığı, sonra ayrı bir olayla küçük resmi gönderir: anahtar küçük resmin varlığını da içerir.
            var key = $"{s.SourceAppUserModelId}|{props?.Title}|{props?.Artist}|{props?.Thumbnail is not null}";
            if (key == _trackKey) return;
            byte[]? bytes = null;
            if (props?.Thumbnail is { } thumb)
            {
                using var stream = await thumb.OpenReadAsync();
                if (stream.Size is > 0 and < 8 * 1024 * 1024)
                {
                    var buffer = new global::Windows.Storage.Streams.Buffer((uint)stream.Size);
                    var read = await stream.ReadAsync(buffer, (uint)stream.Size, InputStreamOptions.None);
                    using var reader = DataReader.FromBuffer(read);
                    bytes = new byte[read.Length];
                    reader.ReadBytes(bytes);
                }
            }
            SetArt(key, bytes);
            Changed?.Invoke();
        }
        catch (Exception) { }
    }

    void SetArt(string key, byte[]? bytes)
    {
        _trackKey = key;
        Volatile.Write(ref _art, bytes);
        Interlocked.Increment(ref _artVersion);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_session is not null)
            {
                _session.MediaPropertiesChanged -= OnProps;
                _session.PlaybackInfoChanged -= OnOther;
                _session.TimelinePropertiesChanged -= OnOther;
            }
            _session = null;
        }
        _volume.Dispose();
    }
}
