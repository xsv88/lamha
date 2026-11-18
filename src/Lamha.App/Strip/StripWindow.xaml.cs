using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Lamha.App.Interop;
using Lamha.App.Providers;
using Lamha.App.Tray;
using Lamha.Core.Strip;
using Microsoft.Win32;

namespace Lamha.App.Strip;

/// <summary>
/// Small transparent window that sits on the taskbar, left of the tray, and shows 1–3 short items.
/// Hover opens the panel, click pins it, drag moves the strip along the taskbar.
/// </summary>
public partial class StripWindow : Window
{
    private const double DragThresholdPx = 4;

    private readonly DispatcherTimer _placeTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private IntPtr _hwnd;
    private bool _enabled = true;
    private double? _offsetFromRight;
    private TaskbarInfo? _bar;
    private PxRect _lastRect;

    // Drag state.
    private Native.POINT _dragStart;
    private double _dragStartRight;
    private bool _mouseDown, _dragging;

    public StripWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            var ex = (long)Native.GetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE);
            // Tool window (not in Alt+Tab) that never takes keyboard focus from the app you are using.
            Native.SetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE, (IntPtr)(ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE));
        };
        _placeTimer.Tick += (_, _) => Reposition();
        SizeChanged += (_, _) => Reposition();
        SystemEvents.UserPreferenceChanged += (_, _) => Dispatcher.BeginInvoke(RecolorAll);

        Root.MouseMove += OnMouseMove;
        Root.MouseLeftButtonDown += OnMouseDown;
        Root.MouseLeftButtonUp += OnMouseUp;
        Root.LostMouseCapture += (_, _) => EndDrag(); // UAC prompt, Alt+Tab...: never leave a drag stuck.
    }

    /// <summary>Mouse rests on the strip: anchor point for the panel (top-center of the strip, physical pixels).</summary>
    public event Action<Native.POINT>? Hovered;

    /// <summary>Click (not a drag).</summary>
    public event Action<Native.POINT>? Clicked;

    /// <summary>User finished dragging; save this offset.</summary>
    public event Action<double>? OffsetChanged;

    /// <summary>Raised when the strip starts or stops being hidden by the environment.</summary>
    public event Action? SuppressedChanged;

    /// <summary>
    /// True while a full-screen app runs, the taskbar auto-hides or is vertical, or Explorer is restarting.
    /// The App stops live data for the strip during this time.
    /// </summary>
    public bool SuppressedByEnvironment { get; private set; }

    /// <summary>Top-center of the strip in physical pixels: where the panel opens above.</summary>
    public Native.POINT Anchor => new() { X = (int)_lastRect.CenterX, Y = (int)_lastRect.Y + 1 };

    public bool ContainsCursor(Native.POINT p) =>
        IsVisible && p.X >= _lastRect.X && p.X < _lastRect.Right && p.Y >= _lastRect.Y && p.Y < _lastRect.Bottom;

    public void Configure(bool enabled, double? offsetFromRight)
    {
        _enabled = enabled;
        _offsetFromRight = offsetFromRight;
        if (enabled) _placeTimer.Start(); else _placeTimer.Stop();
        Reposition();
    }

    /// <summary>Updates the blocks in place (no flicker, animations keep running). Call on the UI thread.</summary>
    public void ShowItems(IReadOnlyList<StripText> items)
    {
        while (Blocks.Children.Count > items.Count) Blocks.Children.RemoveAt(Blocks.Children.Count - 1);

        for (int i = 0; i < items.Count; i++)
        {
            bool wantPill = items[i].Pill is not null;
            if (i < Blocks.Children.Count && (Blocks.Children[i] is PillBlock) != wantPill) Blocks.Children.RemoveAt(i);
            if (i >= Blocks.Children.Count || (Blocks.Children[i] is PillBlock) != wantPill)
                Blocks.Children.Insert(i, wantPill ? new PillBlock() : MakeBlock());

            if (Blocks.Children[i] is PillBlock pill)
            {
                pill.Show(items[i]);
                continue;
            }
            var block = (StackPanel)Blocks.Children[i];
            SetLine((TextBlock)block.Children[0], items[i].Line1);
            SetLine((TextBlock)block.Children[1], items[i].Line2);
            block.Tag = items[i].Tone;
            Recolor(block);
        }
    }

    /// <summary>Arabic lines read right-to-left, English left-to-right, decided per line.</summary>
    private static void SetLine(TextBlock t, string text)
    {
        t.Text = text;
        t.FlowDirection = TextDirection.IsRtl(text) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    private void Reposition()
    {
        _bar = TaskbarInfo.Read();
        bool suppressed = _bar is null || _bar.AutoHide || _bar.Bounds.Height > _bar.Bounds.Width || FullscreenDetector.IsBusy();
        if (suppressed != SuppressedByEnvironment)
        {
            // Diagnostic: record why the strip hides, to find short flickers.
            CrashLog.Note(suppressed
                ? $"strip hidden: bar={(_bar is null ? "null" : "ok")} autoHide={_bar?.AutoHide} vertical={_bar is { } b && b.Bounds.Height > b.Bounds.Width} busy={FullscreenDetector.IsBusy()}"
                : "strip shown again");
            SuppressedByEnvironment = suppressed;
            SuppressedChanged?.Invoke();
        }

        bool show = _enabled && !suppressed && Blocks.Children.Count > 0;
        if (!show)
        {
            if (IsVisible && !suppressed) CrashLog.Note($"strip hidden: enabled={_enabled} blocks={Blocks.Children.Count}");
            if (IsVisible) Hide();
            return;
        }
        if (!IsVisible) Show();

        var bar = _bar!;
        var size = new PxSize(ActualWidth * bar.Scale, ActualHeight * bar.Scale);
        if (StripPlacement.Place(bar.Bounds, bar.TrayNotify, size, _offsetFromRight, bar.Scale) is not PxRect r)
        {
            CrashLog.Note("strip hidden: placement failed");
            Hide(); // Vertical taskbar.
            return;
        }
        _lastRect = r;
        // Topmost again every time: clicking the taskbar can raise it above us.
        Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, (int)r.X, (int)r.Y, (int)r.Width, (int)r.Height,
            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var p = Native.CursorPos();
        if (_mouseDown && e.LeftButton != MouseButtonState.Pressed) EndDrag(); // Button released outside our window.
        if (_mouseDown && _bar is not null)
        {
            if (!_dragging && Math.Abs(p.X - _dragStart.X) > DragThresholdPx) _dragging = true;
            if (_dragging)
            {
                double newRight = _dragStartRight + (p.X - _dragStart.X);
                _offsetFromRight = Math.Max(0, _bar.Bounds.Right - newRight);
                Reposition();
            }
            return;
        }
        Hovered?.Invoke(Anchor);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDown = true;
        _dragging = false;
        _dragStart = Native.CursorPos();
        _dragStartRight = _lastRect.Right;
        Root.CaptureMouse();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        bool wasDragging = _dragging;
        EndDrag();
        if (!wasDragging) Clicked?.Invoke(Anchor);
    }

    /// <summary>Stops any drag and saves the new place if the strip moved.</summary>
    private void EndDrag()
    {
        if (!_mouseDown) return;
        _mouseDown = false;
        bool moved = _dragging;
        _dragging = false;
        if (Root.IsMouseCaptured) Root.ReleaseMouseCapture();
        if (moved && _offsetFromRight is double o) OffsetChanged?.Invoke(o);
    }

    private static StackPanel MakeBlock()
    {
        var block = new StackPanel { Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, MaxWidth = 160 };
        block.Children.Add(new TextBlock { FontSize = 12, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        block.Children.Add(new TextBlock { FontSize = 12, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = 0.85 });
        return block;
    }

    private void RecolorAll()
    {
        foreach (var block in Blocks.Children.OfType<StackPanel>()) Recolor(block);
    }

    private static void Recolor(StackPanel block)
    {
        bool lightBar = TaskbarIsLight();
        Brush brush = (block.Tag as StripTone?) switch
        {
            StripTone.Blue => new SolidColorBrush(lightBar ? Color.FromRgb(0x18, 0x5F, 0xA5) : Color.FromRgb(0x60, 0xB0, 0xF5)),
            StripTone.Yellow => new SolidColorBrush(lightBar ? Color.FromRgb(0x85, 0x4F, 0x0B) : Color.FromRgb(0xF5, 0xB8, 0x4A)),
            StripTone.Red => new SolidColorBrush(lightBar ? Color.FromRgb(0xA3, 0x2D, 0x2D) : Color.FromRgb(0xF0, 0x70, 0x70)),
            _ => lightBar ? Brushes.Black : Brushes.White,
        };
        foreach (TextBlock t in block.Children) t.Foreground = brush;
    }

    /// <summary>The taskbar follows "Windows mode" (SystemUsesLightTheme), which can differ from the apps mode.</summary>
    private static bool TaskbarIsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int v && v == 1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Now-playing "pill": round cover, one-line title, background in the cover's color
    /// (darkened so white text stays readable), and three small bars that move only while playing.
    /// </summary>
    private sealed class PillBlock : Border
    {
        private readonly Ellipse _cover = new() { Width = 22, Height = 22 };
        private readonly TextBlock _title = new()
        {
            Foreground = Brushes.White, FontSize = 12, MaxWidth = 130,
            TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 6, 0),
        };
        private readonly StackPanel _bars = new() { Orientation = Orientation.Horizontal, Height = 11, VerticalAlignment = VerticalAlignment.Center };
        private readonly List<Storyboard> _boards = [];
        private byte[]? _shownImage;
        private bool _running;

        public PillBlock()
        {
            CornerRadius = new CornerRadius(13);
            Padding = new Thickness(2, 2, 8, 2);
            Margin = new Thickness(4, 0, 4, 0);
            VerticalAlignment = VerticalAlignment.Center;

            double[] delays = [0, 0.2, 0.4];
            foreach (double d in delays)
            {
                var bar = new Rectangle
                {
                    Width = 2, Height = 11, Margin = new Thickness(1, 0, 1, 0), Fill = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Bottom, RenderTransformOrigin = new Point(0.5, 1),
                    RenderTransform = new ScaleTransform(1, 0.3),
                };
                _bars.Children.Add(bar);
                var anim = new DoubleAnimation(0.3, 1, TimeSpan.FromSeconds(0.45))
                {
                    AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(d),
                };
                var sb = new Storyboard();
                Storyboard.SetTarget(anim, bar);
                Storyboard.SetTargetProperty(anim, new PropertyPath("RenderTransform.ScaleY"));
                sb.Children.Add(anim);
                _boards.Add(sb);
            }

            // Removed from the strip (song stopped, item turned off): stop the bars so nothing keeps drawing.
            Unloaded += (_, _) =>
            {
                foreach (var sb in _boards) sb.Stop(this);
                _running = false;
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(_cover);
            row.Children.Add(_title);
            row.Children.Add(_bars);
            Child = row;
        }

        public void Show(StripText t)
        {
            var c = t.Pill!.Value;
            Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            _title.Text = t.Line1;
            _title.FlowDirection = TextDirection.IsRtl(t.Line1) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            ToolTip = string.IsNullOrEmpty(t.Line2) ? t.Line1 : $"{t.Line1}\n{t.Line2}";

            if (!ReferenceEquals(t.Image, _shownImage)) // Rebuild the cover only when it changed.
            {
                _shownImage = t.Image;
                _cover.Fill = t.Image is { } px && t.ImageSize > 0
                    ? new ImageBrush(BitmapSource.Create(t.ImageSize, t.ImageSize, 96, 96, PixelFormats.Pbgra32, null, px, t.ImageSize * 4)) { Stretch = Stretch.UniformToFill }
                    : new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
            }

            _bars.Visibility = t.Playing ? Visibility.Visible : Visibility.Collapsed;
            if (t.Playing == _running) return;
            _running = t.Playing; // Animations run only while playing: no drawing cost when paused.
            foreach (var sb in _boards)
            {
                if (t.Playing) sb.Begin(this, isControllable: true);
                else sb.Stop(this);
            }
        }
    }
}
