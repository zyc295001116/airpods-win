# AirPodsWinTray

Windows 11 上的 AirPods 浮窗伴侣。常驻一张小卡片，显示电量和入耳状态，一键连接，或把音频让给 iPhone。

**当前是 0.1.0 测试版**，功能还在收。用起来不对、闪退、某型号识别错了，请直接提 [Issue](https://github.com/zyc295001116/airpods-win/issues/new)。

连断思路来自 [ToothTray](https://github.com/m2jean/ToothTray) 的 `KSPROPERTY_ONESHOT`，电量广播解析参考 [OpenPods](https://github.com/adolfintel/OpenPods)。型号表覆盖全系 AirPods，真机主要在 **AirPods 3（`0x1320`）** 上试过。

## 反馈

提 Issue 时写上这些，会快很多：

- Windows 版本，以及 AirPods 型号
- 做了什么、实际发生了什么
- 如果是闪退或连不上，附上 `%LocalAppData%\AirPodsWinTray\config.json`（可打码设备 ID）

## 能做什么

- 常驻浮窗：电量、入耳、连接状态。不进任务栏，不显示托盘图标
- **连接** 使用电脑音频；已连接时按钮变为 **让路**，把链路让给 iPhone
- 右键菜单在浮窗外侧：刷新设备、大窗/小窗、透明度、退出
- 拖到工作区边缘会收成一条，鼠标移上去展开整张卡片，移开再收起
- 没有配对过会提示去系统蓝牙设置；只配对一副会自动绑上；多副用下拉里切换
- 两只都不在耳时断开音频（单只还在耳则保持）。入耳检测不可靠的型号（Pro 2 / Pro 3）和 AirPods Max 不会因此自动断开
- 默认开机启动。热键仍可用

Windows **不能**像苹果设备之间那样秒切。电脑可以主动放开音频，让 iPhone 接过去；iPhone 正在用的时候，电脑不会自动抢回来。

## 要求

- Windows 10 1809 或 Windows 11，x64
- [.NET 8 运行时](https://dotnet.microsoft.com/download/dotnet/8.0)（自包含发布包除外）
- AirPods 已经和这台电脑配对

## 构建

```powershell
dotnet build src/AirPodsWinTray/AirPodsWinTray.csproj -c Release
dotnet test tests/AirPodsWinTray.Tests
```

Release 输出在 `src/AirPodsWinTray/bin/x64/Release/net8.0-windows10.0.19041.0/`。

## 使用

双击 `AirPodsWinTray.exe`。浮窗可以拖动；贴边会收起。

命令行：

```powershell
AirPodsWinTray.exe list
AirPodsWinTray.exe connect "AirPods"
AirPodsWinTray.exe disconnect "AirPods"
```

## 热键

| 动作 | 默认组合 |
|------|----------|
| 连接 | Ctrl+Alt+P |
| 断开 | Ctrl+Alt+Shift+P |
| 让路 iPhone | Ctrl+Alt+Shift+R |

## 配置

`%LocalAppData%\AirPodsWinTray\config.json`

常用项：开机启动、入耳暂停、全部出耳后断开、锁屏/空闲断开、播放时自动连接、浮窗位置和透明度。这版没有设置页，策略写在这个文件里。

## 音质

听音乐时，建议在「控制面板 → 设备和打印机 → AirPods → 服务」里关掉 **Hands-Free Telephony**。音质更好，连接也更快。关掉之后，这台电脑不能再用 AirPods 当麦克风。

## 许可

- 本项目：MIT（见 [LICENSE](LICENSE)）
- ToothTray 逻辑移植：保留原项目署名与 [LICENSE.md](https://github.com/m2jean/ToothTray/blob/master/LICENSE.md)
