# WakeyWakey

A lightweight Windows system-tray utility that temporarily suppresses power management and sleep on a laptop, keeping the machine awake for long-running operations such as [Claude Code](https://claude.ai/code) sessions, large builds, or overnight data transfers.

---

## Features

- **One-click activation** — left-click the tray icon to toggle sleep suppression on or off (indefinite by default).
- **Timed presets** — right-click and choose 30 minutes, 1 hour, 2 hours, 4 hours, or 8 hours; the suppression stops automatically when the timer expires.
- **Live countdown** — the tooltip shows the remaining time, updated every 15 seconds.
- **Visual indicator** — the tray icon is green when active and grey when inactive.
- **No installer required** — a single self-contained `.exe` with no additional dependencies.
- **Clean restoration** — sleep settings are always restored on exit or deactivation, whether triggered manually or by timer expiry.

---

## How it works

WakeyWakey calls the Windows [`SetThreadExecutionState`](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate) API with the flags:

| Flag | Purpose |
|---|---|
| `ES_CONTINUOUS` | Hold the state until explicitly cleared |
| `ES_SYSTEM_REQUIRED` | Prevent the system from sleeping |
| `ES_AWAYMODE_REQUIRED` | Prevent away mode (used by some power plans) |

When deactivated the flags are cleared with `ES_CONTINUOUS` alone, restoring the system's normal power policy.

---

## Requirements

- Windows 10 or Windows 11
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (Desktop / Windows)

---

## Building from source

```bash
git clone https://github.com/alasdaircs/wakey-wakey.git
cd wakey-wakey
dotnet build WakeyWakey/WakeyWakey.csproj --configuration Release
```

### Produce a standalone executable

```bash
dotnet publish WakeyWakey/WakeyWakey.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true
```

The output `.exe` will be in `WakeyWakey/bin/Release/net10.0-windows/win-x64/publish/`.

---

## Usage

1. Run `WakeyWakey.exe` — a grey circle appears in the system tray.
2. **Left-click** the icon to activate (green circle; sleep is now suppressed indefinitely).
3. **Left-click** again to deactivate, or right-click for timed options.
4. Right-click → **Exit** to close the application cleanly.

---

## Project structure

```
WakeyWakey.sln
└── WakeyWakey/
    ├── WakeyWakey.csproj          — net10.0-windows WinExe
    ├── Program.cs                  — STAThread entry point
    ├── PowerManager.cs             — SetThreadExecutionState wrapper
    └── TrayApplicationContext.cs   — tray icon, menus, timer, icon renderer
```

---

## Licence

MIT — see [LICENSE.md](LICENSE.md).
