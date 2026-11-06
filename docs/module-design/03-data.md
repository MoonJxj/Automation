# Data 模块设计

> 项目：`EquipmentMonitor.Data` ｜ 依赖：`Core`、`Shared`
> 选型：**Dapper 2.x** + **Microsoft.Data.Sqlite** + SQLite 3.x

## 1. 职责边界

**做什么**：数据库连接管理、Schema 初始化与升级、仓储实现、Entity 定义。

**不做什么**：

- ❌ 不含业务规则（不做权限判断、不做数据校验之外的业务逻辑）
- ❌ 不引用任何 UI 类型
- ❌ 不感知 Serilog、NModbus 等具体实现库

**仓储接口放在哪里**：本模块的仓储接口**定义在 `Data` 内**，不放 `Core`。
理由见 [§3 关于接口下沉的边界](#3-关于接口下沉的边界)。

## 2. 依赖关系

```
EquipmentMonitor.Data ──► Core, Shared
```

NuGet 依赖：`Dapper`、`Microsoft.Data.Sqlite`。

> **选用 `Microsoft.Data.Sqlite` 而非 `System.Data.SQLite`**：
> 前者由 .NET 团队维护，与 .NET 8 的异步 IO 和连接池集成更好，无需额外的原生 DLL 分发配置。

## 3. 关于接口下沉的边界

[架构文档 §4](../architecture.md#4-接口下沉模式) 提出"接口定义在 `Core`，实现在业务模块"。**该模式有明确适用范围**：

| 适用「下沉到 Core」 | 不适用（就近定义） |
|---|---|
| **跨模块的服务抽象** | **数据访问接口** |
| `ICommunicationChannel`（Devices 与 App 都要用） | `IUserRepository`（仅 Security 用） |
| `IComponentManager`（App 要用） | `IDeviceConfigRepository`（仅 App/Security 用） |
| `ILogService`（所有模块要用） | |

**理由**：若把 `IUserRepository` 下沉到 `Core`，则 `Core` 必须同时持有 `UserEntity`——
数据库表结构、连接串、SQL 方言等概念会渗透进契约层，`Core` 立刻被污染。
而仓储的使用者（`Security`）本来就已通过依赖矩阵获准引用 `Data`，不存在"为了解耦而绕路"的必要。

**结论**：
- **服务类**抽象 → `Core/Abstractions`
- **数据访问类**抽象 → 各 `Data` 模块内部（`Repositories/` 目录下与实现同放，接口在前、实现在后）

## 4. 目录结构

```
EquipmentMonitor.Data/
├── SqliteConnectionFactory.cs       # 连接创建与 PRAGMA 配置（原 DbContext.cs，见待细化事项）
├── DbInitializer.cs                 # Schema 初始化与版本升级
├── DatabaseOptions.cs               # 强类型配置
├── Repositories/
│   ├── IUserRepository.cs
│   ├── UserRepository.cs
│   ├── IDeviceConfigRepository.cs
│   ├── DeviceConfigRepository.cs
│   ├── IDeviceInfoRepository.cs
│   └── DeviceInfoRepository.cs
├── Entities/
│   ├── UserEntity.cs
│   ├── DeviceConfigEntity.cs
│   └── DeviceInfoEntity.cs
└── Scripts/                         # 内嵌资源（EmbeddedResource）
    ├── 001_initial_schema.sql
    └── 002_add_device_info_index.sql
```

> ⚠️ **命名提醒**：原方案中的 `DbContext.cs` 已更名为 `SqliteConnectionFactory.cs`。
> `DbContext` 是 EF Core 的专有类型名，而本项目使用 Dapper，保留该名称会让后来者误以为用的是 EF Core。
> 详见[架构文档待决问题 #2](../architecture.md#9-待决问题)。

## 5. 核心设计

### 5.1 连接管理

```csharp
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IOptions<DatabaseOptions> options);

    /// <summary>创建并打开连接，已应用必需的 PRAGMA。</summary>
    public async Task<SqliteConnection> OpenAsync(CancellationToken ct = default);

    /// <summary>在事务中执行一组操作，自动重试 SQLITE_BUSY。</summary>
    public Task<Result<T>> ExecuteInTransactionAsync<T>(
        Func<SqliteConnection, SqliteTransaction, Task<T>> action,
        CancellationToken ct = default);
}
```

**必需的 PRAGMA**（在连接打开后立即设置）：

| PRAGMA | 值 | 原因 |
|---|---|---|
| `journal_mode` | `WAL` | 允许读写并发。默认的 `DELETE` 模式下，采集线程写入时会阻塞 UI 读取 |
| `busy_timeout` | `5000` | 写锁竞争时等待而非立即报 `database is locked` |
| `foreign_keys` | `ON` | SQLite 默认**不启用**外键约束，必须显式打开 |
| `synchronous` | `NORMAL` | WAL 下的推荐值，兼顾安全与性能（`FULL` 会显著降低写入速度） |

> ⚠️ `journal_mode` 是**数据库级持久设置**，只需设置一次；其余是**连接级**，每次打开都要设置。

### 5.2 写入并发——本模块最大的架构约束

**SQLite 在任意时刻只允许一个写事务。** 这是 SQLite 的固有限制，不是配置问题。

本项目中存在两个写入来源：

1. **低频**：用户管理、设备配置保存（人工触发，几秒一次）
2. **高频**：设备状态/历史数据写入（由轮询驱动，可能每秒数十至数百次）

若高频写入直接落在轮询线程上，会与配置保存争抢写锁，表现为**界面保存配置时偶发卡顿数秒**。

**解决方案**：为高频写入引入**异步批量写入队列**。

```csharp
public sealed class DeviceInfoRepository : IDeviceInfoRepository
{
    // 写入进入内存队列立即返回；后台单线程消费者按批（如每 500ms 或累积 100 条）落库
    public Task<Result> EnqueueAsync(DeviceInfoEntity entity, CancellationToken ct = default);

    // 查询走独立连接，可读（WAL 下读不阻塞写）
    public Task<Result<IReadOnlyList<DeviceInfoEntity>>> QueryAsync(
        DeviceInfoQuery query, CancellationToken ct = default);
}
```

**要点**：

- 队列**必须有界**（如容量 10000）。队满时按策略丢弃最旧数据并记 `Warning`，
  **绝不无限增长**——这是长周期运行不 OOM 的硬性要求。
- 后台消费者是**单线程**的（保证只有一个写事务），且写入按批提交事务。
- 进程退出时需**冲刷（flush）队列**，避免丢失最后一批数据。

> 该方案的完整设计需在本模块细化阶段确定，**这是 Data 模块最需要先解决的技术问题**。

### 5.3 Schema 版本管理（Dapper 无迁移能力）

Dapper 不提供迁移，需自行实现一个**最小可用**的版本管理：

```csharp
public sealed class DbInitializer
{
    public async Task<Result> InitializeAsync(CancellationToken ct = default);
}
```

机制：

1. 检查 `schema_version` 表是否存在，不存在则创建。
2. 读取当前版本号（无记录视为 0）。
3. 按序号**升序**执行 `Scripts/` 下所有序号大于当前版本的脚本（内嵌资源，按文件名排序）。
4. 每个脚本在**同一事务**中执行 DDL 并更新 `schema_version`。

**设计取舍**：

- **不做回滚（down migration）**。上位机是单机部署，回滚场景罕见；
  引入 down 脚本会让每个变更的维护成本翻倍。确需回退时由现场备份文件恢复。
- **不做校验和比对**。已执行的脚本一律不再校验内容，
  因此**已发布的脚本文件严禁修改**——必须新建 `00N_xxx.sql`。
  这一条应写入团队约定。
- **启动时自动升级**。首次运行（或版本落后）时自动执行，无需人工干预。

### 5.4 Entity 与 Model 的边界

`UserEntity` 与 `Core` 中的 `UserInfo` **不是重复定义，职责不同**：

| 类型 | 位置 | 含密码哈希 | 流转范围 |
|---|---|---|---|
| `UserEntity` | `Data/Entities` | ✅ 是 | 仅限 `Data` 与 `Security` 内部 |
| `UserInfo` | `Core/Models` | ❌ 否（脱敏） | 可跨模块，可到达 UI |

**约定**：`UserEntity` **不得**出现在 `Security` 之外的任何模块签名中，
更不得传递给 `App`。跨模块传递一律转换为 `UserInfo`。
这是防止密码哈希意外泄露到日志或界面的第一道防线。

## 6. 仓储契约

```csharp
public interface IUserRepository
{
    Task<UserEntity?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<UserEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<UserEntity>> GetAllAsync(CancellationToken ct = default);
    Task<Result<int>> InsertAsync(UserEntity entity, CancellationToken ct = default);
    Task<Result> UpdateAsync(UserEntity entity, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public interface IDeviceConfigRepository
{
    Task<IReadOnlyList<DeviceConfigEntity>> GetAllAsync(CancellationToken ct = default);
    Task<DeviceConfigEntity?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<Result> UpsertAsync(DeviceConfigEntity entity, CancellationToken ct = default);
    Task<Result> DeleteAsync(string name, CancellationToken ct = default);
}

public interface IDeviceInfoRepository
{
    // 见 §5.2 —— 高频写入走队列
    Task<Result> EnqueueAsync(DeviceInfoEntity entity, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DeviceInfoEntity>>> QueryAsync(DeviceInfoQuery query, CancellationToken ct = default);
    Task<Result<int>> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
```

**约定**：

- 查询返回 `null`（未找到）而非抛异常——"未找到"是预期情况。
- 写入返回 `Result`，失败信息可读（如唯一键冲突）。
- 所有方法**异步且接受 `CancellationToken`**，即便当前 SQLite 的异步是"假异步"
  （`Microsoft.Data.Sqlite` 的异步方法实际同步执行），也保持签名一致以便将来更换数据库。

## 7. 扩展点

| 需求 | 做法 |
|---|---|
| 新增业务表 | 加 `00N_xxx.sql` 内嵌脚本 + 对应的 Entity 与 Repository |
| 更换数据库 | 替换 `SqliteConnectionFactory` 与 SQL 方言。**注意**：SQL 中避免使用 SQLite 特有语法（如 `AUTOINCREMENT`、`INSERT OR REPLACE`）可降低迁移成本 |
| 新增查询条件 | 在 `Query` 模型（如 `DeviceInfoQuery`）中扩展属性，Repository 内动态拼 WHERE 并用参数化 |
| 归档历史数据 | 由 `IDeviceInfoRepository.PurgeOlderThanAsync` + 后台 Cron 任务组合实现 |

## 8. 安全约定

- **一切 SQL 必须参数化**。Dapper 的匿名对象参数天然防注入，
  但**动态 ORDER BY / 表名 / 列名无法参数化**，必须用白名单校验。
- 密码字段仅存**加盐哈希**（算法由 [Security 模块](04-security.md)决定），
  `Data` 层不感知哈希算法，只负责存储。
- 数据库文件路径应放在**有写权限且不被普通用户随意读取**的目录，
  并在部署说明中提示现场设置文件 ACL。

## 9. 测试要点

- **使用真实的 SQLite 文件或 `:memory:` 数据库**，不要 Mock Dapper——
  测试的价值恰在于验证 SQL 正确性。
- 重点覆盖：
  - `DbInitializer` 在空库、旧版本库、已是当前版本三种情况下的行为
  - **升级脚本的幂等性与顺序**（连续执行两次不应报错）
  - `busy_timeout` 与 WAL 在并发读写下的表现
  - 队列有界性：写满后丢弃最旧数据而非无限增长
  - 唯一键冲突、外键约束的实际行为

## 10. 待细化事项

- [ ] **高频写入队列的完整设计**（容量、批量阈值、冲刷时机、丢弃策略）——优先级最高
- [ ] `DeviceInfoEntity` 的"大数据"定位：
      若为高频时序数据，需实测 SQLite 写入吞吐上限；
      数据量超过千万行时考虑**按月分表**或引入独立时序库
- [ ] 索引设计：`DeviceInfoEntity` 按 `设备编码 + 时间` 建立复合索引；
      `UserEntity.Username` 建唯一索引
- [ ] 是否需要数据备份与恢复功能（SQLite 可在线备份，是否暴露给用户操作）
- [ ] `DatabaseOptions` 的字段构成与默认值
- [ ] 时间字段的存储格式：建议统一存 **UTC 时间戳（Unix 秒或 ISO8601）**，
      避免现场跨时区或夏令时导致的排序错误

## 11. 相关文档

- [总体架构](../architecture.md)
- [Shared 模块](00-shared.md)
- [Security 模块](04-security.md)
- [Devices 模块](06-devices.md)
