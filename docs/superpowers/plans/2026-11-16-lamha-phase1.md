# Lamha Phase 1 (Core) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Lamha v0.1: a tray icon next to the clock whose hover panel shows prayer times (with iqama color states), now playing, and PC load, plus settings, shortcut, theme, do-not-disturb and Arabic/English.

**Architecture:** Two projects. `Lamha.Core` (plain `net10.0`, no Windows APIs) holds all logic that can be unit-tested anywhere: prayer math, prayer state machine, settings, color math, alert rules. `Lamha.App` (`net10.0-windows`, WPF) holds Windows-only code: tray icon, panel window, providers that call Windows APIs, and views. Providers in App feed plain records from Core into views.

**Tech Stack:** C# 13, .NET 10, WPF, `WPF-UI` (lepoco), `H.NotifyIcon.Wpf`, `Microsoft.Toolkit.Uwp.Notifications` (toasts), xUnit.

**Spec:** `docs/superpowers/specs/2026-11-16-lamha-tray-glance-design.md`

## Global Constraints

- Target `net10.0` (Core, tests) and `net10.0-windows10.0.19041.0` (App). Windows 11 first; Windows 10 19041+ best effort.
- No admin rights, ever. No code path may require elevation.
- Light: about 0% CPU when panel closed; under 100 MB RAM.
- Only official, documented Windows APIs. No taskbar injection.
- Settings file: `%AppData%\Lamha\settings.json`. Logs: `%AppData%\Lamha\logs\` (rolling, max 5 files).
- Prayer method default Umm al-Qura. Iqama defaults (minutes): Fajr 25, Dhuhr 20, Asr 20, Maghrib 10, Isha 20. Per-prayer offset range −30…+30 minutes.
- Icon accent when next prayer < 15 minutes away. Yellow from adhan until 60 s before iqama; red for the final 60 s. Shuruq never yellow/red.
- Hover open delay 250 ms; close after mouse leaves icon and panel for 400 ms.
- Default hotkey `Ctrl+Alt+N`. Do-not-disturb default ON. Startup summary is Phase 2 (not here).
- UI languages: Arabic (RTL) and English (LTR); default follows Windows display language.
- Every commit message ends with the session attribution lines.

## Review Focus

1. **PC sleeps overnight or the date changes at midnight** — on resume / date change, prayer times recompute for the new day; countdown never shows negative or yesterday's times. Test in Task 3 (`State_AfterMidnight_UsesNextDayFajr`) and Task 6 (manual: sleep 2 min, resume).
2. **settings.json is corrupt, empty, or from an older version** — app starts with defaults, renames the bad file to `settings.bad.json`, never crashes. Test in Task 4.
3. **Media session has no thumbnail, no timeline (live stream), or disappears mid-update** — card shows a neutral color and hides the progress bar; no exception. Test in Task 7 (`AlbumColor_NoImage_ReturnsNeutral`) + manual YouTube live.
4. **High-latitude or unusual location where the Fajr angle is never reached** — calculator returns `null` for that prayer instead of NaN/crash; card shows "—". Test in Task 2.
5. **Taskbar on top/left/right, multiple monitors, 125–200% scaling** — panel stays fully on the monitor that holds the icon. Pure placement math tested in Task 5 (`PanelPlacement_*`).

---

## File Structure

```
Lamha.sln
src/Lamha.Core/
  Prayer/PrayerName.cs            enum Fajr, Shuruq, Dhuhr, Asr, Maghrib, Isha
  Prayer/PrayerTimes.cs           record of the 6 times for one date
  Prayer/PrayerCalculator.cs      astronomy + Umm al-Qura method + offsets
  Prayer/PrayerState.cs           enum + record for current state
  Prayer/PrayerStateMachine.cs    Normal / IqamaWait / LastMinute logic
  Settings/AppSettings.cs         records + defaults
  Settings/SettingsStore.cs       JSON load/save/recovery
  Media/AlbumColor.cs             dominant color + contrast darkening
  Alerts/AlertRules.cs            when to toast / accent icon; DND queue
