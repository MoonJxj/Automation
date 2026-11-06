# 总体架构设计

> 本文档为**框架级**设计，定义分层边界、依赖规则与关键接口。各模块的实现细节见 [`module-design/`](module-design/) 下对应文档。

## 1. 项目定位

工业设备监控上位机：通过多种工业协议接入现场设备（PLC、伺服、气缸、传感器），
完成实时状态采集、协议调试、设备配置与用户权限管理，并提供可视化的监控与调试界面。

**核心特征**（决定了后续所有架构取舍）：

- **长周期运行**：现场上位机通常连续运行数周至数月，不允许内存泄漏与句柄泄漏。
- **实时性敏感**：轮询周期通常在 50–500ms 量级，UI 不得阻塞采集线程。
- **现场环境不稳定**：网络抖动、设备掉线、串口拔出是常态，必须具备自动重连与降级能力。
- **多协议并存**：同一台上位机往往同时对接 Modbus 设备、AB PLC、MQTT 网关等。

## 2. 技术选型

| 模块 | 选型 | 版本基线 | 许可证 | 备注 |
|---|---|---|---|---|
| 运行时 | .NET | 8.0 (LTS) → 建议迁移 10.0 | — | 见下方 [2.1 关于 .NET 版本](#21-关于-net-版本) |
| UI | WinForms | — | — | 用户指定 |
| UI 控件库 | SunnyUI.NET | 最新稳定版 | MIT | 70+ 工控控件与仪表盘 |
| 日志 | Serilog | 3.x | Apache 2.0 | 结构化日志，多 Sink 分文件输出 |
| Modbus | NModbus | 3.0.83 | **MIT** | TCP / UDP / 串口 RTU / ASCII。串口传输自实现，理由见 [Communication 模块 §4.2](module-design/05-communication.md#42-nmodbus-的串口支持为什么不用-nmodbusserial) |
| MQTT | MQTTnet | **5.2.0.1603** | **MIT** | 包内 LICENSE 实读确认，v5 未改授权 |
| OPC UA | LibUA | 1.0.1 | **Apache 2.0** | 通道尚未实现（框架 + TODO），包暂未引入 |
| EtherNet/IP | **EEIP.NetStandard** | 1.0.2 | **MIT** | 用于 PC↔MES 通讯。见 [2.2](#22-关于-ethernetip-库选型) |
| 串口 / Socket | System.IO.Ports + System.Net.Sockets | BCL | — | .NET 内置 |
| 状态机 | Stateless | 5.x | Apache 2.0 | 支持层级状态、守卫条件、进入/退出动作 |
| ORM | **Dapper** | 2.x | Apache 2.0 | 见 [2.3](#23-关于-orm-选型) |
| 数据库 | **SQLite** | 3.x | Public Domain | 单机零部署，文件级备份 |
| DI 容器 | Microsoft.Extensions.DependencyInjection | 8.x | MIT | 与 Generic Host 集成 |
| 后台任务 | NCrontab.Scheduler | 2.x | MIT | 轻量 Cron 调度 |
| 测试框架 | xUnit + FluentAssertions | — | Apache 2.0 | 见 [8. 测试策略](#8-测试策略) |

### 2.1 关于 .NET 版本

**.NET 8 LTS 的支持截止日期为 2026-11-10**，距今不足两个月。新项目建议直接采用 **.NET 10 LTS**（支持至 2028-11）。

若因现场环境约束必须停留在 .NET 8，需将 `<TargetFramework>` 统一锁定为 `net8.0-windows`，
并在 `Directory.Build.props` 中集中管理，避免各项目不一致。
#注 现场环境可能会出现WIN7系统，但我们还是推荐使用.NET 10 以获取支持。考虑在win7 系统离线部署.NET 10。

### 2.2 关于 EtherNet/IP 库选型

> ⚠️ **本节在 M3 阶段被修正。** 原结论是「选 libplctag.NET」，
> 经许可证实读与场景澄清后改为 **EEIP.NetStandard**。

选择 **EEIP.NetStandard 1.0.2**（许可证：**MIT**，nuget nuspec 实读确认）。理由两条，且指向同一结论：

**① 适用性：本项目的 EtherNet/IP 用于 PC ↔ MES 通讯，不是连 AB PLC。**
libplctag 的核心价值是 AB/Rockwell（ControlLogix / CompactLogix / SLC）的**符号化 Tag 访问**，
而这类场景本项目用不到。它反而带来额外负担：依赖 `win-x64` 原生二进制、
不支持 CIP Security / TLS、单个 Tag 实例非线程安全。
EEIP.NetStandard 是纯托管的通用 CIP 实现，与本场景匹配。

**② 许可证：libplctag 是 MPL-2.0，不满足本项目「MIT 或 Apache 2.0」的约束。**
（`libplctag` 1.5.2 的 nuspec 实读为 `MPL-2.0`。）

**备选与本项目已排除的选项**：

| 包 | 许可证 | 结论 |
|---|---|---|
| EEIP.NetStandard | MIT | ✅ 采用 |
| libplctag | MPL-2.0 | ❌ 许可不符（且能力用不上） |
| `EEIP.NET` | — | ❌ **NuGet 上不存在该包名**，真实包名是 `EEIP.NetStandard` |

**注意**：EtherNet/IP 通道目前是「框架 + TODO」骨架，尚未实现。
详见 [Communication 模块文档](module-design/05-communication.md) 与通道类的 TODO 清单。


#注 EtherNet/IP 主要为了与 MES 系统或其他PC通讯，暂时不考虑使用 EtherNet/IP 连接 PLC 设备  AB/Rockwell（ControlLogix / CompactLogix / SLC / MicroLogix）。
### 2.3 关于 ORM 选型

选择 **Dapper**：

- 使用最广、社区最活跃、文档与问答资源最丰富，长期维护风险最低。
- 性能与内存占用最优（微 ORM，接近原生 ADO.NET），契合上位机"资源受限 + 高频写入"的场景。
- 本系统的表结构规模有限（用户、设备配置、设备信息），手写 SQL 完全可控。

**代价与应对**：Dapper 无变更跟踪、无 LINQ、**无数据库迁移能力**。
迁移能力由 [3. 数据模块](module-design/03-data.md) 中的内嵌 SQL 脚本 + `schema_version` 表自行实现。

**备选**：SqlSugar 在国内上位机项目中实际出现频率更高，易用性更好，若团队有相关经验可平滑替换。
FreeSql 功能更全（批量操作、多库适配），但有对比文章提到其更新节奏偏慢、社区问答资源较少。
Repository 接口定义在 `Core` 层，替换 ORM 的改动被限制在 `Data` 模块内部。
#注 使用 SqlSugar 。 SqlSugar 开发难度低，开发速度快，国内支持高。

## 3. 分层架构

```
┌────────────────────────────────────────────────────────────────────┐
│  L4  EquipmentMonitor.App                                          │
│      WinForms 窗体 · 导航容器 · Generic Host · DI 注册 · 配置绑定     │
├────────────────────────────────────────────────────────────────────┤
│  L3  业务模块（互相之间按下方矩阵引用，不形成环）                        │
│                                                                    │
│   Logging        Security      Communication     Devices    Data   │
│   日志配置        认证授权        协议实现          元件管理   数据访问  │
├────────────────────────────────────────────────────────────────────┤
│  L2  EquipmentMonitor.Core     接口 / 模型 / 枚举 / 事件            │
│     EquipmentMonitor.Shared    Result / Extensions / Constants     │
│     ↑ 两者均为叶子模块：不依赖任何其他项目，且不引入业务 NuGet 包        │
└────────────────────────────────────────────────────────────────────┘

                            ▲
             只允许 上层 → 下层 的单向引用
             禁止任何形式的反向引用与跨层跳跃
```

### 3.1 依赖矩阵

行 = 依赖方，列 = 被依赖方。`√` 表示允许引用。

| ↓ 依赖方 \ 被依赖 → | Core | Shared | Data | Security | Communication | Devices | Logging |
|---|---|---|---|---|---|---|---|
| **Shared** | — | — | | | | | |
| **Core** | — | √ | | | | | |
| **Data** | √ | √ | — | | | | |
| **Security** | √ | √ | √ | — | | | |
| **Communication** | √ | √ | | | — | | |
| **Devices** | √ | √ | | | √ | — | |
| **Logging** | √ | √ | | | | | — |
| **App** | √ | √ | √ | √ | √ | √ | √ |

**规则说明**：

- ⚠️ **`Core → Shared`（M2 阶段新增）**。原设计声明两者互不依赖，但落地时发现
  `IAuthService` / `IPermissionService` 的返回值需要表达「预期内的失败」，
  而这正是 `Shared.Result<T>` 的职责。在 `Core` 里再定义一套 `Result` 会产生两种失败表示；
  改用抛异常则违反「预期内失败不抛异常」的约定，且异常容易被 `catch` 吞掉而丢失审计记录。
  `Shared` 仍是叶子节点，依赖图无环。
  详见 [Security 模块文档 §2.1](module-design/04-security.md#21-对架构文档的一处修改)。
- `Core` 仅依赖 `Shared`；`Shared` 是唯一的叶子节点。
- `Devices → Communication`：元件需要直接调用通讯通道读写数据。
- `Security → Data`：认证需要查询用户表。因此 **`UserRepository` 只应存在于 `Data` 模块**
  （见 [9. 待决问题](#9-待决问题)）。
- `Logging` 不依赖 `Data`：日志写入文件与内存，不落库（避免日志本身成为数据库负载来源）。

## 4. 接口下沉模式

本架构的核心模式：**接口定义在 `Core`，实现在各业务模块，由 `App` 在启动时绑定**。

| 接口（`Core/Abstractions`） | 实现（所属模块） |
|---|---|
| `ILogService` | `Logging.SerilogLogService` |
| `IAuthService` | `Security.AuthService` |
| `ICommunicationChannel` | `Communication.Channels.*`（7 种协议） |
| `IComponentManager` | `Devices.ComponentManager` |
| `IDeviceElement` | `Devices.Elements.*`（伺服/气缸/传感器/监控） |

**收益**：

1. 业务模块之间不需要互相引用——`Devices` 只认 `ICommunicationChannel` 接口，不认具体协议。
2. `Core` 零外部依赖，所有接口可被单元测试直接 Mock，无需启动 WinForms。
3. 新增协议 / 新增元件类型时，改动被隔离在单一模块内，符合开闭原则。

**约束**：`Core` 项目**不得引用任何第三方 NuGet 包**（BCL 除外）。
一旦 `Core` 引入了 Serilog 或 NModbus 的类型，整个分层就退化为强耦合。

## 5. 运行时数据流

### 5.1 采集主链路

```
 现场设备                  L3                        L3                    L4
┌──────────┐      ┌──────────────────┐    ┌──────────────────┐   ┌─────────────┐
│ PLC      │      │ Communication    │    │ Devices          │   │ App         │
│ 伺服驱动  │◄────►│ Manager          │◄──►│ ComponentManager │──►│ MainForm    │
│ 气缸     │ 物理 │  └ Channels[]    │    │  └ Elements[]    │事件│ 仪表盘/列表  │
│ 传感器    │ 层   │     · ModbusTcp  │    │     · Stateless  │   │             │
│ MQTT网关 │      │     · ModbusRtu  │    │       状态机      │   │  BeginInvoke │
│ OPC UA   │      │     · Serial/Sock│    │     · 轮询服务    │   │  切回UI线程  │
└──────────┘      │     · EthernetIp │    └──────────────────┘   └─────────────┘
                  │     · Mqtt/OpcUa  │              │
                  └──────────────────┘              │
                            │                       │
                            └───────┬───────────────┘
                                    ▼ 结构化日志
                          ┌───────────────────┐
                          │ Logging (Serilog) │
                          │  文件 / 内存 Sink  │
                          └───────────────────┘
```

**时序要点**：

1. `DevicePollingService` 按元件配置的周期，在后台线程调用 `ICommunicationChannel` 读取原始数据。
2. 原始数据（寄存器/线圈/Tag 值）经元件解析为工程量（如"气缸伸出"、"伺服到位"）。
3. 状态变化驱动 Stateless 状态机迁移，触发 `DeviceStateChanged` 事件。
4. 事件在**后台线程**上引发，`App` 层必须通过 `Control.BeginInvoke` 切回 UI 线程后再更新界面。

### 5.2 线程模型

| 线程            | 职责                                   | 约束                                                 |
| ------------- | ------------------------------------ | -------------------------------------------------- |
| **UI 线程**     | 窗体绘制、用户交互                            | 禁止任何阻塞调用（同步 IO、`Thread.Sleep`、`.Result`/`.Wait()`） |
| **轮询线程池**     | `DevicePollingService` 与各元件的采集任务     | 每个通道独立串行，避免同一通道并发读写                                |
| **通讯 IO 线程**  | 由 NModbus / MQTTnet / libplctag 内部管理 | 不在其上做业务处理                                          |
| **Cron 调度线程** | `LogCleanupService` 等定时任务            | —                                                  |

**强制约束**：

- 同一 `ICommunicationChannel` **实例上的读写必须串行化**（用 `SemaphoreSlim(1,1)` 或专用队列）。
  原因：Modbus RTU 等串口协议物理上不支持并发；libplctag 单个 Tag 实例也非线程安全。
- 跨线程更新 UI 一律使用 `Control.BeginInvoke`（异步，不阻塞后台线程），**不要用 `Invoke`**。
- 所有后台循环必须接受 `CancellationToken`，并在 `FormClosing` 时优雅停止。

## 6. 配置模型

配置分三层，优先级从低到高：

1. **`appsettings.json`**（随程序发布）：默认连接、日志级别、轮询周期等。
2. **用户配置**（SQLite `device_config` 表）：设备清单、通道参数、点位映射，由 `DeviceConfigForm` 维护。
3. **用户偏好**（本地文件）：窗体位置、列宽、上次登录用户等。

```
appsettings.json
├── Serilog              # 日志级别、文件路径、保留天数
├── Database             # SQLite 路径、WAL、超时
├── Communication        # 各通道默认超时、重试次数、重连间隔
├── Devices              # 默认轮询周期、状态机超时
└── Security             # 认证策略启用顺序、会话超时、密码策略
```

**约定**：配置通过 `IOptions<T>` 强类型绑定注入，**禁止在业务代码中直接读 `IConfiguration`**。
配置模型类定义在各自模块内，`App` 在启动时注册绑定。

## 7. 横切关注点

### 7.1 错误处理与降级

- 统一返回类型 [`Result` / `Result<T>`](module-design/00-shared.md)——预期内的失败（校验不通过、设备离线）走 `Result`，
  预期外的异常（Bug、IO 故障）才 `throw`。
- **通讯层失败必须降级而非崩溃**：单次读写超时仅记录日志并标记通道不健康，
  由 `CommunicationManager` 按退避策略重连，绝不允许异常冒泡到 UI 线程导致进程退出。
- 全局兜底：`App` 注册 `Application.ThreadException` 与 `AppDomain.UnhandledException`，记录日志并提示用户。

### 7.2 日志

- 采用 Serilog 结构化日志，**禁止字符串拼接**，一律使用消息模板：
  ```csharp
  // 正确
  Log.Information("元件 {ElementName} 状态由 {OldState} 迁移至 {NewState}", name, oldState, newState);
  // 错误
  Log.Information($"元件 {name} 状态变为 {newState}");
  ```
- 各模块输出到独立文件，便于现场排障时快速定位（详见 [Logging 模块](module-design/02-logging.md)）。

### 7.3 权限

- `SecurityLevel` 驱动控制方案：不同安全等级的用户可见的窗体、可执行的写操作不同。
- **UI 隐藏不等于权限控制**：窗体层的按钮禁用只是体验优化，真正的校验必须在业务层执行。

## 8. 测试策略

| 层次 | 范围 | 是否依赖硬件 |
|---|---|---|
| **单元测试** | `Core` 模型与枚举、状态机迁移逻辑、协议报文编解码 | 否，全部 Mock |
| **集成测试** | `Communication` 各通道（对接 Modbus 从站模拟器 / MQTT Broker 模拟器） | 模拟器 |
| **手工验收** | `App` 窗体交互、真实设备联调 | 是 |

- 测试框架：**xUnit** + **FluentAssertions** + **NSubstitute**（Mock）。
- `tests/` 下的三个测试项目对应 `Core`、`Communication`、`Devices`。
- **`App`（WinForms）不写自动化 UI 测试**——投入产出比低，改用 ViewModel 化 + 手工验收。
  业务逻辑尽可能下沉到 `Devices` / `Communication` 以使其可测。

## 9. 待决问题

以下问题在框架设计阶段已识别，需在对应模块细化时确认：

| # | 问题 | 现状 | 影响模块 |
|---|---|---|---|
| 1 | **`UserRepository` 重复定义** | `Security/UserRepository.cs` 与 `Data/Repositories/UserRepository.cs` 都有。按依赖规则应只保留 `Data` 中的那份 | Security / Data |
| 2 | **`Data/DbContext.cs` 命名误导** | `DbContext` 是 EF Core 专有概念，选 Dapper 后易被误解。建议改名 `SqliteConnectionFactory` | Data |
| 3 | **`Directory.Build.props` 用于包版本管理** | 现推荐 **Central Package Management**（`Directory.Packages.props`）集中管理版本 | 全局 |
| 4 | **.NET 8 临近 EOL** | 支持至 2026-11-10，建议迁移 .NET 10 LTS | 全局 |
| 5 | **`DeviceInfoEntity` 的"大数据"定位** | 若为高频时序数据，需评估 SQLite 的写入吞吐是否足够，或是否需要分表/独立存储 | Data / Devices |
| 6 | **现有 `Aotumation` 模板项目** | 仓库中现有的 WinForms 空模板，与新方案无关，需确认删除或保留 | 全局 |

## 10. 相关文档

- [编码规范](conventions.md)
- [开发计划](roadmap.md)
- [模块设计索引](module-design/)
