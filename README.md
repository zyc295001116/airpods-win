<div align="center">

# AirPodsWinTray

A resident AirPods card for Windows. Battery, in-ear state, one-click connect, or hand the audio back to iPhone.

[![.NET](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![C#](https://img.shields.io/badge/C%23-12-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![WinUI](https://img.shields.io/badge/WinUI-3-0078D4?logo=windows&logoColor=white)](https://learn.microsoft.com/windows/apps/winui/)
[![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-0078D4)](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

🌎 English | 🌏 [简体中文](README-CN.md)

</div>

**0.1.0 test build.** Behavior is still settling. If something is wrong, crashes, or misreads a model, please [open an issue](https://github.com/zyc295001116/airpods-win/issues/new).

Connect and disconnect follow [ToothTray](https://github.com/m2jean/ToothTray)'s `KSPROPERTY_ONESHOT` approach. Battery broadcasts are parsed from [OpenPods](https://github.com/adolfintel/OpenPods). The model table covers the AirPods lineup. Real-device checks so far are mainly **AirPods 3 (`0x1320`)**.

Windows cannot hand off audio the way Apple devices do. This app can release the PC link so iPhone can take it. It will not grab the link back while iPhone is using it.

## ✨ Features

- Resident card: battery, in-ear, and link state. No tray icon, no taskbar button.
- **Connect** uses the PC for audio. Once connected, the button becomes **Release** and yields the link to iPhone.
- Right-click opens a menu beside the card: refresh devices, large/compact, opacity, quit.
- Drag to a work-area edge and the card collapses to a strip. Hover expands the full card; leave and it collapses again.
- No paired AirPods: a prompt opens Windows Bluetooth settings. One pair binds automatically. Several pairs use a dropdown.
- Disconnects when every bud is out. One bud still in the ear keeps the link. Models with unreliable ear detection (Pro 2 / Pro 3) and AirPods Max do not auto-disconnect for this.
- Starts with Windows by default. Hotkeys stay available.

## 🧰 Tech stack

| Layer | Choice |
| --- | --- |
| Runtime | .NET 8 (`net8.0-windows10.0.19041.0`) |
| Language | C# |
| UI | WinUI 3, unpackaged x64 |
| Platform | Windows App SDK 1.8, self-contained |
| Minimum OS | Windows 10 2004 (build 19041) |
| Tests | xUnit 2.5 |
| Layout | `AirPodsWinTray` app + `AirPodsWinTray.Core` class library |

Third-party packages:

- [Microsoft.WindowsAppSDK 1.8](https://www.nuget.org/packages/Microsoft.WindowsAppSDK) (1.8.260921001)
- [Microsoft.Windows.SDK.BuildTools](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools) (10.0.28000.2957)
- [Microsoft.Win32.SystemEvents](https://www.nuget.org/packages/Microsoft.Win32.SystemEvents) (8.0.0)
- [xUnit](https://xunit.net/) 2.5.3, [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk) 17.8.0

## 💻 System requirements

- Windows 10 2004 or Windows 11, x64. 32-bit and ARM64 are not built.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build. Running the built exe also needs the .NET 8 runtime.
- AirPods already paired with this PC.

## 🛠️ Build and test

```powershell
dotnet build src/AirPodsWinTray/AirPodsWinTray.csproj -c Release
dotnet test tests/AirPodsWinTray.Tests
```

Release output: `src/AirPodsWinTray/bin/x64/Release/net8.0-windows10.0.19041.0/`.

## 🚀 Usage

Run `AirPodsWinTray.exe`. Drag the card; it docks when it meets a screen edge.

```powershell
AirPodsWinTray.exe list
AirPodsWinTray.exe connect "AirPods"
AirPodsWinTray.exe disconnect "AirPods"
```

### Hotkeys

| Action | Default |
| --- | --- |
| Connect | Ctrl+Alt+P |
| Disconnect | Ctrl+Alt+Shift+P |
| Release to iPhone | Ctrl+Alt+Shift+R |

### Configuration

`%LocalAppData%\AirPodsWinTray\config.json`

Startup, pause on ear removal, disconnect when all buds are out, lock/idle disconnect, connect when media plays, and the card position, size, and opacity. This build has no settings page; those policies live in the file.

### Audio quality

For music, turn off **Hands-Free Telephony** in Control Panel → Devices and Printers → AirPods → Services. Sound and connect speed improve. The PC can no longer use the AirPods microphone.

## 🤝 Contribute

This is a test build. The most useful help right now is a bug report.

- [Open an issue](https://github.com/zyc295001116/airpods-win/issues/new) for a bug or a missing behavior.
- [Open a pull request](https://github.com/zyc295001116/airpods-win/compare) for a fix or a doc change.

Please include the Windows version, the AirPods model, what you did, and what happened. For a crash or a failed connect, attach `%LocalAppData%\AirPodsWinTray\config.json` with device ids redacted.

Keep `README.md` and `README-CN.md` aligned: same section order, features, commands, links, and credits.

## 🍺 Credits

- [ToothTray](https://github.com/m2jean/ToothTray) — connect/disconnect via `KSPROPERTY_ONESHOT`. Original notice and [LICENSE.md](https://github.com/m2jean/ToothTray/blob/master/LICENSE.md) stay with that work.
- [OpenPods](https://github.com/adolfintel/OpenPods) — BLE advertisement parsing.
- This project: MIT, see [LICENSE](LICENSE).
