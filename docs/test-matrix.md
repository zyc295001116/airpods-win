# 测试矩阵

## AirPods 3（标杆）

| 场景 | 期望 |
|------|------|
| BLE 识别 | `0x1320` |
| 托盘连接 | A2DP 出声 |
| 托盘断开 | iPhone 可接管 |
| 入耳摘下 | 媒体暂停（开启时） |

## 其他 AirPods

在无真机时通过 `AppleProximityParserTests` fixture 验证型号注册表；有真机时补手工项。