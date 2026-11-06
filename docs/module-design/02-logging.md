# Logging 模块设计

> 项目：`EquipmentMonitor.Logging` ｜ 依赖：`Core`、`Shared`
> 选型：Serilog 3.x + Serilog.Sinks.File + Serilog.Sinks.Map + Serilog.Sinks.Console + NCrontab.Scheduler

## 1. 职责边界

**做什么**：Serilog 的统一配置、按模块分流日志文件、内存日志缓冲（供实时查看）、日志文件定时清理。

**不做什么**：

- ❌ 不定义日志接口（`ILogService` 定义在 `Core`）
- ❌ 不写业务日志（本模块只提供**管道**，日志内容由各业务模块产生）
- ❌ 日志不落数据库（避免日志写入成为数据库负载来源，也避免日志与业务数据争抢 SQLite 的写锁）

## 2. 依赖关系

```
EquipmentMonitor.Logging ──► Core, Shared
```

## 3. 目录结构

```
EquipmentMonitor.Logging/
├── SerilogConfigurator.cs           # Serilog 统一配置入口
├── SerilogLogService.cs             # ILogService 的 Serilog 实现
├── LogEnrichers/
│   └── ModuleEnricher.cs            # 由命名空间推导模块名并注入属性
├── LogSinks/
│   └── InMemoryLogSink.cs           # 环形缓冲，供 LogViewerForm 实时查看
├── LogCleanupService.cs             # NCrontab 定时清理
└── Models/
    ├── LoggingOptions.cs            # 强类型配置模型
    └── LogEntry.cs                  # 内存 Sink 对外暴露的条目
```

## 4. 配置结构

```jsonc
// appsettings.json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "EquipmentMonitor.Communication": "Debug",   // 通讯模块单独调高
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "LogDirectory": "logs",
    "FileRetentionDays": 30,
    "FileSizeLimitMB": 32,
    "InMemoryCapacity": 5000,          // 内存 Sink 保留条数
    "ConsoleEnabled": true             // 仅开发环境为 true
  }
}
```

配置通过 `IOptions<LoggingOptions>` 注入，**不直接读 `IConfiguration`**。

## 5. 核心设计

### 5.1 模块名推导（ModuleEnricher）

需求：各模块的日志输出到**独立文件**，便于现场排障时快速定位。
例如通讯问题只看 `Communication-*.log`，不必在全局日志里翻找。

**实现方式**：`ModuleEnricher` 是一个 `ILogEventEnricher`，
从日志事件的 `SourceContext`（即产生日志的类全名）中**按命名空间前缀推导模块名**：

```
EquipmentMonitor.Communication.Channels.ModbusTcpChannel
                  └──────┬───────┘
                     Communication      ← 推导出的模块名
```

```csharp
public sealed class ModuleEnricher : ILogEventEnricher
{
    private static readonly string Prefix = "EquipmentMonitor.";
    private static readonly string[] KnownModules =
        ["App", "Core", "Shared", "Logging", "Security", "Communication", "Devices", "Data"];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        var module = ResolveModule(logEvent);
        logEvent.AddPropertyIfAbsent(factory.CreateProperty("Module", module));
    }
}
```

**关键点**：

- 该方案**不需要调用方配合**——不依赖开发者在每处日志手动标记模块。
- 它依赖 `SourceContext`，因此**日志必须带类型上下文**（见 §5.2）。
- 无法识别的命名空间归入 `Other`，不影响记录。

### 5.2 获取 Logger 的方式（影响 ModuleEnricher 是否生效）

`ModuleEnricher` 依赖 `SourceContext`，而 `SourceContext` 只有通过 `ForContext<T>()` 才会写入。
**因此禁止使用无上下文的静态 `Serilog.Log.Information(...)` 直接写日志。**

