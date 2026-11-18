using System.Globalization;
using Lamha.Core.Prayer;
using Lamha.Core.Settings;

namespace Lamha.App.Ui;

/// <summary>
/// All user-visible text in English and Arabic. Numbers stay Western (0-9) in both.
/// </summary>
public static class Loc
{
    public static bool IsArabic { get; private set; }

    /// <summary>Gregorian dates in Arabic need a culture whose calendar is Gregorian (ar-SA is Hijri).</summary>
    public static CultureInfo DateCulture => IsArabic ? new CultureInfo("ar-AE") : CultureInfo.GetCultureInfo("en-US");

    public static void Set(LanguageMode mode) => IsArabic = mode switch
    {
        LanguageMode.Arabic => true,
        LanguageMode.English => false,
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar",
    };

    public static string T(string key) =>
        (IsArabic ? Ar : En).TryGetValue(key, out var s) ? s : key;

    public static string Prayer(PrayerName p) => T(p.ToString());

    public static string HijriMonth(int month) => T("Hijri" + month);

    /// <summary>Short month name for the taskbar strip, e.g. "Jumada II".</summary>
    public static string HijriMonthShort(int month) => T("HijriShort" + month);

    public static Lamha.Core.Strip.StripWords StripWords =>
        new(T("In"), T("Iqama"), T("Min"), T("HourShort"), T("MinuteShort"));

    private static readonly Dictionary<string, string> En = new()
    {
        ["Fajr"] = "Fajr", ["Shuruq"] = "Sunrise", ["Dhuhr"] = "Dhuhr", ["Asr"] = "Asr", ["Maghrib"] = "Maghrib", ["Isha"] = "Isha",
        ["IqamaIn"] = "Iqama in",
        ["Hijri1"] = "Muharram", ["Hijri2"] = "Safar", ["Hijri3"] = "Rabi al-Awwal", ["Hijri4"] = "Rabi al-Thani",
        ["Hijri5"] = "Jumada al-Ula", ["Hijri6"] = "Jumada al-Akhirah", ["Hijri7"] = "Rajab", ["Hijri8"] = "Shaban",
        ["Hijri9"] = "Ramadan", ["Hijri10"] = "Shawwal", ["Hijri11"] = "Dhu al-Qadah", ["Hijri12"] = "Dhu al-Hijjah",
        ["CPU"] = "CPU", ["RAM"] = "RAM", ["GPU"] = "GPU", ["ShowMore"] = "Show more",
        ["CloseApp"] = "Close {0}? Unsaved work may be lost.", ["ForceClose"] = "{0} is still running. Force close?",
        ["Settings"] = "Settings", ["Exit"] = "Exit",
        ["ToastSoonTitle"] = "{0} at {1}", ["ToastSoonBody"] = "In {0} minutes.",
        ["ToastMissedTitle"] = "Prayer reminder", ["ToastMissedBody"] = "You were in full screen. {0} at {1}.",
        ["HotkeyInUse"] = "This shortcut is in use. Kept {0}.",
        ["In"] = "in", ["Iqama"] = "Iqama", ["Min"] = "min", ["HourShort"] = "h", ["MinuteShort"] = "m",
        ["HijriShort1"] = "Muharram", ["HijriShort2"] = "Safar", ["HijriShort3"] = "Rabi I", ["HijriShort4"] = "Rabi II",
        ["HijriShort5"] = "Jumada I", ["HijriShort6"] = "Jumada II", ["HijriShort7"] = "Rajab", ["HijriShort8"] = "Shaban",
        ["HijriShort9"] = "Ramadan", ["HijriShort10"] = "Shawwal", ["HijriShort11"] = "Dhul-Qadah", ["HijriShort12"] = "Dhul-Hijjah",
        ["StripNextPrayer"] = "Next prayer", ["StripNowPlaying"] = "Now playing", ["StripCpuRam"] = "CPU and RAM",
        ["StripHijriDate"] = "Hijri date", ["StripNone"] = "None", ["Taskbar"] = "Taskbar",
        ["ShowStrip"] = "Show text on the taskbar", ["ResetPosition"] = "Reset position",
        ["StripItems"] = "Items (up to 3, left to right)", ["StripHint"] = "Tip: drag the text on the taskbar to move it.",
    };

    private static readonly Dictionary<string, string> Ar = new()
    {
        ["Fajr"] = "الفجر", ["Shuruq"] = "الشروق", ["Dhuhr"] = "الظهر", ["Asr"] = "العصر", ["Maghrib"] = "المغرب", ["Isha"] = "العشاء",
        ["IqamaIn"] = "الإقامة بعد",
        ["Hijri1"] = "محرم", ["Hijri2"] = "صفر", ["Hijri3"] = "ربيع الأول", ["Hijri4"] = "ربيع الآخر",
        ["Hijri5"] = "جمادى الأولى", ["Hijri6"] = "جمادى الآخرة", ["Hijri7"] = "رجب", ["Hijri8"] = "شعبان",
        ["Hijri9"] = "رمضان", ["Hijri10"] = "شوال", ["Hijri11"] = "ذو القعدة", ["Hijri12"] = "ذو الحجة",
        ["CPU"] = "المعالج", ["RAM"] = "الرام", ["GPU"] = "الكرت", ["ShowMore"] = "عرض المزيد",
        ["CloseApp"] = "إغلاق {0}؟ قد يضيع العمل غير المحفوظ.", ["ForceClose"] = "{0} ما زال يعمل. إغلاق إجباري؟",
        ["Settings"] = "الإعدادات", ["Exit"] = "خروج",
        ["ToastSoonTitle"] = "{0} الساعة {1}", ["ToastSoonBody"] = "بعد {0} دقيقة.",
        ["ToastMissedTitle"] = "تذكير بالصلاة", ["ToastMissedBody"] = "كنت في وضع ملء الشاشة. {0} الساعة {1}.",
        ["HotkeyInUse"] = "هذا الاختصار مستخدم. أبقينا {0}.",
        ["In"] = "بعد", ["Iqama"] = "الإقامة", ["Min"] = "د", ["HourShort"] = "س", ["MinuteShort"] = "د",
        ["HijriShort1"] = "محرم", ["HijriShort2"] = "صفر", ["HijriShort3"] = "ربيع ١", ["HijriShort4"] = "ربيع ٢",
        ["HijriShort5"] = "جمادى ١", ["HijriShort6"] = "جمادى ٢", ["HijriShort7"] = "رجب", ["HijriShort8"] = "شعبان",
        ["HijriShort9"] = "رمضان", ["HijriShort10"] = "شوال", ["HijriShort11"] = "ذو القعدة", ["HijriShort12"] = "ذو الحجة",
        ["StripNextPrayer"] = "الصلاة الجاية", ["StripNowPlaying"] = "المقطع الشغال", ["StripCpuRam"] = "المعالج والرام",
        ["StripHijriDate"] = "التاريخ الهجري", ["StripNone"] = "لا شيء", ["Taskbar"] = "شريط المهام",
        ["ShowStrip"] = "إظهار النص على شريط المهام", ["ResetPosition"] = "إرجاع المكان الافتراضي",
        ["StripItems"] = "العناصر (حتى ٣، بالترتيب)", ["StripHint"] = "تلميح: اسحب النص على الشريط لتحريكه.",
    };
}
