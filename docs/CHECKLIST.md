# Lamha v0.1 — manual test checklist

Run on the desktop PC (Windows 11, AMD GPU, 3 monitors) and the Surface Pro 9 (touch, 200% scaling).
Mark each line ✅ / ❌ and add a note for every ❌.

## Tray icon and panel
| # | Check | PC | Surface |
|---|---|---|---|
| 1 | Crescent icon appears next to the clock (or inside ^) | | |
| 2 | Second launch does nothing (only one icon) | | |
| 3 | Mouse rests on icon ~¼ s → panel opens above it | | |
| 4 | Mouse leaves icon and panel → panel closes after ~½ s | | |
| 5 | Moving from icon into panel keeps it open | | |
| 6 | Click icon → panel stays; click outside or Esc → closes | | |
| 7 | Touch tap on icon opens pinned panel | — | |
| 8 | Taskbar on top / left / right → panel stays fully on screen | | |
| 9 | Icon on a second monitor → panel opens on that monitor | | — |
| 10 | Text sharp at 125% / 150% / 200% scaling | | |

## Prayer
| # | Check | PC | Surface |
|---|---|---|---|
| 11 | Six times match a trusted Dammam timetable (±2 min) | | |
| 12 | Hijri date correct | | |
| 13 | Set Dhuhr offset so adhan is in 2 min, iqama 2 → blue → yellow → red (last 60 s) → blue (next prayer) | | |
| 14 | Tray icon follows the same colors | | |
| 15 | Sleep PC 2 min, wake → countdown correct, no old times | | |
| 16 | Reminder toast arrives N minutes before adhan | | |
| 17 | Full-screen game during reminder → no toast; one "missed" toast after exit | | |

## Now playing
| # | Check | PC | Surface |
|---|---|---|---|
| 18 | Spotify song → card shows cover, title, artist; color matches cover | | |
| 19 | YouTube in browser → card shows | | |
| 20 | Live stream → no progress bar, no crash | | |
| 21 | Pause → bars stop; buttons prev/play/next work | | |
| 22 | Close player → card disappears | | |

## PC load
| # | Check | PC | Surface |
|---|---|---|---|
| 23 | CPU/RAM/GPU values close to Task Manager | | |
| 24 | GPU % rises in a game (AMD RX 9070 XT) | | — |
| 25 | Show more → top 3 apps; close asks first; force close asks again | | |
| 26 | Click bars → Task Manager opens | | |

## Settings and look
| # | Check | PC | Surface |
|---|---|---|---|
| 27 | Hide / reorder sections → panel follows | | |
| 28 | Change city → times change | | |
| 29 | Shortcut Ctrl+Alt+N opens/closes panel; a taken shortcut shows "in use" | | |
| 30 | Windows light/dark switch → panel follows | | |
| 31 | Opacity 60% works | | |
| 32 | Arabic: panel mirrored, prayer names and Hijri month in Arabic | | |
| 33 | Start with Windows: restart PC → icon appears | | |
| 34 | Restart Lamha → all settings kept | | |

## Performance (spec: ~0% CPU closed, < 100 MB RAM)
| Measure (Task Manager, panel closed 10 min) | PC | Surface |
|---|---|---|
| Lamha CPU % | | |
| Lamha memory (MB) | | |

## Taskbar strip (Phase 2A)
| # | Check | PC | Surface |
|---|---|---|---|
| 35 | Text "Fajr 04:37 / in 7:10" shows on the taskbar, left of the tray icons | | |
| 36 | Hover the text → panel opens above it; click → pins | | |
| 37 | Drag the text left/right → moves; restart Lamha → same place | | |
| 38 | Settings → Taskbar: choose 3 items → all show; "Reset position" works | | |
| 39 | Prayer text turns yellow after adhan, red in last minute | | |
| 40 | Full-screen game → strip hides; taskbar auto-hide → strip hides | | |
| 41 | Taskbar light/dark → text color readable | | |
| 42 | Click on the taskbar → strip stays visible (back on top within 2 s) | | |

## Batteries (Phase 2B)
| # | Check | PC | Surface |
|---|---|---|---|
| 43 | Bluetooth headset/mouse/keyboard that shows battery in Windows Settings → same % in panel | | |
| 44 | Surface: "This PC" row with charging mark when plugged in | — | |
| 45 | Desktop with no battery devices → Batteries section hidden | | — |
| 46 | Strip item "Lowest battery" shows device name + %; red under 20 % | | |
| 47 | Device under 20 % → one toast; no repeat until charged to 30 % | | |
| 48 | Tray icon blue when a device is low (and no prayer color active) | | |