推荐做法（二选一，取决于 [`Core/ILogService` 的最终去留](01-core.md#45-ilogservice)）：

```csharp
// 方案 A：Serilog 原生（每类一个静态 Logger）
private static readonly ILogger Log = Serilog.Log.ForContext<ModbusTcpChannel>();

Log.Information("通道 {ChannelName} 已连接", Name);

// 方案 B：依赖注入 ILogger<T>（若采用 Microsoft.Extensions.Logging 方案）
public ModbusTcpChannel(ILogger<ModbusTcpChannel> logger) => _logger = logger;
```

两种方式都会写入 `SourceContext`，ModuleEnricher 均可正常工作。

### 5.3 按模块分流文件

使用 `Serilog.Sinks.Map`，按 `Module` 属性值动态创建文件 Sink：

```csharp
.WriteTo.Map(
    keyPropertyName: "Module",
    defaultKey: "Other",
    configure: (module, writeTo) => writeTo.File(
        path: Path.Combine(options.LogDirectory, $"{module}-.log"),
        rollingInterval: RollingInterval.Day,
        fileSizeLimitBytes: options.FileSizeLimitMB * 1024 * 1024,
        rollOnFileSizeLimit: true,
        retainedFileCountLimit: null,          // 保留策略交给 LogCleanupService
        outputTemplate: OutputTemplate))
```

**产物示例**：

```
logs/
├── Communication-20260926.log
├── Devices-20260926.log
├── Security-20260926.log
├── App-20260926.log
└── Other-20260926.log
```

**注意**：`Map` 会为每个模块值创建一个独立的 Sink 实例。
模块数量有限（8 个），不存在 Sink 爆炸风险，但**不要在 `Module` 属性中放高基数字段**（如元件 ID），
否则会导致文件句柄耗尽。

### 5.4 输出模板

```
{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Module}] [{SourceContext}] {Message:lj}{NewLine}{Exception}
```

包含 `{Module}` 与 `{SourceContext}` 是为了让单模块文件中仍能定位到具体类与具体方法。

## 6. 内存日志 Sink

`LogViewerForm` 需要实时查看日志，且**不能读取正在被 Serilog 写入的文件**（文件锁冲突）。
方案是实现一个内存 Sink，维护最近 N 条的环形缓冲。

```csharp
public sealed class InMemoryLogSink : ILogEventSink
{
    public InMemoryLogSink(int capacity);

    /// <summary>批量推送，节流后触发；UI 订阅此事件。</summary>
    public event EventHandler<IReadOnlyList<LogEntry>>? EntriesAppended;

    public IReadOnlyList<LogEntry> Snapshot();       // 全量快照
    public IReadOnlyList<LogEntry> Query(LogFilter filter);  // 按级别/模块/关键字过滤
    public void Clear();

    public void Emit(LogEvent logEvent);             // ILogEventSink
}
```

### 6.1 必须节流（否则会拖垮 UI）

上位机在高负载时（例如开 Verbose 级别调试协议报文）可达**每秒数千条**日志。
若每条都触发一次事件并让 UI 刷新，界面会直接卡死。设计约束：

- **环形缓冲**：容量固定（默认 5000 条），写满后覆盖最旧条目。**内存占用有上界**，
  这是长周期运行不泄漏的前提。
- **批量推送**：不使用 `Timer` 逐条推送，而是累积到阈值（如 200 条）**或**距上次推送超过 200ms 时才触发事件。
  用 `Interlocked` 计数 + 时间戳判断，避免在 `Emit` 中加锁。
- **快照而非引用**：`Snapshot()` 返回已转换好的 `LogEntry`（不可变记录），
  **不要暴露 Serilog 的 `LogEvent`**——`LogEvent` 会持有对属性值和潜在异常的引用，长期存活会显著增加内存压力。
- **UI 侧再节流**：`LogViewerForm` 收到批量事件后同样应使用 `BeginInvoke` + 虚拟列表，
  且**自动滚动仅在用户位于列表底部时启用**（否则用户向上翻阅时会被不断拉回底部）。

### 6.2 运行时调整日志级别

现场排障常需要临时打开 `Verbose` 查看协议报文。使用 `LoggingLevelSwitch`，
允许 `LogViewerForm` **在不重启程序的前提下**调整级别：

```csharp
public sealed class SerilogConfigurator
{
    public LoggingLevelSwitch LevelSwitch { get; }   // 暴露给 UI
    public void Configure(LoggingOptions options);
}
```

## 7. 日志清理

```csharp
public sealed class LogCleanupService : IHostedService
{
    // 默认 Cron：每天 03:00 执行（错开轮询高峰）
    public const string DefaultCron = "0 3 * * *";

    // 职责：删除 logs/ 下最后写入时间超过 FileRetentionDays 的文件
}
```

**设计要点**：

- 使用 `NCrontab.Scheduler` 按 Cron 表达式调度，默认 `0 3 * * *`（凌晨 3 点，避开生产高峰）。
- 清理**必须按文件最后写入时间**判断，而不是文件名中的日期
  （跨天滚动 + 大小滚动会产生文件名与实际写入时间不一致的日志）。
- 删除失败的单个文件（被占用）只记 `Warning` 并继续处理其余文件，**不中断整个清理任务**。
- 清理结果（删除数量、释放空间）记入 `Information` 日志，便于事后确认策略是否生效。

**补充约束**：`Serilog.Sinks.File` 的 `retainedFileCountLimit` 设为 `null`，
保留策略**完全交给本服务**——避免两套清理机制互相干扰。

## 8. 待细化事项

- [ ] **`ILogService` 是否保留**（与 [Core 模块 §4.5](01-core.md#45-ilogservice) 是同一个决定）。
      倾向方案：移除自定义 `ILogService`，改用 `Microsoft.Extensions.Logging.ILogger<T>` +
      `Serilog.Extensions.Hosting`，理由是减少一层无收益的包装，并天然获得 `SourceContext`
- [ ] `InMemoryLogSink` 的批量推送阈值与时间窗口的实测调优
- [ ] 是否需要对日志做**脱敏**（连接字符串、密码）——建议在 Enricher 层统一过滤
- [ ] 是否引入 `Serilog.Sinks.Async`（异步 Sink）以避免磁盘 IO 阻塞采集线程。
      **这是一个性能风险点**：同步文件 Sink 在机械硬盘或磁盘繁忙时可能阻塞调用线程
- [ ] 日志是否需要落盘到独立的日志服务器（当前设计仅本地文件）

## 9. 相关文档

- [总体架构](../architecture.md)
- [编码规范 §8 日志](../conventions.md#8-日志)
- [Core 模块](01-core.md)
- [App 模块](07-app.md)
