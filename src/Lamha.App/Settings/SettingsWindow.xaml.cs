using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lamha.Core.Prayer;
using Lamha.Core.Settings;
using Lamha.Core.Strip;
using ThemeMode = Lamha.Core.Settings.ThemeMode;

namespace Lamha.App.Settings;

/// <summary>
/// Settings window. Every change builds a new <see cref="AppSettings"/> and raises
/// <see cref="Changed"/>; the App saves it and applies it immediately.
/// </summary>
public partial class SettingsWindow : Window
{
    private static readonly (string Name, double Lat, double Lon)[] Cities =
    [
        ("Dammam", 26.4207, 50.0888), ("Khobar", 26.2172, 50.1971), ("Riyadh", 24.7136, 46.6753),
        ("Jeddah", 21.4858, 39.1925), ("Makkah", 21.3891, 39.8579), ("Madinah", 24.5247, 39.5692),
    ];

    private static readonly PrayerName[] AdjustablePrayers =
        [PrayerName.Fajr, PrayerName.Dhuhr, PrayerName.Asr, PrayerName.Maghrib, PrayerName.Isha];

    private AppSettings _settings;
    private bool _loading;
    private readonly Dictionary<PrayerName, (Slider Offset, Slider Iqama)> _prayerRows = new();
    private List<SectionId> _order;
    private bool _resetStrip;

