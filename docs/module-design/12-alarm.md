# 报警信息服务设计（Alarm Service）

> 状态：**P0 已实施（2026-11-01）**；P1/P2 见 §14。
> 范围：`Core.Abstractions` 报警契约、`Infrastructure/Alarms` 实现、`Desktop` 采集与报警页。
> 本文是规格，不是现状描述；代码落地后把「实现状态」一节改成与代码一致的事实。

---

## 1. 结论

- **唯一入口**：`IAlarmService`。所有报警的生产者、消费者都只认这一个接口；
  任何类都不得自己维护报警列表、自己弹报警框、自己拼报警文本。
- **设备只做适配**：PLC、板卡、伺服、气缸、通信各自实现 `IAlarmSource`（持续状态）
  或注入 `IAlarmService.Report(...)`（瞬时事件）；报警的等级、文本、确认、历史、权限
  全部由报警服务统一处理。
- **文本集中**：报警码 → 标题/等级/处理建议由 `IAlarmCatalog` 解析，设备用
  `IAlarmDefinitionProvider` 注册自己的码表；未注册的码回退为「未定义报警 + 原始码」，永不空串。
- **报警不是状态机**：元件/机台状态描述物理事实，报警描述「需要人注意并经人确认的事件」。
  报警服务不修改元件状态、不触发停机；联锁与急停仍由 PLC/硬件兜底。
- **采集用拉模型 + diff**：每个源返回「当前存在的报警集合 + 源是否可用」；
  服务负责上升沿、恢复、去重、抖动合并。**源不可用时冻结已有报警**，不能把「读不到」当成「没报警」。
- **权限独立**：新增 `Permission.AlarmAcknowledge`（观察员只读，操作员起可确认）。
  确认是业务入口，走 `RequirePermission`；UI 只用 `HasPermission` 控制显隐。
- **历史一期落 JSONL**：宿主尚未接数据库，先用按天滚动的 JSONL 保证「重启不丢」；
  接口 `IAlarmHistoryStore` 已定，二期换 SQLite 不改调用点。

---

## 2. 现状与缺口（证据）

| 事实 | 证据 |
|---|---|
| 报警页是占位页 | `Desktop/Forms/PlaceholderPage.cs:47-54`；`10-desktop.md` 的原「报警信息」行标「占位；报警服务/历史待接」 |
| 轴只把故障位映射为元件错误，故障码仅留在快照里 | `src/EquipmentMonitor.Core/Axis/AxisElement.cs:938-961`；`Core/Models/AxisStatus.cs:65-69` |
| 状态字 bit4「报警（可继续运行）」被丢弃 | `AxisFeedback.HasAlarm` 存在（`AxisFeedback.cs:53-54`），但 `AxisFeedbackExtensions.ToAxisStatus` 没有映射它 |
| 气缸有 1001 超时 / 1002 限位矛盾两个故障码，只进 `LastError` | `src/EquipmentMonitor.Core/Cylinder/CylinderElement.cs:36-37, 235-241` |
| 采集错误只落在一个字符串上 | `Desktop/Monitoring/AxisMonitorSnapshot.cs` 的 `LastError`；气缸快照的 `Error` |
| 无报警服务、无历史、无确认 | 全仓库无 `IAlarmService`；`SRS.md:292-295` 明确「报警清单、确认与记录未实现」 |
| 权限点里没有「确认报警」 | `Core/Enums/Permission.cs` 共 6 个权限点；`RolePermissionPolicy.cs:20-48` |
| 审计只写结构化日志，未落库 | `Infrastructure/Auditing/LogAuditService.cs`；`10-desktop.md:218` |

**结论（设计前）**：当时每个元件各有一份「错误文本」，没有统一的报警事实、没有历史、没有确认，
所以页面无法回答「现在有什么报警、谁确认过、什么时候恢复的」。这正是本设计要补的缺口。
> **P0 已补齐（2026-11-01）**：页面替换、统一入口、确认与历史均已落地，落地记录见 §13.5；
> 本节保留为变更前的证据，不再代表现状。

---

## 3. 设计原则

1. **单一入口（Single Entry）**
   生产中只有 `IAlarmService` 一个写入口；消费中只有 `IAlarmService` 一个读入口。
   禁止出现「轴自己的报警集合」「PLC 自己的报警表」这类第二真相源。

2. **通过注入获取（Constructor Injection）**
   需要报警能力的类只声明 `IAlarmService`（或 `IAlarmSource`）构造参数，
   由 DI 容器给出实现；不 `new` 具体报警类、不静态访问、不依赖 `Desktop` 类型。

3. **设备差异下沉到适配器**
   PLC 的报警字是「多位位图」，伺服是「单个故障码」，通信是「连接健康度」，
   气缸是「超时/限位矛盾」——这些差异全部在 `IAlarmSource` 实现里消化；
   上层只看到统一的 `AlarmSignal` 列表。

4. **报警 ≠ 状态机**
   元件状态由 `IElement.RefreshAsync` 读出，报警服务只订阅这个事实。
   反向不成立：`Core` 的 `Machine`/`Element` 不依赖报警服务，避免 Core 反向依赖 Infrastructure。
   「报警后自动停机」属于联锁，放在 PLC/安全回路；报警服务不做设备动作。

5. **失败不影响主流程**
   报警历史写失败、源读取失败、审计写失败，都不能让采集或命令失败。
   实现内部吞异常并记 Warning/Error（与 `IAuditService` 的约定一致）。

6. **不可变快照 + 后台线程事件**
   `AlarmSnapshot` 整体替换，UI 无锁读一帧；`Changed` 在后台线程触发，
   UI 按现有约定 `BeginInvoke` 或定时拉取（`conventions.md §4`）。

---

## 4. 总体架构

```text
设备侧（差异在这里）                         统一侧（只有一套）
┌──────────────────────────┐
│ 伺服/板卡轴               │──┐
│  PLC 报警字（Modbus）     │──┤   IAlarmSource.ReadAsync()
│  气缸限位/超时            │──┼──►  AlarmMonitorService（后台，500ms）
│  通信健康度（P1）         │──┘         │
└──────────────────────────┘            ▼
                                 IAlarmService.EvaluateAsync()
                                         │  diff：上升沿 / 恢复 / 抖动合并 / 源离线冻结
                                         ▼
                                 AlarmService（唯一状态与唯一入口）
                                   ├─ AlarmSnapshot（活动报警，不可变）
                                   ├─ Changed 事件（后台线程）
                                   ├─ IAlarmCatalog（码 → 等级/文本/建议）
                                   ├─ IAlarmHistoryStore（JSONL → 二期 SQLite）
                                   └─ IAuditService（确认动作审计）
                                         │
                    ┌────────────────────┼────────────────────┐
                    ▼                    ▼                    ▼
              AlarmInfoPage        MainForm 状态栏       未来的 MES/SCADA
              （列表/确认/历史）    （报警计数/最高等级）   （只读同一接口）
```

瞬时事件（命令失败、启动失败、配置错误）不走 `IAlarmSource`，由产生方直接注入
`IAlarmService` 调用 `Report(...)`；它和持续状态进入同一个服务、同一张历史表、同一个确认流程。

---

## 5. 契约（Core/Abstractions）

> 命名空间沿用现状：接口 `EquipmentMonitor.Core.Abstractions`，
> 模型 `EquipmentMonitor.Core.Models`，枚举 `EquipmentMonitor.Core.Enums`，事件 `EquipmentMonitor.Core.Events`。
> **不新增** `Alarms` 子命名空间——命名空间分裂已经是一次债务（`handover-axis-framework.md §3.2`）。

### 5.1 枚举

```csharp
namespace EquipmentMonitor.Core.Enums;

/// <summary>报警等级。只影响显示与排序，不驱动设备动作。</summary>
public enum AlarmLevel
{
    /// <summary>提示：无需处理，仅记录。</summary>
    Info = 0,

    /// <summary>警告：可继续运行，需要关注（状态字 bit4、参数接近边界）。</summary>
    Warning = 1,

    /// <summary>错误：设备不能继续运行，需要人工介入（伺服故障、气缸超时）。</summary>
    Error = 2,

    /// <summary>严重：安全相关（急停、安全门、超程）。由 PLC/安全回路判定。</summary>
    Critical = 3
}

/// <summary>报警记录的生命周期状态。</summary>
public enum AlarmState
{
    /// <summary>活动、未确认。</summary>
    Active = 0,

    /// <summary>活动、已确认；源仍在报告。</summary>
    Acknowledged = 1,

    /// <summary>源已恢复、但操作员尚未确认（防止「一闪而过没人看见」）。</summary>
    RecoveredUnacknowledged = 2,

    /// <summary>已恢复、已确认；从活动列表归档到历史。</summary>
    Cleared = 3
}

/// <summary>报警来源的物理类别，用于 UI 分组与文本。</summary>
public enum AlarmSourceKind
{
    MotionCard = 0,
    Plc = 1,
    Servo = 2,
    Cylinder = 3,
    Io = 4,
    Communication = 5,
    Command = 6,
    System = 7
}

/// <summary>一次变更的类型（用于事件订阅方的增量刷新）。</summary>
public enum AlarmChangeKind
{
    Raised = 0,
    Acknowledged = 1,
    Recovered = 2,
    Updated = 3
}
```

