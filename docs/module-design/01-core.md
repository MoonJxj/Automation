# Core 模块设计

> 项目：`EquipmentMonitor.Core` ｜ **零外部依赖**（BCL 除外）

## 1. 职责边界

**做什么**：定义整个系统的**契约**——接口、数据模型、枚举、事件参数。

**不做什么**：

- ❌ 不含任何实现逻辑（接口无默认实现，模型无业务方法）
- ❌ 不引用任何其他项目
- ❌ **不引用任何第三方 NuGet 包**

**为什么这条约束不可破**：`Core` 被所有业务模块引用。
一旦它引入 Serilog 或 NModbus 的类型，所有模块都会被动产生传递依赖，
`Core` 就从"契约层"退化为"又一个业务模块"，分层随之失效。
**`Core` 项目的 `.csproj` 中不应出现任何 `<PackageReference>`。**

## 2. 依赖关系

```
EquipmentMonitor.Core ──► （无）
```

## 3. 目录结构

```
EquipmentMonitor.Core/
├── Abstractions/
│   ├── IDeviceElement.cs            # 元件抽象
│   ├── IComponentManager.cs         # 元件管理器
│   ├── ICommunicationChannel.cs     # 通讯通道抽象
│   ├── IAuthService.cs              # 认证服务
│   └── ILogService.cs               # 日志服务
├── Models/
│   ├── DeviceElementBase.cs         # 元件基类
│   ├── DeviceElementType.cs         # 元件类型枚举
│   ├── DeviceState.cs               # 设备状态枚举
│   ├── DeviceElementSnapshot.cs     # 元件瞬时快照（跨线程传递）
│   ├── ChannelReadRequest.cs        # 通道读请求
│   ├── ChannelWriteRequest.cs       # 通道写请求
│   ├── ChannelStatus.cs             # 通道状态枚举
│   ├── DataType.cs                  # 值类型枚举
│   ├── UserInfo.cs                  # 用户信息
│   ├── UserSession.cs               # 会话
│   └── LoginRequest.cs
├── Enums/
│   ├── SecurityLevel.cs             # 安全等级
│   └── CommunicationProtocol.cs     # 通讯协议类型
└── Events/
    ├── DeviceStateChangedEventArgs.cs
    ├── ChannelStatusChangedEventArgs.cs
    └── CommunicationErrorEventArgs.cs
```

## 4. 接口契约

### 4.1 ICommunicationChannel

所有协议的统一抽象。**`Devices` 层只认这个接口，不认具体协议。**

```csharp
public interface ICommunicationChannel : IAsyncDisposable
{
    string Name { get; }                        // 通道唯一标识（来自 ChannelConfig）
    CommunicationProtocol Protocol { get; }
    ChannelStatus Status { get; }               // Disconnected / Connecting / Connected / Faulted
    bool IsConnected { get; }
    DateTimeOffset? LastSuccessTime { get; }
    int ConsecutiveFailures { get; }

    event EventHandler<ChannelStatusChangedEventArgs>? StatusChanged;

    Task<Result> ConnectAsync(CancellationToken ct = default);
    Task<Result> DisconnectAsync(CancellationToken ct = default);

    Task<Result<T>> ReadAsync<T>(ChannelReadRequest request, CancellationToken ct = default)
        where T : struct;

    Task<Result> WriteAsync<T>(ChannelWriteRequest request, T value, CancellationToken ct = default)
        where T : struct;

    Task<Result<T[]>> ReadBatchAsync<T>(IReadOnlyList<ChannelReadRequest> requests, CancellationToken ct = default)
        where T : struct;
}
```

**设计说明**：

- `ReadAsync<T>` 是**协议无关的通用读**，`T` 为 `bool` / `short` / `ushort` / `int` / `float` / `double`。
  协议差异被封装进 `ChannelReadRequest.Address` 的解释中：
  Modbus 解释为寄存器地址，EtherNet/IP 解释为 Tag 名，OPC UA 解释为 NodeId，MQTT 解释为 Topic。
- **`ReadBatchAsync` 是性能关键接口**。libplctag 单次 CIP 请求可读 200 个 Tag（千兆网约 5ms），
  逐个 `ReadAsync` 会导致性能下降两个数量级。Modbus 也应合并连续寄存器为一次请求。
