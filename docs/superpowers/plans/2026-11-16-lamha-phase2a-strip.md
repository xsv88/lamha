# Lamha Phase 2A (Taskbar Strip) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Show 1–3 user-chosen items as text on the taskbar, left of the tray, with hover → panel.

**Architecture:** Pure formatting, settings and placement math in `Lamha.Core` (tested on Linux). A WPF `StripWindow` in `Lamha.App` renders items, follows the taskbar, and feeds the existing `HoverController`.

**Tech Stack:** C#/.NET 10, WPF, Win32 (`FindWindow`, `GetWindowRect`, `SetWindowPos`, `SHAppBarMessage`).

**Spec:** `docs/superpowers/specs/2026-11-16-lamha-tray-glance-design.md` (Addendum: Phase 2A)

## Global Constraints
- 1–3 items, no duplicates, order kept. Default: strip on, items `[NextPrayer]`.
- Primary taskbar only; hide when taskbar vertical, auto-hidden, or full screen.
- Default x: left of `TrayNotifyWnd`; fallback 260 px × scale from taskbar right. Saved drag offset wins.
- Topmost re-assert every 2 s; content refresh 1 s while visible.
- No admin, no Explorer injection.

## Review Focus
1. **Taskbar moved to top / made auto-hide while running** — strip moves or hides within 2 s. Owner: Task 3 (`StripPlacement_TaskbarTop_*`, manual).
2. **Saved offset outside new screen width (resolution change)** — clamped onto the taskbar. Owner: Task 2 (`StripPlacement_OffsetBeyondTaskbar_Clamped`).
3. **Long song titles / Arabic text** — truncated, strip width bounded (max 160 px per item). Owner: Task 1 (`Format_NowPlaying_LongTitleTruncated`).
4. **Settings file with 0, 5 or duplicate items** — normalized to 1–3 distinct. Owner: Task 1 (`Normalize_*`).
5. **Strip covering taskbar icons** — user can drag it; manual check.

---

### Task 1: Strip items model, formatting and settings
**Files:** Create `src/Lamha.Core/Strip/StripItem.cs`, `src/Lamha.Core/Strip/StripFormatter.cs`; Modify `src/Lamha.Core/Settings/AppSettings.cs`; Test `tests/Lamha.Core.Tests/StripFormatterTests.cs`, `StripSettingsTests.cs`.

**Interfaces — Produces:**
- `enum StripItem { NextPrayer, NowPlaying, CpuRam, HijriDate }`
- `record StripText(string Line1, string Line2, StripTone Tone)`; `enum StripTone { Normal, Blue, Yellow, Red }`
- `StripFormatter.NextPrayer(PrayerState s, Func<PrayerName,string> name) -> StripText`; `.NowPlaying(string title, string artist)`; `.CpuRam(double cpu, double ramGb)`; `.Hijri(int day, string month, int year)`
- `AppSettings.Strip` = `record StripSettings(bool Enabled = true, IReadOnlyList<StripItem> Items = [NextPrayer], double? OffsetFromRightPx = null)`; `Normalized()` keeps 1–3 distinct items (empty → `[NextPrayer]`).

- [ ] Tests: `Format_NextPrayer_NormalShowsTimeAndIn`, `Format_NextPrayer_IqamaShowsIqama_Yellow`, `Format_NextPrayer_LastMinute_Red`, `Format_NextPrayer_Under15_Blue`, `Format_NowPlaying_LongTitleTruncated` (18 chars + "…"), `Format_CpuRam`, `Normalize_EmptyItems_DefaultsToNextPrayer`, `Normalize_FiveItems_KeepsFirstThree`, `Normalize_Duplicates_Removed`, `Defaults_StripOnWithNextPrayer`.
- [ ] Run → fail; implement; run → pass; commit `feat(core): add taskbar strip items and settings`.

### Task 2: Strip placement math
**Files:** Create `src/Lamha.App/Strip/StripPlacement.cs` (no WPF types, linked into tests); Test `tests/Lamha.Core.Tests/StripPlacementTests.cs`.

**Interfaces — Produces:** `static PxRect? StripPlacement.Place(PxRect taskbar, PxRect? trayNotify, PxSize strip, double? offsetFromRight, double scale)` → null when taskbar is vertical (height > width).
- Vertically centered in taskbar. x = offset given ? `taskbar.Right - offset - width` : tray ? `tray.X - width - 8*scale` : `taskbar.Right - 260*scale - width`; clamped inside taskbar.

- [ ] Tests: `StripPlacement_BottomTaskbar_LeftOfTray`, `StripPlacement_NoTrayRect_FallbackOffset`, `StripPlacement_SavedOffset_Wins`, `StripPlacement_OffsetBeyondTaskbar_Clamped`, `StripPlacement_TaskbarTop_CenteredInTaskbar`, `StripPlacement_VerticalTaskbar_Null`.
- [ ] Fail → implement → pass; commit `feat(app): add strip placement math`.

### Task 3: Strip window
**Files:** Create `src/Lamha.App/Strip/StripWindow.xaml(.cs)`, `src/Lamha.App/Strip/TaskbarInfo.cs`; Modify `Interop/Native.cs`, `Tray/HoverController.cs` (accept hover from strip), `App.xaml.cs`.

- `TaskbarInfo.Read()` → taskbar rect (`FindWindow("Shell_TrayWnd")`), tray rect (`FindWindowEx(..,"TrayNotifyWnd")`), auto-hide (`SHAppBarMessage(ABM_GETSTATE)`).
- `StripWindow`: `AllowsTransparency=true`, transparent background, `WS_EX_TOOLWINDOW`, topmost; one `StackPanel` of item blocks (2 lines, 12 px, max width 160 DIP); text color follows taskbar theme (`SystemUsesLightTheme`); prayer tone colors.
- 2 s timer: read taskbar, place (or hide), `SetWindowPos(HWND_TOPMOST)`; hide while `FullscreenDetector.IsBusy()`.
- Mouse: hover → `HoverController.OnStripHover(anchor = strip top-center)`; click → pin; drag (left button held >150 ms and moved) → move along taskbar, save `OffsetFromRightPx`.
- [ ] Build 0 errors; commit `feat(app): add taskbar strip window`.

### Task 4: Settings + wiring
**Files:** Modify `Settings/SettingsWindow.xaml(.cs)`, `App.xaml.cs`, `Ui/Loc.cs`.
- New tab "Taskbar": show strip checkbox; 3 combo boxes (item or "None"); Reset position button.
- App feeds strip from `PrayerService.Updated`, media `Changed` (keep latest even when panel closed → MediaProvider must run while strip shows NowPlaying), `SystemProvider` (1 s while CpuRam item shown).
- [ ] Build; tests green; commit `feat: wire taskbar strip into settings and services`.

### Task 5: Docs and checklist
- Add strip lines to `docs/CHECKLIST.md`, README feature list. Commit `docs: taskbar strip checklist`.