### 5.2 模型

```csharp
namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 报警的去重键：同一来源 + 同一码 + 同一原始码 = 同一条报警。
/// </summary>
/// <remarks>
/// 把 <see cref="RawCode"/> 放进键，是为了让「故障码 16 → 17」产生新记录而不是
/// 静默改写同一条：现场需要知道到底报过哪些码。
/// </remarks>
public readonly record struct AlarmKey(string SourceId, string Code, int? RawCode = null);

/// <summary>
/// 设备侧上报的一条报警事实（不可变）。
/// </summary>
/// <remarks>
/// <see cref="IAlarmSource"/> 返回它表示「当前仍然存在」；<see cref="IAlarmService.Report"/>
/// 接收它表示「刚刚发生的一次事件」。两者都只描述事实，不做去重或确认。
/// </remarks>
public sealed record AlarmSignal
{
    /// <summary>来源标识，如 <c>axis:1:0</c>、<c>cylinder:Cyl-1</c>、<c>plc:1</c>。</summary>
    public required string SourceId { get; init; }

    /// <summary>来源类别。</summary>
    public required AlarmSourceKind SourceKind { get; init; }

    /// <summary>稳定的报警码，如 <c>AXIS.FAULT</c>、<c>CYLINDER.TIMEOUT</c>。</summary>
    public required string Code { get; init; }

    /// <summary>等级；未在码表中登记时以此为准。</summary>
    public required AlarmLevel Level { get; init; }

    /// <summary>关联元件（可选），用于 UI 定位到具体设备。</summary>
    public string? ElementId { get; init; }

    /// <summary>元件显示名（可选），用于消息模板的 <c>{ElementName}</c>。</summary>
    public string? ElementName { get; init; }

    /// <summary>设备原始码（伺服故障码、PLC 位号等）；没有则为 <c>null</c>。</summary>
    public int? RawCode { get; init; }

    /// <summary>补充说明（如异常消息）；只进消息文本，不进去重键。</summary>
    public string? Detail { get; init; }

    /// <summary>
    /// 瞬时报警的自动恢复时间；<c>null</c> 表示由下一次采集或人工确认处理。
    /// 只对 <see cref="IAlarmService.Report"/> 有意义，持续状态由源消失来恢复。
    /// </summary>
    public TimeSpan? AutoClearAfter { get; init; }
}

/// <summary>
/// 报警码的定义：等级、文本、处理建议。设备通过 <see cref="IAlarmDefinitionProvider"/> 注册。
/// </summary>
public sealed record AlarmDefinition
{
    public required string Code { get; init; }
    public required AlarmLevel Level { get; init; }
    public required string Title { get; init; }

    /// <summary>
    /// 消息模板，支持 <c>{ElementName}</c>、<c>{SourceId}</c>、<c>{Code}</c>、
    /// <c>{RawCode}</c>、<c>{Detail}</c>；未提供的占位符替换为空串。
    /// </summary>
    public string MessageTemplate { get; init; } = "{ElementName} 报警（{Code}，原始码 {RawCode}）";

    /// <summary>触发条件的文字说明（SRS §10.3 要求）。</summary>
    public string? Trigger { get; init; }

    /// <summary>恢复条件的文字说明。</summary>
    public string? Recovery { get; init; }

    /// <summary>处理建议。</summary>
    public string? Remedy { get; init; }

    /// <summary>是否必须人工确认后才能从活动列表消失。</summary>
    public bool RequiresAcknowledge { get; init; } = true;
}

/// <summary>
/// 一条报警的完整记录（不可变）。UI、历史查询、审计共用同一形状。
/// </summary>
public sealed record AlarmRecord
{
    /// <summary>实例标识；确认与历史按它定位。同一 key 恢复后再次发生会生成新 Id。</summary>
    public required string Id { get; init; }

    public required AlarmKey Key { get; init; }
    public required AlarmLevel Level { get; init; }
    public required AlarmSourceKind SourceKind { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string? Remedy { get; init; }
    public string? ElementId { get; init; }
    public string? ElementName { get; init; }

    /// <summary>首次发生时间。</summary>
    public required DateTimeOffset RaisedAt { get; init; }

    /// <summary>最近一次被源确认仍存在的时间。</summary>
    public required DateTimeOffset LastSeenAt { get; init; }

    /// <summary>在抖动窗口内重复发生的次数（含首次）。</summary>
    public int RaiseCount { get; init; }

    public DateTimeOffset? AcknowledgedAt { get; init; }
    public string? AcknowledgedBy { get; init; }
    public DateTimeOffset? ClearedAt { get; init; }
    public bool RequiresAcknowledge { get; init; } = true;

    /// <summary>源当前是否仍在报告。</summary>
    public bool IsActive => ClearedAt is null;

    /// <summary>生命周期状态（由时间字段推导，避免两个真相源）。</summary>
    public AlarmState State => ClearedAt is null
        ? (AcknowledgedAt is null ? AlarmState.Active : AlarmState.Acknowledged)
        : (AcknowledgedAt is null ? AlarmState.RecoveredUnacknowledged : AlarmState.Cleared);

    /// <summary>来源标识（冗余自 <see cref="Key"/>，便于绑定）。</summary>
    public string SourceId => Key.SourceId;

    /// <summary>报警码（冗余自 <see cref="Key"/>，便于绑定）。</summary>
    public string Code => Key.Code;

    /// <summary>设备原始码（冗余自 <see cref="Key"/>）。</summary>
    public int? RawCode => Key.RawCode;
}

/// <summary>
/// 活动报警的一帧快照（不可变）。UI 只读它，不逐条订阅。
/// </summary>
public sealed record AlarmSnapshot(
    IReadOnlyList<AlarmRecord> Active,
    int UnacknowledgedCount,
    AlarmLevel HighestLevel,
    DateTimeOffset CapturedAt)
{
    /// <summary>空快照：尚未采集或没有任何报警。</summary>
    public static readonly AlarmSnapshot Empty = new([], 0, AlarmLevel.Info, DateTimeOffset.MinValue);

    /// <summary>活动报警条数。</summary>
    public int Count => Active.Count;
}

/// <summary>
/// 历史查询条件。所有字段都是可选的「与」条件。
/// </summary>
public sealed record AlarmHistoryQuery
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public AlarmLevel? MinimumLevel { get; init; }
    public AlarmState? State { get; init; }
    public string? SourceId { get; init; }
    public string? ElementId { get; init; }

    /// <summary>最多返回条数；实现必须夹在 1..5000。</summary>
    public int Limit { get; init; } = 500;
}

/// <summary>
/// 一个报警源在当前时刻的读数。
/// </summary>
/// <remarks>
/// <b>三态是刻意的</b>：<see cref="IsAvailable"/>=false 表示「读不到」，不是「没有报警」。
/// 报警服务必须据此冻结该源已有的报警，否则一次断线就会把全部报警清空——
/// 操作工会误以为设备恢复了。
/// </remarks>
public sealed record AlarmSourceReading
{
    public required bool IsAvailable { get; init; }
    public IReadOnlyList<AlarmSignal> Alarms { get; init; } = [];
    public string? Error { get; init; }

    public static AlarmSourceReading Available(IReadOnlyList<AlarmSignal> alarms)
        => new() { IsAvailable = true, Alarms = alarms };

    public static AlarmSourceReading Unavailable(string error)
        => new() { IsAvailable = false, Error = error };
}
```

### 5.3 接口

