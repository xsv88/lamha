using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lamha.App.Ui;
using Lamha.Core.Batteries;

namespace Lamha.App.Panel.Sections;

/// <summary>Battery level of each device. Under 20 % shows in red.</summary>
public partial class BatteriesSection : UserControl
{
    private static readonly Brush Red = Frozen(0xE2, 0x4B, 0x4A);
    private static readonly Brush Green = Frozen(0x63, 0x99, 0x22);

    public BatteriesSection() => InitializeComponent();

    public void Show(IReadOnlyList<BatteryReading> readings)
    {
        Visibility = readings.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        Rows.Children.Clear();
        foreach (var r in readings)
        {
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };

            var pct = new TextBlock
            {
                Text = $"{r.Percent}%", FlowDirection = FlowDirection.LeftToRight, VerticalAlignment = VerticalAlignment.Center,
                Foreground = r.IsLow ? Red : r.Charging ? Green : null,
            };
            if (r.IsLow) pct.FontWeight = FontWeights.SemiBold;
            DockPanel.SetDock(pct, Dock.Right);
            row.Children.Add(pct);

            if (r.Charging)
            {
                var bolt = new TextBlock { Text = "", FontFamily = Icons, Foreground = Green, Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(bolt, Dock.Right);
                row.Children.Add(bolt);
            }

            row.Children.Add(new TextBlock { Text = Icon(r.Kind), FontFamily = Icons, Width = 22, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock
            {
                Text = r.Kind == DeviceKind.ThisPc ? Loc.T("ThisPc") : r.Name,
                TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
            });
            Rows.Children.Add(row);
        }
    }

    private static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    /// <summary>Segoe Fluent Icons glyphs.</summary>
    private static string Icon(DeviceKind k) => k switch
    {
        DeviceKind.Headset => "",
        DeviceKind.Mouse => "",
        DeviceKind.Keyboard => "",
        DeviceKind.Controller => "",
        DeviceKind.ThisPc => "",
        _ => "",
    };

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
