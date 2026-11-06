# 轴参数设计（接口设计）

> 状态：**接口设计**（2026-11-01）。本文定义轴参数设计功能的契约与边界；
> 实现（JSON 存储、参数目录、控制器能力）按 §8 分阶段落地。

## 1. 背景与缺口

当前轴控框架已经有：

- `IElementAxis`：命令（使能/回零/定位/点动/停止/复位）与状态快照；
- `AxisElementConfig` / `AxisConfig`：启动时的默认速度、加减速度、软限位、缩放等字段；
- `IAxisController`：厂商控制卡/PLC 的运动命令；
- `IElementCommandTarget`：命令表与命令参数（本次动作的参数）；
- `IAxisParameterService` 等本文件定义的接口（尚未实现）。

缺少的是**轴参数设计**：

- 没有“轴参数目录”，UI 无法知道一根轴有哪些可配置项、单位/范围/权限/生效方式；
- 没有参数读取/校验/保存/下发入口，`AxisConfig` 只能手改 JSON；
- `IAxisController` 没有参数读写能力，`AxisElement` 只把默认值用于显示，改配置不会自动下发；
- 没有参数变更审计与权限区分（设备参数 vs 工艺参数）。

## 2. 目标与非目标

### 目标

1. 以**描述符**统一描述轴参数，UI 按描述符生成参数页，不手写每根轴的表单；
2. 支持读取配置值、读取控制器回读值、校验、保存、下发；
3. 区分**设备参数**（地址、缩放、软限位、回零方式）与**工艺参数**（默认速度/加速度等），分别校验 `MachineConfig` / `ProcessConfig`；
4. 明确参数的生效方式：立即、下一条命令、重启；
5. 参数变更可审计；失败可解释（哪个键、为什么）；
6. 不破坏 `IAxisController` 的厂商同步契约：参数能力通过独立接口探测。

### 非目标

- 不在本设计里做驱动器整定/PID 自整定；
- 不替代 PLC/轴卡的实时软限位、急停与安全回路；
- 不把参数值直接写进 UI 控件；UI 只依赖描述符与服务接口。

## 3. 分层

```text
Desktop / 参数设计页
        │
        ▼
IAxisParameterService                业务入口：目录、读、校验、保存/下发、审计
        │
        ├─ IElementParameterStore       持久化端口（JSON / SQLite / 远端）
        │
        └─ IAxisParameterController  可选控制器能力（读/写参数）
                │
                ▼
        IAxisController / 厂商 DLL / PLC 寄存器
```

`IAxisParameterService` 在 Core；实现放 Infrastructure。UI 不直接读 `axis-framework.json`，
也不直接调用 `IAxisController`。

### 3.1 为什么是三个接口，但不是三个 UI 入口

这三个接口服务于不同调用方，生命周期和变化原因都不同：

| 接口 | 主要调用方 | 典型生命周期 | 变化原因 |
|---|---|---|---|
| `IAxisParameterService` | UI / 业务 | Singleton 或 Scoped（宿主/会话） | 权限、校验、审计、保存与下发的编排 |
| `IElementParameterStore` | Service 实现 | Singleton（文件/数据库/缓存） | JSON/SQLite/远端格式、原子写、版本冲突 |
| `IAxisParameterController` | Service 实现 | 每个 PLC/连接一个实例 | 厂商 DLL/寄存器/参数映射 |

它们不是“三个平级入口”：UI 只依赖 `IAxisParameterService`；
存储与控制器是服务实现的下游端口。接口拆开是为了隔离生命周期和变化原因，
不是为了增加 UI 可见的入口。

如果项目只做 JSON 单一存储、且没有第三方控制器插件，`IElementParameterStore` /
`IAxisParameterController` 也可以收成 Infrastructure 内部接口；但只要出现第二种存储
或外部控制器实现，公共端口能让替换不碰 UI 与服务实现。当前按三端口设计，
后续可根据真实实现数量缩减。

## 4. 核心契约

### 4.1 参数描述符

`ElementParameterDescriptor`（`Core/Models`）：

