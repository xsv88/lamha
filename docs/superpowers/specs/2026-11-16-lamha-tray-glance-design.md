# Lamha (لمحة) — Windows tray glance app — Design spec

Date: 2026-11-16
Owner: Khaled (reviews code) · Claude (writes code)
Working name: **Lamha** ("a glance"). Can be renamed before the first release.

## 1. Goal

A small Windows app that lives as an icon next to the clock (beside Wi-Fi / sound / battery).
When the mouse hovers over the icon, a panel opens above it and shows, at one glance,
what the user needs to know: next prayer, what is playing, upcoming events, weather,
network, device batteries, and PC load.

Purpose: **information**, not health reminders. The app tells you what you have,
and warns you only when something needs attention.

Audience: Khaled first, then portfolio, then public release (free).

Success criteria:
- Panel opens in under 200 ms after hover.
- Stable across Windows updates (uses only official, documented Windows features).
- Light: about 0% CPU when the panel is closed; under 100 MB RAM.
- No admin rights needed, ever.
- Arabic and English UI.

Non-goals (for now): health/break reminders, embedding text inside the taskbar,
replacing the Windows Quick Settings panel.

## 2. Tech choices

- Language: C# on .NET 10 (LTS).
- UI: WPF + `WPF-UI` library (Windows 11 Fluent look).
- Tray icon: `H.NotifyIcon.Wpf`.
- Target: Windows 11 first. Windows 10 (19041+) best effort.
- Package: single-folder app with installer first; Microsoft Store (MSIX) later.

Reason: WPF is mature and stable, has full access to Windows APIs, and C# is close
to Java, so Khaled can review it. WinUI 3 was rejected because borderless,
transparent, always-on-top windows are still hard there.

## 3. How it looks

### 3.1 Tray icon
- Normal state: neutral icon.
- Accent state (color change) when:
  - next prayer is less than 15 minutes away, or
  - a tracked device battery is under 20%.

### 3.2 Panel (top to bottom; each section can be turned off in Settings)
1. **Header** — Gregorian date + Hijri date.
2. **Prayer** — next prayer name + countdown (big), all 6 times in one row (Fajr, Shuruq, Dhuhr, Asr, Maghrib, Isha).
   The prayer card has three color states:

   | State | When | Card color | Countdown shows |
   |---|---|---|---|
   | Normal | Before adhan | Accent (blue) | Time to adhan |
   | Adhan → iqama | From adhan until 1 minute before iqama | **Yellow** | Time to iqama ("الإقامة بعد 12:40") |
   | Last minute | Final 60 seconds before iqama | **Red** | Seconds to iqama |

   After iqama the card returns to Normal and counts to the next prayer.
   The tray icon uses the same yellow / red colors in these two states.
   Shuruq has no iqama, so it never enters the yellow / red states.
3. **Now playing** — style 1 (approved):
   - Card background takes the dominant color of the album art (darkened so text stays readable).
   - Album art thumbnail, title, artist, source app name.
   - Small animated equalizer bars while playing.
   - Progress bar with elapsed / total time.
   - Previous / play-pause / next buttons.
   - Hidden when nothing is playing.
