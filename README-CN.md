<div align="center">

# AirPodsWinTray

Windows 上的 AirPods 常驻浮窗。显示电量和入耳状态，一键连接，或把音频让给 iPhone。

[![.NET](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![C#](https://img.shields.io/badge/C%23-12-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![WinUI](https://img.shields.io/badge/WinUI-3-0078D4?logo=windows&logoColor=white)](https://learn.microsoft.com/windows/apps/winui/)
[![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-0078D4)](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

🌎 [English](README.md) | 🌏 简体中文

</div>

**0.1.0 测试版**，功能还在收。用起来不对、闪退、某型号识别错了，请直接提 [Issue](https://github.com/zyc295001116/airpods-win/issues/new)。

连断思路来自 [ToothTray](https://github.com/m2jean/ToothTray) 的 `KSPROPERTY_ONESHOT`，电量广播解析参考 [OpenPods](https://github.com/adolfintel/OpenPods)。型号表覆盖全系 AirPods，真机主要在 **AirPods 3（`0x1320`）** 上试过。

Windows **不能**像苹果设备之间那样秒切。电脑可以主动放开音频，让 iPhone 接过去；iPhone 正在用的时候，电脑不会自动抢回来。

## ✨ 特性

- 常驻浮窗：电量、入耳、连接状态。不进任务栏，不显示托盘图标。
- **连接** 使用电脑音频；已连接时按钮变为 **让路**，把链路让给 iPhone。
- 右键菜单在浮窗外侧：刷新设备、大窗/小窗、透明度、退出。
- 拖到工作区边缘会收成一条，鼠标移上去展开整张卡片，移开再收起。
- 没有配对过会提示去系统蓝牙设置；只配对一副会自动绑上；多副用下拉里切换。
- 两只都不在耳时断开音频，单只还在耳则保持。入耳检测不可靠的型号（Pro 2 / Pro 3）和 AirPods Max 不会因此自动断开。
- 默认开机启动。热键仍可用。

## 🧰 技术栈

| 层 | 选择 |
| --- | --- |
| 运行时 | .NET 8（`net8.0-windows10.0.19041.0`） |
| 语言 | C# |
| 界面 | WinUI 3，非打包 x64 |
| 平台 | Windows App SDK 1.8，自包含 |
| 最低系统 | Windows 10 2004（版本 19041） |
| 测试 | xUnit 2.5 |
| 结构 | `AirPodsWinTray` 应用 + `AirPodsWinTray.Core` 类库 |

第三方包：

- [Microsoft.WindowsAppSDK 1.8](https://www.nuget.org/packages/Microsoft.WindowsAppSDK)（1.8.260921001）
- [Microsoft.Windows.SDK.BuildTools](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools)（10.0.28000.2957）
- [Microsoft.Win32.SystemEvents](https://www.nuget.org/packages/Microsoft.Win32.SystemEvents)（8.0.0）
- [xUnit](https://xunit.net/) 2.5.3、[Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk) 17.8.0

## 💻 系统要求

- Windows 10 2004 或 Windows 11，x64。未提供 32 位和 ARM64 构建。
- 构建需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。直接运行编出来的 exe 还需要 .NET 8 运行时。
- AirPods 已经和这台电脑配对。

## 🛠️ 构建与测试

```powershell
dotnet build src/AirPodsWinTray/AirPodsWinTray.csproj -c Release
dotnet test tests/AirPodsWinTray.Tests
```

Release 输出在 `src/AirPodsWinTray/bin/x64/Release/net8.0-windows10.0.19041.0/`。

## 🚀 使用

双击 `AirPodsWinTray.exe`。浮窗可以拖动，贴边会收起。

```powershell
AirPodsWinTray.exe list
AirPodsWinTray.exe connect "AirPods"
AirPodsWinTray.exe disconnect "AirPods"
```

### 热键

| 动作 | 默认组合 |
| --- | --- |
| 连接 | Ctrl+Alt+P |
| 断开 | Ctrl+Alt+Shift+P |
| 让路 iPhone | Ctrl+Alt+Shift+R |

### 配置

`%LocalAppData%\AirPodsWinTray\config.json`

开机启动、入耳暂停、全部出耳后断开、锁屏/空闲断开、播放时自动连接，以及浮窗位置、大小和透明度。这版没有设置页，策略写在这个文件里。

### 音质

听音乐时，建议在「控制面板 → 设备和打印机 → AirPods → 服务」里关掉 **Hands-Free Telephony**。音质更好，连接也更快。关掉之后，这台电脑不能再用 AirPods 当麦克风。

## 🤝 贡献

这是测试版，目前最有用的是把问题报回来。

- [打开 Issue](https://github.com/zyc295001116/airpods-win/issues/new)，报告错误或缺少的行为。
- [提交 PR](https://github.com/zyc295001116/airpods-win/compare)，修问题或改文档。

请写上 Windows 版本、AirPods 型号、做了什么、实际发生了什么。闪退或连不上时，附上 `%LocalAppData%\AirPodsWinTray\config.json`（可打码设备 ID）。

更新文档时，请让 `README.md` 和 `README-CN.md` 的章节顺序、功能、命令、链接和致谢保持一致。

## 🍺 致谢

- [ToothTray](https://github.com/m2jean/ToothTray)：用 `KSPROPERTY_ONESHOT` 做连断。移植部分保留原项目署名与 [LICENSE.md](https://github.com/m2jean/ToothTray/blob/master/LICENSE.md)。
- [OpenPods](https://github.com/adolfintel/OpenPods)：BLE 广播解析。
- 本项目：MIT，见 [LICENSE](LICENSE)。