- `Key`：参数键，取 `AxisParameterKeys` 常量；
- `Name` / `Group`：显示名与分组；
- `ValueKind`：Bool / Number / Text（复用 `CommandValueKind`）；
- `Unit` / `Precision` / `Minimum` / `Maximum` / `DefaultValue` / `Choices`；
- `IsReadOnly`：只读参数（例如控制器回读项）；
- `Scope`：`Device` / `Process` / `Controller`；
- `RequiredPermission`：修改该参数所需权限；
- `ApplyMode`：`Immediate` / `NextCommand` / `Restart`；
- `Description`。

描述符只描述形状，不承载值；写错（范围颠倒、缺省类型不符）在构造参数目录时抛异常。

### 4.2 参数值

`ElementParameterSet`：

```csharp
public sealed record ElementParameterSet
{
    public required string ElementId { get; init; }
    public required IReadOnlyDictionary<string, CommandValue> Values { get; init; }
    public string? Version { get; init; }   // 乐观并发
}
```

- 复用 `CommandValue`，不引入 `object`；
- `ElementId` 必须稳定；当前生成值 `Axis-{controllerNo}-{axisNo}` 会随控制器编号变化漂移，
  **建议给 `AxisConfig` 增加独立的 `ElementId` 列**，不要拿生成值当持久化主键；
- `Version` 用于保存冲突检测；存储实现发现版本不一致时返回失败，不覆盖。

### 4.3 快照与结果

- `ElementParameterSnapshot`：`ElementId`、`DisplayName`、`Descriptors`、`Values`、`ControllerValues`（可选）、`ReadAt`；
- `ElementParameterValidationIssue`：`Key` + `Code` + `Message`；
- `ElementParameterValidationResult`：问题列表，`IsValid`；
- `ElementParameterChangeResult`：更新后的值、变化键、是否保存、是否下发、是否需要重启。

### 4.4 服务接口

```csharp
public interface IAxisParameterService
{
    Task<Result<ElementParameterSnapshot>> GetAsync(string axisId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ElementParameterSnapshot>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<ElementParameterValidationResult>> ValidateAsync(
        string axisId, ElementParameterSet proposed, CancellationToken ct = default);
    Task<Result<ElementParameterChangeResult>> UpdateAsync(
        string axisId, ElementParameterSet proposed, ParameterUpdateMode mode,
        CancellationToken ct = default);
}
```

`ParameterUpdateMode`：

| 模式 | 行为 |
|---|---|
| `SaveOnly` | 只保存配置；不碰控制器 |
| `ApplyOnly` | 只写控制器；不保存（调试用，重启回到配置值） |
| `SaveAndApply` | 先写控制器，成功后保存配置；控制器失败则不保存 |

`ParameterApplyMode` 决定是否需要控制器能力：

- `Immediate` / `NextCommand`：必须实现 `IAxisParameterController`，否则 `ApplyOnly` / `SaveAndApply` 返回失败；
- `Restart`：只允许 `SaveOnly`（或 `SaveAndApply` 但不写硬件），返回 `RestartRequired = true`。

### 4.5 控制器能力

`IAxisParameterController` 是**可选**能力，放在 Core/Abstractions（公共端口）：

```csharp
public interface IAxisParameterController
{
    IReadOnlySet<string> SupportedParameterKeys { get; }
    Task<Result<IReadOnlyDictionary<string, CommandValue>>> ReadParametersAsync(
        int controllerNo, int axisNo, CancellationToken ct = default);
    Task<Result> WriteParametersAsync(
        int controllerNo, int axisNo,
        IReadOnlyDictionary<string, CommandValue> values,
        CancellationToken ct = default);
}
```

不塞进 `IAxisController` 的原因：

- 雷赛/固高/Modbus 的参数能力差异大；
- 不支持参数的控制器不应被迫实现一堆 `NotSupportedException`；
- 服务可以按 `SupportedParameterKeys` 只下发支持的键；
- 生命周期按 PLC/连接实例，而服务是宿主级单例。

### 4.6 持久化

`IElementParameterStore`（Core/Abstractions）：

```csharp
Task<Result<ElementParameterSet>> LoadAsync(string axisId, CancellationToken ct = default);
Task<Result<IReadOnlyList<ElementParameterSet>>> LoadAllAsync(CancellationToken ct = default);
Task<Result> SaveAsync(ElementParameterSet values, CancellationToken ct = default);
```

