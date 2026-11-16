using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Lamha.App.Providers;
using Lamha.Core.Media;

namespace Lamha.App.Panel.Sections;

/// <summary>
/// "Now playing" card. Its background takes the album cover's main color,
/// darkened until white text is readable (WCAG 4.5:1).
/// </summary>
public partial class NowPlayingSection : UserControl
{
    private readonly DispatcherTimer _progressTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<Storyboard> _bars = [];
    private NowPlaying? _current;

    public NowPlayingSection()
    {
        InitializeComponent();
        BuildEqualizer();
        _progressTimer.Tick += (_, _) => UpdateProgress();
        PrevButton.Click += (_, _) => Previous?.Invoke();
        PlayButton.Click += (_, _) => PlayPause?.Invoke();
        NextButton.Click += (_, _) => Next?.Invoke();
        Card.MouseLeftButtonUp += OnCardClick;
    }

    public event Action? Previous;
    public event Action? PlayPause;
    public event Action? Next;

    /// <summary>Call on the UI thread. Null hides the card.</summary>
    public void Show(NowPlaying? np)
    {
        _current = np;
        if (np is null)
        {
            Visibility = Visibility.Collapsed;
            _progressTimer.Stop();
            SetBarsRunning(false);
            return;
        }

        Visibility = Visibility.Visible;
        TitleText.Text = np.Title;
        SubText.Text = string.IsNullOrEmpty(np.Artist) ? AppName(np.SourceAppId) : $"{np.Artist} · {AppName(np.SourceAppId)}";
        PlayButton.Content = np.IsPlaying ? "" : ""; // Pause / Play glyph.

        var color = AlbumColor.Neutral;
        if (np.ThumbnailBgra is { } px)
        {
            Cover.Source = BitmapSource.Create(np.ThumbW, np.ThumbH, 96, 96, PixelFormats.Pbgra32, null, px, np.ThumbW * 4);
            color = AlbumColor.Dominant(px, np.ThumbW, np.ThumbH);
        }
        else
        {
            Cover.Source = null;
        }
        color = AlbumColor.EnsureContrastWithWhite(color);
        Card.Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));

        bool hasTimeline = np.Duration is { } d && d > TimeSpan.Zero;
        ProgressArea.Visibility = hasTimeline ? Visibility.Visible : Visibility.Collapsed; // Live streams have none.
        UpdateProgress();
        if (hasTimeline && np.IsPlaying) _progressTimer.Start(); else _progressTimer.Stop();
        SetBarsRunning(np.IsPlaying);
    }

    private void UpdateProgress()
    {
        if (_current is not { Duration: { } total, Position: { } pos } np || total <= TimeSpan.Zero) return;
        if (np.IsPlaying) pos += DateTimeOffset.Now - np.PositionUpdatedAt; // Windows reports position only now and then.
        if (pos > total) pos = total;
        if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;
        Progress.Value = pos.TotalSeconds / total.TotalSeconds;
        ElapsedText.Text = Format(pos);
        TotalText.Text = Format(total);
    }

    private void OnCardClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && FindParent<Button>(d) is not null) return; // Buttons handle themselves.
        if (_current is null || string.IsNullOrEmpty(_current.SourceAppId)) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{_current.SourceAppId}") { UseShellExecute = true });
        }
        catch (Exception ex) { CrashLog.Write(ex); }
    }

    private void BuildEqualizer()
    {
        double[] delays = [0, 0.2, 0.4, 0.1];
        foreach (double delay in delays)
        {
            var bar = new Rectangle
            {
                Width = 3, Height = 14, Margin = new Thickness(1, 0, 1, 0), Fill = Brushes.White,
                RadiusX = 1.5, RadiusY = 1.5, VerticalAlignment = VerticalAlignment.Bottom,
                RenderTransformOrigin = new Point(0.5, 1), RenderTransform = new ScaleTransform(1, 0.3),
            };
            Equalizer.Children.Add(bar);

            var anim = new DoubleAnimation(0.3, 1, TimeSpan.FromSeconds(0.45))
            {
                AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(delay),
                EasingFunction = new SineEase(),
            };
            var sb = new Storyboard();
            Storyboard.SetTarget(anim, bar);
            Storyboard.SetTargetProperty(anim, new PropertyPath("RenderTransform.ScaleY"));
            sb.Children.Add(anim);
            _bars.Add(sb);
        }
    }

    private void SetBarsRunning(bool running)
    {
        Equalizer.Visibility = running ? Visibility.Visible : Visibility.Hidden;
        foreach (var sb in _bars)
        {
            if (running) sb.Begin(this, isControllable: true);
            else sb.Stop(this);
        }
    }

    /// <summary>"Spotify.exe" or "SpotifyAB.SpotifyMusic_xyz!Spotify" → "Spotify".</summary>
    private static string AppName(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        string name = id.Split('!')[^1].Split('\\')[^1];
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return name.Length > 0 ? char.ToUpper(name[0]) + name[1..] : name;
    }

    private static string Format(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";

    private static T? FindParent<T>(DependencyObject d) where T : DependencyObject
    {
        while (d is not null and not T) d = VisualTreeHelper.GetParent(d);
        return d as T;
    }
}
