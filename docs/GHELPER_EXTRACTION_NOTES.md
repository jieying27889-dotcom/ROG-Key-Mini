# G-Helper 抽离记录

## 当前状态

已完成当前 `P0` 范围内的最小抽离方向确认与本地落地：

1. `ScreenBrightnessService`
   - 已使用 `WMI` 路径实现屏幕亮度降低。
   - 当前本地实现依赖：
     - `WmiMonitorBrightness`
     - `WmiMonitorBrightnessMethods.WmiSetBrightness`

2. `AsusAcpiService`
   - 已接入 `\\.\ATKACPI` 设备路径。
   - 已接入最小 `CreateFile + DeviceIoControl + CloseHandle` 调用链。
   - 当前本地实现尝试：
     - `UniversalControl = 0x00100021`
     - `KB_Light_Down = 0x00C5`

3. `AsusHidService`
   - 已接入最小 `HID` 后备路径。
   - 当前本地实现使用：
     - `INPUT_ID = 0x5A`
     - primer: `ASUS Tech.Inc.`
     - brightness payload: `[0x5A, 0xBA, 0xC5, 0xC4, level]`
   - 执行策略为：
     - 已知 `Aura PID` 优先
     - 广义 `ASUS 0x5A` 后备
     - 首个成功即停止

## 当前风险

1. 本机仍缺少 `.NET SDK`，还没有做本地构建验证。
2. `ACPI` 路径和 `HID` 路径都还缺目标机器实机验证。
3. `HID` 路径当前使用的是估算背光等级，不是硬件回读值。

## 下一步优先关注

1. 具备 `.NET SDK` 后先完成首次构建验证。
2. 在 `2021 G14` 实机上确认：
   - `ACPI` 是否真实生效；
   - `HID` 是否真实生效；
   - 哪条路径更适合作为实际主路径。
3. 如后续继续参考 `G-Helper`，优先只查看与：
   - `AsusACPI.cs`
   - `USB/AsusHid.cs`
   - `USB/Aura.cs`
   直接相关的最小逻辑，不扩散到风扇、电源模式或其他模块。

## 记录规则

每次继续正式抽离或重新核对时补充：

1. 原始文件路径
2. 抽离目的
3. 保留方法
4. 删除逻辑
5. 重命名情况
6. 本地验证结果