```csharp
namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 报警的唯一入口：设备上报、服务评估、UI 查询、人工确认都经过它。
/// </summary>
/// <remarks>
/// <para><b>唯一性</b>：这是报警事实的唯一写入口，也是活动报警的唯一读入口。
/// 不要在元件、驱动或页面里再维护一份报警集合。</para>
/// <para><b>线程</b>：实现必须线程安全；<see cref="Changed"/> 在后台线程触发，
/// 订阅方（尤其 UI）自行切回。</para>
/// <para><b>失败</b>：参数错误、告警不存在返回 <c>Result.Fail</c>；
/// 越权抛 <see cref="UnauthorizedAccessException"/>（与其他业务入口一致）。</para>
/// </remarks>
public interface IAlarmService
{
    /// <summary>当前活动报警快照；任何线程可读。</summary>
    AlarmSnapshot Snapshot { get; }

    /// <summary>活动报警发生变化时触发；事件在后台线程。</summary>
    event EventHandler<AlarmChangedEventArgs>? Changed;

    /// <summary>
    /// 采集一轮：遍历全部 <see cref="IAlarmSource"/>，做上升沿/恢复/去重。
    /// </summary>
    /// <remarks>
    /// <b>只应由后台采集服务调用</b>（<c>AlarmMonitorService</c>），UI 不调用。
    /// 单源异常必须被隔离，不能中断其余源。
    /// </remarks>
    Task EvaluateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 上报一条瞬时报警（命令失败、初始化失败、配置错误）。
    /// </summary>
    /// <remarks>
    /// 同步返回：只做内存更新与事件通知，历史落盘由实现内部排队；
    /// 调用方（可能是命令线程）不会被 I/O 阻塞。
    /// </remarks>
    Result Report(AlarmSignal signal);

    /// <summary>确认一条报警。权限：<see cref="Enums.Permission.AlarmAcknowledge"/>。</summary>
    Result Acknowledge(string alarmId);

    /// <summary>确认全部未确认报警；<paramref name="sourceId"/> 非空时只确认该来源。</summary>
    Result AcknowledgeAll(string? sourceId = null);

    /// <summary>查询历史；实现负责把 <see cref="AlarmHistoryQuery.Limit"/> 夹到合法范围。</summary>
    Task<IReadOnlyList<AlarmRecord>> QueryHistoryAsync(
        AlarmHistoryQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 持续型报警源：服务周期读取「当前存在的报警集合」。
/// </summary>
/// <remarks>
/// <para>
/// PLC 报警字、板卡/伺服状态字、气缸故障、通信健康度都实现它。
/// 实现方只描述事实，不负责去重、确认、历史——那些是 <see cref="IAlarmService"/> 的职责。
/// </para>
/// <para>
/// <b>SourceId 的层级约定</b>：本接口的 <see cref="SourceId"/> 是<b>源组</b>标识
/// （如 <c>axis</c>、<c>cylinder</c>、<c>plc:1</c>）；<see cref="AlarmSignal.SourceId"/> 是
/// <b>具体实例</b>（如 <c>axis:1:0</c>），必须以 <c>组标识 + ":"</c> 开头。
/// 报警服务按这个前缀把记录归组，源离线时只冻结本组的报警。
/// </para>
/// </remarks>
public interface IAlarmSource
{
    /// <summary>稳定且唯一的源组标识；同一设备重启后保持不变。</summary>
    string SourceId { get; }

    /// <summary>来源类别。</summary>
    AlarmSourceKind Kind { get; }

    /// <summary>读取当前状态；失败返回 <see cref="AlarmSourceReading.Unavailable"/>，不要抛异常。</summary>
    ValueTask<AlarmSourceReading> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>报警码表：把 <see cref="AlarmSignal"/> 解析成等级/文本/建议。</summary>
public interface IAlarmCatalog
{
    /// <summary>查找定义；未注册时返回通用回退定义（永不返回 null）。</summary>
    AlarmDefinition Resolve(AlarmSignal signal);
}

/// <summary>
/// 设备码表扩展点：每种设备/品牌注册自己的报警码定义。
/// </summary>
/// <remarks>
/// 实现由设备驱动提供（如 <c>LeadShineAlarmDefinitions</c>、<c>PlcAlarmDefinitions</c>），
/// 通过 DI 注册；<c>AlarmCatalog</c> 合并全部 provider。新增设备不必改 UI 或报警服务。
/// </remarks>
public interface IAlarmDefinitionProvider
{
    IEnumerable<AlarmDefinition> GetDefinitions();
}

/// <summary>报警历史持久化。</summary>
/// <remarks>
/// 写失败不得影响报警主流程；实现内部吞异常并记日志。
/// 一期用 JSONL（宿主尚未接数据库），二期换 SQLite 时替换实现即可。
/// </remarks>
public interface IAlarmHistoryStore
{
    /// <summary>追加一条记录（同 Id 的后续状态更新也追加新行）。</summary>
    Task AppendAsync(AlarmRecord record, CancellationToken cancellationToken = default);

    /// <summary>按条件查询；实现负责按 Id 折叠到最新状态并按时间倒序。</summary>
    Task<IReadOnlyList<AlarmRecord>> QueryAsync(
        AlarmHistoryQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 运行期元件注册表：轴/气缸装配完成后登记，供报警源等后台消费者读取。
/// </summary>
/// <remarks>
/// 元件由 <c>AxisFrameworkLoader</c> 手工创建，不在 DI 里；
/// 这个注册表是「装配结果」与「DI 单例消费者」之间的最小桥。
/// </remarks>
public interface IElementRegistry
{
    IReadOnlyList<IElementAxis> Axes { get; }
    IReadOnlyList<IElementCylinder> Cylinders { get; }
}
```

```csharp
namespace EquipmentMonitor.Core.Events;

/// <summary>报警变化事件参数（不可变）。</summary>
public sealed class AlarmChangedEventArgs : EventArgs
{
    public required AlarmChangeKind Kind { get; init; }
    public required AlarmRecord Record { get; init; }
    public required AlarmSnapshot Snapshot { get; init; }
}
```

### 5.4 为什么是这些形状

| 决策 | 理由 | 否决的替代方案 |
|---|---|---|
| `EvaluateAsync` 放在 `IAlarmService` 上 | 采集循环只依赖一个接口；不引入第二个「评估器」入口 | 单独 `IAlarmEngine`：多一个注入点，UI 更容易误用 |
| `Report` 同步 | 命令线程上报不能等落盘；内存更新 + 事件足够快 | 异步 `ReportAsync`：调用方要传播 `await`，命令路径被 I/O 绑架 |
| `AlarmKey` 含 `RawCode` | 故障码变化要留痕，不能静默改写 | 只用 `SourceId + Code`：历史里看不出到底报过哪些码 |
| `AlarmRecord.State` 由时间字段推导 | 只有一个真相源（时间），不会出现「已确认但状态还是 Active」 | 单独存 `State` 字段：两个字段可能不一致 |
| `AlarmSourceReading` 三态 | 断线 ≠ 无报警 | `IReadOnlyList<AlarmSignal>`：无法表达「读不到」 |
| 模型放 `Core.Models` 而非 `Core.Abstractions` | 与 `AxisStatus`/`CylinderStatus` 的现有位置一致 | 全部塞进 Abstractions：目录语义混乱 |

---

## 6. 报警码、文本与配置

### 6.1 码命名规范

```text
<域>.<类型>[.<子类型>]
```

| 码 | 等级 | 来源 | 触发 | 恢复 | 处理建议 |
|---|---|---|---|---|---|
| `AXIS.FAULT` | Error | 轴（板卡/PLC/伺服） | `AxisStatus.IsFault` 或 `FaultCode != 0` | `MC_Reset` 后故障位清零 | 查驱动器报警码，排除后复位 |
| `AXIS.WARNING` | Warning | 轴 | 状态字 bit4（`AxisFeedback.HasAlarm`） | 报警位清零 | 关注趋势，不强制停机 |
| `AXIS.REFRESH_FAILED` | Error | 轴 | `LastError` 非空且元件 `State == Error`，且不是故障码导致的 | 下一次刷新成功 | 检查通信/控制器 |
| `CYLINDER.TIMEOUT` | Error | 气缸 | 动作超时（`FaultCode == 1001`） | 复位 | 检查气路、阀、限位 |
| `CYLINDER.SENSOR_CONFLICT` | Error | 气缸 | 两限位同时 ON（`FaultCode == 1002`） | 复位 | 检查传感器/短路/机械卡死 |
| `COMM.SOURCE_OFFLINE` | Error | 服务生成 | 源 `IsAvailable == false` 且曾成功过 | 源恢复 | 检查连接/从站/网线 |
| `PLC.ALARM.<n>` | 配置 | PLC 报警字 | 配置的位为 1 | 位清零 | 由码表配置 |
| `SYS.STARTUP_FAILED` | Critical | 宿主 | 装配失败（`Report`） | 人工确认 | 看启动日志 |

