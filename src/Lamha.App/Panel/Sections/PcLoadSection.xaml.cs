using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lamha.App.Providers;

namespace Lamha.App.Panel.Sections;

/// <summary>CPU, RAM and GPU bars. "Show more" lists the 3 apps using the most RAM, each with a close button.</summary>
public partial class PcLoadSection : UserControl
{
    private static readonly Brush Normal = Frozen(0x37, 0x8A, 0xDD);
    private static readonly Brush High = Frozen(0xEF, 0x9F, 0x27);
    private static readonly Brush Critical = Frozen(0xE2, 0x4B, 0x4A);

    private readonly (ProgressBar Bar, TextBlock Value)[] _rows = new (ProgressBar, TextBlock)[3];

    public PcLoadSection()
    {
        InitializeComponent();
        string[] labels = ["CPU", "RAM", "GPU"];
        for (int i = 0; i < 3; i++)
        {
            var label = new TextBlock { Text = labels[i], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 0, 3) };
            var bar = new ProgressBar { Height = 6, Minimum = 0, Maximum = 100, BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)), VerticalAlignment = VerticalAlignment.Center };
            var value = new TextBlock { TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(label, i); Grid.SetRow(bar, i); Grid.SetRow(value, i);
            Grid.SetColumn(bar, 1); Grid.SetColumn(value, 2);
            Bars.Children.Add(label); Bars.Children.Add(bar); Bars.Children.Add(value);
            _rows[i] = (bar, value);
        }
        Bars.MouseLeftButtonUp += (_, _) => Clicked?.Invoke();
        Bars.Cursor = Cursors.Hand;
    }

    /// <summary>True while "Show more" is open: the provider then also reads the top apps.</summary>
    public bool WantsTopApps => More.IsExpanded;

    /// <summary>Raised when the bars are clicked (opens Task Manager).</summary>
    public event Action? Clicked;

    public void Show(SystemSnapshot s)
    {
        Set(0, s.CpuPercent, $"{s.CpuPercent:0}%");
        Set(1, s.RamPercent, $"{s.RamUsedGb:0.0} GB");
        if (s.GpuPercent is double g) Set(2, g, $"{g:0}%");
        else Set(2, 0, "—");

        if (More.IsExpanded) ShowTopApps(s.TopApps);
    }

    private void Set(int i, double percent, string text)
    {
        _rows[i].Bar.Value = percent;
        _rows[i].Bar.Foreground = percent >= 90 ? Critical : percent >= 70 ? High : Normal;
        _rows[i].Value.Text = text;
    }

    private void ShowTopApps(IReadOnlyList<AppUsage> apps)
    {
        TopList.Children.Clear();
        foreach (var app in apps)
        {
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var close = new Button { Content = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                Padding = new Thickness(6, 2, 6, 2), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = Critical, ToolTip = $"Close {app.Name}" };
            close.Click += (_, _) => CloseApp(app.Name);
            DockPanel.SetDock(close, Dock.Right);
            row.Children.Add(close);
            row.Children.Add(new TextBlock { Text = $"{app.Name}   {app.PrivateGb:0.0} GB", VerticalAlignment = VerticalAlignment.Center });
            TopList.Children.Add(row);
        }
    }

    /// <summary>Asks first, closes politely, and force-closes only after a second yes.</summary>
    private static async void CloseApp(string name)
    {
        var answer = MessageBox.Show($"Close {name}? Unsaved work may be lost.", "Lamha",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        var procs = Process.GetProcessesByName(name);
        foreach (var p in procs) { try { p.CloseMainWindow(); } catch { /* Already gone. */ } }

        await Task.Delay(TimeSpan.FromSeconds(3));
        var left = procs.Where(p => { try { return !p.HasExited; } catch { return false; } }).ToList();
        if (left.Count > 0 &&
            MessageBox.Show($"{name} is still running. Force close?", "Lamha",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            foreach (var p in left) { try { p.Kill(); } catch (Exception ex) { CrashLog.Write(ex); } }
        }
        foreach (var p in procs) p.Dispose();
    }

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
