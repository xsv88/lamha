# Lamha (لمحة)

A small Windows app that lives next to the clock. Hover over its icon and a panel shows
everything you need at a glance:

- **Prayer times** — next prayer countdown, adhan → iqama color states (blue → yellow → red), per-prayer time adjustment.
- **Now playing** — a card tinted with the album art color, with media controls.
- **PC load** — CPU, RAM and GPU, plus the top apps using RAM.
- **Upcoming events, weather, network, and device batteries** (later phases).

> Status: design stage. See the [design spec](docs/superpowers/specs/2026-11-16-lamha-tray-glance-design.md).

## Tech
C# · .NET 10 · WPF · WPF-UI · H.NotifyIcon

## Goals
- Light: about 0% CPU when the panel is closed, under 100 MB RAM.
- Stable: only official Windows features. No admin rights.
- Arabic and English.

## License
[MIT](LICENSE)