- 通常注册为单例（文件/数据库/缓存是进程级资源）；
- JSON 实现写临时文件再原子替换；
- 保存时校验 `ElementParameterSet.Version`，冲突返回 `AXIS_PARAM_VERSION_CONFLICT`；
- 读取到不存在的轴返回 `AXIS_PARAM_AXIS_NOT_FOUND`。

## 5. 参数目录（当前轴参数）

参数目录在 `Core/Axis/AxisParameterCatalog`，不依赖 `AxisConfig`（SqlSugar 实体）；`Create()` 返回内置描述符。
键名用 `AxisParameterKeys`。

| 分组 | 参数键 | 类型 | 单位 | Scope | 权限 | ApplyMode |
|---|---|---|---|---|---|---|
| 身份 | `Name` | Text | — | Device | `MachineConfig` | Restart |
| 身份 | `Unit` | Text | — | Device | `MachineConfig` | Restart |
| 身份 | `StationId` | Number | — | Device | `MachineConfig` | Restart |
| 身份 | `IsEnabled` | Bool | — | Device | `MachineConfig` | Restart |
| 运动 | `DefaultVelocity` | Number | mm/s | Process | `ProcessConfig` | NextCommand |
| 运动 | `Acceleration` | Number | mm/s² | Process | `ProcessConfig` | NextCommand |
| 运动 | `Deceleration` | Number | mm/s² | Process | `ProcessConfig` | NextCommand |
| 运动 | `Jerk` | Number | mm/s³ | Process | `ProcessConfig` | NextCommand |
| 限位 | `MaxVelocity` | Number | mm/s | Device | `MachineConfig` | Immediate |
| 限位 | `SoftLimitMin` | Number | mm | Device | `MachineConfig` | Immediate |
| 限位 | `SoftLimitMax` | Number | mm | Device | `MachineConfig` | Immediate |
| 回零 | `HomeMethod` | Number | — | Device | `MachineConfig` | Immediate |
| 缩放 | `PositionScale` | Number | — | Device | `MachineConfig` | Restart |
| 缩放 | `VelocityScale` | Number | — | Device | `MachineConfig` | Restart |
| 缩放 | `TorqueScale` | Number | — | Device | `MachineConfig` | Restart |
| 缩放 | `AccelerationScale` | Number | — | Device | `MachineConfig` | Restart |
| 缩放 | `DecelerationScale` | Number | — | Device | `MachineConfig` | Restart |
| 缩放 | `JerkScale` | Number | — | Device | `MachineConfig` | Restart |
| 地址 | `StateStartAddress` | Number | — | Device | `MachineConfig` | Restart |
| 地址 | `CommandStartAddress` | Number | — | Device | `MachineConfig` | Restart |
| 地址 | `ExecuteCoilAddress` | Number | — | Device | `MachineConfig` | Restart |

说明：

- **软限位仍由轴卡/PLC 兜底**。参数服务把软限位写进控制器/PLC，不是让 `AxisElement` 用 0.1s 轮询去拦截；
- `DefaultVelocity` / `Acceleration` 等当前 `AxisElement` 只用于显示；真正生效需要控制器实现
  `IAxisParameterController`（写 PLC 寄存器/厂商 DLL 参数），或者重启后由控制器映射读取 `AxisConfig`；
- `Restart` 参数（地址/缩放）会改变运行时映射，必须重新装配轴元件；UI 要明确提示“重启后生效”。

轴专属的跨字段校验在 `AxisParameterValidator`（速度/加速度/软限位/缩放），不放进通用的
`ElementParameterDescriptor`；气缸有自己的 `CylinderParameterValidator`。

## 6. 更新流程

