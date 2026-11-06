# 模块设计文档索引

本目录包含框架级模块设计文档，均为**框架级**——
定义职责边界、依赖关系、关键接口签名与扩展点，不含完整实现。

## 阅读顺序

按**依赖顺序**（也是开发顺序）排列。`00` → `07` 依次阅读即可建立完整认知；
单独细化某个模块时，建议先读它的"依赖关系"一节所列的上游模块。

| #                         | 模块                | 一句话职责                              | 上游依赖                        |
| ------------------------- | ----------------- | ---------------------------------- | --------------------------- |
| [00](00-shared.md)        | **Shared**        | 统一返回结果、扩展方法、常量                     | 无                           |
| [01](01-core.md)          | **Core**          | 全部接口、模型、枚举、事件（零依赖）                 | 无                           |
| [02](02-logging.md)       | **Logging**       | Serilog 配置、按模块分流文件、内存日志缓冲          | Core, Shared                |
| [03](03-data.md)          | **Data**          | Dapper + SQLite 仓储、Schema 版本管理     | Core, Shared                |
| [04](04-security.md)      | **Security**      | 三种认证策略、权限校验、密码哈希                   | Core, Data, Shared          |
| [05](05-communication.md) | **Communication** | 7 种工业协议统一到 `ICommunicationChannel` | Core, Shared                |
| [06](06-devices.md)       | **Devices**       | 元件语义、状态机、轮询调度                      | Core, Communication, Shared |
| [07](07-app.md)           | **App**           | WinForms 窗体、DI 宿主、跨线程更新            | 全部                          |

### 后期新增（不属于旧体系九模块）

| # | 文档 | 一句话职责 |
|---|---|---|
| [09](09-ui-migration.md) | **UI 迁移** | 阶段十 AntdUI 替换作业指导与逐窗体清单 |
| [10](10-desktop.md) | **新框架桌面宿主** | 启动流程、线程模型、R1/R2 验收与 R3 缺口 |
| [11](11-authorization.md) | **新框架用户权限** | 5 角色与权限矩阵、本地账号与用户管理、认证/会话/授权接口、Windows/远端扩展路线 |
| [12](12-alarm.md) | **报警信息服务** | 唯一入口 `IAlarmService`、设备无关报警源、确认/历史/权限、PLC/板卡接入 |
| [13](13-axis-parameters.md) | **轴参数设计** | 参数描述符/服务/存储/控制器能力、权限与生效方式 |
| [14](14-external-axis-adapter.md) | **外部轴适配者接入** | 只依赖 Core 实现 `IAxisController`、错误语义与样例 |
| [15](15-cylinder-parameters.md) | **气缸参数设计** | 与轴共用 `ElementParameter*`，IO/超时/权限与生效方式 |

## 分层视图

```
┌────────────────────────────────────────────────────────────┐
│  07 App          WinForms 窗体 · 导航 · DI 宿主 · 配置绑定    │
├────────────────────────────────────────────────────────────┤
│  02 Logging   04 Security   05 Communication   06 Devices   │
│  03 Data                                                    │
├────────────────────────────────────────────────────────────┤
│  00 Shared          01 Core                                 │
│  （两者互不依赖，且不引用任何第三方 NuGet 包，Core 尤其严格）    │
└────────────────────────────────────────────────────────────┘
```

完整依赖矩阵见 [architecture.md §3.1](../architecture.md#31-依赖矩阵)。

## 各模块的"待细化"事项

每份文档末尾都有该模块的**待细化事项**清单，是后续逐个提供提示词时的入口。
其中被标记为高优先级、需要**先定下来才能动手写代码**的问题：

| 模块 | 必须先定的问题 |
|---|---|
| [Core](01-core.md#8-待细化事项) | `ILogService` 是否保留（改用 `ILogger<T>` 则整个接口移除） |
| [Logging](02-logging.md#8-待细化事项) | 同上；以及是否引入异步 Sink（同步文件 Sink 可能阻塞采集线程） |
| [Data](03-data.md#10-待细化事项) | **高频写入队列的完整设计**（SQLite 单写者限制导致，优先级最高） |
| [Security](04-security.md#8-待细化事项) | 会话超时行为——`锁定界面` 可能中断无人值守的监控 |
| [Communication](05-communication.md#10-待细化事项) | `ChannelConfig` 特有参数的承载方式（`Extra` 字典 vs 协议专属子类型） |
| [Devices](06-devices.md#11-待细化事项) | **四种元件的完整状态集合与迁移表**；`MonitorElement` 的语义边界 |
| [App](07-app.md#12-待细化事项) | 主监控界面的呈现方式；SunnyUI 的早期兼容性验证 |

## 跨模块的架构决策

以下决策**已确定**，各模块细化时应作为前提，不再重复讨论：

| 决策 | 结论 | 出处 |
|---|---|---|
| 服务接口位置 | 服务类抽象下沉到 `Core`；**数据访问接口就近放在 `Data`** | [Data §3](03-data.md#3-关于接口下沉的边界) |
| 通道线程安全 | 同一通道实例的读写**必须串行化**，由 `ChannelBase` 统一保证 | [Communication §5.1](05-communication.md#51-读写串行化强制) |
| 事件线程 | 所有事件在**后台线程**引发，UI 负责 `BeginInvoke` 切回 | [Core §5.3](01-core.md#53-事件参数) |
| 失败表达 | 预期内失败用 `Result`，预期外异常才 `throw` | [Shared §4](00-shared.md#4-核心类型result) |
| 双层状态 | `DeviceState`（粗粒度统一）+ 元件特有细粒度状态 | [Core §4.2](01-core.md#42-ideviceelement) / [Devices §4](06-devices.md#4-双层状态模型) |
| 密码存储 | PBKDF2-SHA256，每用户独立盐，恒定时间比较 | [Security §4.3](04-security.md#43-密码哈希) |
| 权限校验 | **UI 隐藏不算权限控制**，业务层入口必须校验 | [Security §4.5](04-security.md#45-权限校验的位置关键约定) |
| 日志写 API | 禁止无上下文的静态 `Log.Information(...)`，需带 `SourceContext` | [Logging §5.2](02-logging.md#52-获取-logger-的方式影响-moduleenricher-是否生效) |

## 相关文档

- [总体架构](../architecture.md)
- [编码规范](../conventions.md)
- [开发计划](../roadmap.md)
