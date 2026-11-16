using System.Globalization;
using System.Windows.Controls;
using Lamha.App.Ui;

namespace Lamha.App.Panel.Sections;

/// <summary>Top line: today's Gregorian and Hijri (Umm al-Qura) dates.</summary>
public partial class HeaderSection : UserControl
{
    private DateOnly _shown;

    public HeaderSection() => InitializeComponent();

    /// <summary>Forces a redraw (language changed).</summary>
    public void Invalidate() => _shown = default;

    public void Refresh()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today == _shown) return;
        _shown = today;

        var dt = today.ToDateTime(TimeOnly.MinValue);
        GregorianText.Text = dt.ToString("dddd d MMMM", Loc.DateCulture);

        var hijri = new UmAlQuraCalendar();
        HijriText.Text = $"{hijri.GetDayOfMonth(dt)} {Loc.HijriMonth(hijri.GetMonth(dt))} {hijri.GetYear(dt)}";
    }
}
