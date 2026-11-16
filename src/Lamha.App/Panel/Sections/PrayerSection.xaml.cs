using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Lamha.Core.Prayer;

namespace Lamha.App.Panel.Sections;

/// <summary>
/// Prayer card. Blue before adhan, yellow while waiting for iqama, red in the last minute.
/// </summary>
public partial class PrayerSection : UserControl
{
    private static readonly Brush Blue = Frozen(0x18, 0x5F, 0xA5);
    private static readonly Brush Yellow = Frozen(0xEF, 0x9F, 0x27);
    private static readonly Brush Red = Frozen(0xE2, 0x4B, 0x4A);
    private static readonly Brush LightText = Frozen(0xFF, 0xFF, 0xFF);
    private static readonly Brush DarkText = Frozen(0x41, 0x24, 0x02); // Readable on yellow.

    private readonly TextBlock[] _names = new TextBlock[6];
    private readonly TextBlock[] _times = new TextBlock[6];

    public PrayerSection()
    {
        InitializeComponent();
        for (int i = 0; i < 6; i++)
        {
            _names[i] = new TextBlock { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.85 };
            TimesRow.Children.Add(_names[i]);
        }
        for (int i = 0; i < 6; i++)
        {
            _times[i] = new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center };
            TimesRow.Children.Add(_times[i]);
        }
    }

    /// <summary>Raised when the user clicks the card (opens prayer settings).</summary>
    public event Action? Clicked;

    public void Show(PrayerState state, PrayerTimes today)
    {
        var (bg, fg) = state.Phase switch
        {
            PrayerPhase.IqamaWait => (Yellow, DarkText),
            PrayerPhase.LastMinute => (Red, LightText),
            _ => (Blue, LightText),
        };
        Card.Background = bg;
        Card.SetValue(TextElement.ForegroundProperty, fg);

        string name = DisplayName(state.Target);
        (TitleText.Text, CountdownText.Text) = state.Phase switch
        {
            PrayerPhase.Normal => (name, Format(state.Remaining)),
            PrayerPhase.IqamaWait => ($"{name} · Iqama in", Format(state.Remaining)),
            _ => ($"{name} · Iqama in", $"{Math.Ceiling(state.Remaining.TotalSeconds):0}s"),
        };

        foreach (var p in Enum.GetValues<PrayerName>())
        {
            int i = (int)p;
            _names[i].Text = DisplayName(p);
            _times[i].Text = today.Times[p]?.ToString("HH:mm") ?? "—";
            _times[i].FontWeight = p == state.Target ? FontWeights.Bold : FontWeights.Normal;
        }
    }

    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        Clicked?.Invoke();
    }

    /// <summary>Display name. Arabic names come in Task 11.</summary>
    private static string DisplayName(PrayerName p) => p.ToString();

    private static string Format(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
