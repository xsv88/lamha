# Lamha (لمحة)

A small Windows app that lives next to the clock. Hover over its icon and a panel shows
everything you need at a glance:

- **Prayer times** — next prayer countdown, adhan → iqama color states (blue → yellow → red), per-prayer time adjustment.
- **Now playing** — a card tinted with the album art color, with media controls.
- **PC load** — CPU, RAM and GPU, plus the top apps using RAM.
- **Taskbar strip** — 1–3 items (next prayer, now playing, CPU/RAM, Hijri date) shown right on the taskbar; drag to move.
- **Upcoming events, weather, network, and device batteries** (later phases).

> Status: v0.1 in testing. See the [design spec](docs/superpowers/specs/2026-11-16-lamha-tray-glance-design.md) and [test checklist](docs/CHECKLIST.md).

## Tech
C# · .NET 10 · WPF · H.NotifyIcon · Windows media and performance APIs

## Goals
- Light: about 0% CPU when the panel is closed, under 100 MB RAM.
- Stable: only official Windows features. No admin rights.
- Arabic and English.

## License
[MIT](LICENSE)

## Build
1. Install Visual Studio 2026 with **.NET desktop development**.
2. Open `Lamha.sln`, press ▶ (`Lamha.App`).
3. Tests: `dotnet test` (prayer math, iqama colors, settings, alerts, placement).
4. Release exe: `dotnet publish src/Lamha.App -p:PublishProfile=win-x64` → `publish/win-x64/Lamha.exe`.