> **每个报警只由一个源负责**：`AXIS.FAULT` 由轴源报，`COMM.SOURCE_OFFLINE` 由服务报，
> 不要在两个源里重复报同一件事，否则同一故障会出现两条记录。

### 6.2 码表合并与回退

```csharp
public sealed class AlarmCatalog : IAlarmCatalog
{
    private readonly Dictionary<string, AlarmDefinition> _definitions;

    public AlarmCatalog(IEnumerable<IAlarmDefinitionProvider> providers)
    {
        _definitions = providers
            .SelectMany(p => p.GetDefinitions())
            .GroupBy(d => d.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal); // 后注册覆盖先注册
    }

    public AlarmDefinition Resolve(AlarmSignal signal)
    {
        if (_definitions.TryGetValue(signal.Code, out var definition))
        {
            return definition;
        }

        // 未注册的码：等级用上报值，文本保底，永不空串（延续旧 ServoAlarmTexts 规则）。
        return new AlarmDefinition
        {
            Code = signal.Code,
            Level = signal.Level,
            Title = "未定义报警",
            MessageTemplate = "{SourceId} 报警 {Code}（原始码 {RawCode}）{Detail}",
            Remedy = "该码尚未登记，请补充报警码表。"
        };
    }
}
```

- 合并顺序按 DI 注册顺序；**同一 code 后者覆盖前者**，便于现场用配置覆盖内置默认值。
- 模板替换只做 `{Name}` 直接替换，不引入模板引擎；未知占位符替换为空串。
- **UI 不得硬编码报警文本**；新增码只在 provider/配置里加。
- 服务自身生成的码（`COMM.SOURCE_OFFLINE`）由内置的 `SystemAlarmDefinitions` 注册，
  不依赖任何设备 provider——否则它自己会变成「未定义报警」。

### 6.3 配置面（P0 可选，P1 完整）

配置模型：

```csharp
namespace EquipmentMonitor.Infrastructure.Configuration;

/// <summary>报警服务配置；通过 IOptions&lt;AlarmOptions&gt; 注入，业务代码不直接读 IConfiguration。</summary>
public sealed class AlarmOptions
{
    /// <summary>采集周期（毫秒），默认 500。</summary>
    public int PollIntervalMs { get; set; } = 500;

    /// <summary>抖动窗口（秒）：恢复后在此窗口内再次发生，复用原记录。</summary>
    public int ChatterWindowSeconds { get; set; } = 5;

    /// <summary>恢复后是否保留「未确认」记录，直到人工确认。</summary>
    public bool RetainUnacknowledgedOnRecovery { get; set; } = true;

    /// <summary>活动报警上限；超出时丢弃新报警并记 Warning，不丢已有报警。</summary>
    public int MaxActiveAlarms { get; set; } = 200;

    /// <summary>JSONL 历史保留天数。</summary>
    public int HistoryRetentionDays { get; set; } = 90;

    /// <summary>通道快照文件路径；默认 &lt;运行目录&gt;/logs/alarms。</summary>
    public string HistoryDirectory { get; set; } = "logs/alarms";

    /// <summary>报警码定义（覆盖内置码表）与 PLC 报警字映射。</summary>
    public List<AlarmDefinition> Definitions { get; set; } = new();
    public List<PlcAlarmWordOptions> PlcAlarmWords { get; set; } = new();
}

/// <summary>PLC 报警字配置。</summary>
public sealed class PlcAlarmWordOptions
{
    public int ControllerId { get; set; }
    public ushort Address { get; set; }
    public ushort Count { get; set; } = 1;
    public List<PlcAlarmBitOptions> Bits { get; set; } = new();
}

/// <summary>单个报警位的映射。</summary>
public sealed class PlcAlarmBitOptions
{
    public int Bit { get; set; }
    public required string Code { get; set; }
    public AlarmLevel Level { get; set; } = AlarmLevel.Error;
    public string? Message { get; set; }
}
```

`alarms.json`（程序运行目录）：

```json
{
  "pollIntervalMs": 500,
  "chatterWindowSeconds": 5,
  "retainUnacknowledgedOnRecovery": true,
  "maxActiveAlarms": 200,
  "definitions": [
    {
      "code": "AXIS.FAULT",
      "level": "Error",
      "title": "伺服故障",
      "messageTemplate": "轴 {ElementName} 故障，故障码 {RawCode}",
      "trigger": "状态字 bit5 或故障码非 0",
      "recovery": "MC_Reset 后故障位清零",
      "remedy": "查驱动器报警码，排除后复位",
      "requiresAcknowledge": true
    }
  ],
  "plcAlarmWords": [
    {
      "controllerId": 1,
      "address": 100,
      "count": 2,
      "bits": [
        { "bit": 0, "code": "PLC.ALM.1000", "level": "Error", "message": "PLC 报警位 100.0" }
      ]
    }
  ]
}
```

- `AlarmOptions` 放 `Infrastructure/Configuration/`（配置模型跟随使用模块，`conventions.md §6`）。
- 宿主用 `IOptions<AlarmOptions>` 读取，业务代码不直接读 `IConfiguration`。
- 文件不存在时用内置默认值；JSON 错误按 `Result.Fail` 记日志并用默认值启动（报警配置错误不能阻止设备启动）。

---

## 7. 生命周期与状态机

### 7.1 状态图

```text
                  源报告
                    │
                    ▼
              ┌──────────┐   确认    ┌──────────────┐
              │  Active  │─────────►│ Acknowledged │
              │ (未确认) │          │  (仍活动)    │
              └────┬─────┘          └──────┬───────┘
                   │ 源恢复                 │ 源恢复
                   ▼                       ▼
        ┌──────────────────────┐   ┌──────────────┐
        │ RecoveredUnacknowledged │ │   Cleared    │
        │      (恢复未确认)       │ │  (归档)      │
        └──────────┬───────────┘   └──────────────┘
                   │ 确认
                   └──────────────► Cleared
```

- **活动列表** = `Active` + `Acknowledged` + `RecoveredUnacknowledged`；
  `Cleared` 只进历史。
- `RetainUnacknowledgedOnRecovery = false` 时，恢复即归档（不推荐：会漏掉一闪而过的报警）。
- 确认是幂等的：已确认再确认返回 `Result.Ok()`，不重复写审计。
- 确认已归档的报警：返回 `Result.Fail("Alarm.AlreadyCleared")`，不修改历史。

### 7.2 每轮评估（`EvaluateAsync`）

```text
1. 取全局评估锁（SemaphoreSlim(1,1)，防重入）
2. 串行读取全部 IAlarmSource：
     成功 → (source, reading)
     抛异常 → (source, Unavailable(ex.Message))     ← 单源失败隔离
3. 在内存锁内逐源 diff：
     reading 不可用：
        首次成功过？否 → 跳过（启动宽限，避免启动瞬间满屏离线）
                      是 → 生成/刷新 COMM.SOURCE_OFFLINE，并冻结该源已有记录
     reading 可用：
         清除该源的 COMM.SOURCE_OFFLINE
         当前集合 - 已有记录 = 新报警 → 新建 Active（或抖动窗口内复用旧记录）
         已有记录 - 当前集合 = 已恢复 → ClearedAt = now（按配置决定是否保留）
         交集 = 更新 LastSeenAt / Message（不重复计数）
4. 清理过期的瞬时报警（AutoClearAfter）
5. 重新计算 Snapshot（排序：等级降序、RaisedAt 升序）
6. 锁外逐条触发 Changed（每条变化一次）
```

**去重规则**：

| 情况 | 行为 |
|---|---|
| 同一 `AlarmKey` 连续两轮都报 | 更新 `LastSeenAt`，`RaiseCount` 不变，不新增历史行 |
| 恢复后 `chatterWindow` 内再次报 | 复用最近一条记录：`Id` 不变、`RaiseCount++`、`ClearedAt=null`、`AcknowledgedAt/AcknowledgedBy=null`、状态回 `Active` |
| 恢复超过窗口后再次报 | 新 `Id`，历史新增一条 |
| 瞬时 `Report` 同一 key 重复 | 复用活动记录并 `RaiseCount++`；不同 key 各成一条 |
| 活动报警超过 `maxActiveAlarms` | 丢弃新报警并记 Warning 日志；不丢已有报警 |

