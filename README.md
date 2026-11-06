# EquipmentMonitor — 工业设备监控上位机

通过多种工业协议接入现场设备（PLC、伺服、气缸、传感器），
完成实时状态采集、协议调试、设备配置与用户权限管理的 Windows 桌面应用。

> **当前状态（2026-11-01）**：可构建入口是 `AxisFramework.slnx`（4 个 src 项目 + 2 个测试项目，
> 构建 0 错误 / 33 条空引用警告，152 条测试全绿）。
> 旧体系（`EquipmentMonitor.slnx`、9 个 `EquipmentMonitor.*` 模块、6 个旧测试项目）已整体移入 Windows 回收站。
> 下文的技术选型与“架构一览”是旧体系的设计记录；新框架的现状、缺陷与路线见
> [交接文档](docs/handover-axis-framework.md)。

## 技术栈

| 模块          | 选型                                       | 许可证           |
| ----------- | ---------------------------------------- | ------------- |
| UI          | WinForms (.NET 8) + SunnyUI.NET          | MIT           |
| 日志          | Serilog                                  | Apache 2.0    |
| Modbus      | NModbus                                  | MIT           |
| MQTT        | MQTTnet                                  | MIT           |
| OPC UA      | LibUA                                    | Apache 2.0    |
| EtherNet/IP | libplctag.NET                            | Apache 2.0    |
| 状态机         | Stateless                                | Apache 2.0    |
| ORM         | Dapper                                   | Apache 2.0    |
| 数据库         | SQLite                                   | Public Domain |
| DI 容器       | Microsoft.Extensions.DependencyInjection | MIT           |
| 后台任务        | NCrontab.Scheduler                       | MIT           |

全部为 MIT / Apache 2.0 / Public Domain，可自由商用。
完整选型理由与已知约束见 [架构文档 §2](docs/architecture.md#2-技术选型)。

## 架构一览

```
┌────────────────────────────────────────────────────────────┐
│  App          WinForms 窗体 · 导航 · DI 宿主 · 配置绑定       │
├────────────────────────────────────────────────────────────┤
│  Logging   Security   Communication   Devices   Data       │
├────────────────────────────────────────────────────────────┤
│  Core（接口/模型，零依赖）    Shared（Result/工具，零依赖）     │
└────────────────────────────────────────────────────────────┘

只允许 上层 → 下层 的单向引用；Core 与 Shared 不引用任何第三方包
```

核心模式是**接口下沉**：`Core` 定义 `ICommunicationChannel` / `IComponentManager` / `IAuthService`
等抽象，各模块提供实现，`App` 在启动时绑定。这使得业务模块之间无需互相引用，且全部可单元测试。

## 文档

| 文档 | 内容 |
|---|---|
| [**总体架构**](docs/architecture.md) | 分层、依赖矩阵、技术选型与约束、数据流与线程模型、测试策略 |
| [编码规范](docs/conventions.md) | 依赖规则、异步、**线程与并发**、资源管理、日志、配置约定 |
| [开发计划](docs/roadmap.md) | M0–M6 里程碑、交付物、验收标准、风险点 |
| [模块设计](docs/module-design/) | 8 个模块的职责边界、接口契约、扩展点、待细化事项 |
| [桌面宿主](docs/module-design/10-desktop.md) | 新框架 WinForms 宿主：启动流程、线程模型、R1/R2 验收 |
| [用户权限](docs/module-design/11-authorization.md) | 新框架 5 角色、本地账号与用户管理、认证/会话/授权接口与远端扩展 |
| [新框架交接](docs/handover-axis-framework.md) | 新旧两套体系并存现状、构建/测试基线、缺陷与路线 |

**建议阅读顺序**：本 README → [架构文档](docs/architecture.md) → [编码规范](docs/conventions.md)
→ 按需查阅 [模块设计](docs/module-design/)。

## 开始开发前必读

以下四点是本项目最容易出错、且代价最高的地方，在写第一行代码前应已了解：

1. **依赖规则不可破** — `Core` 不得引用任何第三方 NuGet 包。
   这条一旦破例，整个分层的意义就消失了。见[依赖矩阵](docs/architecture.md#31-依赖矩阵)。

2. **同一通道的读写必须串行化** — Modbus RTU 是半双工，并发的响应会互相错配；
   libplctag 单 Tag 实例非线程安全。这类 Bug 极难排查。见[通讯模块 §5.1](docs/module-design/05-communication.md#51-读写串行化强制)。

3. **SQLite 只允许一个写事务** — 高频轮询写入与配置保存会争抢写锁，
   必须为高频写入设计批量队列，否则表现为"界面保存配置时卡顿数秒"。
   见[数据模块 §5.2](docs/module-design/03-data.md#52-写入并发本模块最大的架构约束)。

4. **UI 隐藏不等于权限控制** — 业务层入口必须校验权限，
   窗体上的按钮禁用只是体验优化。见[安全模块 §4.5](docs/module-design/04-security.md#45-权限校验的位置关键约定)；
   新框架的五角色本地实现见[用户权限](docs/module-design/11-authorization.md)。

## 尚待确认的事项

以下问题在框架设计阶段已识别，需在对应模块细化时定夺，
完整清单见[架构文档 §9](docs/architecture.md#9-待决问题)：

| # | 事项 |
|---|---|
| 1 | `UserRepository` 在 `Security` 与 `Data` 中重复定义，应只保留 `Data` 中的那份 |
| 2 | `Data/DbContext.cs` 命名误导（EF Core 概念），建议改为 `SqliteConnectionFactory` |
| 3 | 包版本管理建议改用 Central Package Management（`Directory.Packages.props`） |
| 4 | **.NET 8 LTS 支持至 2026-11-10**，建议直接采用 .NET 10 LTS |
| 5 | `DeviceInfoEntity` 若承载高频时序数据，需评估 SQLite 吞吐或考虑分表 |
| 6 | 仓库中现有的 `Aotumation` WinForms 空模板与本方案无关，需确认删除或保留 |

## 目录结构（当前实际）

```
AxisFramework.slnx                       # 唯一解决方案与构建入口
├── src/
│   ├── EquipmentMonitor.Common/         # 常量/Result/工具（第三方包经此传递）
│   ├── EquipmentMonitor.Core/           # 轴控抽象：IElement/IMachine/IAxisController…
│   ├── EquipmentMonitor.Infrastructure/ # 模拟轴、协议、寄存器、命令契约实现
│   └── EquipmentMonitor.Desktop/        # WinForms + AntdUI 桌面宿主（R1 总览 / R2 伺服操作台）
├── tests/
│   ├── EquipmentMonitor.AxisFramework.Tests/   # 110 条
│   └── EquipmentMonitor.Desktop.Tests/         # 42 条
├── docs/                                 # 设计文档
├── Directory.Build.props                 # 统一编译属性
├── Directory.Packages.props              # 统一包版本（CPM）
└── .editorconfig
```

> 旧体系的 `src/EquipmentMonitor.App|Core|Shared|Logging|Security|Communication|Devices|Data|Mes`
> 与对应测试项目已于 2026-11-01 移入 Windows 回收站；`docs/module-design/00–07` 等章节仍按旧体系书写。