    private ComboBox[] StripBoxes => [StripItem1, StripItem2, StripItem3];

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _order = settings.SectionOrder.ToList();
        Build();
        Load();
        Localize();
    }

    public event Action<AppSettings>? Changed;

    /// <summary>Shown under the shortcut box when registration fails (set by the App).</summary>
    public void ShowHotkeyStatus(string message) => HotkeyStatus.Text = message;

    public void OpenPrayerTab() => Tabs.SelectedItem = PrayerTab;

    private void Localize()
    {
        if (!Ui.Loc.IsArabic) return;
        FlowDirection = FlowDirection.RightToLeft;
        Title = "إعدادات لمحة";
        string[] tabs = ["الأقسام", "الموقع", "الصلاة", "الاختصار", "الشكل", "شريط المهام", "عام"];
        for (int i = 0; i < Tabs.Items.Count && i < tabs.Length; i++) ((TabItem)Tabs.Items[i]).Header = tabs[i];
        UpButton.Content = "أعلى"; DownButton.Content = "أسفل";
        LocAuto.Content = "استخدم موقع ويندوز"; LocCity.Content = "اختر مدينة"; LocManual.Content = "أدخل الإحداثيات";
        AlertsBox.Content = "ذكّرني قبل كل صلاة";
        StartupBox.Content = "تشغيل لمحة مع ويندوز";
        DndBox.Content = "عدم الإزعاج أثناء الألعاب والفيديو والعروض بملء الشاشة";
    }

    private void Build()
    {
        foreach (var c in Cities) CityBox.Items.Add(c.Name);
        AsrBox.Items.Add("Standard (Shafi, Maliki, Hanbali)");
        AsrBox.Items.Add("Hanafi");
        foreach (var t in Enum.GetNames<ThemeMode>()) ThemeBox.Items.Add(t);
        foreach (var box in StripBoxes)
        {
            box.Items.Add(Ui.Loc.T("StripNone"));
            foreach (var item in Enum.GetValues<StripItem>()) box.Items.Add(Ui.Loc.T("Strip" + item));
            box.SelectionChanged += (_, _) => Commit();
        }
        StripBox.Content = Ui.Loc.T("ShowStrip");
        StripBox.Click += (_, _) => Commit();
        StripItemsLabel.Text = Ui.Loc.T("StripItems");
        ResetStripButton.Content = Ui.Loc.T("ResetPosition");
        ResetStripButton.Click += (_, _) => { _resetStrip = true; Commit(); };
        StripHint.Text = Ui.Loc.T("StripHint");
        TaskbarTab.Header = Ui.Loc.T("Taskbar");
        foreach (var l in Enum.GetNames<LanguageMode>()) LanguageBox.Items.Add(l);

        // One row per prayer: name, offset slider + value, iqama slider + value.
        int row = 0;
        foreach (var p in AdjustablePrayers)
        {
            PrayerGrid.RowDefinitions.Add(new RowDefinition());
            var offset = new Slider { Minimum = -30, Maximum = 30, IsSnapToTickEnabled = true, TickFrequency = 1, Margin = new Thickness(4) };
            var iqama = new Slider { Minimum = 0, Maximum = 60, IsSnapToTickEnabled = true, TickFrequency = 1, Margin = new Thickness(4) };
            AddCell(new TextBlock { Text = p.ToString(), VerticalAlignment = VerticalAlignment.Center }, row, 0);
            AddCell(offset, row, 1);
            AddCell(ValueLabel(offset, "{0:+0;-0;0}"), row, 2);
            AddCell(iqama, row, 3);
            AddCell(ValueLabel(iqama, "{0}m"), row, 4);
            offset.ValueChanged += (_, _) => Commit();
            iqama.ValueChanged += (_, _) => Commit();
            _prayerRows[p] = (offset, iqama);
            row++;
        }

        // Every control saves on change.
        foreach (var rb in new[] { LocAuto, LocCity, LocManual }) rb.Checked += (_, _) => Commit();
        CityBox.SelectionChanged += (_, _) => { if (!_loading) { LocCity.IsChecked = true; Commit(); } };
        LatBox.LostFocus += (_, _) => Commit();
        LonBox.LostFocus += (_, _) => Commit();
        AlertsBox.Click += (_, _) => Commit();
        AlertSlider.ValueChanged += (_, _) => Commit();
        AsrBox.SelectionChanged += (_, _) => Commit();
        ThemeBox.SelectionChanged += (_, _) => Commit();
        LanguageBox.SelectionChanged += (_, _) => Commit();
        OpacitySlider.ValueChanged += (_, _) => Commit();
        StartupBox.Click += (_, _) => Commit();
        DndBox.Click += (_, _) => Commit();
        HotkeyBox.PreviewKeyDown += OnHotkeyKey;
        UpButton.Click += (_, _) => Move(-1);
        DownButton.Click += (_, _) => Move(+1);
    }

    private void Load()
    {
        _loading = true;
        var s = _settings;

        RefreshSectionList();

        LocAuto.IsChecked = s.Location.Auto;
        int city = Array.FindIndex(Cities, c => c.Name == s.Location.CityName);
        if (!s.Location.Auto && city >= 0) { LocCity.IsChecked = true; CityBox.SelectedIndex = city; }
        else if (!s.Location.Auto) LocManual.IsChecked = true;
        LatBox.Text = s.Location.Latitude?.ToString(CultureInfo.InvariantCulture) ?? "";
        LonBox.Text = s.Location.Longitude?.ToString(CultureInfo.InvariantCulture) ?? "";

        AlertsBox.IsChecked = s.Prayer.AlertsEnabled;
        AlertSlider.Value = s.Prayer.AlertMinutesBefore;
        AsrBox.SelectedIndex = (int)s.Prayer.Asr;
        foreach (var (p, row) in _prayerRows)
        {
            row.Offset.Value = s.Prayer.OffsetMinutes.GetValueOrDefault(p);
            row.Iqama.Value = s.Prayer.IqamaMinutes.GetValueOrDefault(p);
        }

        HotkeyBox.Text = s.Hotkey;
        ThemeBox.SelectedIndex = (int)s.Theme;
        OpacitySlider.Value = Math.Round(s.PanelOpacity * 100);
        LanguageBox.SelectedIndex = (int)s.Language;
        StartupBox.IsChecked = s.StartWithWindows;
        StripBox.IsChecked = s.Strip.Enabled;
        for (int i = 0; i < 3; i++)
            StripBoxes[i].SelectedIndex = i < s.Strip.Items.Count ? (int)s.Strip.Items[i] + 1 : 0; // 0 = None.
        DndBox.IsChecked = s.DoNotDisturbFullscreen;

        _loading = false;
    }

    private void Commit()
    {
        if (_loading) return;

        LocationSetting location;
        if (LocCity.IsChecked == true && CityBox.SelectedIndex >= 0)
        {
            var c = Cities[CityBox.SelectedIndex];
            location = new LocationSetting(false, c.Name, c.Lat, c.Lon);
        }
        else if (LocManual.IsChecked == true && TryCoord(LatBox.Text, 90, out var lat) && TryCoord(LonBox.Text, 180, out var lon))
        {
            location = new LocationSetting(false, null, lat, lon);
        }
        else
        {
            location = LocationSetting.Default;
        }

        _settings = _settings with
        {
            SectionOrder = _order.ToList(),
            HiddenSections = SectionList.Items.OfType<CheckBox>()
                .Where(cb => cb.IsChecked != true).Select(cb => (SectionId)cb.Tag).ToList(),
            Location = location,
            Prayer = _settings.Prayer with
            {
                AlertsEnabled = AlertsBox.IsChecked == true,
                AlertMinutesBefore = (int)AlertSlider.Value,
                Asr = (AsrMethod)Math.Max(0, AsrBox.SelectedIndex),
                OffsetMinutes = _prayerRows.ToDictionary(kv => kv.Key, kv => (int)kv.Value.Offset.Value),
                IqamaMinutes = _prayerRows.ToDictionary(kv => kv.Key, kv => (int)kv.Value.Iqama.Value),
            },
            Hotkey = HotkeyBox.Text,
            Theme = (ThemeMode)Math.Max(0, ThemeBox.SelectedIndex),
            PanelOpacity = OpacitySlider.Value / 100,
            Language = (LanguageMode)Math.Max(0, LanguageBox.SelectedIndex),
            StartWithWindows = StartupBox.IsChecked == true,
            DoNotDisturbFullscreen = DndBox.IsChecked == true,
            Strip = _settings.Strip with
            {
                Enabled = StripBox.IsChecked == true,
                Items = StripBoxes.Where(b => b.SelectedIndex > 0).Select(b => (StripItem)(b.SelectedIndex - 1)).ToList(),
                OffsetFromRightPx = _resetStrip ? null : _settings.Strip.OffsetFromRightPx,
            },
        };
        _resetStrip = false;
        Changed?.Invoke(_settings);
    }

    private void RefreshSectionList()
    {
        SectionList.Items.Clear();
        foreach (var id in _order.Where(IsAvailableInPhase1))
        {
            var cb = new CheckBox { Content = id.ToString(), Tag = id, IsChecked = !_settings.HiddenSections.Contains(id) };
            cb.Click += (_, _) => Commit();
            SectionList.Items.Add(cb);
        }
    }

    private void Move(int delta)
    {
        if (SectionList.SelectedItem is not CheckBox { Tag: SectionId id }) return;
        int i = _order.IndexOf(id), j = i + delta;
        // Skip over sections not shown in Phase 1 so one click always moves visibly.
        while (j >= 0 && j < _order.Count && !IsAvailableInPhase1(_order[j])) j += delta;
        if (j < 0 || j >= _order.Count) return;
        (_order[i], _order[j]) = (_order[j], _order[i]);
        RefreshSectionList();
        SectionList.SelectedIndex = _order.Where(IsAvailableInPhase1).ToList().IndexOf(id);
        Commit();
    }

    private void OnHotkeyKey(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return; // Wait for the main key.

        var mods = Keyboard.Modifiers;
        if (mods == ModifierKeys.None) { ShowHotkeyStatus("Use at least one of Ctrl, Alt, Shift or Win."); return; }

        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());

        HotkeyBox.Text = string.Join("+", parts);
        ShowHotkeyStatus("");
        Commit();
    }

    private static bool IsAvailableInPhase1(SectionId id) =>
        id is SectionId.Header or SectionId.Prayer or SectionId.NowPlaying or SectionId.PcLoad;

    private static bool TryCoord(string text, double limit, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && Math.Abs(value) <= limit;

    private void AddCell(UIElement e, int row, int col)
    {
        Grid.SetRow(e, row);
        Grid.SetColumn(e, col);
        PrayerGrid.Children.Add(e);
    }

    private static TextBlock ValueLabel(Slider s, string format)
    {
        var t = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        t.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Value") { Source = s, StringFormat = format });
        return t;
    }
}
