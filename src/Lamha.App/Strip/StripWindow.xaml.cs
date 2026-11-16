using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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
    }

    /// <summary>Mouse rests on the strip: anchor point for the panel (top-center of the strip, physical pixels).</summary>
    public event Action<Native.POINT>? Hovered;

    /// <summary>Click (not a drag).</summary>
    public event Action<Native.POINT>? Clicked;

    /// <summary>User finished dragging; save this offset.</summary>
    public event Action<double>? OffsetChanged;

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

    /// <summary>Updates the text blocks. Call on the UI thread.</summary>
    public void ShowItems(IReadOnlyList<StripText> items)
    {
        while (Blocks.Children.Count < items.Count) Blocks.Children.Add(MakeBlock());
        while (Blocks.Children.Count > items.Count) Blocks.Children.RemoveAt(Blocks.Children.Count - 1);

        for (int i = 0; i < items.Count; i++)
        {
            var block = (StackPanel)Blocks.Children[i];
            ((TextBlock)block.Children[0]).Text = items[i].Line1;
            ((TextBlock)block.Children[1]).Text = items[i].Line2;
            block.Tag = items[i].Tone;
            Recolor(block);
        }
    }

    private void Reposition()
    {
        _bar = TaskbarInfo.Read();
        bool show = _enabled && _bar is { AutoHide: false } && !FullscreenDetector.IsBusy() && Blocks.Children.Count > 0;
        if (!show)
        {
            if (IsVisible) Hide();
            return;
        }
        if (!IsVisible) Show();

        var bar = _bar!;
        var size = new PxSize(ActualWidth * bar.Scale, ActualHeight * bar.Scale);
        if (StripPlacement.Place(bar.Bounds, bar.TrayNotify, size, _offsetFromRight, bar.Scale) is not PxRect r)
        {
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
        Root.ReleaseMouseCapture();
        _mouseDown = false;
        if (_dragging)
        {
            _dragging = false;
            if (_offsetFromRight is double o) OffsetChanged?.Invoke(o);
        }
        else
        {
            Clicked?.Invoke(Anchor);
        }
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
        foreach (StackPanel block in Blocks.Children) Recolor(block);
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
}