> 抖动复用必须**清空确认信息**：重新发生的报警不能继承上一次的「已确认」，
> 否则操作工看到的是「已确认」而事实上这次没人确认过。

**源离线**：

- 首次成功前不生成离线报警（启动宽限）；避免初始化顺序导致「一启动就报所有设备离线」。
- 成功后失败：生成本源离线报警，并**冻结**该源组（`SourceId` 前缀匹配）的记录
  （`LastSeenAt` 不更新，状态不变）。
- 恢复后：清离线报警；源返回的集合与冻结记录做正常 diff（真正消失的才 clear）。

### 7.3 瞬时报警（`Report`）

- 用于无法「回读」的事件：命令失败、初始化失败、配置错误。
- 默认 `AutoClearAfter = null` → 必须人工确认；调用方可指定 `TimeSpan` 自动恢复。
- 不参与源 diff；由服务单独维护，`EvaluateAsync` 负责到期清理。
- `Report` 内部只做内存更新 + 事件 + 历史入队；**不抛 I/O 异常**。

---

## 8. 设备接入

### 8.1 伺服 / 板卡轴：`AxisAlarmSource`

数据来源：`IElementRegistry.Axes`（`IElementAxis` 的 `Status`/`State`/`LastError`）。

```csharp
public sealed class AxisAlarmSource : IAlarmSource
{
    private readonly IElementRegistry _registry;

    public string SourceId => "axis";          // 具体报警的 SourceId 用 "axis:{controllerNo}:{axisNo}"
    public AlarmSourceKind Kind => AlarmSourceKind.Servo;

    public ValueTask<AlarmSourceReading> ReadAsync(CancellationToken ct = default)
    {
        var alarms = new List<AlarmSignal>();

        foreach (var axis in _registry.Axes)
        {
            if (!axis.ElementMetadata.IsEnabled)
            {
                continue;                       // 禁用元件不产生报警
            }

            var status = axis.Status;

            if (status.IsFault)
            {
                alarms.Add(new AlarmSignal
                {
                    SourceId = $"axis:{axis.ControllerNo}:{axis.AxisNo}",
                    SourceKind = AlarmSourceKind.Servo,
                    Code = "AXIS.FAULT",
                    Level = AlarmLevel.Error,
                    ElementId = axis.ElementMetadata.ElementId,
                    ElementName = axis.ElementMetadata.Name,
                    RawCode = status.FaultCode
                });
            }
            else if (status.IsWarning)           // 需要先给 AxisStatus 补这个字段，见 §8.7
            {
                alarms.Add(new AlarmSignal
                {
                    SourceId = $"axis:{axis.ControllerNo}:{axis.AxisNo}",
                    SourceKind = AlarmSourceKind.Servo,
                    Code = "AXIS.WARNING",
                    Level = AlarmLevel.Warning,
                    ElementId = axis.ElementMetadata.ElementId
                });
            }
        }

        return ValueTask.FromResult(AlarmSourceReading.Available(alarms));
    }
}
```

**为什么从注册表读而不是从 `AxisSnapshotStore` 读**：
`AxisSnapshotStore` 在 `Desktop`，`Infrastructure` 不能引用 `Desktop`；
注册表是装配结果的最小暴露面，且未来 `Machine` 装配也能复用。
`AxisFrameworkBootstrapper` 在装配成功后调用 `ElementRegistry.Attach(...)`。

### 8.2 气缸：`CylinderAlarmSource`

同样从 `IElementRegistry.Cylinders` 读 `CylinderStatus`：

| 条件 | 码 | 等级 | RawCode |
|---|---|---|---|
| `Status.IsFault && FaultCode == 1001` | `CYLINDER.TIMEOUT` | Error | 1001 |
| `Status.IsFault && FaultCode == 1002` | `CYLINDER.SENSOR_CONFLICT` | Error | 1002 |
| `Status.IsFault && 其他` | `CYLINDER.FAULT` | Error | `FaultCode` |

> 不在 source 里判 `AxisElement`/`CylinderElement` 的具体类型：`IElementCylinder` 已暴露
> `Status` 与 `ElementMetadata`，够用；具体故障码的语义由码表文本解释。

### 8.3 PLC 报警字：`PlcAlarmWordSource`（P1）

```csharp
public sealed class PlcAlarmWordSource : IAlarmSource
{
    // 依赖：ModbusRegisterControllerFactory + AlarmOptions.PlcAlarmWords
    // 读取：ReadRegistersAsync(controllerId, address, count)
    // 映射：每个 bit → AlarmSignal { Code = 配置的 code, RawCode = wordIndex * 16 + bit }
}
```

- 配置驱动：`controllerId + address + count + bits[]`（见 §6.3）。
- 与轴共用同一个 `ModbusProtocol`（`ModbusProtocolPool`），不新建连接。
- PLC 报警字通常是「位保持型」：位为 1 即报警，位为 0 即恢复；不需要脉冲沿处理。
- 读取失败 → `AlarmSourceReading.Unavailable`，由服务统一报 `COMM.SOURCE_OFFLINE`。

### 8.4 通信健康度：`CommunicationAlarmSource`（P1）

当前新框架还没有统一的通道健康接口（`ModbusProtocol` 未暴露 `IsConnected`）。
P1 建议：先给协议/通道补 `IChannelHealth`（`IsConnected`、`LastError`、`LastSuccessAt`），
再由 `CommunicationAlarmSource` 按控制器生成 `COMM.OFFLINE`。
P0 不实现，避免为了报警先改通信层。

### 8.5 瞬时事件：命令失败 / 启动失败（P1）

```csharp
// AxisElement 构造注入 IAlarmService（可选参数，测试可传 null）
_alarms?.Report(new AlarmSignal
{
    SourceId = $"axis:{ControllerNo}:{AxisNo}",
    SourceKind = AlarmSourceKind.Command,
    Code = "AXIS.COMMAND_FAILED",
    Level = AlarmLevel.Warning,
    ElementId = ElementMetadata.ElementId,
    Detail = $"{request.CommandKey}: {result.ErrorCode} {result.Message}",
    AutoClearAfter = TimeSpan.FromSeconds(30)
});
```

- 只在 `ElementCommandResult.Error` / `TimedOut` / `Rejected` 时上报；
  `Accepted`（已下发）不算失败。
- 命令失败是「操作事件」，等级用 Warning 而不是 Error：它本身不改变设备状态；
  设备真故障由 `AXIS.FAULT` 报。
- P1 才改 `AxisElement`/`CylinderElement` 构造签名；P0 先用现有 `LastError` 承载，
  避免一次变更同时改契约、DI、测试和 UI。

### 8.6 新增一种设备要做什么

以「新增 EtherCAT 伺服」为例，**三步，不碰 UI 和报警服务**：

1. 实现 `IEtherCatAlarmSource : IAlarmSource`，把厂商状态字翻译成 `AlarmSignal`；
2. 实现 `IEtherCatAlarmDefinitions : IAlarmDefinitionProvider`，登记码表文本；
3. DI 注册：
   ```csharp
   services.AddSingleton<IAlarmSource, EtherCatAlarmSource>();
   services.AddSingleton<IAlarmDefinitionProvider, EtherCatAlarmDefinitions>();
   ```

报警页、历史、确认、权限、MainForm 状态栏全部不动。这就是「统一入口 + 注入」的实际收益。

### 8.7 P0 前置：`AxisStatus` 补 `IsWarning`

`AxisFeedback.HasAlarm`（bit4）当前没有进入 `AxisStatus`，`AxisElement.ComputeElementState`
也只判 `IsFault`，导致「可继续运行的报警」被完全丢弃。P0 必须补：

```csharp
// Core/Models/AxisStatus.cs
public bool IsWarning { get; init; }

// src/EquipmentMonitor.Core/Models/AxisFeedbackExtensions.cs
IsWarning = feedback.HasAlarm,

// AxisElement.ComputeElementState：在 IsFault 之后、Running 之前
if (status.IsWarning) return ElementState.Warning;
```

这是**契约变更**，要同步 `docs/axis-command-interface.md`（bit4 语义）与元件测试。

---

## 9. DI、生命周期与线程

### 9.1 注册（`InfrastructureModuleRegister` + `Program`）

