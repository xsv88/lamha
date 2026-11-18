# Lamha Phase 2B (Device Batteries) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Show battery levels of every device Windows can report, in the panel, on the strip, and as low-battery alerts.

**Architecture:** Core holds the reading model, low-battery alert rules, sorting/formatting (tested on Linux). App holds `BatteryProvider` (four Windows sources merged, deduplicated by name) and the panel section.

**Spec:** `docs/superpowers/specs/2026-11-16-lamha-tray-glance-design.md` (Addendum: Phase 2B)

## Global Constraints
- Low = under 20 %. Alert once; re-arm at ≥ 30 %.
- Refresh 30 s when panel open or strip shows Battery; 5 min otherwise.
- Each source isolated in try/catch; no admin; no vendor dongles.

## Review Focus
1. **Device disconnects between reads** — row disappears, no stale value, no alert spam on reconnect. Owner: Task 1 (`Alerts_DeviceGoneAndBackLow_NoSecondAlert`).
2. **Same device from two sources** (Bluetooth property + GATT) — shown once. Owner: Task 1 (`Merge_SameNameTwoSources_KeepsOne`).
3. **Percent outside 0–100 or unknown** — clamped / skipped. Owner: Task 1 (`Normalize_OutOfRange_Clamped`).
4. **No battery devices at all (desktop PC)** — section hidden, strip item skipped. Manual.
5. **PC battery while charging at 15 %** — no low alert while charging. Owner: Task 1 (`Alerts_ChargingLow_NoAlert`).

### Task 1: Core battery model, merge, alerts, strip text
Files: `src/Lamha.Core/Batteries/BatteryReading.cs`, `BatteryRules.cs`; modify `Strip/StripItem.cs` (add `Battery`), `Strip/StripFormatter.cs`; tests `BatteryRulesTests.cs`, add to `StripFormatterTests.cs`.
- `enum DeviceKind { Headset, Mouse, Keyboard, Controller, ThisPc, Other }`
- `record BatteryReading(string Name, DeviceKind Kind, int Percent, bool Charging = false)`
- `BatteryRules.Merge(IEnumerable<BatteryReading>) -> IReadOnlyList<BatteryReading>`: clamp 0–100, dedupe by name (case-insensitive, keep lowest percent), sort ThisPc last then by percent ascending.
- `BatteryRules.Lowest(list) -> BatteryReading?` (excluding charging).
- `class LowBatteryAlerts { IReadOnlyList<BatteryReading> Tick(IReadOnlyList<BatteryReading> now) }` — returns devices that just went low.
- `StripFormatter.Battery(BatteryReading r) -> StripText(name cut, "15%", Red if <20)`.

### Task 2: BatteryProvider (App)
`src/Lamha.App/Providers/BatteryProvider.cs`: `Task<IReadOnlyList<BatteryReading>> ReadAsync()` merging four sources; kind from Bluetooth class-of-device / appearance / name keywords.

### Task 3: Panel section, strip item, alerts, tray accent, settings label
`Panel/Sections/BatteriesSection.xaml(.cs)`; wire in App (timer 30 s / 5 min), strip `Battery` item, toast, tray accent; Loc strings; Sections list includes Batteries.

### Task 4: Checklist + README
