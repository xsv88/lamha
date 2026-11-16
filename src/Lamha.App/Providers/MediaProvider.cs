using Windows.Graphics.Imaging;
using Windows.Media.Control;

namespace Lamha.App.Providers;

/// <summary>What is playing right now, from any app that reports to Windows media controls.</summary>
public sealed record NowPlaying(
    string Title,
    string Artist,
    string SourceAppId,
    byte[]? ThumbnailBgra,
    int ThumbW,
    int ThumbH,
    TimeSpan? Position,
    TimeSpan? Duration,
    DateTimeOffset PositionUpdatedAt,
    bool IsPlaying);

/// <summary>
/// Reads the current media session (Spotify, browsers, ...) through the official
/// GlobalSystemMediaTransportControlsSessionManager. Active only while the panel is open.
/// Every failure ends in Changed(null): the card hides, the app keeps running.
/// </summary>
public sealed class MediaProvider
{
    private const int ThumbSize = 64;

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private byte[]? _thumb;
    private string? _thumbKey;

    /// <summary>Raised on a background thread. Null = nothing playing.</summary>
    public event Action<NowPlaying?>? Changed;

    public async Task StartAsync()
    {
        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += OnSessionChanged;
            Attach(_manager.GetCurrentSession());
            await PublishAsync(refreshThumbnail: true);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            Changed?.Invoke(null);
        }
    }

    public void Stop()
    {
        if (_manager is not null) _manager.CurrentSessionChanged -= OnSessionChanged;
        Attach(null);
    }

    public Task PlayPauseAsync() => Run(s => s.TryTogglePlayPauseAsync().AsTask());
    public Task NextAsync() => Run(s => s.TrySkipNextAsync().AsTask());
    public Task PreviousAsync() => Run(s => s.TrySkipPreviousAsync().AsTask());

    private async Task Run(Func<GlobalSystemMediaTransportControlsSession, Task<bool>> action)
    {
        try { if (_session is not null) await action(_session); }
        catch (Exception ex) { CrashLog.Write(ex); }
    }

    private void OnSessionChanged(GlobalSystemMediaTransportControlsSessionManager m, CurrentSessionChangedEventArgs e)
    {
        Attach(m.GetCurrentSession());
        _ = PublishAsync(refreshThumbnail: true);
    }

    private void Attach(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnMediaChanged;
            _session.PlaybackInfoChanged -= OnPlaybackChanged;
            _session.TimelinePropertiesChanged -= OnTimelineChanged;
        }
        _session = session;
        if (_session is not null)
        {
            _session.MediaPropertiesChanged += OnMediaChanged;
            _session.PlaybackInfoChanged += OnPlaybackChanged;
            _session.TimelinePropertiesChanged += OnTimelineChanged;
        }
    }

    private void OnMediaChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e) =>
        _ = PublishAsync(refreshThumbnail: true);

    private void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e) =>
        _ = PublishAsync(refreshThumbnail: false);

    private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs e) =>
        _ = PublishAsync(refreshThumbnail: false);

    private async Task PublishAsync(bool refreshThumbnail)
    {
        try
        {
            var session = _session;
            if (session is null) { Changed?.Invoke(null); return; }

            var props = await session.TryGetMediaPropertiesAsync();
            if (props is null || string.IsNullOrWhiteSpace(props.Title)) { Changed?.Invoke(null); return; }

            string key = props.Title + "|" + props.Artist;
            if (refreshThumbnail && key != _thumbKey)
            {
                _thumbKey = key;
                _thumb = await ReadThumbnailAsync(props.Thumbnail);
            }

            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            TimeSpan? duration = timeline.EndTime > TimeSpan.Zero ? timeline.EndTime - timeline.StartTime : null;
            TimeSpan? position = duration is null ? null : timeline.Position;

            Changed?.Invoke(new NowPlaying(
                props.Title, props.Artist ?? "", session.SourceAppUserModelId ?? "",
                _thumb, ThumbSize, ThumbSize, position, duration, timeline.LastUpdatedTime,
                playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing));
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex); // A player can close mid-read.
            Changed?.Invoke(null);
        }
    }

    private static async Task<byte[]?> ReadThumbnailAsync(Windows.Storage.Streams.IRandomAccessStreamReference? reference)
    {
        if (reference is null) return null;
        try
        {
            using var stream = await reference.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var transform = new BitmapTransform { ScaledWidth = ThumbSize, ScaledHeight = ThumbSize, InterpolationMode = BitmapInterpolationMode.Fant };
            var data = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
            return data.DetachPixelData();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            return null;
        }
    }
}