```csharp
// Infrastructure
services.AddOptions<AlarmOptions>();                                  // P1：由 AlarmOptionsLoader 从 alarms.json 绑定
services.AddSingleton<AlarmCatalog>();
services.AddSingleton<IAlarmCatalog>(sp => sp.GetRequiredService<AlarmCatalog>());
services.AddSingleton<ElementRegistry>();
services.AddSingleton<IElementRegistry>(sp => sp.GetRequiredService<ElementRegistry>());
services.AddSingleton<IAlarmDefinitionProvider, SystemAlarmDefinitions>();
services.AddSingleton<IAlarmDefinitionProvider, AxisAlarmDefinitions>();
services.AddSingleton<IAlarmDefinitionProvider, CylinderAlarmDefinitions>();
services.AddSingleton<IAlarmSource, AxisAlarmSource>();
services.AddSingleton<IAlarmSource, CylinderAlarmSource>();
services.AddSingleton<IAlarmHistoryStore, JsonlAlarmHistoryStore>();
services.AddSingleton<IAlarmService, AlarmService>();

// Desktop/Program
services.AddSingleton<AlarmMonitorService>();
services.AddHostedService(sp => sp.GetRequiredService<AlarmMonitorService>());
```

> `AlarmOptions` 由 `AlarmOptionsLoader` 从 `alarms.json` 读取后绑定（P1）；P0 可只用默认值，
> 但**不得**直接注入可变配置对象——统一走 `IOptions<T>`（`conventions.md §6`）。

### 9.2 生命周期

| 服务 | 生命周期 | 理由 |
|---|---|---|
| `AlarmService` / `AlarmCatalog` / `IAlarmHistoryStore` | Singleton | 全局唯一状态；历史写入单写者 |
| `IAlarmSource`（各设备） | Singleton | 无状态适配器；有状态的部分（编码器缓存）自行保证线程安全 |
| `AlarmMonitorService` | Hosted Service | 宿主启动即采集，停止时退出循环 |
| `ElementRegistry` | Singleton | 装配结果只读发布 |
| `AlarmInfoPage` | Transient | 页面按需创建（与现有页面一致） |

- **WinForms 禁 `Scoped`**（`module-contract §7`）；报警服务不能是 Scoped。
- 注册顺序：安全/审计 → 元件注册表 → 报警源 → 报警服务 → 宿主采集。
  `AlarmService` 构造依赖 `IEnumerable<IAlarmSource>`，DI 顺序不影响解析，但停止顺序按反序，
  采集服务先停，报警服务最后释放。

### 9.3 线程模型

| 动作 | 线程 | 说明 |
|---|---|---|
| `EvaluateAsync` | `AlarmMonitorService` 后台线程 | 500ms 一轮；与轴采集（200ms）独立，相位差 ≤ 500ms |
| `Report` | 命令线程 / 启动线程 | 只做内存更新，不阻塞 I/O |
| `Acknowledge` | UI 线程（按钮点击） | 权限校验 + 内存更新 + 审计排队 |
| `Changed` | 触发者线程（后台） | UI 必须 `BeginInvoke` 或定时拉快照 |
| `QueryHistoryAsync` | UI 线程调用，`await` | 文件/DB I/O 在线程池，UI 不阻塞 |

**并发保护**：

- 内存状态用单一 `lock`（`_gate`）；临界区内不做 I/O、不触发事件。
- `EvaluateAsync` 用 `SemaphoreSlim(1,1)` 防重入；源读取在锁外串行执行。
- `AlarmSnapshot` 每次变更整体替换，读取方拿到的是一帧自洽数据。
- `Changed` 在锁外触发；订阅方异常不能影响报警服务（逐订阅者 try/catch 或事件调用方兜底）。

### 9.4 采集宿主

```csharp
public sealed class AlarmMonitorService : BackgroundService
{
    private readonly IAlarmService _alarms;
    private readonly TimeSpan _interval;

    public AlarmMonitorService(IAlarmService alarms, IOptions<AlarmOptions> options)
    {
        _alarms = alarms;
        _interval = TimeSpan.FromMilliseconds(Math.Max(100, options.Value.PollIntervalMs));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await _alarms.EvaluateAsync(stoppingToken).ConfigureAwait(false);
        }
    }
}
```

- 与 `AxisMonitorService` 分开：报警采集失败不影响轴快照刷新，反之亦然。
- 单源异常在 `AlarmService` 内部隔离，宿主循环不 try/catch 单源。
- 采集周期可配置（`AlarmOptions.PollIntervalMs`），默认 500ms；
  **报警不是安全功能**，不能用来替代急停/安全门。

---

## 10. 历史与持久化

### 10.1 一期：JSONL（P0）

`logs/alarms/alarms-YYYYMMDD.jsonl`，每行一条 `AlarmRecord` 的 JSON（UTF-8，追加写）。

- **更新语义**：同一 `Id` 的确认/恢复追加新行，查询时按 `Id` 取最后一行——不原地改写文件。
- 单写者：内部 `SemaphoreSlim(1,1)` + 后台写队列；`AppendAsync` 失败只记日志。
- 查询：读文件 → 按 `Id` 折叠最新 → 过滤 → 按 `RaisedAt` 倒序 → `Take(Limit)`。
- 按天滚动，保留 90 天（可配置）；启动时清理过期文件。
- 为什么不用直接 SQLite：宿主尚未注册 `DbContext`（连接串/迁移未闭环），
  JSONL 零依赖、可 grep、坏了不影响报警主流程；二期换实现不改接口。

### 10.2 二期：SQLite（P2）

```sql
CREATE TABLE AlarmHistory (
    Id              TEXT PRIMARY KEY,
    AlarmKey        TEXT NOT NULL,      -- SourceId|Code|RawCode
    Code            TEXT NOT NULL,
    SourceId        TEXT NOT NULL,
    SourceKind      INTEGER NOT NULL,
    Level           INTEGER NOT NULL,
    ElementId       TEXT NULL,
    ElementName     TEXT NULL,
    Title           TEXT NOT NULL,
    Message         TEXT NOT NULL,
    Remedy          TEXT NULL,
    RaisedAt        TEXT NOT NULL,      -- ISO-8601 UTC
    LastSeenAt      TEXT NOT NULL,
    RaiseCount      INTEGER NOT NULL,
    AcknowledgedAt  TEXT NULL,
    AcknowledgedBy  TEXT NULL,
    ClearedAt       TEXT NULL,
    RequiresAcknowledge INTEGER NOT NULL
);

CREATE INDEX IX_AlarmHistory_RaisedAt ON AlarmHistory (RaisedAt DESC);
CREATE INDEX IX_AlarmHistory_Active   ON AlarmHistory (ClearedAt, Level);
CREATE INDEX IX_AlarmHistory_Source   ON AlarmHistory (SourceId, RaisedAt DESC);
```

- 迁移脚本按项目约定新增 `00N_*.sql`，不改已发布脚本。
- `AlarmKey` 存成字符串便于排查；查询主要走 `SourceId`/时间索引。
- **与 JSONL 的语义差异**：JSONL 是事件流（同 Id 多行，查询时折叠）；
  SQLite 表存每个 Id 的**最新状态**（确认/恢复用 `UPDATE`/UPSERT），查询结果一致。
  实现 `SqliteAlarmHistoryStore` 时不要照搬「追加同 Id 行」的做法。

### 10.3 与审计的分工

| 内容 | 去向 | 理由 |
|---|---|---|
| 报警发生/恢复 | 报警历史（`IAlarmHistoryStore`） | 现场要查「什么时候报过什么」 |
| 报警确认 | 审计（`IAuditService`）+ 报警历史字段 | 确认是人的操作，必须留操作者 |
| 命令执行成败 | 现有审计 | 命令不是报警；失败可按需上报瞬时报警（P1） |
| 报警服务自身异常 | 结构化日志 | 服务内部故障，不是设备报警 |

---

## 11. 权限与审计

### 11.1 新权限点

```csharp
// Core/Enums/Permission.cs
/// <summary>确认报警：标记「已知悉」，不改变设备状态。</summary>
AlarmAcknowledge = 6
```

**为什么独立于 `MachineOperate`**：
确认报警不产生设备动作，观察员之外的岗位都应该能做；把它绑在 `MachineOperate` 上
会让「只想确认报警的人」同时获得启停/点动权限，违反最小权限。
「工艺/设备/管理员」天然拥有操作员权限，因此矩阵是单调的。

### 11.2 矩阵变化（`RolePermissionPolicy`）

| 权限点 | 观察员 | 操作员 | 工艺 | 设备 | 管理员 |
|---|:---:|:---:|:---:|:---:|:---:|
| `AlarmAcknowledge` | — | ✓ | ✓ | ✓ | ✓ |

