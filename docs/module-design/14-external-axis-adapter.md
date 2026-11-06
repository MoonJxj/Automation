# 外部轴适配者接入指南

> 面向“拿到框架后适配自己设备”的开发者。  
> 目标：只依赖 Core 就能实现轴控制器，不被迫引入 Modbus/SqlSugar/Serilog。

## 1. 依赖边界

```text
你的设备适配项目
    │  只引用
    ▼
EquipmentMonitor.Core
    │  内部引用
    ▼
EquipmentMonitor.Common（Result、配置/日志等基础类型）
```

`EquipmentMonitor.Infrastructure` 是**参考实现**（Sim、Modbus、雷赛/固高、JSON、DI），不是接入必需的。

参考样例：

- [SampleAxisController.cs](../../samples/EquipmentMonitor.AxisAdapter.Sample/SampleAxisController.cs)
- [EquipmentMonitor.AxisAdapter.Sample.csproj](../../samples/EquipmentMonitor.AxisAdapter.Sample/EquipmentMonitor.AxisAdapter.Sample.csproj)

样例项目只引用 Core；它能在解决方案里编译，说明外部适配器不需要 Infrastructure。

## 2. 必须实现什么

第一版只需要实现：

```csharp
EquipmentMonitor.Core.Abstractions.IAxisController
```

它继承 `IControllerBase`：

- `Initialize()` / `IsInitialize` / `ControllerCount`
- `Dispose()`
- 轴命令：`ServeOn` / `ServeOff` / `Home` / `MoveAbs` / `MoveRel` / `MoveInPos` / `Jog` / `Stop` / `ResetAxis` / `ResetAll`
- 状态读取：`GetPos` / `GetVel` / `GetTorque` / `ReadStateWord`
- 失败信息：`GetLastError`

可选能力：

```csharp
EquipmentMonitor.Core.Abstractions.IAxisParameterController
```

用于参数读写；不实现时，参数只能保存配置，不能在线下发。

## 3. 契约语义

### 命令是“已下发”，不是“已到位”

`MoveAbs` / `MoveRel` 返回 `true` 只表示命令已成功写入控制器/PLC；  
到位、故障、回零完成由 `AxisElement.RefreshAsync` 读取状态判断。

不要在 `MoveAbs` 里同步等待到位，也不要把“写成功”当“已完成”。

### 失败语义

- 预期失败返回 `false`，并把原因写入 `GetLastError(controllerNo, axisNo)`；
- 无法用 `false` 表达的错误（离线、超时、从站异常）可以抛
  `EquipmentMonitor.Core.Models.AxisControllerException`，由 `AxisElement` 统一转成 `Result.Fail`；
- 不要在适配器里 `throw` 一个 Infrastructure 内部异常类型。

### 线程

`IAxisController` 是同步厂商契约。调用方（`AxisElement`）会把命令/刷新放到后台线程；
适配器内部不要再把同步等待放到 UI 线程。

### 状态字

`ReadStateWord` 的 `type` 取：

- `AxisStateWordTypes.StateWord`
- `AxisStateWordTypes.FaultCode`

状态字位定义见 `EquipmentMonitor.Core.Enums.AxisStatusWord`。

## 4. 最小接线

```csharp
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Models;

var axis = new AxisElement(
    new AxisElementConfig
    {
        ElementId = "Axis-0-0",
        Name = "X 轴",
        ControllerNo = 0,
        AxisNo = 0,
        Unit = "mm",
        DefaultVelocity = 10
    },
    new SampleAxisController(),
    permissions,
    audit);

await axis.InitializeAsync();
await axis.MoveAbsAsync(100);
```

`permissions` / `audit` 是 Core 的 `IPermissionService` / `IAuditService`；  
外部宿主可以自己实现最小版本，或复用 Infrastructure 的本地实现（如果愿意引入参考实现）。

## 5. 什么时候需要 Infrastructure

以下能力在 Infrastructure，按需引用：

| 能力 | 位置 |
|---|---|
| 模拟轴/模拟 IO | `SimulatedAxisController` / `SimulatedIoController` |
| Modbus 寄存器与轴 | `Hardware/PLC/Modbus` |
| 雷赛/固高适配 | `Hardware/Card` |
| JSON 配置 + 装配 | `AxisFrameworkOptions` / `AxisFrameworkLoader` / `AxisFrameworkBootstrapper` |
| DI 注册 | `InfrastructureModuleRegister` |

如果你只实现一台自定义设备，建议先走“Core + 自己的 `IAxisController` + 自己的宿主装配”，
不要为了用加载器而引入整个 Infrastructure。

## 6. 参数设计

参数描述符在 Core：

```csharp
var descriptors = EquipmentMonitor.Core.Axis.AxisParameterCatalog.Create();
```

参数服务契约在 Core：

- `IAxisParameterService`
- `IElementParameterStore`
- `IAxisParameterController`

注意：**服务与 JSON 存储实现尚未落地**；当前只有契约、描述符和样例控制器。
如果设备参数需要在线读写，先实现 `IAxisParameterController`，再等 `IAxisParameterService` 实现。

## 7. 接入检查清单

- [ ] 适配项目只引用 Core（或 Core + 自己的依赖），不引用 Infrastructure；
- [ ] `IAxisController.Initialize` 可重复调用，失败返回 `false`；
- [ ] 每个失败都写入 `GetLastError`，或抛 `AxisControllerException`；
- [ ] 命令返回 `true` 表示“已下发”，不表示“已到位”；
- [ ] `ReadStateWord` 支持 `StateWord` / `FaultCode`；
- [ ] `Dispose` 可重复调用，释放自己的连接/句柄；
- [ ] 有一个不依赖真实硬件的 fake/模拟实现，用于宿主的单元测试；
- [ ] 不把厂商 DLL 的异常类型泄漏到 Core。

## 8. 待补

- `IAxisParameterService` / `JsonAxisParameterStore` 尚无实现；
- 配置 DTO 仍复用 `AxisConfig`（带 SqlSugar 特性）；外部项目若不用数据库，建议直接使用
  `AxisElementConfig` 或自己维护配置；
- 没有正式的 NuGet 打包/版本策略；当前按源码/项目引用接入。