```text
1. GetAsync(axisId)
   → 读配置值 + 读控制器值（若支持）
   → 返回描述符/值/版本

2. UI 编辑
   → 按描述符生成控件
   → 只允许编辑 HasPermission(descriptor.RequiredPermission) 的字段

3. ValidateAsync(axisId, proposed)
   → 类型/范围/默认值
   → 跨字段：SoftLimitMin < SoftLimitMax；DefaultVelocity ≤ MaxVelocity；
     Acceleration/Deceleration > 0；Jerk ≥ 0；Scale > 0；地址范围
   → 任何问题都返回列表，不抛异常

4. UpdateAsync(axisId, proposed, mode)
   → 权限：对每个变化键检查 RequiredPermission（取最严）
   → SaveAndApply：先写控制器（仅支持键），成功后保存；失败返回失败
   → ApplyOnly：只写控制器
   → SaveOnly：只保存
   → 审计：参数键/旧值/新值/模式/结果；不记录凭据
   → 结果：ChangedKeys、Persisted、AppliedToController、RestartRequired
```

### 6.1 失败语义

- 校验不通过：`Result.Fail`，错误码 `AXIS_PARAM_VALIDATION_FAILED`，消息带问题列表；
- 权限不足：`UnauthorizedAccessException`（与项目权限约定一致），由 UI 兜底；
- 控制器不支持：`Result.Fail`，错误码 `AXIS_PARAM_CONTROLLER_UNSUPPORTED`；
- 保存冲突：`Result.Fail`，错误码 `AXIS_PARAM_VERSION_CONFLICT`；
- 控制器成功但保存失败：`Result.Fail`，错误码 `AXIS_PARAM_SAVE_FAILED`，消息明确“已下发、未落盘”。

## 7. 权限与审计

| 动作 | 权限 |
|---|---|
| 查看参数 | `MachineView` |
| 修改运动默认值 | `ProcessConfig` |
| 修改地址/缩放/软限位/回零方式 | `MachineConfig` |
| 修改控制器专属参数 | `MachineConfig`（具体由描述符声明） |

审计事件建议：

- `AXIS_PARAMETER_UPDATED`（成功；含轴、键、旧值/新值、模式）；
- `AXIS_PARAMETER_REJECTED`（校验/权限/控制器拒绝）；
- `AXIS_PARAMETER_SAVE_FAILED`（已下发但未落盘）。

## 8. 实现阶段

| 阶段 | 内容 |
|---|---|
| P0 | `ElementParameterDescriptor` / `ElementParameterSet` / `IAxisParameterService` / `IElementParameterStore` / `IAxisParameterController` 契约（本文） |
| P1 | `AxisParameterCatalog`（已落 Core，无 `AxisConfig` 依赖）；`JsonAxisParameterStore` 原子读写 `axis-framework.json`；`AxisParameterService` 的 Get/Validate/SaveOnly |
| P2 | `UpdateAsync` 的 `ApplyOnly` / `SaveAndApply`；`IAxisParameterController` 的模拟实现与测试 |
| P3 | 参数设计页（Desktop）；按描述符生成控件；权限显隐/二次确认；重启提示 | **模板已交付（2026-11-02）**：`ParameterSettingsPage` + `ParameterEditorControl`；当前用 `TemplateParameterPageSource` 内存示例，真实轴参数服务接入后替换 `IParameterPageSource` |
| P4 | 控制器专属参数（增益、编码器分辨率等）与地址级预演 |

## 9. 待确认

1. **稳定轴标识**：是否给 `AxisConfig` 增加 `ElementId`（推荐），还是用 `AxisConfig.Id` 作为 `ElementId`？
2. **运行时生效**：`DefaultVelocity` 等参数是只写控制器，还是要同时更新 `AxisElement` 的运行时配置（涉及重建命令表/状态快照）？
3. **重启语义**：地址/缩放改动后，UI 是自动重启宿主，还是只提示“重启生效”？
4. **控制器参数范围**：第一版是否只做 `AxisConfig` 已有字段，还是同时接入厂商 DLL 的增益/分辨率？

## 10. 测试计划

- 描述符校验：范围颠倒、缺省类型不符、Choices 用于非 Text 时抛异常；
- `ElementParameterSet`：`WithValue`/`WithoutValue` 不修改原集合；
- 服务：权限（设备/工艺分离）、校验问题列表、SaveOnly 不碰控制器、
  ApplyOnly 不写配置、SaveAndApply 的失败顺序、版本冲突；
- 存储：原子替换、并发版本冲突、未知参数键拒绝；
- UI：按描述符生成控件、权限禁用、Restart 提示（后续）。