- `OperatorGrants` 增加 `Permission.AlarmAcknowledge`；
- `PermissionCatalog.DisplayName` 增加「确认报警」；
- `RolePermissionPolicyTests` 的单调性测试会自动覆盖新权限点（它校验逐级包含）。

### 11.3 确认流程

```text
UI 点击「确认」
  → IAlarmService.Acknowledge(id)
      → _permissions.RequirePermission(Permission.AlarmAcknowledge)   // 越权抛异常
      → 找不到 → Result.Fail("Alarm.NotFound")
      → 已归档 → Result.Fail("Alarm.AlreadyCleared")
      → 写 AcknowledgedAt / AcknowledgedBy（当前会话用户名）
      → 更新 Snapshot、触发 Changed
      → 历史追加；审计排入队列（失败不影响确认结果）
```

- 操作者由服务从 `ISessionProvider.Current` 补齐（与 `LogAuditService` 同一模式），
  调用方不传用户名，避免「某个入口忘了带操作者」。
- 审计事件：`ALARM_ACKNOWLEDGED`、`ALARM_ACKNOWLEDGED_ALL`（新增常量）。

### 11.4 审计事件常量

```csharp
public static class AuditEventTypes
{
    // ... 现有常量
    public const string AlarmAcknowledged = "ALARM_ACKNOWLEDGED";
    public const string AlarmAcknowledgedAll = "ALARM_ACKNOWLEDGED_ALL";
}
```

---

## 12. UI 设计

### 12.1 `AlarmInfoPage`（替换占位页）

布局：

```text
┌──────────────────────────────────────────────────────────────────────┐
│ [等级 ▼] [状态 ▼] [时间范围 ▼] [刷新]        [确认] [全部确认]        │
├──────────────────────────────────────────────────────────────────────┤
│ 等级 │ 状态 │ 首次发生 │ 最近发生 │ 来源 │ 代码 │ 描述 │ 确认人/时间   │
│──────┼──────┼──────────┼──────────┼──────┼──────┼──────┼──────────────│
│ 错误 │ 未确认│ 10:21:03 │ 10:21:05 │ X轴  │AXIS.FAULT│ 伺服故障…│ —  │
│ 警告 │ 已确认│ 10:20:11 │ 10:20:11 │ 气缸1│CYLINDER.TIMEOUT│…│ 张三│
├──────────────────────────────────────────────────────────────────────┤
│ 详情：触发条件 / 恢复条件 / 处理建议 / 原始码 / 元件                  │
└──────────────────────────────────────────────────────────────────────┘
```

- 数据源：`IAlarmService.Snapshot`（活动）+ `QueryHistoryAsync`（历史）；
  **不订阅 `Changed`**，用 500ms `System.Windows.Forms.Timer` 拉快照（与现有页面一致，
  避免页面反复开关时事件泄漏）。
- 排序：等级降序 → 首次发生时间升序（最老的未确认排最前）。
- 颜色：`Critical` 深红 / `Error` 红 / `Warning` 橙 / `Info` 蓝；
  `RecoveredUnacknowledged` 灰色 + 「已恢复」标记。
- 按钮：`确认` 仅在选中项 `State == Active/RecoveredUnacknowledged` 且
  `HasPermission(AlarmAcknowledge)` 时可用；`全部确认` 同理。
- 空状态：`当前无报警`（不是空白表格）。
- 历史页签/筛选：按时间范围、等级、状态、来源；默认「今天」。
- 双击报警行：P1 再跳转到对应设备操作台（需要处理跨页面导航状态）。
- **分部类**：按 `10-desktop.md §5.1` 与 `winforms-designer` 技能，
  `AlarmInfoPage.Designer.cs` 只放控件字段与 `InitializeComponent`，行为在 `.cs`；
  无参构造只调 `InitializeComponent()`，DI 构造 `: this()`；控件字段不加 `readonly`。

### 12.2 `MainForm` 状态栏

- 新增标签：`报警：3（未确认 2）`；颜色取最高等级（红/橙）。
- 每秒刷新（现有 `_statusTimer`），从 `IAlarmService.Snapshot` 读；不订阅事件。
- 点击标签 → `ShowPage(typeof(AlarmInfoPage))`。
- 无报警：`报警：无`，正常前景色。
- 导航项「报警信息」已存在（`MainForm.cs:34`），权限仍为 `MachineView`。

### 12.3 刷新与线程

| 数据 | 刷新 | 线程 |
|---|---|---|
| 活动报警列表 | 500ms 定时拉 `Snapshot` | UI 定时器 |
| 历史列表 | 打开页面 / 切换筛选 / 确认后 | `await QueryHistoryAsync` |
| 状态栏计数 | 1s（现有） | UI 定时器 |
| 报警事件 | P2 的横幅/声音才订阅 `Changed` | 后台线程 → `BeginInvoke` |

> 报警页不需要「实时到毫秒」；500ms 与现有监控页一致，且避免每 200ms 重排表格。

---

## 13. P0 文件清单与测试

### 13.1 新增文件

| 项目 | 文件 | 内容 |
|---|---|---|
| Core | `Enums/AlarmLevel.cs` | 等级 |
| Core | `Enums/AlarmState.cs` | 生命周期状态 |
| Core | `Enums/AlarmSourceKind.cs` | 来源类别 |
| Core | `Enums/AlarmChangeKind.cs` | 事件类型 |
| Core | `Models/AlarmKey.cs` | 去重键 |
| Core | `Models/AlarmSignal.cs` | 上报事实 |
| Core | `Models/AlarmDefinition.cs` | 码表定义 |
| Core | `Models/AlarmRecord.cs` | 记录 |
| Core | `Models/AlarmSnapshot.cs` | 快照 |
| Core | `Models/AlarmHistoryQuery.cs` | 查询 |
| Core | `Models/AlarmSourceReading.cs` | 源读数 |
| Core | `Abstractions/IAlarmService.cs` | 唯一入口 |
| Core | `Abstractions/IAlarmSource.cs` | 报警源 |
| Core | `Abstractions/IAlarmCatalog.cs` | 码表 |
| Core | `Abstractions/IAlarmDefinitionProvider.cs` | 设备码表 |
| Core | `Abstractions/IAlarmHistoryStore.cs` | 历史存储 |
| Core | `Abstractions/IElementRegistry.cs` | 元件注册表 |
| Core | `Events/AlarmChangedEventArgs.cs` | 事件参数 |
| Infrastructure | `Alarms/AlarmService.cs` | 状态机 + diff + 确认 |
| Infrastructure | `Alarms/AlarmCatalog.cs` | 码表合并/回退 |
| Infrastructure | `Alarms/ElementRegistry.cs` | 装配结果注册表 |
| Infrastructure | `Alarms/Definitions/AxisAlarmDefinitions.cs` | 轴内置码表 |
| Infrastructure | `Alarms/Definitions/CylinderAlarmDefinitions.cs` | 气缸内置码表 |
| Infrastructure | `Alarms/Definitions/SystemAlarmDefinitions.cs` | 服务自身码（`COMM.SOURCE_OFFLINE`） |
| Infrastructure | `Alarms/Sources/AxisAlarmSource.cs` | 轴报警源 |
| Infrastructure | `Alarms/Sources/CylinderAlarmSource.cs` | 气缸报警源 |
| Infrastructure | `Alarms/Sources/DemoAlarmSource.cs` | 演示源（仅 `--demo-alarms` 注册） |
| Infrastructure | `Alarms/History/JsonlAlarmHistoryStore.cs` | JSONL 历史 |
| Infrastructure | `Configuration/AlarmOptions.cs` | 配置模型 |
| Desktop | `Monitoring/AlarmMonitorService.cs` | 500ms 采集宿主 |
| Desktop | `Forms/AlarmInfoPage.cs` + `.Designer.cs` | 报警页 |

### 13.2 修改文件