- **实现方必须保证同一实例上的读写串行化**（见 [编码规范 §5](../conventions.md#5-线程与并发)）。

```csharp
public sealed record ChannelReadRequest(
    string Address,          // 协议相关地址
    DataType DataType,       // 值类型
    ushort Length = 1);      // 读取长度（寄存器数 / 数组长度 / 字符串长度）
```

### 4.2 IDeviceElement

单个物理元件的抽象。

```csharp
public interface IDeviceElement
{
    string Id { get; }                          // 唯一标识
    string Name { get; }                        // 显示名
    DeviceElementType ElementType { get; }
    DeviceState State { get; }                  // 归一的粗粒度状态（供 UI 与报警聚合）
    string? DetailedState { get; }              // 元件特有的细粒度状态描述
    string ChannelName { get; }                 // 所属通讯通道
    bool IsEnabled { get; }
    int PollingIntervalMs { get; }
    DateTimeOffset? LastPollTime { get; }

    event EventHandler<DeviceStateChangedEventArgs>? StateChanged;

    Task<Result> InitializeAsync(ICommunicationChannel channel, CancellationToken ct = default);
    Task<Result> PollAsync(CancellationToken ct = default);
    Task<Result> WriteAsync(string command, object? parameter = null, CancellationToken ct = default);

    DeviceElementSnapshot GetSnapshot();         // 线程安全的瞬时快照
}
```

**关于 `State` 与 `DetailedState` 的双层设计**：

这是本模块一个容易产生分歧的设计点，明确如下：

- `DeviceState` 是**跨元件类型归一**的粗粒度状态（`Unknown` / `Offline` / `Idle` / `Running` / `Alarm` / `Fault`），
  用于：UI 列表统一着色、报警聚合、全局状态统计。
- 各元件类型的**特有状态**（气缸的"伸出/缩回"、伺服的"使能/定位中/到位"）
  由各自的 Stateless 状态机维护，通过 `DetailedState` 暴露。

**理由**：若强行用一套枚举表达所有元件状态，会产生大量语义空洞的取值；
若完全不做归一，UI 又无法统一展示。分层是这两个需求的折中。

**`GetSnapshot()` 的用途**：UI 线程需要读取元件状态时，**不应直接访问元件的可变字段**（跨线程竞态）。
`GetSnapshot()` 返回一个不可变的 `record`，可安全跨线程传递。

### 4.3 IComponentManager

系统内所有元件的注册表与生命周期管理者。

```csharp
public interface IComponentManager
{
    IReadOnlyCollection<IDeviceElement> Elements { get; }
    bool IsPolling { get; }

    event EventHandler<DeviceStateChangedEventArgs>? ElementStateChanged;

    IDeviceElement? GetById(string elementId);
    IEnumerable<IDeviceElement> GetByType(DeviceElementType type);
    IEnumerable<IDeviceElement> GetByChannel(string channelName);

    Result Add(IDeviceElement element);
    Result Remove(string elementId);

    Task<Result> LoadFromConfigAsync(CancellationToken ct = default);
    Task<Result> StartPollingAsync(CancellationToken ct = default);
    Task<Result> StopPollingAsync(CancellationToken ct = default);
}
```

**注意**：`ElementStateChanged` 是所有元件事件的**聚合转发**，
使 UI 只需订阅一个事件，而不必逐个订阅元件。事件在**后台线程**引发。

### 4.4 IAuthService

```csharp
public interface IAuthService
{
    UserSession? CurrentSession { get; }
    bool IsAuthenticated { get; }

    event EventHandler? SessionChanged;

    Task<Result<UserSession>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result> LogoutAsync(CancellationToken ct = default);
    bool HasPermission(SecurityLevel required);
}
```

### 4.5 ILogService

```csharp
public enum LogLevel { Verbose, Debug, Information, Warning, Error, Fatal }

public interface ILogService
{
    void Write(LogLevel level, string messageTemplate, params object?[] args);
    void Write(LogLevel level, Exception? exception, string messageTemplate, params object?[] args);

    IDisposable BeginScope(string moduleName);   // 作用域内日志自动带模块标签
}
```

> ⚠️ **设计待议**：把 Serilog 再包一层存在争议——Serilog 的 `ILogger` 本身已是良好抽象，
> 且 `Log.Logger` 是静态可用的。业界更常见的做法是直接依赖
> `Microsoft.Extensions.Logging.Abstractions` 的 `ILogger<T>`，并把 Serilog 挂为 Provider。
> 代价是 `Core` 需要引用这一个微软官方包（无传递依赖），略微放宽"零依赖"约束。
>
> **两种方案的取舍需在 Logging 模块细化时确定**，见 [02-logging.md §8](02-logging.md#8-待细化事项)。
> 若选择 `ILogger<T>` 方案，本节的 `ILogService` 应整体移除。

### 4.6 IElementCommandTarget / IAxisElement（元件操作契约，2026-10-05 新增）

> **实现状态**：契约已在 `Core` 落地（里程碑 A1，15 个类型）；`ElementBase` 以**空命令表**实现
> （里程碑 A3）；伺服命令语义在里程碑 C。完整决策（D1–D8）见 [变更计划](../ChangePlan.md) §4。

```csharp
public interface IElementCommandTarget
{
    IReadOnlyList<ElementCommandDescriptor> Commands { get; }   // 命令表：UI 据此自动生成界面
    CommandAvailability EvaluateCommand(string commandKey);     // 置灰 + 说得出原因
    Task<Result<ElementCommandResult>> ExecuteCommandAsync(
        ElementCommandRequest request, CancellationToken cancellationToken = default);
}

public interface IAxisElement : IElement
{
    AxisStatus AxisStatus { get; }        // 对齐 0004.PLC 的 ST_AxisStatus
    HomingStatus HomingStatus { get; }    // 对齐 ST_HomingStatus
}
```

**失败语义分两层（关键约定）**：

- **接受层**（`Result`）：命令**该不该执行**。拒绝（未写出任何报文）→ `Result.Fail`，
  错误码见 `CommandRejectionCodes`（未知命令 / 参数非法 / 互锁 / 离线 / 忙）；
  判据必须与 `EvaluateCommand` **同源**。
- **执行层**（`ElementCommandResult.Outcome`）：命令**执行得怎么样**（包在 `Result.Ok` 内）。
  `Done` / `CommandAborted` / `Error` / `Timeout` —— 由设备回读决定，不是"写成功"。

**命令键用字符串（PLCopen 块名，如 `MC_MoveAbsolute`）+ 结构化描述符**：字符串的"无编译期检查"
由"描述符构造时 `Validate()`、执行入口按描述符校验参数"补偿；枚举方案被否是因为新增命令必须改 `Core`，
且 UI 仍需要参数元数据才能自动生成控件。

配套模型：`ElementCommandDescriptor`（命令 + 参数描述符 + 风险级别）、
`ElementCommandRequest`（键 → `DataPointValue` 字典）、`MotionParameters`（对齐 `ST_ServoTarget`）。
写地址经 `ElementMetadata.WritePoints` 声明（见 [Devices 模块 §14](06-devices.md)）。

## 5. 模型

### 5.1 DeviceElementBase

所有元件的基类，承载共有属性与状态变更通知。

```csharp
public abstract class DeviceElementBase : IDeviceElement
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public abstract DeviceElementType ElementType { get; }
    public DeviceState State { get; protected set; } = DeviceState.Unknown;
    public string? DetailedState { get; protected set; }
    public string ChannelName { get; init; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int PollingIntervalMs { get; set; } = 200;

    public event EventHandler<DeviceStateChangedEventArgs>? StateChanged;

    protected ICommunicationChannel? Channel { get; private set; }

    public virtual Task<Result> InitializeAsync(ICommunicationChannel channel, CancellationToken ct);
    public abstract Task<Result> PollAsync(CancellationToken ct);
    public abstract Task<Result> WriteAsync(string command, object? parameter, CancellationToken ct);
    public abstract DeviceElementSnapshot GetSnapshot();

    /// <summary>由派生类在状态机迁移后调用，统一触发事件与日志。</summary>
    protected void SetState(DeviceState newState, string? reason = null);
}
```

**关键约定**：

- `SetState` 是**唯一**的状态变更入口。派生类不得直接赋值 `State`。
  这样保证：状态变更必然触发事件、必然写日志、必然产生快照。
- `PollAsync` 的职责是：读取原始数据 → 解析为工程量 → 驱动状态机 → 提交新状态。
  **若读数与上次相同，不得触发 `StateChanged` 事件**（避免 UI 无谓刷新）。

### 5.2 枚举

```csharp
public enum DeviceElementType { Unknown, Servo, Cylinder, Sensor, Monitor }

public enum DeviceState
{
    Unknown,        // 未初始化
    Offline,        // 通讯中断
    Initializing,   // 初始化中
    Idle,           // 待机 / 就绪
    Running,        // 运行中 / 动作中
    Alarm,          // 报警（可继续运行）
    Fault           // 故障（不可运行）
}

public enum ChannelStatus { Disconnected, Connecting, Connected, Faulted, Reconnecting }

public enum DataType { Bool, Int16, UInt16, Int32, UInt32, Float, Double, String }

public enum CommunicationProtocol
{
    ModbusTcp, ModbusRtu, ModbusAscii,
    SerialPort, Socket,
    EthernetIp, Mqtt, OpcUa
}

public enum SecurityLevel
{
    Viewer = 0,        // 只读，可查看监控界面
    Operator = 1,      // 可执行常规操作（启停、手动调试）
    Engineer = 2,      // 可修改设备配置与点位映射
    Administrator = 3  // 用户管理、系统配置
}
```

`SecurityLevel` 使用连续整数是为了支持 `>=` 比较语义（`HasPermission` 的实现基础）。

### 5.3 事件参数

```csharp
public sealed class DeviceStateChangedEventArgs : EventArgs
{
    public string ElementId { get; }
    public string ElementName { get; }
    public DeviceState OldState { get; }
    public DeviceState NewState { get; }
    public string? DetailedState { get; }
    public string? Reason { get; }        // 触发原因，写入日志
    public DateTimeOffset Timestamp { get; }
}
```

**跨线程约定**：所有事件在**后台线程**引发，订阅方（尤其是 UI）负责自行切换线程。

## 6. 扩展点

| 需求 | 做法 |
|---|---|
| 新增一种元件类型 | 在 `DeviceElementType` 加枚举值 + 在 `Devices/Elements/` 新增继承 `DeviceElementBase` 的类。**`Core` 只改枚举**，不动接口 |
| 新增一种通讯协议 | 在 `CommunicationProtocol` 加枚举值 + 在 `Communication/Channels/` 实现 `ICommunicationChannel`。**`Core` 只改枚举** |
| 新增安全等级 | 在 `SecurityLevel` 加枚举值（注意保持排序语义）+ 更新 `SecurityLevelPolicy` 映射 |
| 新增事件类型 | 加到 `Events/`，继承 `EventArgs`，**必须不可变** |

**扩展点设计原则**：`Core` 的接口应足够稳定，使得新增协议/元件类型只增加枚举值而非修改接口签名。
若某次扩展不得不改动现有接口，说明抽象设计存在缺陷，应优先重构而非将就。

## 7. 测试要点

`Core` 是单元测试**成本最低、收益最高**的模块（零依赖，无需 Mock 框架）：

- 枚举取值的连续性与排序语义（`SecurityLevel` 的 `>=` 比较）
- `DeviceElementBase.SetState` 的状态变更与事件触发（用测试替身派生类）
- 事件参数类型的不可变性
- `DeviceElementSnapshot` 的线程安全构造

## 8. 待细化事项

- [ ] **`ILogService` 去留**（见 §4.5 设计待议）——这是本模块最需要先定的问题
- [ ] `DeviceState` 的取值是否足够覆盖四类元件，是否需要 `Paused` / `Maintenance`
- [x] ~~`IDeviceElement.WriteAsync` 的 `command` 用字符串还是枚举更合适~~
      **已定（2026-10-05）**：取字符串（PLCopen 块名）+ `ElementCommandDescriptor` 结构化描述符；
      "字符串无编译期检查"由"描述符构造时 `Validate()` + 执行入口按描述符校验参数"补偿；
      枚举方案被否：新增命令必须改 `Core`，且 UI 仍需参数元数据。见 §4.6。
- [ ] `ICommunicationChannel` 是否需要暴露协议特有的原生能力
      （如 Modbus 的从站扫描、EtherNet/IP 的 Tag 浏览）——按当前设计，这类能力需另行定义接口
- [ ] `UserSession` 的字段构成（超时策略、是否携带权限快照）

## 9. 相关文档

- [总体架构](../architecture.md)
- [Shared 模块](00-shared.md)
- [Communication 模块](05-communication.md)
- [Devices 模块](06-devices.md)
