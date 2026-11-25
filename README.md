# AirPodsWinTray

Windows 11 上的轻量化 AirPods 伴侣：托盘一键连断、BLE 电量/开盖提示、入耳暂停、与 iPhone 共用的让路策略。

基于 [ToothTray](https://github.com/m2jean/ToothTray) 的 `KSPROPERTY_ONESHOT` 连断思路与 [OpenPods](https://github.com/adolfintel/OpenPods) 广播解析。默认以 **AirPods 3 (`0x1320`)** 验收，注册表覆盖全系 AirPods。

## 要求

- Windows 10 1809+ / Windows 11，x64
- 已与 PC 配对的 AirPods

## 构建

```powershell
dotnet build src/AirPodsWinTray/AirPodsWinTray.csproj -c Release
dotnet test tests/AirPodsWinTray.Tests
```

## 使用

- 双击运行：托盘图标 → **连接** / **断开（让路 iPhone）**
- CLI（阶段 0 spike）：

```powershell
AirPodsWinTray.exe list
AirPodsWinTray.exe connect "AirPods"
AirPodsWinTray.exe disconnect "AirPods"
```

## 配置

`%LocalAppData%\AirPodsWinTray\config.json`：`boundContainerName`、热键、自动连断策略等。

## 热键（默认）

| 动作 | 组合 |
|------|------|
| 连接 | Ctrl+Alt+P |
| 断开 | Ctrl+Alt+Shift+P |
| 让路 iPhone | Ctrl+Alt+Shift+R |

## 系统建议

音乐为主时禁用 **Hands-Free Telephony**（控制面板 → 设备和打印机 → AirPods → 服务），可改善音质与连接速度；此时 PC 不能使用 AirPods 麦克风。

## 许可

- 本项目：MIT（见 LICENSE）
- ToothTray 逻辑移植：保留原项目署名与 [LICENSE.md](https://github.com/m2jean/ToothTray/blob/master/LICENSE.md)