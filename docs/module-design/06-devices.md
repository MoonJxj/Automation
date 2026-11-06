# Devices 模块设计

> 项目：`EquipmentMonitor.Devices` ｜ 依赖：`Core`、`Communication`、`Shared`
> 选型：Stateless 5.x

## 1. 职责边界

**做什么**：把通讯层读到的**原始数值**翻译成**设备语义状态**；
元件注册表；轮询调度；状态机驱动与转换日志。

**不做什么**：

- ❌ 不直接操作串口/Socket（一律经 `ICommunicationChannel`）
- ❌ 不引用任何 UI 类型
- ❌ 不负责持久化（需要落库的数据通过 `Core` 的模型交给 `App` 转交 `Data`）

**本模块是整个系统的语义中枢**：通讯层不知道寄存器 40001 代表什么，
UI 需要的是"1号气缸已伸出"而非"寄存器 40001 值为 1"。这个翻译发生在这里。

## 2. 依赖关系

```
EquipmentMonitor.Devices ──► Core, Communication, Shared
```

## 3. 目录结构

```
EquipmentMonitor.Devices/
├── ComponentManager.cs
├── Elements/
│   ├── ServoElement.cs
│   ├── CylinderElement.cs
│   ├── SensorElement.cs
│   ├── MonitorElement.cs
│   └── Mapping/
│       ├── PointMapping.cs          # 点位映射：语义 → 通道地址
│       └── ValueConverter.cs        # 原始值 → 工程量（缩放、取位、枚举）
├── StateMachines/
│   ├── CylinderStateMachine.cs
│   ├── ServoStateMachine.cs
│   ├── SensorStateMachine.cs
│   ├── MonitorStateMachine.cs
│   └── StateTransitionLogger.cs
└── Polling/
    ├── DevicePollingService.cs
    ├── PollingGroup.cs              # 按通道分组的轮询单元
    └── PollingSchedule.cs           # 到期判定与批量请求合并
```

## 4. 双层状态模型

