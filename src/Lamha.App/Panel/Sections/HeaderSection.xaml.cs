using System.Globalization;
using System.Windows.Controls;

namespace Lamha.App.Panel.Sections;

/// <summary>Top line: today's Gregorian and Hijri (Umm al-Qura) dates.</summary>
public partial class HeaderSection : UserControl
{
    private static readonly string[] HijriMonthsEn =
    [
        "Muharram", "Safar", "Rabi al-Awwal", "Rabi al-Thani", "Jumada al-Ula", "Jumada al-Akhirah",
        "Rajab", "Shaban", "Ramadan", "Shawwal", "Dhu al-Qadah", "Dhu al-Hijjah",
    ];

    private DateOnly _shown;

    public HeaderSection() => InitializeComponent();

    public void Refresh()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today == _shown) return;
        _shown = today;

        var dt = today.ToDateTime(TimeOnly.MinValue);
        GregorianText.Text = dt.ToString("dddd d MMMM", CultureInfo.CurrentUICulture);

        var hijri = new UmAlQuraCalendar();
        HijriText.Text = $"{hijri.GetDayOfMonth(dt)} {HijriMonthsEn[hijri.GetMonth(dt) - 1]} {hijri.GetYear(dt)}";
    }
}