| 文件 | 改动 |
|---|---|
| `Core/Enums/Permission.cs` | 新增 `AlarmAcknowledge = 6` |
| `Core/Models/PermissionCatalog.cs` | 显示名 |
| `Core/Abstractions/IAuditService.cs` | 审计事件常量 |
| `Core/Models/AxisStatus.cs` | 新增 `IsWarning` |
| `src/EquipmentMonitor.Core/Models/AxisFeedbackExtensions.cs` | 映射 bit4 |
| `src/EquipmentMonitor.Core/Axis/AxisElement.cs` | `ComputeElementState` 处理 Warning（P0）；注入 `IAlarmService`（P1） |
| `Infrastructure/Axis/AxisFrameworkBootstrapper.cs` | 装配成功后 `ElementRegistry.Attach` |
| `Infrastructure/DI/InfrastructureModuleRegister.cs` | 注册报警服务与源 |
| `Infrastructure/Security/RolePermissionPolicy.cs` | Operator 起授予 `AlarmAcknowledge` |
| `Desktop/Forms/PlaceholderPage.cs` | 移除 `AlarmInfoPage` 占位类 |
| `Desktop/Forms/MainForm.cs` + `.Designer.cs` | 状态栏报警标签 + 点击导航 |
| `Desktop/Program.cs` | 注册 `AlarmMonitorService` + 报警页构造 |
| `docs/module-design/README.md` | 索引加本文 |
| `docs/module-design/10-desktop.md` | 报警页状态改为「设计完成，P0 实施中」 |
| `docs/module-design/11-authorization.md` | 权限点/矩阵 |
| `docs/SRS.md` §10.3 / §15.1 | 报警现状与追踪矩阵 |

### 13.3 测试清单（P0）

| 测试 | 覆盖 |
|---|---|
| `AlarmServiceTests` | 上升沿/重复合并/确认/恢复/恢复未确认保留/抖动复用/新实例/源离线冻结/恢复清除/单源异常隔离/瞬时 AutoClear/快照排序/确认越权 |
| `AlarmCatalogTests` | 已定义/未定义回退/后注册覆盖 |
| `JsonlAlarmHistoryStoreTests` | 追加/按 Id 折叠/过滤/损坏行跳过 |
| `AlarmSourceTests` | 轴故障映射/RawCode/禁用元件忽略；气缸 1001/1002/其他码/正常 |
| `DemoAlarmSourceTests` | 演示源固定三条报警、SourceId 前缀、等级/原始码 |
| `AlarmMonitorServiceTests` | 周期与最小 100ms 夹取；单轮调用 |
| `AlarmInfoPageTests` | 列表渲染/空状态/确认按钮权限/确认调用/历史切换 |
| `MainFormTests` | 状态栏「报警：无」 |
| `AxisFeedbackExtensionsTests` | 状态字 bit4 → `IsWarning` |
| `RolePermissionPolicyTests` | 单调性包含新权限点 |
| `DesktopHostTests` | `IAlarmService` / `AlarmMonitorService` 可解析、页面可解析 |

### 13.4 验证

```powershell
dotnet build AxisFramework.slnx -t:Rebuild   # 0 错误，33 条既有可空警告
dotnet test  AxisFramework.slnx              # 201 条全绿（轴框架 148 + 桌面 53）
```

UI 改动按 `change-workflow §5` **实跑**：登录 → 打开报警页 → 空态显示「当前无报警」，
历史按钮切换后显示「历史报警 0 条（近 7 天）」；状态栏显示「报警：无」；
`确认`/`全部确认` 在无记录时置灰。另用 `--demo-alarms` 实跑三条模拟报警与确认流程（见 §13.5）。
`Sim` 控制器当前没有 UI 入口注入故障，**报警触发/确认/恢复/历史由自动化测试覆盖**
（`AlarmServiceTests` + `AlarmSourceTests` + `AlarmInfoPageTests`），不把“测试绿”当成故障注入实跑。

### 13.5 P0 落地记录（2026-11-01）

| 项 | 结果 |
|---|---|
| Core 契约 | 枚举 4、模型 7、接口 6、事件 1；`AlarmAcknowledge` 权限点；`AxisStatus.IsWarning` |
| Infrastructure | `AlarmService`（diff/确认/离线冻结/抖动复用）、`AlarmCatalog`、`ElementRegistry`、轴/气缸源、JSONL 历史 |
| Desktop | `AlarmMonitorService`（500ms）、`AlarmInfoPage`（活动/历史/确认/筛选）、`MainForm` 状态栏报警链接 |
| 测试 | 新增 41 条（报警 P0）；当前全量 **201**（轴框架 148 + 桌面 53，基线 152）；构建 0 错误 |
| 与设计的偏差 | ① `AlarmService` 用 `Serilog.Log.ForContext<T>()`（Infrastructure 无 `Microsoft.Extensions.Logging` 包），符合 `coding-standards §3` 的允许方式；② `alarms.json` 配置加载留到 P1，P0 用内置码表 + 默认 `AlarmOptions`；③ `AxisFrameworkBootstrapper` 的 `ElementRegistry` 参数可选，保持既有测试直接构造；④ 命令失败注入、通信源、PLC 报警字按计划留在 P1 |

**演示模式（仅验收/演示）**：启动时加 `--demo-alarms`，注册 `DemoAlarmSource`，固定产生三条报警
（`AXIS.FAULT` 码 42、`AXIS.WARNING`、`CYLINDER.TIMEOUT` 码 1001）。默认不注册，现场启动不会凭空出现报警。

- **Visual Studio**：调试目标下拉框选 `DemoAlarms`（`Properties/launchSettings.json` 已配置 `--demo-alarms`），直接 F5。
- **命令行**：

```powershell
dotnet run --project src/EquipmentMonitor.Desktop/EquipmentMonitor.Desktop.csproj --launch-profile DemoAlarms
```

实跑结果：报警页显示 3 条未确认、状态栏「报警：3（未确认 3）」；确认首条后变为「已确认」，
状态栏变为「报警：3（未确认 2）」，历史记录可查。

---

## 14. 分期

| 阶段 | 交付 | 退出条件 |
|---|---|---|
| **P0** | Core 契约 + `AlarmService` + 轴/气缸源 + JSONL 历史 + 确认权限 + 报警页/状态栏 + 测试 | ✅ 2026-11-01：全量 201 条测试绿；页面空态与 `--demo-alarms` 演示实跑通过 |
| **P1** | 命令失败 `Report`、通信源、PLC 报警字配置、报警横幅/声音、历史筛选/CSV 导出、双击跳转 | 真实 PLC 报警字能映射；命令失败进报警 |
| **P2** | SQLite 历史、报警规则引擎（阈值/持续时长）、MES/SCADA 上行、报警联动（独立功能） | 现场 72h 运行；历史可查询/导出 |

---

## 15. 待决问题

| # | 问题 | 建议 | 影响 |
|---|---|---|---|
| Q1 | 「确认报警」是否独立权限点？ | **独立**（`AlarmAcknowledge`），操作员起授予 | 影响权限矩阵与测试；见 §11 |
| Q2 | 历史一期 JSONL 是否可接受？ | 可以；接口已定，二期换 SQLite 不改调用点 | 现场查历史依赖文件；需备份策略 |
| Q3 | 报警是否应自动停机？ | **否**。联锁/急停由 PLC/硬件负责，报警只记录与提示 | 避免报警服务成为安全回路的一部分 |
| Q4 | 报警文本由谁维护？ | PLC/驱动器手册为准，现场用 `alarms.json` 覆盖内置码表 | 需要交付「报警清单」（SRS §14） |
| Q5 | 轴/气缸源从注册表读，还是把快照仓下沉？ | 注册表。快照仓属于宿主展示层，不下沉 | 影响 `ElementRegistry` 的引入 |
| Q6 | 报警页是否需要跨页跳转到操作台？ | P1 再做；P0 只显示元件名 | 跨页导航会牵动 `MainForm` 的页面状态 |

---

## 16. 与现有文档的关系

- [总体架构](../architecture.md)：接口下沉模式（§4）与线程模型（§5.2）适用于本设计；
  无新增项目引用边，`Infrastructure → Core` 已存在。
- [10-desktop.md](10-desktop.md)：报警页已从「占位」替换为可用页面；
  `MainForm` 状态栏与采集服务在此登记。
- [11-authorization.md](11-authorization.md)：新增 `AlarmAcknowledge` 权限点与矩阵行。
- [SRS.md](../SRS.md) §10.3 / §15.1：报警数据要求与追踪矩阵更新。
- [06-devices.md](06-devices.md) §15.4：旧 `ServoAlarmTexts` 的「未配置回退报警码 N」规则由
  本设计的 `AlarmCatalog` 回退继承；旧配置格式不迁移，码表改由 `alarms.json` 维护。
- [axis-command-interface.md](../axis-command-interface.md)：bit4「报警（可继续运行）」
  在 P0 补进 `AxisStatus.IsWarning` 后与此表一致。