> 该设计的动机见 [Core 模块 §4.2](01-core.md#42-ideviceelement)，此处展开实现方式。

**问题**：气缸的"缩回/伸出中/伸出"与伺服的"未使能/使能待机/运动中/到位"
是完全不同的状态集合，但 UI 需要一个统一的颜色标记和报警聚合口径。

**方案**：

```
                     ┌──────────────────────────────┐
                     │  DeviceState（粗粒度，统一）    │
                     │  Offline / Idle / Running /   │
                     │  Alarm / Fault / Unknown      │
                     └──────────────▲───────────────┘
                                    │  MapToDeviceState()
                     ┌──────────────┴───────────────┐
                     │  元件特有状态（细粒度）          │
                     │  CylinderState / ServoState…  │
                     │  由各自的 Stateless 状态机维护  │
                     └──────────────────────────────┘
```

每个元件实现一个映射：

```csharp
// CylinderElement
protected override DeviceState MapToDeviceState(CylinderState detail) => detail switch
{
    CylinderState.Retracted    => DeviceState.Idle,
    CylinderState.Extending    => DeviceState.Running,
    CylinderState.Extended     => DeviceState.Idle,
    CylinderState.Retracting   => DeviceState.Running,
    CylinderState.BothOnAlarm  => DeviceState.Alarm,      // 两限位同时触发 = 传感器故障
    CylinderState.TimeoutFault => DeviceState.Fault,
    _                          => DeviceState.Unknown
};
```

**注意 `BothOnAlarm`**：气缸的两个限位同时触发在物理上不可能，
这是**传感器或接线故障的确定性信号**。这类"物理常识校验"是上位机的重要价值——
纯数值采集系统只会显示"两个限位都是 1"，不会告诉操作工这是故障。

## 5. 状态机

### 5.1 基类

```csharp
public abstract class DeviceStateMachine<TDetail> where TDetail : struct, Enum
{
    protected StateMachine<TDetail> Machine { get; }
    public TDetail CurrentDetail { get; }
    public DeviceState CurrentDeviceState { get; }

    public event EventHandler<DeviceStateChangedEventArgs>? StateChanged;

    protected abstract void Configure();                      // 派生类配置迁移
    protected abstract DeviceState MapToDeviceState(TDetail detail);

    public Result Fire<TTrigger>(TTrigger trigger) where TTrigger : struct, Enum;
}
```

### 5.2 迁移定义（示例：气缸）

```csharp
// CylinderStateMachine.Configure()
Machine.Configure(CylinderState.Retracted)
    .Permit(CylinderTrigger.ExtendSignal, CylinderState.Extending)
    .Permit(CylinderTrigger.FaultSignal,  CylinderState.TimeoutFault);

Machine.Configure(CylinderState.Extending)
    .Permit(CylinderTrigger.ExtendedLimit, CylinderState.Extended)
    .Permit(CylinderTrigger.RetractSignal, CylinderState.Retracting)
    .PermitReentry(CylinderTrigger.ExtendSignal)
    .OnEntry(() => _extendTimeoutTimer.Restart())
    .OnExit(() => _extendTimeoutTimer.Stop());

// 守卫条件：两限位同时为真 → 直接进入报警，不经过常规迁移
Machine.Configure(CylinderState.Extending)
    .PermitIf(CylinderTrigger.BothLimitsOn, CylinderState.BothOnAlarm, () => BothLimitsActive);
```

**Stateless 特性使用约定**：

| 特性 | 用途 | 本项目中的典型场景 |
|---|---|---|
| `Permit` | 常规迁移 | 限位触发导致状态变化 |
| `PermitIf` | 带守卫条件的迁移 | "到位"但需同时满足无报警 |
| `PermitReentry` | 自迁移（状态不变但重入） | 同方向重复触发，需重置超时计时器 |
| `Ignore` | 显式忽略某触发器 | 已缩回时再收到缩回信号 |
| `OnEntry` / `OnExit` | 进入/退出动作 | 启动/停止超时计时器 |
| `SubstateOf` | 层级状态 | 所有"运动中"状态共享超时逻辑 |

**始终配置超时**：气缸"伸出中"停留超过设定时间仍未到位，必须迁移到 `TimeoutFault`。
这是上位机对现场**最有价值的功能之一**——设备卡住时立即暴露，而不是一直停留在中间态。

### 5.3 状态转换日志

```csharp
public sealed class StateTransitionLogger
{
    // 订阅状态机的每一次迁移，统一记录：
    // {ElementName} 状态由 {OldState}({OldDetail}) 迁移至 {NewState}({NewDetail})，原因：{Reason}
}
```

**要求**：状态迁移日志使用 `Information` 级别（不是 `Debug`）——
现场排障时最常回溯的就是"设备是什么时候、因为什么进入故障的"。

### 5.4 元件特有状态参考

| 元件 | 特有状态 |
|---|---|
| `CylinderElement` | `Retracted` / `Extending` / `Extended` / `Retracting` / `BothOnAlarm` / `TimeoutFault` |
| `ServoElement` | `Disabled` / `Enabled` / `Homing` / `Moving` / `InPosition` / `Alarm` / `Fault` |
| `SensorElement` | `Off` / `On` / `Unstable`（信号抖动）/ `Fault` |
| `MonitorElement` | `Idle` / `Monitoring` / `Recording` / `Alarm` / `Fault` |

> `SensorElement` 的 `Unstable` 状态需要**去抖（debounce）**：
> 现场传感器在临界位置会产生毫秒级抖动，若直接映射为状态变化，
> 会导致事件风暴与日志刷屏。需实现"连续 N 次采样一致才认定变化"。

## 6. 轮询服务（性能关键）

### 6.1 为什么不能"每个元件各自轮询"

最朴素的做法是每个元件起一个定时器，到期就调 `Channel.ReadAsync`。**这在现场会失败**：

1. **串行化冲突**：同一通道上的多个元件并发读，会全部阻塞在 `ChannelBase` 的信号量上，
   排队时间不可控，轮询周期失真。
2. **无法批量优化**：100 个元件读相邻寄存器 = 100 次 Modbus 请求，
   而合并后只需 1 次。Modbus 单次请求的固定开销（帧头、CRC、往返延迟）占绝对主导，
   不合并的话差距是**两个数量级**。
3. **线程数膨胀**：200 个元件 × 定时器 = 大量线程池调度压力。

### 6.2 按通道分组的轮询模型

```
DevicePollingService
│
├── PollingGroup("PLC-1")          ← 每个通道一个组
│   ├── 调度周期：min(该组元件的 PollingIntervalMs)
│   ├── 每 tick：
│   │   ① 筛选到期的元件
│   │   ② PollingSchedule 合并请求
│   │      · Modbus：连续地址合并为一个区间读
│   │      · EtherNet/IP：合并为一次批量 Tag 读（libplctag 单次可读 200 点）
│   │   ③ 一次 I/O（自动被 ChannelBase 串行化保护）
│   │   ④ 分发结果 → 各元件解析 → 驱动状态机
│   └── 单次失败不中断循环，记录后继续
│
├── PollingGroup("COM3")
└── PollingGroup("MQTT-1")         ← 订阅型通道：组只负责健康检查
```

```csharp
public sealed class DevicePollingService : IHostedService
{
    // 每个通道一个长期运行的 Task，而非一个元件一个定时器
    public Task StartAsync(CancellationToken ct);
    public async Task StopAsync(CancellationToken ct);   // 必须优雅停止
}
```

### 6.3 合并请求的约束

批量合并**不能想当然**，需满足：

- Modbus：合并的地址必须是**同一功能区**（不能把线圈和保持寄存器合并）
  且**在从站允许的最大读取长度内**（常见上限 125 个寄存器/次）。
- EtherNet/IP：合并的 Tag 必须是**同一类型**，libplctag 的批量接口有数量上限。
- 跨区间的"空洞"应截断（合并后长度远大于有效数据量时，拆分为多次请求更优）。

**这些约束应由 `PollingSchedule` 统一处理**，元件本身不感知。

### 6.4 变化检测

**读数未变化时不得触发 `StateChanged` 事件**。
轮询每秒执行多次，而状态变化是稀疏事件。

```csharp
// 元件内：仅在值真正变化时才驱动状态机 / 触发事件
if (newValue != _lastValue)
{
    _lastValue = newValue;
    // 驱动状态机 ...
}
```

否则 UI 每秒刷新数十次，CPU 占用会显著上升（尤其在元件数量多时）。

## 7. ComponentManager

```csharp
public sealed class ComponentManager : IComponentManager
{
    // 职责：
    // 1. 持有全部元件的注册表（ConcurrentDictionary）
    // 2. 从配置加载元件（点位映射 → 元件实例）
    // 3. 聚合转发所有元件的 StateChanged 事件
    // 4. 启动/停止轮询服务
    // 5. 为元件按名称解析所属通道
}
```

**事件聚合的价值**：UI 只需订阅 `ComponentManager.ElementStateChanged` 一次，
而不必在元件动态增删时反复订阅/取消订阅（这正是内存泄漏的高发地带）。

**注意取消订阅**：`ComponentManager` 订阅了每个元件的 `StateChanged`，
`Remove(elementId)` 时**必须取消订阅**，否则元件对象无法被 GC 回收。
这是上位机长周期运行的典型泄漏点。

## 8. 点位映射

元件与物理地址的连接通过映射配置完成：

```csharp
public sealed record PointMapping
{
    public string Semantic { get; init; }        // "ExtendCommand" / "ExtendedLimit" / "AlarmWord"
    public string Address { get; init; }         // "40001" / "Axis1.Status" / "Line1/valve"
    public DataType DataType { get; init; }
    public double Scale { get; init; } = 1.0;    // 工程量换算
    public double Offset { get; init; } = 0.0;
    public int? BitIndex { get; init; }          // 从字中取第 N 位
}
```

**`BitIndex` 的必要性**：PLC 常把一个字节/字的 8~16 个位打包成多个信号。
若不能按位解析，元件就必须自行做位运算——把这段逻辑收敛到映射层，
可以让 `DeviceConfigForm` 提供直观的配置界面。

## 9. 扩展点

| 需求 | 做法 |
|---|---|
| 新增元件类型 | ① `Core` 的 `DeviceElementType` 加枚举值 ② 新建 `XxxElement : DeviceElementBase` ③ 新建 `XxxStateMachine` ④ 在 `ComponentManager` 的工厂中注册 |
| 新增状态 | 在对应元件的状态枚举中增加，并更新 `MapToDeviceState` 映射 |
| 新增触发器 | 在状态机枚举中增加，在 `Configure()` 中定义迁移 |
| 新增轮询策略 | 扩展 `PollingSchedule`，元件无需改动 |

## 10. 测试要点

**元件解析逻辑**（纯逻辑，无硬件）：

- 原始值 → 工程量的换算（缩放、偏移、按位取值）
- 边界值、异常值（如超出量程）的处理
- **状态机的全部迁移路径**（这是本模块测试价值最高的部分）：
  - 正常路径：缩回 → 伸出中 → 伸出
  - 超时路径：伸出中 → 超时 → `TimeoutFault`
  - 异常路径：两限位同时触发 → `BothOnAlarm`
  - 守卫条件不满足时**不发生**迁移
- 变化检测：相同值不触发事件

**轮询调度**（用 Mock 通道）：

- 请求合并的正确性（连续地址合并、超长截断、跨区不合并）
- 通道故障时组内元件均被标记为 `Offline`
- `StopAsync` 后无残留任务

**性能测试**：

- 200 元件单通道轮询的实际周期与合并效果
- 长时间运行（≥ 8 小时）的内存与句柄稳定性

## 11. 待细化事项

- [ ] **四种元件的完整状态集合与迁移表**——本模块最需要先定的内容
- [ ] `SensorElement` 去抖算法的参数（连续 N 次一致；N 与轮询周期相关）
- [ ] `MonitorElement`（监控控制设备）的语义边界：
      它是代表一个具体设备，还是聚合多个下位元件的逻辑单元？**这决定了它是否应该有状态机**
- [ ] 元件配置的存储形态（哪些字段落 `device_config` 表，哪些从配置文件读）
- [ ] 状态机的持久化：程序重启后是否需要恢复上次状态（当前设计为重启即重算）
- [ ] 轮询分组的动态调整（元件增删后是否需要重建分组）
- [ ] 是否需要"手动强制状态"功能（现场调试时绕过状态机直接设置状态）

## 12. 数据出口（M3.5 新增）

> ⚠️ **实现状态**：本节描述的两个接口已在 `Core` 中定义；
> `NoopDataPointSink` 已实现（`EquipmentMonitor.Devices` 项目，M3.5 新建）。
> **`Devices` 模块本体（元件、状态机、轮询、`ComponentManager`）尚未实现** ——
> 也就是说，下面的「调用点」目前还无人调用。

### 12.1 双向数据出口架构

上位机有**两个独立的数据出口，互不依赖**：

```
                        上位机（WinForms）
                              │
        ┌─────────────────────┴─────────────────────┐
        │                                           │
        ▼                                           ▼
  ┌───────────────┐                         ┌───────────────┐
  │   MesPush     │                         │      Api      │
  │  HTTP 客户端   │                         │  HTTP 服务端   │
  └───────┬───────┘                         └───────┬───────┘
          │ 上位机 → MES                             │ 其他系统 → 上位机
          ▼                                          ▼
    ┌──────────┐                          ┌──────────────────────┐
    │   MES    │                          │ HMI / 大屏 / 其他系统  │
    │ （服务端）│                          └──────────────────────┘
    └──────────┘
          ▲                                          ▲
          │                                          │
          │ 契约由 MES 定义                            │ 契约由上位机定义
          │ 认证 Bearer / API Key                     │ 认证 API Key
          │ 用途：实时数据推送                          │ 用途：机台查询、历史查询、状态快照
          │                                          │
          └──────────────┬───────────────────────────┘
                         │
                  ┌──────┴──────┐
                  │   Devices   │  ← 共用的数据源
                  │  （内存状态） │
                  └─────────────┘
                         │
                  ┌──────┴──────┐
                  │ Communication│
                  └─────────────┘
```

**关键约束**：

| 约束 | 说明 |
|---|---|
| **两通道互不依赖** | MES 断线不影响 Api；Api 被频繁访问也不影响 MesPush 队列 |
| **共用数据源、出口独立** | 两者都从 Devices 的内存状态读，但各有各的队列与线程 |
| **定位是「机台边缘节点」** | **不提供跨机台聚合、不做长期归档** —— 那是 MES / Historian 的职责 |

**定位的推论**：上位机只回答「我这台设备现在怎么样」。
「这条产线今天产量多少」「最近 30 天趋势」这类问题必须由上游系统回答 ——
上位机把数据推出去或供出去之后，就不该再为它负责。

### 12.2 IDataPointSink（Historian 预留）

```csharp
public interface IDataPointSink
{
    Task WriteAsync(DataPointSnapshot snapshot, CancellationToken ct);
}
```

**调用时机**：Devices 在数据点**发生变化**时调用（不是每次轮询都调 —— 变化检测见 §6.4）。

**调用约束（三条，均由调用方保证）**：

1. **不得阻塞轮询循环**。fire-and-forget，不 await。
   实现内部用自有队列 + 后台写入任务。
2. **落库失败不影响 MES 推送**。两条路径完全独立，各自 try/catch。
3. **队列必须有界**且丢弃量可观测 —— 无界队列会在下游卡住时耗尽内存。

**默认实现 `NoopDataPointSink`**：不落库，仅 Debug 级别记录。
- 用 **Debug 而非 Information**：高频采集下每秒数百条，Information 会瞬间刷满日志并拖慢轮询
- 零副作用：无队列、无后台任务、无锁，`WriteAsync` 实际同步完成
- Historian 模块实现后用 `TryAddSingleton` 前置注册替换，**调用方无需改动**

**`DataPointSnapshot.Value` 是 `object` —— 两条使用约定**：

| 约定 | 原因 |
|---|---|
| 只放不可变的值 | 放引用类型时生产者必须保证构造后不再修改，否则消费者会读到半更新或跨帧混合的数据 |
| 批量写入会装箱 | 高频场景应考虑给 `IDataPointSink` 加强类型重载，或改为存储序列化后的字节 |

之所以仍用 `object`：数据点类型由现场点位映射决定，编译期无法穷举；
用泛型会把类型参数透传到 Historian、MES 推送、Api 三层，复杂度远超收益。

### 12.3 IMachineQueryService（Api 预留）

```csharp
public interface IMachineQueryService
{
    Task<MachineSnapshot?> GetSnapshotAsync(string machineCode, CancellationToken ct);
    Task<IReadOnlyList<MachineSummary>> ListMachinesAsync(CancellationToken ct);
    Task<IReadOnlyList<ElementSnapshot>> ListElementsAsync(string machineCode, CancellationToken ct);
}
```

**Mes 模块不消费本接口** —— 见 §12.1 的出口分离。

**分层**：元数据从 SQLite 读，实时状态从内存读。

**线程安全是硬要求**：Api 从 HTTP 线程调用，Devices 从轮询线程更新。三条要求：

1. 读取过程**不持有会被写入方长时间占用的锁** —— 高频查询不能拖慢轮询
2. 返回对象**不可变**，调用方拿到后不会再看到它变化
3. 单次调用的快照**内部一致**

**一致性的推荐做法**：让 `ComponentManager` 对每个元件维护一个**不可变的
`ElementSnapshot`**（状态变化时整体替换引用），取机台快照就只是收集一批引用 ——
天然一致，且几乎无需加锁。这比「快照期内加读锁」更好：
加锁会让高频 API 查询阻塞轮询线程。

**`MachineSnapshot.IsStale`**：调用方**应当检查**。
轮询停止时（设备全离线）快照接口仍返回上一次的数据，
不检查的话 Api 使用方会把「3 分钟前的状态」当成实时状态。

**权限**：本接口**不**调用 `RequirePermission`。
它的调用方是 Api 模块，走独立的 API Key 认证路径 ——
不是「上位机本地用户的操作」。若将来需按外部调用方区分可见范围，应在 Api 层做。

## 13. 与 Communication 模块的对接契约

Communication 已交付并实机验证，Devices 必须复用其机制，**不得自建平行实现**。

### 13.1 四条必须遵守的约定

| # | 约定 | 原因 |
|---|---|---|
| 1 | **复用 `Communication.ExponentialBackoff`** | 它的 `NextDelay()` 是纯函数不碰时钟，等待由调用方 `Task.Delay` 执行。Devices 的轮询重试若需要退避，直接用同一个类 —— 自己再写一份会得到两套不同的重试行为 |
| 2 | **不得绕过通道的背压机制** | 通道用有界 `Channel<T>` + DropOldest，丢弃量通过 `ChannelHealth.DroppedFrames` 可观测。Devices 若额外缓冲，**必须声明上限 + 丢弃策略 + 可观测指标**，否则就是换了个地方 OOM |
| 3 | **MQTT `Payload` 必须立即 `ToArray()` 复制** | 它是 `ReadOnlySequence<byte>`，底层指向连接内部**池化缓冲区**，回调返回后可能被复用。持有原始引用会读到别的消息内容 —— 这类 bug 只在高负载时偶发 |
| 4 | **只通过 `CommunicationManager` + `ChannelId` 读写** | 不直接 `new` 协议客户端。否则通道的串行化、健康度、重连机制全部失效 |

### 13.2 配置去重校验（Devices 负责）

⚠️ **若同一 `IP:Port` 出现两条通道，Modbus TCP 从站承受不了两个主站连接。**

若 Devices 负责加载机台-通道映射，应在**配置加载时做去重校验 + 启动告警**：

- 同一 `(Host, Port)` 被多条 `ModbusTcp` 通道引用 → 启动时 `LogError` 并提示
- 同一 `PortName` 被多条串口通道引用 → 同上（串口是独占资源）
- **建议**：这类配置错误应当**阻止启动**而非仅告警 ——
  两个主站连同一个从站的表现是间歇性通讯失败，排查成本极高

### 13.3 数据流的对接方式

```
拉模型（Modbus / OPC UA 读）
    Devices 的轮询任务 ──► IModbusChannel.ReadHoldingRegistersAsync(...)
                              └─► Result<ushort[]>  ← 通过返回值交付，不产生数据帧

推模型（MQTT 订阅 / 串口上报）
    ICommunicationChannel.DataReader (ChannelReader<ChannelDataFrame>)
        └─► Devices 的消费任务逐帧处理
            └─► 解析为数据点 ──► 变化检测 ──┬──► MES 推送（后台队列）
                                            └──► IDataPointSink.WriteAsync（fire-and-forget）
```

**注意**：Modbus 这类「请求-响应」协议**不产生数据帧** ——
它的结果通过 `ReadAsync` 的返回值交付。只有推模型协议才会往 `DataReader` 写数据。

## 14. 元件操作契约与写点位（里程碑 A 交付部分，2026-10-05）

> **实现状态**：写点位声明与空命令表已交付（里程碑 A2/A3）；
> 伺服命令语义（8 条 PLCopen 单轴命令、互锁、回读观察）在里程碑 C。
> 契约定义见 [Core §4.6](01-core.md)；寄存器级约定见
> [轴控命令接口表](../axis-command-interface.md)（草案，待双方评审）。

### 14.1 写点位声明（WritePoints）

`ElementMetadata.WritePoints`：语义名 → Modbus 地址（**必须是写功能码** 05/06/15/16）。

- **配置面**：模板 `Devices:Elements:<模板名>:WritePoints` 与实例
  `Machines[].Elements[].Overrides.WritePoints`，**逐键合并**（与 `Attributes` 同一条理由：
  现场通常只改其中一个地址）。
- **校验三处同源**（不一致时以更严的为准）：
  `DevicesOptionsValidator`（跨机台聚合报错，一次列全）、
  `ModbusElementBase` 构造（格式 + 写功能码，错误当场抛出）、
  `ElementMetadata.Validate`（空白键/空地址）。
- **元件访问**：`TryGetWriteAddress(semantic, out _)`；
  `RequireWriteAddress(semantic)` 用于子类声明"我需要哪些写点位"——
  **缺失即构造失败**，沿用寄存器数量校验的同一原则（配置错误当场炸掉）。

```jsonc
"Servo_Axis_Template": {
  "Address": "1:03:100:9",          // 读块（状态区，9 寄存器，见接口表 §2）
  "WritePoints": {                  // 离散语义的写地址（完整集合由 ServoCommandMap 断言）
    "ControlWord":    "1:06:1000:1",   // CiA402 控制字
    "Mode":           "1:06:1001:1",   // 操作模式（E_ServoMode）
    "TargetPosition": "1:16:1002:2",   // 32 位，高字在前
    "Relative":       "1:06:1004:1",
    "TargetVelocity": "1:06:1005:1",
    "Accel":          "1:06:1006:1",
    "Decel":          "1:06:1007:1",
    "Jerk":           "1:06:1008:1",   // 0=不使用；可省略（唯一非必需项）
    "HomingMethod":   "1:06:1009:1",
    "Execute":        "1:05:20:1"      // 触发脉冲（线圈）
  }
}
```

> 语义名、控制字与控制模式常量在 `ServoCommandMap`（`Devices/Elements/`）——
> 与 `0004.PLC` 的 `E_Cia402Command` / `E_ServoMode` **逐值对齐**，并有单元测试冻结这些字面值。

### 14.2 空命令表与统一契约

四个元件（含监测元件）统一实现 `IElementCommandTarget`，基类 `ElementBase` 提供空实现：
`Commands` 为空表、`EvaluateCommand` 返回"不支持操作命令"、`ExecuteCommandAsync`
以 `COMMAND_UNKNOWN` 拒绝（且拒绝理由与 `EvaluateCommand` 同源）。
UI 由此**不需要判空或类型判断**；`ElementFactory` 构造路径无改动（四类元件回归通过）。

## 15. 操作与命令（PLCopen 对齐，里程碑 C 交付）

> **实现状态**：伺服 8 条单轴命令已交付（里程碑 C1–C8）；气缸命令在里程碑 F。
> 寄存器级契约见 [轴控命令接口表](../axis-command-interface.md)（草案，待与 `0004.PLC` 双方评审）。
> 契约定义（`IElementCommandTarget` 等）见 [Core §4.6](01-core.md)。

### 15.1 命令表（伺服）

| 命令 | 激活 | 风险 | 参数 | 写序列（按序） | 期望（回读） | 超时 |
|---|---|---|---|---|---|---|
| `MC_Power` | Enable | Confirm | `Enable` | 控制字 `0x000F` / `0x0007` | 使能位 = 请求值 | 5s |
| `MC_Reset` | Execute | Normal | — | 控制字 `0x0080`，若期望使能则续写 `0x000F` | 报警/故障位与报警码清零 | 5s |
| `MC_Home` | Execute | Confirm | `HomingMethod` | 回零方式 → `Mode=6` → 脉冲 | 回零中 → 回零完成（**需证据**） | 60s |
| `MC_MoveAbsolute` | Execute | Confirm | `Position`/`Velocity`/`Accel`/`Decel` | 目标位置(32位) → `Relative=0` → 速度/加减速 → `Mode=1` → 脉冲 | 定位中 → 到位（**需证据**） | 30s |
| `MC_MoveRelative` | Execute | Confirm | `Distance`/… | 同上，`Relative=1` | 同上 | 30s |
| `MC_MoveVelocity` | Execute | Confirm | `Velocity`/`Accel`/`Decel` | 速度（**符号表方向**）→ 加减速 → `Mode=2` → 脉冲 | 速度回读在 ±5% 内 | 30s |
| `MC_Halt` | Execute | Confirm | `Decel`/`Jerk?` | 减速 → 目标速度 0 → `Mode=2` → 脉冲 | 速度归零（**保持使能**） | 30s |
| `MC_Stop` | Execute | Confirm | `Decel`/`Jerk?` | 减速 → 控制字 `QUICK_STOP` | 停止且未使能（**锁定**） | 30s |

**"触发脉冲"**：先把 `Execute` 线圈写 0 再写 1 —— PLC 侧检测上升沿；
先写 0 省掉"上位机记忆上次写了什么"的重启失准问题（接口表 §5-Q1 的实现选择）。

### 15.2 两类期望语义（决策 D4 的两种形态）

- **状态目标型**（`MC_Power` / `MC_Halt` / `MC_Stop`）：目标是"处于某状态"，
  本来就满足即**立即完成**（停着的轴按 Halt 就是已完成）。
- **动作证据型**（`MC_Home` / `MC_Move*`）：必须**证明这次动作发生了** ——
  回零完成位与到位位都是**电平保持**的（上一次的结果一直挂着），
  只看它们会让新命令在轴没动时报"秒完成"（**假成功比超时更危险**）。
  判据 = 见过"进行中"位，或完成位相对命令起点出现上升沿。

### 15.3 互锁矩阵（可执行制品）

`ServoElement.EvaluateCommandCore` 是唯一实现处，测试 `ServoElementCommandTests` 的
`Interlock_*` 用例逐格钉住（未使能/故障/回零中/运动中/离线 × 8 命令）。
两条刻意的不对称：

- **停止类（Halt/Stop）永远可用**：故障中、未使能时"停下来"同样有意义，
  拦下停止命令只会把人推向物理断电（安全方向）；
- **回零要求静止**：运动中回零的"基准"落在哪取决于时序，先停再建基准（PLCopen 惯例）。

### 15.4 报警码文本与轴状态快照

- **`ServoAlarmTexts`**（`Attributes["AlarmTexts"]="16=过流;17=编码器断线"`）：
  配置驱动的现场事实，格式错误在构造期抛；未配置的码回退"报警码 N"，永不空串。
- **`IAxisElement`**（`AxisStatus` / `HomingStatus`）：对齐 `ST_AxisStatus` / `ST_HomingStatus`，
  **不可变快照整体替换**（UI 无锁读取）。读块因此扩为 **9 个寄存器**
  （+6/+7 目标位置回显、+8 回零方式回显）——**这是契约变更**，现场 `Address` 必须同步改 9。

### 15.5 测试覆盖的分工（四类用例）

每条命令的「正常 / 互锁 / 超时 / 中断」**不逐命令重复**，按机制与差异分工：

- **共享机制**（队列、写阶段只发一次、期望评估、超时窗口、Aborting 替代、审计、取消）
  在 `ElementCommandEntryTests` / `ElementCommandPipelineTests` / `ElementCommandAuditTests` /
   `ElementCommandConcurrencyTests` 中按机制各覆盖一次；
- **命令特有差异**（互锁矩阵、期望判据、中断交接、复位无效、急停被忽略、越界不发报文）
  在 `ServoElementCommandTests` 中逐条覆盖。

## 16. 相关文档

- [总体架构](../architecture.md)
- [Core 模块](01-core.md)（元件与命令契约）
- [Communication 模块](05-communication.md)
- [Data 模块](03-data.md)（元件配置持久化）
- [轴控命令接口表](../axis-command-interface.md)（上位机 ↔ PLC 寄存器契约）
