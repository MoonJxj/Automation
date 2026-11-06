# 开发计划

> 按**依赖顺序**推进：每个里程碑只依赖已完成的前序模块，保证任一时刻主干可编译、可运行。
> 里程碑划分的依据是[依赖矩阵](architecture.md#31-依赖矩阵)，而非功能重要性。

## 里程碑总览

```
M0 骨架        M1 地基         M2 数据与安全      M3 通讯           M4 设备         M5 界面         M6 联调
─────────►    ─────────►     ─────────►        ─────────►       ─────────►     ─────────►     ─────────►
解决方案          Shared          Data              Communication     Devices         App            真实设备
+ 构建配置        Core            Security          7 种协议          元件 + 状态机    窗体 + 导航      验收 + 发布
+ DI 宿主         Logging                                                                          性能调优
```

**关键路径**：`Core` → `Communication` → `Devices` → `App`。这条链上任一环节延期都会直接推迟联调，
资源应优先保障。

---

## M0 — 解决方案骨架

**目标**：搭好可编译、可运行的空壳，把构建与包管理配置一次做对。

**交付物**

- `EquipmentMonitor.sln` 与 9 个项目（8 个 `src/` + 测试项目）
- `Directory.Build.props`：统一 `TargetFramework`、`Nullable`、`ImplicitUsings`、`LangVersion`、
  `TreatWarningsAsErrors`
- `Directory.Packages.props`：Central Package Management，集中声明所有包版本
- `.editorconfig`、`.gitignore`
- `App/Program.cs` 构建 Generic Host，注册 DI，跑通一个空白 `MainForm`
- `appsettings.json` 骨架

**验收标准**

- [ ] `dotnet build` 在根目录一次通过，0 警告 0 错误
- [ ] `dotnet run --project src/EquipmentMonitor.App` 能弹出空窗体
- [ ] 依赖检查通过：`Core` / `Shared` 无任何项目引用，无违规的反向引用
- [ ] 包版本只存在于 `Directory.Packages.props`

**风险**：`TreatWarningsAsErrors` 在引入 SunnyUI 等第三方库时可能因分析器冲突导致构建失败，
若发生则对该项目局部关闭。

---

## M1 — 地基模块（Shared / Core / Logging）

**目标**：打通"接口定义 → 实现 → 日志落地"的最小闭环，为后续所有模块提供基础设施。

**交付物**

- **Shared**：`Result` / `Result<T>`、`Constants`、常用 `Extensions`
- **Core**：5 个接口（`ILogService`、`IAuthService`、`ICommunicationChannel`、`IComponentManager`、
  `IDeviceElement`）、全部模型与枚举、`DeviceStateChangedEventArgs`
- **Logging**：`SerilogConfigurator`（多 Sink + 分模块文件）、`ModuleEnricher`、
  `InMemoryLogSink`（环形缓冲）、`LogCleanupService`（NCrontab 定时清理）

**验收标准**

- [ ] `Core` 项目的 NuGet 依赖列表为空
- [ ] 日志能按模块输出到独立文件，且带模块名结构化属性
- [ ] `InMemoryLogSink` 能保留最近 N 条日志，环形缓冲不无限增长
- [ ] `LogCleanupService` 能按 Cron 表达式清理超期日志文件
- [ ] `Core` 单元测试覆盖模型与 `Result` 的行为

**定义先行**：本里程碑的 `Core` 接口签名一旦定稿，后续模块都应视为**契约**。
若后续发现接口需要变更，必须同步更新本模块文档与所有实现方。

---

## M2 — 数据与安全

**目标**：让用户能登录、权限能被校验、配置能持久化。

**交付物**

- **Data**：`SqliteConnectionFactory`、`DbInitializer`（内嵌 SQL 脚本 + `schema_version` 表）、
  三个 Repository（User / DeviceConfig / DeviceInfo）、三个 Entity
- **Security**：`AuthService` + 三种策略（本地 / Windows / 远程）、`PermissionService`、
  `SecurityLevelPolicy`、`UserRepository`（**位于 Data，不在 Security**，见[待决问题 #1](architecture.md#9-待决问题)）

**验收标准**

- [ ] 首次启动能自动建表并写入默认管理员账号
- [ ] 三种认证策略可通过配置启用/禁用与排序
- [ ] 密码以加盐哈希存储（**不得明文**）
- [ ] `SecurityLevel` → 允许的窗体与操作的映射表可配置
- [ ] SQLite 启用 WAL 模式，并发读写下不出现 `database is locked`

**风险**：Dapper 无迁移能力，`schema_version` 表的升级逻辑需自行设计。
建议首版只做"顺序执行增量脚本"，不做回滚。

---

## M3 — 通讯模块

**目标**：把 7 种协议统一到 `ICommunicationChannel` 抽象下，并具备自动重连能力。

**推荐顺序**（由易到难，先打通抽象再铺量）：

1. `SocketChannel` — 无第三方依赖，用于验证抽象设计是否成立
2. `ModbusTcpChannel` — NModbus，工业场景最常用
3. `ModbusSerialChannel` — 复用 NModbus，增加串口参数处理
4. `SerialPortChannel` — 裸串口，用于自定义协议
5. `MqttChannel` — MQTTnet，支持 v3.1.0 ~ v5.0.0
6. `OpcUaChannel` — LibUA，安全配置与订阅
7. `EthernetIpChannel` — libplctag.NET，注意原生依赖与 Tag 类型预声明

**交付物**：`CommunicationManager`、7 个 Channel 实现、`ProtocolFactory`、`ChannelConfig`

**验收标准**

- [ ] 每种通道均可独立连接、读写、断开，并暴露统一的状态与健康度
- [ ] **断线自动重连**：拔网线/拔串口后按退避策略重连，不崩溃、不泄漏句柄
- [ ] 同一通道上的并发读写被正确串行化（用压力测试验证）
- [ ] 每种通道对接模拟器（Modbus 从站 / MQTT Broker）有集成测试
- [ ] 通道异常不冒泡到调用方，以 `Result` 返回失败

**风险**（已在[架构文档](architecture.md#22-关于-ethernetip-库选型)记录）：

- libplctag.NET 依赖原生二进制，发布配置错误会运行时报 `DllNotFoundException`
- libplctag 单 Tag 实例非线程安全，串行化必须落到实处
- LibUA 的学习曲线较陡，建议预留缓冲时间

---

## M4 — 设备与元件

**目标**：把裸数据变成有语义的设备状态。

**交付物**

- **Devices**：`ComponentManager`、4 类元件（Servo / Cylinder / Sensor / Monitor）、
  `DeviceStateMachine`（Stateless）、`StateTransitionLogger`、`DevicePollingService`

**验收标准**

- [ ] 元件继承 `DeviceElementBase`，通过 `ICommunicationChannel` 读写，**不直接依赖任何具体协议**
- [ ] 状态机迁移由 Stateless 驱动，含守卫条件与进入/退出动作
- [ ] 状态变化触发 `DeviceStateChanged` 事件，且事件在后台线程引发（UI 层负责切线程）
- [ ] 每次状态迁移均写入日志（含元件名、旧状态、新状态、触发原因）
- [ ] `DevicePollingService` 支持按元件配置不同轮询周期，可优雅停止
- [ ] 长时间轮询（≥ 8 小时）无内存增长

**风险**：轮询周期设计是**性能关键点**。
若 200 个元件各按 100ms 轮询，将产生巨大流量。需支持按元件分组、共享通道读取（一次读一批寄存器）。

---

## M5 — 界面层

**目标**：把能力暴露给用户。

**交付物**：`Program.cs`（Host + DI + Serilog 初始化）、6 个窗体、自定义控件、UI 辅助类

| 窗体 | 职责 |
|---|---|
| `LoginForm` | 登录，选择认证策略 |
| `MainForm` | 导航容器，承载各功能页 |
| `UserManagementForm` | 用户增删改查、权限分配 |
| `DeviceConfigForm` | 设备与通道参数配置、点位映射 |
| `DeviceDebugForm` | 手动读写寄存器/Tag，协议调试 |
| `LogViewerForm` | 实时查看 `InMemoryLogSink` 的日志 + 按条件过滤 |

**验收标准**

- [ ] DI 容器正确解析所有服务，无运行时 `InvalidOperationException`
- [ ] 后台事件更新 UI 全部走 `BeginInvoke`，无跨线程异常
- [ ] 界面在高负载轮询下不卡顿（UI 线程无阻塞调用）
- [ ] 按安全等级隐藏/禁用对应功能，**且业务层有对应的权限校验**
- [ ] 关闭主窗体时，所有后台任务被优雅停止，进程干净退出

**风险**：SunnyUI 在 .NET 8 下的 DPI 缩放与主题兼容性需早期验证，
不要等到写完所有窗体才发现控件库不兼容。

---

## M6 — 联调与发布

**目标**：在真实环境下稳定运行。

**交付物**

- 真实设备联调记录
- 发布配置（`dotnet publish` 含正确的 RID 以携带 libplctag 原生库）
- 现场部署说明与配置模板
- 性能调优报告

**验收标准**

- [ ] 连续运行 **≥ 72 小时**无崩溃、无内存/句柄泄漏（用性能监视器验证）
- [ ] 断网、断电、设备掉线等异常场景均能自恢复
- [ ] 单通道 200 点位轮询周期实测满足设计指标
- [ ] 发布包在**干净的**目标机器上可直接运行（无遗漏的运行时或原生依赖）
- [ ] 日志文件按策略自动清理，磁盘不会写满

---

## 后续演进方向（不在本期范围）

以下能力在架构上已预留扩展点，但不在当前里程碑内：

- **历史数据与趋势图**：`DeviceInfoEntity` 若承担高频时序数据，SQLite 可能成为瓶颈，
  届时可引入独立的时序存储（如 TDengine / InfluxDB）
- **报警与通知**：P0 已交付活动报警、确认、JSONL 历史与统一 `IAlarmService`
  （见 [12-alarm.md](module-design/12-alarm.md)）；后续为报警规则引擎、横幅/声音、MES 上行
- **配方管理**：设备参数组的版本化管理
- **无界面服务模式**：得益于业务模块与 WinForms 解耦，可额外提供一个 Windows Service 宿主

## 相关文档

- [总体架构](architecture.md)
- [编码规范](conventions.md)
- [模块设计索引](module-design/)