src/Lamha.App/
  App.xaml(.cs)                   startup, single instance, DI wiring
  Tray/TrayIconController.cs      icon, hover, click, icon color
  Tray/PanelPlacement.cs          (pure math, linked into tests) panel rect
  Panel/PanelWindow.xaml(.cs)     borderless topmost window, section host
  Panel/Sections/*.xaml           Header, Prayer, NowPlaying, PcLoad
  Providers/MediaProvider.cs      GlobalSystemMediaTransportControlsSessionManager
  Providers/SystemProvider.cs     CPU/RAM/GPU/top processes
  Providers/FullscreenDetector.cs SHQueryUserNotificationState
  Services/HotkeyService.cs       RegisterHotKey
  Services/ToastService.cs        Windows toasts
  Services/StartupService.cs      HKCU Run key
  Settings/SettingsWindow.xaml(.cs)
  Localization/Strings.resx, Strings.ar.resx
tests/Lamha.Core.Tests/           xUnit, one file per Core class (+ PanelPlacementTests)
```

---

### Task 1: Solution scaffold with a working tray icon

**Files:**
- Create: `Lamha.sln`, `src/Lamha.Core/Lamha.Core.csproj`, `src/Lamha.App/Lamha.App.csproj`, `src/Lamha.App/App.xaml(.cs)`, `src/Lamha.App/Tray/TrayIconController.cs`, `src/Lamha.App/Assets/lamha.ico`, `tests/Lamha.Core.Tests/Lamha.Core.Tests.csproj`, `tests/Lamha.Core.Tests/SmokeTests.cs`, `Directory.Build.props`

**Interfaces:**
- Produces: `TrayIconController(Action onOpenPanel, Action onExit)`; `void SetAccent(TrayAccent accent)` where `enum TrayAccent { None, Blue, Yellow, Red }`.

- [ ] **Step 1:** `Directory.Build.props`: `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `LangVersion=latest`, `EnableWindowsTargeting=true` (so Core/tests also build on non-Windows CI).
- [ ] **Step 2:** Create projects. App: `OutputType=WinExe`, `UseWPF=true`, `TargetFramework=net10.0-windows10.0.19041.0`, references Core, packages `WPF-UI`, `H.NotifyIcon.Wpf`. App has no main window; `ShutdownMode=OnExplicitShutdown`.
- [ ] **Step 3:** Single instance: named `Mutex("Lamha.SingleInstance")`; second launch exits silently. Global exception handlers (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) write to `%AppData%\Lamha\logs\lamha-yyyyMMdd.log`, keep newest 5 files.
- [ ] **Step 4:** Tray icon with tooltip "Lamha" and right-click menu: Settings (disabled for now), Exit.
- [ ] **Step 5:** `SmokeTests.Core_Assembly_Loads` asserts `typeof(PrayerName).Assembly` is not null (add the `PrayerName` enum now).
- [ ] **Step 6:** Run `dotnet build` and `dotnet test` — both green. Manual on Windows: run App, icon appears near clock, Exit closes it, second launch does nothing.
- [ ] **Step 7:** Commit `feat: scaffold solution with tray icon`.

### Task 2: Prayer time calculator (Umm al-Qura)

**Files:**
- Create: `src/Lamha.Core/Prayer/PrayerTimes.cs`, `src/Lamha.Core/Prayer/PrayerCalculator.cs`
- Test: `tests/Lamha.Core.Tests/PrayerCalculatorTests.cs`

**Interfaces:**
- Produces:
  - `record PrayerTimes(DateOnly Date, IReadOnlyDictionary<PrayerName, DateTimeOffset?> Times)`
  - `record Location(double Latitude, double Longitude, TimeSpan UtcOffset)`
  - `enum AsrMethod { Standard, Hanafi }`
  - `static PrayerTimes PrayerCalculator.Calculate(DateOnly date, Location loc, IReadOnlyDictionary<PrayerName,int> offsetMinutes, bool isRamadan, AsrMethod asr = AsrMethod.Standard)`

Method rules (Umm al-Qura): Fajr sun at −18.5°; Shuruq/Maghrib sun at −0.833°; Dhuhr solar noon + 1 min; Asr shadow factor 1 (Standard) or 2 (Hanafi); `isRamadan` comes from `UmAlQuraCalendar` month 9; Isha = Maghrib + 90 min (120 min when `isRamadan`). Use the standard NOAA solar position formulas (declination, equation of time). A time whose hour-angle `acos` argument is outside [−1, 1] → `null`.

- [ ] **Step 1:** Generate reference times: `pip install adhan` (Python) and print Umm al-Qura times for Dammam (26.4207, 50.0888, UTC+3) on 2026-01-15, 2026-06-21, 2026-11-16. Paste them as constants in the test file with the generating script in a comment.
- [ ] **Step 2:** Tests:
  - `Calculate_Dammam_MatchesReference(date)` — each of 6 times within ±1 minute of reference.
  - `Calculate_Ramadan_IshaIs120AfterMaghrib`.
  - `Calculate_Offsets_ShiftEachPrayer` — offset +5 on Dhuhr only moves Dhuhr by exactly 5 min.
  - `Calculate_Offsets_ClampedToPlusMinus30` — +45 behaves as +30.
  - `Calculate_HighLatitude_FajrNullNotNaN` — lat 66.0, 2026-06-21: Fajr is `null`, Dhuhr not null.
  - `Calculate_Hanafi_AsrLaterThanStandard`.
- [ ] **Step 3:** Run tests — fail.
- [ ] **Step 4:** Implement `PrayerCalculator.Calculate`.
- [ ] **Step 5:** Run tests — pass.
- [ ] **Step 6:** Commit `feat(core): add Umm al-Qura prayer calculator`.

### Task 3: Prayer state machine (blue / yellow / red)

**Files:**
- Create: `src/Lamha.Core/Prayer/PrayerState.cs`, `src/Lamha.Core/Prayer/PrayerStateMachine.cs`
- Test: `tests/Lamha.Core.Tests/PrayerStateMachineTests.cs`

**Interfaces:**
- Consumes: `PrayerTimes`, `PrayerName`.
- Produces:
  - `enum PrayerPhase { Normal, IqamaWait, LastMinute }`
  - `record PrayerState(PrayerPhase Phase, PrayerName Target, DateTimeOffset TargetTime, TimeSpan Remaining)` — in Normal, Target = next adhan; in IqamaWait/LastMinute, Target = current prayer and TargetTime = its iqama.
  - `static PrayerState PrayerStateMachine.Evaluate(DateTimeOffset now, PrayerTimes today, PrayerTimes tomorrow, IReadOnlyDictionary<PrayerName,int> iqamaMinutes)`
  - `static TrayAccentHint AccentFor(PrayerState s)` returning `enum TrayAccentHint { None, Blue, Yellow, Red }`; Blue when Normal and Remaining < 15 min.

- [ ] **Step 1:** Tests (Dhuhr adhan 11:52, iqama 20):
  - `State_BeforeAdhan_NormalCountsToAdhan` — 11:40 → Normal, Remaining 12:00, accent Blue.
  - `State_ThirtyMinBefore_NoAccent` — 11:22 → accent None.
  - `State_AtAdhan_IqamaWait` — 11:52:00 → IqamaWait, Remaining 20:00, Yellow.
  - `State_61sBeforeIqama_StillYellow`; `State_60sBeforeIqama_Red`.
  - `State_AtIqama_BackToNormalNextPrayer` — 12:12:00 → Normal, Target Asr.
  - `State_ShuruqNeverYellow` — 1 min after Shuruq → Normal, Target Dhuhr.
  - `State_AfterMidnight_UsesNextDayFajr` — 23:59 after Isha iqama → Target Fajr from `tomorrow`.
  - `State_IshaIqamaCrossesMidnight` — Isha 23:50 + 20 min → at 00:05 still IqamaWait.
  - `State_NullPrayerSkipped` — Fajr null → Target skips to Shuruq/Dhuhr without exception.
- [ ] **Step 2:** Run — fail. **Step 3:** Implement. **Step 4:** Run — pass.
- [ ] **Step 5:** Commit `feat(core): add prayer phase state machine`.

### Task 4: Settings model and store

**Files:**
- Create: `src/Lamha.Core/Settings/AppSettings.cs`, `src/Lamha.Core/Settings/SettingsStore.cs`
- Test: `tests/Lamha.Core.Tests/SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `record AppSettings` with: `int Version=1`, `IReadOnlyList<SectionId> SectionOrder`, `IReadOnlySet<SectionId> HiddenSections`, `LocationSetting Location` (`Auto` or manual `Location` + city name), `PrayerSettings Prayer` (`Method="UmmAlQura"`, `AsrMethod Asr=Standard`, `AlertMinutesBefore=10`, `AlertsEnabled=true`, `OffsetMinutes`, `IqamaMinutes` with the Global Constraints defaults), `string Hotkey="Ctrl+Alt+N"`, `bool DoNotDisturbFullscreen=true`, `bool StartWithWindows=true`, `ThemeMode Theme=FollowWindows`, `double PanelOpacity=1.0` (range 0.6–1.0), `LanguageMode Language=Auto`.
  - `enum SectionId { Header, Prayer, NowPlaying, Events, WeatherNetwork, Batteries, PcLoad }` (Phase 1 views exist for Header, Prayer, NowPlaying, PcLoad only).
  - `SettingsStore(string filePath)`; `AppSettings Load()`; `void Save(AppSettings s)` (write temp file then atomic replace).

- [ ] **Step 1:** Tests: `Load_MissingFile_ReturnsDefaults`; `SaveThenLoad_RoundTrips`; `Load_CorruptJson_ReturnsDefaultsAndRenamesToBad` (file `settings.bad.json` exists after); `Load_EmptyFile_ReturnsDefaults`; `Load_UnknownFields_Ignored`; `Load_OpacityOutOfRange_Clamped`; `Defaults_IqamaMatchSpec` (25/20/20/10/20).
- [ ] **Step 2:** Run — fail. **Step 3:** Implement with `System.Text.Json` source generation. **Step 4:** Run — pass.
- [ ] **Step 5:** Commit `feat(core): add settings model and resilient store`.

### Task 5: Panel window, hover open/close, placement

**Files:**
- Create: `src/Lamha.App/Tray/PanelPlacement.cs` (no WPF types; linked into test project), `src/Lamha.App/Panel/PanelWindow.xaml(.cs)`
- Modify: `src/Lamha.App/Tray/TrayIconController.cs`
- Test: `tests/Lamha.Core.Tests/PanelPlacementTests.cs`

**Interfaces:**
- Produces:
  - `static Rect PanelPlacement.Place(Rect iconRect, Rect monitorWorkArea, Size panelSize, double margin=12)` (all in device pixels; `Rect`/`Size` are small records in this file).
  - `PanelWindow.ShowNear(Rect iconRect)`, `PanelWindow.HideAnimated()`, `bool IsPinned`.

Behavior: rich-tooltip messages `NIN_POPUPOPEN`/`NIN_POPUPCLOSE` (`NOTIFYICON_VERSION_4`) start a 250 ms open timer / 400 ms close timer; mouse inside panel cancels close. Left-click toggles pinned open; click outside (window `Deactivated`) or `Esc` unpins and hides. Icon rect from `Shell_NotifyIconGetRect`; monitor from `MonitorFromRect` + `GetMonitorInfo`. Window: `WindowStyle=None`, `AllowsTransparency=false`, `Topmost=true`, `ShowInTaskbar=false`, `WS_EX_TOOLWINDOW`.

- [ ] **Step 1:** Tests: `PanelPlacement_TaskbarBottom_AboveIcon`; `PanelPlacement_TaskbarTop_BelowIcon`; `PanelPlacement_TaskbarLeft_RightOfIcon`; `PanelPlacement_TaskbarRight_LeftOfIcon`; `PanelPlacement_NearScreenEdge_StaysInsideWorkArea`; `PanelPlacement_SecondMonitorNegativeCoords_StaysOnThatMonitor`.
- [ ] **Step 2:** Run — fail. **Step 3:** Implement `Place`. **Step 4:** Run — pass.
- [ ] **Step 5:** Implement window + hover wiring (empty panel with placeholder text).
- [ ] **Step 6:** Manual on Windows: hover opens after ~¼ s, leaving closes, moving into panel keeps it, click pins, Esc closes; touch tap on Surface opens pinned.
- [ ] **Step 7:** Commit `feat(app): add hover panel with placement`.

### Task 6: Header + prayer section

**Files:**
- Create: `src/Lamha.App/Panel/Sections/HeaderSection.xaml(.cs)`, `src/Lamha.App/Panel/Sections/PrayerSection.xaml(.cs)`, `src/Lamha.App/Services/PrayerService.cs`

**Interfaces:**
- Consumes: `PrayerCalculator.Calculate`, `PrayerStateMachine.Evaluate/AccentFor`, `AppSettings.Prayer`, `TrayIconController.SetAccent`.
- Produces: `PrayerService` with `event Action<PrayerState, PrayerTimes> Updated`; `void SetPanelOpen(bool open)` (1 s tick when open, 60 s when closed); recomputes `today/tomorrow` on date change and on `SystemEvents.PowerModeChanged` resume and `SystemEvents.TimeChanged`.

Header: Gregorian date (current UI culture) + Hijri via `UmAlQuraCalendar`. Prayer card colors: Normal = accent blue, IqamaWait = yellow, LastMinute = red (WPF-UI palette brushes). Countdown text: Normal "{Prayer} in hh:mm:ss"; IqamaWait "Iqama in mm:ss"; LastMinute "Iqama in ss". Null time shows "—". Location comes from settings (manual) or Windows location (`Windows.Devices.Geolocation`, fall back to Dammam if denied).

- [ ] **Step 1:** Implement service + views.
- [ ] **Step 2:** Manual: set Dhuhr offset so adhan is 2 minutes from now and iqama gap 2 → watch blue → yellow → red at 60 s → blue for Asr; tray icon follows. Sleep PC 2 min, resume: countdown correct.
- [ ] **Step 3:** Commit `feat(app): add prayer card with iqama color states`.

### Task 7: Now playing card

**Files:**
- Create: `src/Lamha.Core/Media/AlbumColor.cs`, `src/Lamha.App/Providers/MediaProvider.cs`, `src/Lamha.App/Panel/Sections/NowPlayingSection.xaml(.cs)`
- Test: `tests/Lamha.Core.Tests/AlbumColorTests.cs`

**Interfaces:**
- Produces:
  - `record Rgb(byte R, byte G, byte B)`; `static Rgb AlbumColor.Dominant(ReadOnlySpan<byte> bgra, int width, int height)` (quantize to 4-bit per channel, ignore near-white/near-black and alpha<128, most frequent bucket; empty → neutral `Rgb(60,60,70)`); `static Rgb AlbumColor.EnsureContrastWithWhite(Rgb c, double minRatio=4.5)` (darken in HSL lightness steps of 0.02 until WCAG ratio ≥ 4.5); `static double AlbumColor.ContrastRatio(Rgb a, Rgb b)`.
  - `MediaProvider` with `event Action<NowPlaying?> Changed`; `record NowPlaying(string Title, string Artist, string SourceAppId, byte[]? ThumbnailBgra, int ThumbW, int ThumbH, TimeSpan? Position, TimeSpan? Duration, bool IsPlaying)`; `Task PlayPauseAsync()`, `NextAsync()`, `PreviousAsync()`; `Start()`/`Stop()`.

- [ ] **Step 1:** Tests: `AlbumColor_SolidRed_ReturnsRedBucket`; `AlbumColor_NoImage_ReturnsNeutral`; `AlbumColor_AllWhite_ReturnsNeutral`; `EnsureContrast_LightYellow_DarkenedToAtLeast4_5`; `EnsureContrast_AlreadyDark_Unchanged`; `ContrastRatio_BlackWhite_Is21`.
- [ ] **Step 2:** Run — fail. **Step 3:** Implement `AlbumColor`. **Step 4:** Run — pass.
- [ ] **Step 5:** Implement `MediaProvider` (events `CurrentSessionChanged`, `MediaPropertiesChanged`, `PlaybackInfoChanged`, `TimelinePropertiesChanged`; every handler wrapped in try/catch → `Changed(null)` on failure) and the card (spec 3.2 item 3): thumbnail 64 px, title, artist, app name, 4 animated equalizer bars only while playing, progress bar hidden when Duration is null/zero, prev/play-pause/next buttons. Section hidden when `NowPlaying` is null. Clicking the card (not buttons) activates the source app.
- [ ] **Step 6:** Manual: Spotify, YouTube in Chrome, YouTube live (no progress bar), pause → bars stop, close player → card hides.
- [ ] **Step 7:** Commit `feat: add now playing card tinted by album art`.

### Task 8: PC load + top apps

**Files:**
- Create: `src/Lamha.App/Providers/SystemProvider.cs`, `src/Lamha.App/Panel/Sections/PcLoadSection.xaml(.cs)`

**Interfaces:**
- Produces: `record SystemSnapshot(double CpuPercent, double RamPercent, double RamUsedGb, double RamTotalGb, double? GpuPercent, IReadOnlyList<AppUsage> TopApps)`; `record AppUsage(int Pid, string Name, double PrivateGb)`; `SystemProvider.Sample(bool includeTopApps)`.

CPU: PDH `\Processor Information(_Total)\% Processor Utility` (clamp 0–100). RAM: `GlobalMemoryStatusEx`. GPU: sum `\GPU Engine(*engtype_3D)\Utilization Percentage` per adapter, take max, clamp 100; counter missing → `null` → row shows "—". Top apps: group `Process.GetProcesses()` by name, sum private bytes, top 3; skip processes that throw on access. Sampling only while panel open (1 s); top apps only while "Show more" is expanded.

- [ ] **Step 1:** Implement provider + section: three bars, "Show more" expander with top 3 and a close button each. Close asks "Close {Name}? Unsaved work may be lost." (Yes/No) then `CloseMainWindow()`, and after 3 s `Kill()` only if user confirmed a second "Force close?" prompt.
- [ ] **Step 2:** Manual on PC (AMD RX 9070 XT): GPU % moves when a game/video runs; panel closed 10 min → Lamha CPU ~0% in Task Manager.
- [ ] **Step 3:** Commit `feat(app): add PC load section with top apps`.

### Task 9: Settings window

**Files:**
- Create: `src/Lamha.App/Settings/SettingsWindow.xaml(.cs)`, `src/Lamha.App/Services/StartupService.cs`
- Modify: `App.xaml.cs` (load settings at startup, apply on save), `TrayIconController.cs` (enable Settings menu)

**Interfaces:**
- Consumes: `SettingsStore`, `AppSettings`.
- Produces: `event Action<AppSettings> SettingsChanged` on `App`; `StartupService.SetEnabled(bool)` writes/removes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Lamha`.

Pages (WPF-UI `NavigationView`): Sections (toggle + drag reorder), Location (auto / pick city: Dammam, Riyadh, Jeddah, Makkah, Madinah, Khobar + manual lat/lon), Prayer (alerts on/off, alert minutes, Asr Standard/Hanafi, per-prayer offset sliders −30…+30, iqama number boxes 0–60), Shortcut (record keys), Appearance (theme, opacity slider 60–100%, language), General (start with Windows, do-not-disturb).

- [ ] **Step 1:** Implement; every change saves immediately and raises `SettingsChanged`.
- [ ] **Step 2:** Manual: hide Now playing → panel hides it; reorder → panel order changes; offset Dhuhr +3 → card time +3; restart app → settings kept.
- [ ] **Step 3:** Commit `feat(app): add settings window`.

### Task 10: Hotkey, alerts, do-not-disturb, click-through

**Files:**
- Create: `src/Lamha.Core/Alerts/AlertRules.cs`, `src/Lamha.App/Services/HotkeyService.cs`, `src/Lamha.App/Services/ToastService.cs`, `src/Lamha.App/Providers/FullscreenDetector.cs`
- Test: `tests/Lamha.Core.Tests/AlertRulesTests.cs`

**Interfaces:**
- Produces:
  - `AlertRules(PrayerSettings s, bool dndEnabled)`; `IReadOnlyList<Alert> Tick(DateTimeOffset now, PrayerState state, bool fullscreenBusy)`; `record Alert(string Key, PrayerName Prayer, DateTimeOffset Due)`. Rules: one alert per prayer per day at `adhan − AlertMinutesBefore`; none when `AlertsEnabled=false`; while `fullscreenBusy && dndEnabled`, alerts are queued; first tick after busy ends returns one combined alert (Key `"missed"`) if queue non-empty.
  - `HotkeyService.Register(string combo, Action onPressed)` → `bool` (false if taken).
  - `FullscreenDetector.IsBusy()` → true for `QUNS_BUSY`, `QUNS_RUNNING_D3D_FULL_SCREEN`, `QUNS_PRESENTATION_MODE`.

- [ ] **Step 1:** Tests: `Tick_AtAlertTime_EmitsOnce` (second tick same minute emits nothing); `Tick_AlertsDisabled_Nothing`; `Tick_FullscreenBusy_Queued`; `Tick_AfterBusyEnds_OneMissedAlert`; `Tick_DndOff_EmitsEvenWhenBusy`; `Tick_NewDay_ResetsKeys`.
- [ ] **Step 2:** Run — fail. **Step 3:** Implement `AlertRules`. **Step 4:** Run — pass.
- [ ] **Step 5:** Wire: hotkey toggles panel (pinned); settings shortcut page shows "This shortcut is in use" when `Register` returns false and keeps old combo; toasts via `ToastService`; icon does not flash while busy. Click-through per spec 3.3 (Prayer → settings Prayer page; PC load → `taskmgr.exe`).
- [ ] **Step 6:** Manual: alert 10 min before; full-screen game → no toast, one "missed" toast after exit.
- [ ] **Step 7:** Commit `feat: add hotkey, prayer alerts and do-not-disturb`.

### Task 11: Theme, transparency, Arabic/English

**Files:**
- Create: `src/Lamha.App/Localization/Strings.resx`, `Strings.ar.resx`
- Modify: all section/window XAML to use `{x:Static loc:Strings.*}`; `PanelWindow.xaml.cs`

- [ ] **Step 1:** Move every UI string to resources (English + Arabic). Arabic sets `FlowDirection=RightToLeft` on panel and settings; numbers stay Western digits in both.
- [ ] **Step 2:** Theme: WPF-UI `ApplicationThemeManager` follows Windows (`SystemThemeWatcher`) or forced light/dark. Windows 11 22H2+: Mica via `DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE)`; older: solid background. Opacity slider sets panel `Opacity`.
- [ ] **Step 3:** Manual: switch Windows dark/light → panel follows; Arabic layout mirrored and readable; opacity 60% works.
- [ ] **Step 4:** Commit `feat(app): add themes, transparency and Arabic UI`.

### Task 12: v0.1 release

**Files:**
- Create: `src/Lamha.App/Properties/PublishProfiles/win-x64.pubxml`, `docs/CHECKLIST.md`
- Modify: `README.md` (screenshots, download link)

- [ ] **Step 1:** Publish profile: `win-x64`, self-contained, single file, `PublishTrimmed=false`, `PublishReadyToRun=true`.
- [ ] **Step 2:** `docs/CHECKLIST.md` = spec section 8 manual list; run it on PC and Surface; tick results.
- [ ] **Step 3:** Performance: panel closed 10 min → CPU ~0%, working set < 100 MB. Record numbers in checklist.
- [ ] **Step 4:** Add 2 screenshots + short GIF to README.
- [ ] **Step 5:** Commit `chore: release v0.1`; tag `v0.1.0`; create GitHub release with the zipped publish output.