4. **Upcoming events** — next 2–3 events from Google Calendar.
5. **Weather + network** — one row: city temperature + icon; download / upload speed or "offline".
6. **Batteries** — Bluetooth devices that report battery (headset, mouse, keyboard). Under 20% shown in red.
7. **PC load** — CPU, RAM, GPU bars.
   - "Show more" expands to the **top 3 apps by RAM** with a close button next to each (feature #2). Closing asks for confirmation.

### 3.3 Click-through (feature #6)
Clicking a section opens its related app:
- Prayer → prayer settings page.
- Now playing → the source app (Spotify, browser…).
- Events → Google Calendar in the browser.
- Weather → weather page in the browser.
- PC load → Task Manager.

### 3.4 Theme (feature #7)
- Follows Windows light/dark by default.
- Background material: Mica / Acrylic on Windows 11 (`DWMWA_SYSTEMBACKDROP_TYPE`), solid color on Windows 10.
- **Transparency slider** in Settings (panel opacity).
- Language: Arabic (RTL) or English (LTR), default follows Windows display language.

## 4. Behavior

### 4.1 Open / close
- Hover on icon: open after 250 ms. Uses the tray "rich tooltip" messages (`NIN_POPUPOPEN` / `NIN_POPUPCLOSE`, `NOTIFYICON_VERSION_4`).
- Panel is placed above the icon using `Shell_NotifyIconGetRect`; it never goes off-screen.
- Close when the mouse leaves both icon and panel for 400 ms.
- Left-click on the icon pins the panel open until a click outside it or `Esc`.

### 4.2 Keyboard shortcut (feature #5)
- Default: `Ctrl+Alt+N` toggles the panel (`RegisterHotKey`).
- User can change it in Settings. If the combo is taken by another app, Settings shows "This shortcut is in use" and keeps the old one.

### 4.3 Do-not-disturb in full screen (feature #4)
- **On by default**, toggle in Settings.
- When a full-screen game, video, or presentation is running (`SHQueryUserNotificationState`), the app does not show toasts and does not flash the icon. Missed alerts appear as one toast after full screen ends.

### 4.4 Startup summary (feature #3)
- **Off by default**, toggle in Settings.
- After Windows sign-in, one toast: today's event count, next prayer time, current temperature.

### 4.5 Alerts
- Prayer: toast N minutes before (default 10, configurable, can be off).
- Device battery under 20%: one toast per device per charge cycle.

## 5. Inside the app

Each part has one job and talks to the others only through a small interface.

```
TrayIconController ──hover/hotkey──> PanelWindow
                                        │ shows
                                        ▼
                                  Section views (one per section)
                                        ▲ data
                                        │
     Providers (each implements IProvider<T>, runs on its own timer)
       PrayerProvider      — offline calculation, Umm al-Qura method
       MediaProvider       — Windows.Media.Control (GlobalSystemMediaTransportControlsSessionManager)
       CalendarProvider    — Google Calendar API, OAuth sign-in once, token stored with DPAPI
       WeatherProvider     — Open-Meteo (free, no key)
       NetworkProvider     — System.Net.NetworkInformation byte counters
       BatteryProvider     — Bluetooth LE GATT Battery Service (0x180F)
       SystemProvider      — CPU: PDH "Processor Information"; RAM: GlobalMemoryStatusEx;
                             GPU: PDH "GPU Engine(*)\Utilization Percentage"; top apps: Process list
AlertService      — decides when to toast / color the icon; respects do-not-disturb
SettingsService   — JSON at %AppData%\Lamha\settings.json
```

### 5.1 Refresh rates (keeps it light)
| Provider | Panel open | Panel closed |
|---|---|---|
| Prayer | 1 s (countdown) | 60 s (for alerts) |
| Media | event-driven | stopped |
| System / Network | 1 s | stopped |
| Battery | 30 s | 5 min (for alerts) |
| Weather | 15 min cache | 15 min cache |
| Calendar | 5 min cache | 15 min (only if startup summary on) |

### 5.2 Errors
- Each provider returns `Ok(data)` or `Unavailable(reason)`.
- A failing provider never crashes the app. Its section shows a short "Not available" line, or hides if the user chose "hide when empty".
- Network-based providers (weather, calendar) keep the last good data and show its age.
- Unhandled exceptions are written to `%AppData%\Lamha\logs\` (rolling, max 5 files).

## 6. Settings page
- Sections: on/off for each, drag to reorder.
- Location: auto (Windows location) or city chosen by hand.
- Prayer: method (default Umm al-Qura), alert minutes before, Asr calculation.
- Prayer time adjustment: per prayer, shift the calculated time by −30 … +30 minutes (to match the local mosque).
- Adhan → iqama gap: per prayer, in minutes. Defaults (common Saudi values, editable): Fajr 25, Dhuhr 20, Asr 20, Maghrib 10, Isha 20.
- Shortcut key.
- Do-not-disturb in full screen (on).
- Startup summary (off).
- Start with Windows (on).
- Theme: follow Windows / light / dark; transparency slider.
- Language: auto / Arabic / English.
- Google account: sign in / sign out.

## 7. Phases

| Phase | Content |
|---|---|
| 1 — Core | Tray icon, hover panel, prayer, now playing (style 1), PC load + top apps, settings, shortcut, theme + transparency, click-through, do-not-disturb, Arabic/English |
| 2 — Online info | Weather, network, Google Calendar, startup summary |
| 3 — Devices | Bluetooth device batteries + battery alerts |
| 4 — Phone (separate project) | Phone battery, notifications, files. Android companion app or KDE Connect protocol. Gets its own spec. |

## 8. Testing
- Unit tests (xUnit):
  - Prayer times for Dammam vs. published Umm al-Qura times (±1 min) for several dates.
  - Per-prayer adjustment and iqama gap applied correctly; state machine Normal → Yellow → Red → Normal at the right seconds (including across midnight for Isha).
  - Album-color contrast: text/background contrast ratio ≥ 4.5.
  - Settings load/save, defaults, and broken-file recovery.
  - AlertService rules (15-min accent, do-not-disturb queue, one toast per charge cycle).
- Manual checklist per phase: hover open/close, multi-monitor, taskbar on other edges, 125%/150% scaling, RTL layout, light/dark, Windows 10 smoke test.
- Performance check: CPU and RAM with panel closed for 10 minutes.

## 9. Open items
- Final app name and icon.

Dropped: pausing or lowering media at adhan (Khaled chose not to include it).
