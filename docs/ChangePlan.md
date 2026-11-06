# 变更计划：元件操作接口 + HMI 替代操作 UI

> 按**依赖顺序**推进：每个里程碑只依赖已完成的前序模块，保证任一时刻主干可编译、可运行。
> 里程碑用**字母编号（A–G）**，避免与 `roadmap.md` 的 `M0–M6` 和 `09-ui-migration.md` 的"阶段十"冲突。
> 基线（开工前实测）：构建 0 警告；测试 **991** 条（2026-10-04）。每完成一个里程碑，**只增不减**。
>
> 与既有工作线的关系：
> - **需求基线 = [`SRS.md`](SRS.md)**（2026-10-05 初稿）：本计划只覆盖其中"元件操作接口 + 操作界面"相关的
>   需求子集（对账表见 §2.3）；需求变更须同步 SRS §15.2 变更记录；
> - `roadmap.md` M0–M5 的成果（采集链路、8 窗体）是本变更的**地基**，不重复建设；
> - `09-ui-migration.md`（阶段十，AntdUI 替换，当前 1/8）**并行推进**，不阻塞本计划：
>   里程碑 D 的新界面**生于 AntdUI**（决策 D7），不进入那 8 个窗体的迁移清单；
> - 姊妹项目 **`0004.PLC`（PLC 轴控项目）** 与本计划**共用一套轴控接口**（见 §2.2），
>   跨项目契约的同步是本计划的一等公民（§7、§8）。

## 1. 变更定位

此次变更将基于多种工业协议，深化元件对象（伺服、气缸、传感器）；为元件对象添加可监控可操作的UI界面，目的是替代自动化项目中触摸屏（HMI）的角色；
需要注意的是：HMI与设备强相关，无法脱离设备设计。本次变更主要为上位机软件开发人员设计统一的操作UI，统一的操作接口。未来即使增加不同品牌的同类元件可快速复用。
优先实现伺服控制操作UI,操作接口；

**设计依据（本次修订新增）**：

- **操作接口以 `PLCopen 运动控制规范` 第 1/2 部分的单轴功能块为蓝本**——
  命令命名、参数、执行语义（Enable/Valid 与 Execute/Done 两类）、输出
  （Done / Busy / CommandAborted / Error / ErrorID）逐项对齐，PLC 工程师可零翻译对应；
  依据文档：`C:\Users\MoonJ\Downloads\运动控制2.pdf`（第 3 章，实测 134 页扫描件、打印页 115–248）。
- **参数与状态模型以 `0004.PLC` 轴控项目的数据类型为依据**——
  `ST_ServoTarget` / `ST_AxisStatus` / `ST_HomingStatus` / `E_ServoMode` / `E_Cia402Command`
  是现场已经存在的约定，上位机不另造一套（§2.2 有实测清单）。
- **只做必要接口**——单轴运动块只取运行 HMI 所必需的最小子集（决策 D2），
  扩展块（Additive / Superimposed / Continuous / TouchProbe / CamSwitch / 参数读写 / 多轴）明确不在本期（§9）。

**核心特征**（决定了后续所有架构取舍）：

- **长周期运行**：现场上位机通常连续运行数周至数月，不允许内存泄漏与句柄泄漏。
- **实时性敏感**：轮询周期通常在 50–500ms 量级，UI 不得阻塞采集线程。
- **现场环境不稳定**：网络抖动、设备掉线、串口拔出是常态，必须具备自动重连与降级能力。
- **多协议并存**：同一台上位机往往同时对接 Modbus 设备、AB PLC、MQTT 网关等。

**一句话交付物**：操作工能在上位机界面上**看见**每个元件的实时状态、并按 PLCopen 单轴语义**安全地操作**伺服，
全程有权限校验、二次确认、互锁与审计；新增同类品牌元件时**只加元件类与配置，不改 Core 与 UI 框架**。

---

## 2. 现状基线（2026-10-04/05 实测，非推断）

### 2.1 上位机（本仓库）

| 能力 | 现状 | 证据 |
|---|---|---|
| 采集链路 | 已交付：JSON 配置 → `ElementFactory` → 轮询 `PollAsync` → 状态机 → 快照/事件/数据出口 | `Devices/` 全部实现；App 状态栏、MesPushPage 已在消费 |
| 元件读面 | `IElement` 只有 `PollAsync` / `GetSnapshot` / `TryGetDataPoint`，**无任何写面** | `Core/Abstractions/IElement.cs` |
| 元件写面 | 四类元件（Servo/Cylinder/Sensor/Monitor）全部只读寄存器 | `Devices/Elements/*.cs` |
| 通道写能力 | 已声明已实现：Modbus 写单寄存器(06)/多寄存器(16)/单线圈(05) | `Core/Abstractions/ICommunicationChannel.cs:146-162`、`Communication/Channels/ModbusChannelBase.cs:99-160` |
| 通道写的调用方 | **零调用方**（全仓库除接口与实现外无引用） | 2026-10-04 grep 实测 |
| 设计意图 | 代码注释已写明方向："写操作不属于采集路径——上位机对设备的写命令应当走**独立的、经过权限校验的命令通道**" | `Devices/Protocol/ModbusElementBase.cs:86-88` |
| 权限面 | `Permission.MachineOperate`（"操作设备：启停、手动调试、写入点位"）已定义并被 DeviceDebugForm 使用 | `Core/Enums/Permission.cs:32`、`Security/PermissionService.cs` |
| 审计面 | `IAuditService` + 事件类型常量表已就绪（写库、永久保留、可查） | `Core/Abstractions/IAuditService.cs:54-88` |
| 状态机线程模型 | **单写者**：转移只由轮询线程触发；注释明确"两个线程同时转移会破坏 DurationMs" | `Devices/StateMachines/ElementStateMachine.cs:26-32` |
| 配置面 | appsettings `Devices` 段 = 元件模板池 + 机台引用 + `Overrides`；`Address` 只声明**读**块 | `App/appsettings.json:413+`、`Devices/Configuration/DevicesOptions.cs` |
| UI 结构 | 页面 = 普通 `Form`（去边框、`TopLevel=false`）缓存于 `MainForm._pages`；无 ViewModel/MVVM | `App/Forms/MainForm.cs:42-49,276-321` |
| UI 刷新 | 各页 1s `System.Windows.Forms.Timer` 拉快照；`UiDispatcher.Post` 是唯一的后台事件 marshal 入口 | `App/Helpers/UiDispatcher.cs`、`MesPushPage.cs:145-174` |
| UI 控件库 | AntdUI 已引入、别名机制已验证，迁移进度 **1/8**（仅 LoginForm） | `docs/module-design/09-ui-migration.md §7` |
| 测试 | 6 个测试项目；App.Tests 含窗体冒烟与 AntdUI 探针 | `tests/` |

### 2.2 依据项目 `0004.PLC`（PLC 轴控）与标准文档（实测）

**`0004.PLC` 已有的轴控资产**（本上位机的命令/参数/状态模型直接对齐它们）：

| 资产 | 内容 | 路径 |
|---|---|---|
| `FB_ServoControlSM` | CiA 402 八状态标准状态机；输入 `EnableReq` / `ShutdownReq` / `QuickStopReq` / `FaultResetReq` / `AutoRecover`；输出 `CtrlWord` / `IsOperational` / `IsFault` / `IsQuickStopped` / `ErrorID` / `ErrorClass` | `library/drivers/servo/servo_ctrl_sm.st` |
| `E_Cia402Command` | 控制字常量：SHUTDOWN `16#0006` / SWITCH_ON `16#0007` / ENABLE_OPERATION `16#000F` / DISABLE_OPERATION `16#0007` / QUICK_STOP `16#0002` / FAULT_RESET `16#0080` | `library/drivers/servo/E_Cia402Command.st` |
| `E_ServoMode` | NO_MODE / **PROFILE_POSITION(1)** / **PROFILE_VELOCITY(2)** / PROFILE_TORQUE(3) / **HOMING(6)** / INTERPOLATED_POS(7) / CSP(8) / CSV(9) / CST(10) | `library/drivers/servo/E_ServoMode.st` |
| `ST_ServoTarget` | 目标指令：`Mode` / `Position` / `Relative` / `Immediate` / `Velocity` / `Torque` / `Accel` / `Decel` / `Jerk` | `library/drivers/servo/ST_ServoTarget.st` |
| `ST_HomingStatus` | 回零状态：`Busy` / `Completed` / `Failed` / `Method` | `library/drivers/servo/ST_HomingStatus.st` |
| `ST_ServoFeedback` | 实时反馈：`ActualPosition` / `ActualVelocity` / `ActualTorque` / `FollowingError` / `ErrorCode` / `WarningCode` / … | `library/drivers/servo/ST_ServoFeedback.st` |
| `ST_AxisStatus` | 服务层轴状态：`AxisID` / `ActualPos` / `TargetPos` / `IsMoving` / `InPosition` / `IsEnabled` / `IsFault` / `FaultCode` | `library/services/pnp/types/ST_AxisStatus.st` |
| 标准块对照表 | `MC_Power / MC_MoveAbsolute / MC_MoveRelative / MC_MoveVelocity / MC_Home / MC_Stop / MC_ReadActualPosition / MC_ReadStatus` 跨品牌对照 | `docs/reference/vendor_library_mappings.md` |
| 现状边界 | `pnp-machine/main.st` 的轴运动仍是**模拟占位**（`:296-328`）；`library/services/motion/` 是骨架 —— **上位机↔PLC 的轴控接口寄存器表尚不存在** | 实测 grep |

**标准文档（`运动控制2.pdf`，第 3 章 PLCopen 运动控制规范）实测摘录**：

- §3.1.3 功能块模型：两类激活方式 —— **Execute/Done**（上升沿触发一次；输出 Done/Busy/CommandAborted/Error/ErrorID）
  与 **Enable/Valid**（电平使能；输出 Valid/Busy/Error/ErrorID）。V2.0 增加 `ContinuousUpdate` 输入；
  新命令可中断执行中的块（旧块输出 `CommandAborted`）。
- 表 3-3「PLCopen 运动控制规范第一/二部分命令表」：管理块（MC_Power、MC_Reset、读取类…）与
  运动块（MC_Home、MC_Stop、MC_Halt、MC_MoveVelocity、MC_MoveAbsolute、MC_MoveRelative、
  MC_MoveAdditive、MC_MoveSuperimposed、MC_MoveContinuous*、MC_SetPosition、MC_TouchProbe…）。
- 图 3-4「MC_MoveAbsolute 执行的行为时序」：引脚 `Axis / Execute / ContinuousUpdate / Position / Velocity /
  Acceleration / Deceleration / Jerk / Direction / BufferMode`，输出 `Done / Busy / Active /
  CommandAborted / Error / ErrorID`——**本计划的命令参数以此为准**。

### 2.3 需求基线（SRS）与本计划的覆盖面对账

需求基线：[`docs/SRS.md`](SRS.md)。本计划**只覆盖**与"元件操作接口 + 操作界面"相关的需求子集，其余明确不覆盖：

| SRS 需求 | 覆盖 | 说明 |
|---|---|---|
| FR-004 控制写入 | ✅ 本变更核心 | PLCopen 单轴命令集（里程碑 A–D） |
| FR-005 写后读回 | ✅ 本变更核心 | 决策 D4：回读满足期望才算完成 |
| FR-010 权限控制 | ✅ 命令入口 | `RequirePermission(MachineOperate)`（B/C/D） |
| FR-011 审计日志 | ✅ 命令审计 | 里程碑 B（操作者/命令/参数/结果） |
| FR-013 系统日志 | ✅ 命令日志 | 里程碑 B |
| FR-002 实时采集 | ⚠️ 不改变行为 | 仅保证命令与轮询共存（决策 D3） |
| FR-003 实时显示 | ⚠️ 部分 | 元件总览/操作台（D）；趋势图不在本期 |
| FR-006 报警管理 / FR-007 历史存储 / FR-008 报表导出 / FR-014 备份恢复 / FR-016 系统诊断 | ❌ 不覆盖 | 现状见 SRS §15.1（部分未实现），另行排期 |
| FR-012 点表编辑与热重载 | ❌ 不覆盖 | **后续独立变更**（2026-10-05 确认）；其前置路径"注销时取消未决命令"由里程碑 B 提供 |
| SRS 环境约束 | ⚠️ 影响里程碑 G | **无触摸屏**（鼠标键盘）——G 的形态设计按此修正 |

---

## 3. 差距分析（目标 → 缺口 → 归属里程碑）

| # | 目标能力 | 缺口 | 里程碑 |
|---|---|---|---|
| 1 | **统一操作接口**：UI 能问"这个元件支持哪些操作、参数是什么、当前能不能执行"，并执行拿结果 | `Core` 无命令契约；`IElement` 无写面；01-core.md §4.2 的 `WriteAsync(string)` 草图已被 §8 自己列为"待议" | A、B |
| 2 | **命令集对齐 PLCopen 单轴块**（命名/参数/语义/输出） | 现有代码无任何命令概念；参数结构（Position/Velocity/Accel/Decel/Jerk/Direction/BufferMode）与 PLC 侧 `ST_ServoTarget` 无对应物 | A、C |
| 3 | **上位机 ↔ PLC 轴控接口寄存器表**（命令/状态寄存器，两侧共用契约） | **不存在**；PLC 侧轴控尚未接入真实指令（main.st 为模拟） | A（草案）、C（定稿） |
| 4 | **元件可写设备**：写点位声明、写→回读校验、超时、失败语义 | 写地址无处声明（`Address` 是读块）；无"写成功 ≠ 命令完成"的观察机制 | A、B、C |
| 5 | **命令与采集共存**：写不得破坏"状态机单写者"与轮询周期；新命令可中断旧命令（CommandAborted） | 命令若从 UI 线程直接写状态机，会踩 `ElementStateMachine` 的单写者不变式 | B（决策 D3） |
| 6 | **命令安全**：权限 + 二次确认 + 互锁 + 审计 | 权限/审计设施已就绪但**未接线**；无互锁规则表；无命令级审计事件类型 | B、C、D |
| 7 | **可监控可操作 UI**：元件总览 + 单元件操作台（伺服优先） | 现有 8 窗体均为"配置/调试"角色，无元件级操作界面；无 `Controls/` 目录（07-app.md §3 承诺但未建） | D |
| 8 | **品牌复用**：换品牌 = 加元件类 + 配置，Core/UI 零改动 | 复用约定未定（命令表、写点位、寄存器表）；`ElementFactory` 是硬编码 switch | A、F（用可验证的验收固定） |

---

## 4. 关键设计决策（里程碑 A 开工时定稿，写入 `01-core.md` / `06-devices.md`）

### D1 命令契约 = PLCopen 单轴功能块的 C# 映射

- 命令键**直接用 PLCopen 名称**（`MC_Power`、`MC_MoveAbsolute`…），显示名用中文（"使能"、"绝对定位"…）——
  PLC 工程师零翻译对应，也便于将来与 PLC 侧日志/文档互查。
- `Core` 新增 `IElementCommandTarget`（独立于 `IElement`），由 `ElementBase` 统一实现：
  - `IReadOnlyList<ElementCommandDescriptor> Commands` —— 命令表（键、显示名、参数规格、风险级别）；
  - `CommandAvailability EvaluateCommand(string key)` —— 当前可否执行 + **被拒原因**（互锁/离线），UI 据此置灰并解释；
  - `Task<Result<ElementCommandResult>> ExecuteCommandAsync(ElementCommandRequest request, CancellationToken ct)`。
- **否决"给 `IElement` 加 `WriteAsync(string command, object? param)`"**（01-core.md §4.2 的早期草图）：
  ① 字符串命令无编译期检查（该文档 §8 自己列为待议）；② 传感器等无命令元件被迫实现写方法；
  ③ **"统一的操作 UI"要求 UI 能从契约自动生成界面**——字符串命令给不出"有哪些命令、参数范围、互锁原因"。
- 先例一致：协议特有操作放扩展接口（`IModbusChannel` 之于 `ICommunicationChannel`）。

### D2 命令集 = 必要子集（8 条），其余明确不做

| # | 命令键（PLCopen） | 类型 | 说明 | 必要性 |
|---|---|---|---|---|
| 1 | `MC_Power` | Enable 级 | 使能 / 下使能（参数 `Enable`），元件维持"期望使能状态" | 必要 |
| 2 | `MC_Reset` | Execute | 故障 / 报警复位（含 `ErrorID` 清零） | 必要 |
| 3 | `MC_Home` | Execute | 回零（参数 `Method`，对应 `ST_HomingStatus.Method`） | 必要 |
| 4 | `MC_MoveAbsolute` | Execute | 绝对定位（`Position` / `Velocity` / `Accel` / `Decel` / `Jerk` / `Direction` / `BufferMode`） | 必要 |
| 5 | `MC_MoveRelative` | Execute | 相对定位（`Distance` 替代 `Position`，其余同上） | 必要 |
| 6 | `MC_MoveVelocity` | Execute | 速度运行（`Velocity` / `Direction` / `Accel` / `Decel`） | 必要 |
| 7 | `MC_Halt` | Execute | 停止（减速到零，**保持使能**，可被新命令覆盖） | 必要 |
| 8 | `MC_Stop` | Execute | 受控停止并**锁定轴**（恢复需重新使能/复位；对应 PLC 侧 CiA402 QUICK_STOP） | 必要 |

- **只读类不新增接口**：`MC_ReadStatus` / `MC_ReadActualPosition` / `MC_ReadActualVelocity` /
  `MC_ReadActualTorque` / `MC_ReadAxisError` 的信息由**既有采集链路**覆盖
  （现有数据点 `Position` / `Speed` / `Torque` / `DriveEnabled` / `AlarmCode` + 状态机）。
- **点动不引入 `MC_MoveJog`**：按住 = `MC_MoveVelocity`（带方向），松开 = `MC_Halt`，两块组合即点动。
  少一个命令、少一套看门狗特例（看门狗仍作用于 `MC_MoveVelocity` 的按压场景，见里程碑 D）。
- 裁掉的理由：Additive / Superimposed / Continuous / SetPosition / TouchProbe / CamSwitch / Profile 类 /
  参数读写 / 多轴块均属"调试与工艺扩展"，不进运行 HMI 的最小闭环（§9 有完整清单）。

### D3 命令与轮询共存 = 命令队列由轮询线程执行，**保住"状态机单写者"**（本变更最关键的一处）

- `ElementStateMachine` 明确单写者（`ElementStateMachine.cs:26-32`）。命令从 UI 线程执行 = 引入第二个写者，直接破坏 `DurationMs` 计算。
- 推荐机制：
  1. 元件内持有**有界命令队列**（FIFO，满则拒绝——HMI 按钮连点会得到明确的"忙"）；
  2. `ExecuteCommandAsync` 校验（权限→互锁→离线）后入队，返回 `Task`（`TaskCompletionSource` 驱动）；
  3. **轮询线程每轮出队执行"写阶段"**（写寄存器/线圈，与采集读天然串行，跨元件仍由通道信号量保护——不新增锁）；
  4. **"观察阶段"由后续轮询自然完成**：命令完成判据 = 回读满足期望谓词（见 D4）或超时。
- **BufferMode 语义即中断策略**：默认 `Aborting`——新命令到达时，队列中未执行者作废、执行中者以
  `CommandAborted` 结束（与 PLCopen 及 HMI"按新按钮即换向"的行为一致）；`Buffered` 才排队。
  因此**队列深度默认 1 即可**，`Buffered` 场景才需要深队列（有界）。
- **否决"元件级锁 + 调用线程直接执行"**：运动命令的观察阶段跨多个轮询周期，持锁会让采集停摆；
  该仓库已有教训——"一旦加锁，UI 读状态就会阻塞轮询线程"（`ElementStateMachine.cs:32`）。
- ⚠️ **边角必须在 B 覆盖**：元件注销/配置重载/停机时，未决命令的 `TCS` 必须被完成为"已取消"，否则 UI 永久等待。

### D4 完成判据 = 回读满足期望；结果映射 PLCopen 输出

- 每个命令声明"**期望谓词 + 超时窗口**"（元件代码内，如 `MC_Power` → 使能位回读一致；
  `MC_MoveAbsolute` → `Positioning` → `InPosition`）。
- **结果映射分两层**（A1 定稿，2026-10-05 —— 比初稿更精确）：
  - **接受层**（`Result`）：命令**该不该执行**。拒绝 = 未写出任何报文 → `Result.Fail`，
    错误码见 `CommandRejectionCodes`（未知命令 / 参数非法 / 互锁 / 离线 / 忙），
    判据与 `EvaluateCommand` 同源。
  - **执行层**（`ElementCommandResult.Outcome`）：命令**执行得怎么样**，均包在 `Result.Ok` 内——
    `Done`（回读满足）/ `CommandAborted`（被新命令或 `MC_Stop` 替代）/
    `Error`（设备报警码、写失败）/ `Timeout`（回读永不满足——本项目补充，
    PLCopen 无此输出，因为它假设确定性执行；直连 Modbus 的场景必须自己兜底）。
  - 分层的理由：接受层失败是**调用方的问题**（改参数/等设备/先使能），
    执行层失败是**设备的事实**（没到位/报警了）；混在一起后 UI 无法区分"重试可能有用"与"重试没有意义"。
  - **"写成功即完成"被明确否定**——HMI 上按钮的反馈必须是设备事实。
- 审计映射：`Result.Fail` → `CommandRejected`；`Ok` + `Done` → `CommandExecuted`；
  `Ok` + {Aborted, Error, Timeout} → `CommandFailed`。

### D5 写点位声明 = `ElementMetadata.WritePoints`，构造时校验、缺失即失败

- 形态：语义名 → Modbus 地址（格式同 `Address`，**恒为 4 段**：从站:功能码:起始:长度），可声明在模板与 `Overrides`，例如：
  `"WritePoints": { "ControlWord": "1:06:100:1", "TargetPosition": "1:16:112:2", "TargetSpeed": "1:06:114:1", "ResetAlarmCoil": "1:05:16:1" }`。
- 构造时按 `ModbusAddress.Parse` 解析并校验（必须是写功能码 05/06/15/16，单寄存器写长度必须为 1）；
  **元件必需的写点缺失 = 构造失败**——沿用既有原则"配置错误当场炸掉"
  （`ModbusElementBase.cs:73-76`、`ServoElement` 的 6 寄存器校验）。
- **否决塞进 `Attributes`**：那是无校验的自由字典，会让"抄错地址"变成运行期才暴露的静默错误；
  **否决写死在元件代码**：同一型号在不同产线的地址布局不同，地址是现场事实。
- 一期只进 appsettings（与 `Attributes` 现状一致）；`elements` 表只承载 MES 推送覆盖，不扩表。

### D6 参数与状态模型对齐 `0004.PLC` 的数据类型

| 本计划（Core/C#） | 依据（0004.PLC） | 说明 |
|---|---|---|
| 命令参数 `MotionParameters`：`Position` / `Distance` / `Velocity` / `Torque` / `Accel` / `Decel` / `Jerk` / `Direction` / `BufferMode` / `HomingMethod` | `ST_ServoTarget`：`Mode`/`Position`/`Relative`/`Immediate`/`Velocity`/`Torque`/`Accel`/`Decel`/`Jerk` | 字段名逐项对应；`Relative` 由"命令键是 Absolute 还是 Relative"表达（不再单列布尔） |
| 命令→模式映射（元件类内） | `E_ServoMode` | `MC_Home`→`HOMING`；`MC_MoveAbsolute/Relative`→`PROFILE_POSITION`；`MC_MoveVelocity`→`PROFILE_VELOCITY` |
| 使能/复位语义 | `E_Cia402Command` + `FB_ServoControlSM` | `MC_Power`→`ENABLE_OPERATION`/`DISABLE_OPERATION`；`MC_Reset`→`FAULT_RESET`；`MC_Stop`→`QUICK_STOP`；断线后是否自动恢复使能对齐 `AutoRecover` 配置位 |
| 轴状态快照 `AxisStatus`：`ActualPos` / `TargetPos` / `IsMoving` / `InPosition` / `IsEnabled` / `IsFault` / `FaultCode` | `ST_AxisStatus` | 字段名逐项对应；`InPosition` 由状态机 `Idle`+到位判据推导 |
| 回零进度 `HomingStatus`：`Busy` / `Completed` / `Failed` / `Method` | `ST_HomingStatus` | `MC_Home` 的执行中状态与结果 |
| 只读反馈（供 UI 显示） | `ST_ServoFeedback` | 当前仅取 `ActualPosition` / `ActualVelocity` / `ActualTorque` / `ErrorCode`；`FollowingError`/`Temperature`/`DCBusVoltage` 等留待采集扩展 |

- 对齐方式：**字段名一致、语义一致、命名风格随 C#**（PascalCase 不变）；
  映射表进《轴控命令接口表》（见 §7），两侧评审一次，之后就是可回归的契约。

### D7 新 UI **生于 AntdUI**，不进入阶段十迁移清单（2026-10-05 确认继续采用 AntdUI）

- 新页面直接用 `09-ui-migration.md` 已验证的**全局别名策略 2**（`AntdXxx` 前缀）。
- **否决"先用原生 WinForms，事后再迁移"**：那等于把迁移清单从 8 个扩到 10 个，是纯重复劳动。
- 代价与对策：新页面会用到 LoginForm 未验证过的控件（表格/数字输入等）——
  按仓库专项约定**先写探针测试再实现**（"不盲写无法编译验证的第三方 API"）。
- 协调点：`MainForm` 自身排在阶段十迁移清单第 3 位。加导航项时按其**当时**的形态加（原生或 AntdUI），两边互不阻塞。

### D8 安全基线（**本期内容，不是"以后加"**）

- **边界声明**：本软件**不承担安全功能**（急停、安全门、安全联锁由 PLC 与硬接线负责）。
  上位机互锁只是**操作合理性**校验。此条写入 07-app.md / 06-devices.md 与现场说明。
- 权限：命令入口 `RequirePermission(Permission.MachineOperate)`（业务层、失败抛异常，UI 捕获——
  与 DeviceDebugForm 的既有捕获方式一致）；UI 用 `HasPermission` 决定可见性。
- 二次确认：危险命令（运动类与 `MC_Stop`）确认框必须显示**元件、命令、参数、将写入的地址与值**
  （沿用 07-app.md §7.2 对 DeviceDebugForm 的要求）。
- 互锁规则表（示例，完整表在里程碑 C）：未使能拒绝运动类；`Fault` 未 `MC_Reset` 拒绝 `MC_Power`；
  离线一律拒绝；运动中断由 `BufferMode` 显式表达而非静默覆盖。
- 审计：每条命令**成败均写** `IAuditService`（新增事件类型常量），含操作者（`ISessionProvider`）；
  审计失败不得影响命令结果（旁路，仅日志告警）。
- 命令引发的状态/数值变化走**既有数据出口**（MES/Historian）自然流出，不新增出口。

---

## 5. 里程碑总览

```
A 契约与配置面          B 命令执行内核           C 伺服命令闭环（PLCopen 对齐）
（Core + 配置 + 接口表草案）─►（Devices 元件无关部分）─►（8 条命令 + 互锁 + 回读 + 接口表定稿）
                                                      │
                                                      ├─► D 伺服操作台 UI（App）
                                                      │
                                                      └─► E 联调·安全收口·长稳 ─► F 气缸/传感器铺开与复用验证 ─► G HMI 现场化形态
```

**关键路径**：`A → B → C → D → E`。F 可与 D 并行（同一内核），保守排在其后。
G 按现场试用反馈裁剪或顺延。

| 里程碑 | 一句话 | 主干状态 |
|---|---|---|
| A | 命令契约（PLCopen 映射）、写点位声明、接口表草案、文档定稿 | 可编译可运行（新契约无调用方） |
| B | 队列执行、权限/审计/超时/离线拒绝（单元测试全 Mock） | 可运行（命令以桩验证） |
| C | 伺服 8 条 PLCopen 命令 + 互锁 + 回读观察 + 接口表定稿 | 可运行 |
| D | 总览 + 伺服操作台（HMI 替代的核心界面） | 可运行、可实跑操作 |
| E | 模拟器联调、审计收口、长稳 | 可交付现场试用 |
| F | 气缸命令、传感器监控深化、品牌复用验证 | 可运行 |
| G | 现场化形态（全屏/DPI/报警横幅，鼠标键盘） | 可运行 |

### 5.1 开发计划表（任务分解 WBS）

> 编号 = 里程碑 + 序号，**按执行顺序排列**；"规模"是**相对规模**（S ≈ 单文件/函数级，M ≈ 模块内多文件，L ≈ 跨模块、含测试与文档），**不是工期承诺**。
> 每个任务的"完成定义" = 其所属里程碑的验收标准（§6），不在此重复。状态列开工后逐项勾销（本表可直接粘贴 Excel/Word）。

| 编号 | 任务 | 产出 | 依赖 | 规模 | 状态 |
|---|---|---|---|---|---|
| A1 | Core 命令契约：`IElementCommandTarget` / 描述符 / 请求 / 结果 / 可用性 / `MotionParameters` / `AxisStatus` / `HomingStatus` / 审计常量 | Core 类型 + 单测 | D1/D2/D6 定稿 | M | ✅ 完成（2026-10-05，+31 测试） |
| A2 | 写点位声明：`ElementMetadata.WritePoints` + 解析校验 + 配置面（模板/`Overrides`） | 配置模型 + 校验 + 单测 | A1 | M | ✅ 完成（2026-10-05，+12 测试） |
| A3 | `ElementBase` 空命令表实现（四元件不破坏），`ElementFactory` 适配 | 代码 + 回归 | A1 | S | ✅ 完成（2026-10-05，+6 测试） |
| A4 | 《轴控命令接口表》**草案**（命令/状态寄存器 ↔ `ST_ServoTarget`/`ST_AxisStatus`/`E_ServoMode`） | `docs/axis-command-interface.md` | A1 | M | ✅ 完成（V0.1 草案） |
| A5 | 文档同步：`01-core.md`（契约 + §8 销账）、`06-devices.md`（命令模型 + 写点位）、`CLAUDE.md` 文档地图登记 | 文档 | A1–A2 | M | ✅ 完成 |
| A6 | 里程碑 A 验收：`verify-all.sh` 三段 | 验收记录 | A1–A5 | S | ✅ 完成（1040 全绿 / 0 警告 / 依赖不变量过；接口表评审待双方安排） |
| B1 | 命令队列与请求入口（权限 → 互锁 → 离线 → 入队 + TCS） | 代码 + 单测 | A6 | M | ✅ 完成（2026-10-05，+15 测试） |
| B2 | 轮询线程出队执行写阶段（`ModbusElementBase` 写辅助：05/06/16 按 `WritePoints`） | 代码 + 单测 | B1 | M | ✅ 完成（2026-10-05） |
| B3 | 期望谓词观察 + 超时（`Done`/`Timeout`，消息含期望与实际） | 代码 + 单测 | B1–B2 | M | ✅ 完成（2026-10-05，B2+B3 共 +10 测试） |
| B4 | 中断语义（Aborting：作废未执行 + `CommandAborted`；Buffered 有界排队） | 代码 + 单测 | B1 | M | ⏸ **Aborting 全量完成**（入队替代 + 执行中替代，2026-10-05）；**Buffered 顺延**——无消费方，按需再做（决策 D3 注明） |
| B5 | 取消路径（注销/停机/配置重载 → 未决命令以"已取消"完成） | 代码 + 单测 | B1 | S | ✅ 完成（2026-10-05，+3 测试；注销与停机两路径都接线） |
| B6 | 审计与日志接线（操作者/参数/结果；审计失败旁路） | 代码 + 测试 | B1 | S | ✅ 完成（2026-10-05，+7 测试；操作者由 AuditService 自行解析） |
| B7 | 单元测试包（Mock 通道全链路、拒绝/中断/超时/并发单写者、热路径无锁评审） | 测试 | B1–B6 | L | ✅ 完成（2026-10-05，+2 并发用例；热路径无锁快速路径） |
| B8 | 里程碑 B 验收 | 验收记录 | B7 | S | ✅ 完成（1077 全绿 / 0 警告 / 依赖不变量过；验收记录见 §6 里程碑 B） |
| C1 | 伺服控制字与写点位映射（对齐 `E_ServoMode`/`E_Cia402Command`；`WritePoints` 模板示例） | 代码 + 配置样例 | A2/B2 | M | ✅ 完成（2026-10-06，+8 测试；含构造期必需写点位断言） |
| C2 | `MC_Power` / `MC_Reset`（含 `AutoRecover` 语义对齐） | 代码 + 测试 | C1/B3 | M | ✅ 完成（2026-10-06，+9 测试） |
| C3 | `MC_Home`（`Method` → `ST_HomingStatus`；完成判据） | 代码 + 测试 | C1 | M | ✅ 完成（2026-10-06，+5 测试；含"陈旧完成位"证据规则） |
| C4 | `MC_MoveAbsolute` / `MC_MoveRelative`（参数校验 + 到位判据） | 代码 + 测试 | C1 | M | ✅ 完成（2026-10-06，+7 测试；含缩放与"陈旧到位位"规则） |
| C5 | `MC_MoveVelocity` / `MC_Halt` / `MC_Stop`（速度判据；锁定语义） | 代码 + 测试 | C1 | M | ✅ 完成（2026-10-06，+6 测试；**8 条命令全部就位**） |
| C6 | 互锁表 + 报警码→文本（`ErrorID` 文本化） | 代码 + 测试 | C2–C5 | M | ✅ 完成（2026-10-06，+17 测试；互锁矩阵为可执行制品） |
| C7 | 轴状态快照暴露（`AxisStatus`/`HomingStatus` 供 UI） | 代码 + 测试 | C2–C5 | S | ✅ 完成（2026-10-06，+8 测试；读块扩为 9 寄存器=契约变更） |
| C8 | 命令测试包（每命令：正常/互锁/超时/中断 四类；参数越界不发报文） | 测试 | C2–C7 | L | ✅ 完成（2026-10-06，+4 测试；四类用例按"机制×差异化"分工，见 06-devices.md §15.5） |
| C9 | 《轴控命令接口表》**定稿**（与 `0004.PLC` 双方评审）+ `06-devices.md`"操作与命令"章节 | 文档 | C1–C8 | M | ⚠️ 上位机侧完成（接口表升 V0.2 与实现对齐 + 06-devices.md §15）；**与 PLC 侧评审待人工安排** |
| C10 | 里程碑 C 验收 | 验收记录 | C8–C9 | S | ✅ 完成（2026-10-06，1141 全绿 / 0 警告 / 依赖不变量过；仅"双方评审"一项待人工） |
| D1 | AntdUI 新控件探针（表格/数字输入等，先探针后实现） | 探针测试 | A6 | S | ✅ 完成（2026-10-06，+3 探针：decimal/Column(key,title)/Width 字符串等结论已固化） |
| D2 | 元件总览页（机台分组/状态色/离线报警置顶；定时器拉快照，不订阅事件） | 页面 | C7/D1 | M | ✅ 完成（代码 + 导航 + DI；待实跑） |
| D3 | 伺服操作面板（命令区按描述符生成；参数校验；禁用原因提示） | 页面 | D2 | L | ✅ 完成（代码；含点动行雏形，待实跑） |
| D4 | 二次确认对话框（元件/命令/参数/地址与值）+ 权限可见性 | 对话框 | D3 | M | ✅ 完成（2026-10-06）：`IElementCommandTarget.DescribeWritePlan` 预演写入；确认框显示地址与值；**预演与实写共用同一段计划代码**（有防漂移测试） |
| D5 | 点动按压语义 + 看门狗（失焦/失活/断连自动 `MC_Halt`） | 代码 + 实跑 | D3 | M | ✅ 代码完成（2026-10-06）：按住走/松开停 + `MouseLeave`/`Deactivate`/`FormClosing` 三路看门狗；断连由元件离线拒绝兜底。**待实跑验证** |
| D6 | `App/Controls/` 无状态控件落地（状态卡片等） | 控件 | D1 | S | 待开始 |
| D7 | UI 实跑清单（`09-ui-migration.md §4` 改编）+ 100 次开关无增长 + 干净退出 | 实跑记录 | D2–D6 | M | 待开始 |
| D8 | 里程碑 D 验收 | 验收记录 | D7 | S | 待开始 |
| E1 | 模拟器联调（全命令 + 报文记录 + 异常注入 + `CommandAborted` 场景） | 联调记录 | C10/D8 | L | 待开始 |
| E2 | 断线/超时/设备无响应 场景验证 | 验证记录 | E1 | M | 待开始 |
| E3 | 长稳 ≥ 8h（命令 + 轮询并发；无内存/句柄增长、无未决命令堆积） | 长稳报告 | E1 | M | 待开始 |
| E4 | 审计可查（操作者/命令/参数/结果/时间） | 验证记录 | E1 | S | 待开始 |
| E5 | 文档销账（`06-devices.md`/`roadmap.md`/本文件）+ 记录新测试基线 | 文档 | E1–E4 | S | 待开始 |
| F1 | 气缸命令（Extend/Retract/Stop + 互锁 + 测试；复用 B 内核不改动） | 代码 + 测试 | E5 | M | 待开始 |
| F2 | 传感器监控深化（量程/阈值展示；空命令表路径回归） | 代码 + 回归 | E5 | S | 待开始 |
| F3 | 伺服变体接入验证（不同字序/控制字：只加元件类 + 配置；diff 证明 Core/App 零改动） | 变体 + 证明 | F1 | M | 待开始 |
| F4 | 扩展点文档（新增元件步骤：命令表 + 写点位 + 接口表登记） | 文档 | F3 | S | 待开始 |
| G1 | 全屏/高 DPI 实跑（1366×768 与 1920×1080；鼠标键盘） | 实跑记录 | E5 | S | 待开始 |
| G2 | 报警横幅 | 代码 + 实跑 | G1 | S | 待开始 |
| G3 | 自动登录/操作模式评估（安全权衡书面记录；可裁剪） | 评估记录 | G1 | S | 待开始 |

**执行说明**：

- **关键路径**：A1 → A2 → B1 → B2 → C1 → C2–C7 → C8 → D2 → D3 → D7 → E1 → E3。
- **可并行**：A2/A3 在 A1 后并行；C2–C5 之间互相独立（同一批文件，建议串行改）；F1/F2 可并行；D1 可在 C 期间提前做。
- **串行纪律**：B1–B7 都集中在 `ElementBase`/`ModbusElementBase` 同一批文件——不要与其他改动并行编辑这些文件（避免冲突与互相踩）。
- **每任务完工即跑**：构建 0 警告 + 相关测试；里程碑收口跑 `verify-all.sh` 三段。
- **过程中修复的既有缺陷**（2026-10-05，B1）：`DevicesServiceCollectionExtensions` 里一行裸的
  `TryAddSingleton<ElementFactory>()` 排在工厂委托<b>之前</b>（TryAdd 先到先得），
  使委托成为**死代码** —— 元件的 `changeWriter` 实际被置为 `null`，
  数据点变化从未进入 MES 推送总线。B1 因新增构造依赖（权限/时间源）必须经该委托生效，
  顺手修正并移除裸注册（已在代码原位留注释）。
- **既有间歇性 flaky 测试：已根治**（2026-10-08，D4 期间）：
  `MesPushMessagePublisherTests.OnEvent_IsNotPushedUntilAStateEventOccurs` 偶发失败的根因是
  "`Task.Delay(100)` + 负断言"——负载高时那条"不该推"的变化还没被消费循环处理，
  标记就被设置，于是它与下一条一起被推、`Single()` 抛。
  修法：给推送器加 **`ProcessedChanges` 可观测计数**（处理即计数，与是否推送无关），
  测试改为**确定性排空**（等计数追上，而不是睡固定时长）；连跑 6 次全绿。
- **批量 sed 的教训**（2026-10-06，C7）：地址 6→9 的整文件替换误伤了"地址只是任意输入"的用例
  （`ModbusAddressTests` 等），靠守卫测试逐个抓回。**批量替换必须区分"契约字面量"与"任意输入"。**
- **过程中修复的本变更自引入缺陷**（2026-10-05，B7）：命令管线原先"出队 → 入槽"分两步，
  两步之间存在窗口 —— 取消路径（注销/停机）看到一个"既不在队列、也不在执行槽"的命令，
  于是既不会取消它、它也永远不会被完成，<b>等待方（UI）永久挂起</b>。
  由 B7 的并发竞速用例抓出（测试跑 5 遍时挂起），修复为"出队 + 入槽"在同一临界区
  （`TakeNextForExecution`），并补"取槽后已被取消则不写"的守卫。
  教训记录：**并发用例的价值在钉住不变量（恰一个终态、写串行），它可以抓出设计窗口，
  而不只是防回归**。

### 5.2 开工前置清单（A0，动手前逐项确认）

- [ ] **关闭正在运行的 `EquipmentMonitor.App` 与 Visual Studio**，跑 `bash .claude/skills/change-workflow/scripts/verify-all.sh` 记录真实测试基线（上次因输出 DLL 被占用未跑成——非代码缺陷）
- [ ] D1–D8 决策逐条定稿并写入 `01-core.md` / `06-devices.md`（重点：D1 契约形态、D3 队列与单写者、D5 写点位格式）
- [ ] 按 `change-workflow §1` 通读目标模块文档与上游（`01-core.md` → `06-devices.md` → `05-communication.md`）
- [ ] 与 PLC 侧（`0004.PLC`）约定《轴控命令接口表》双方评审时间（里程碑 A 起草、C 定稿）
- [ ] SRS 签批（`SRS.md` §2 修订历史与 §15.2 变更记录的 `[姓名]`）
- [ ] 建立任务跟踪（本表状态列，或导入 Excel / 看板）

---

## 6. 里程碑详情

### 里程碑 A — 操作契约与配置面

**目标**：把"操作"定义成契约（PLCopen 映射），全部实现方与消费方（含文档）传播到位。

**交付物**

- `Core`：`IElementCommandTarget`、`ElementCommandDescriptor`、`ElementCommandRequest`、
  `MotionParameters`（对齐 `ST_ServoTarget`，见 D6）、`AxisStatus` / `HomingStatus`（对齐 `ST_AxisStatus` / `ST_HomingStatus`）、
  `ElementCommandResult`（`Done` / `CommandAborted` / `Error` / `Timeout` + `ErrorID`）、`CommandAvailability`、
  `AuditEventTypes` 新增命令相关常量。
- `Core`：`ElementMetadata.WritePoints`（+ 校验）；`ElementBase` 以空命令表实现 `IElementCommandTarget`（四元件不破坏）。
- 文档：`docs/axis-command-interface.md` **草案**（上位机 ↔ PLC 轴控的寄存器表：命令寄存器、状态寄存器、
  与 `ST_ServoTarget`/`ST_AxisStatus` 的映射）；
  `01-core.md`（契约 + §8 销账"WriteAsync 用字符串还是枚举"）、`06-devices.md`（写点位格式 + 命令模型章节）。

**验收标准**

- [ ] `Core` 的 `<PackageReference>` 仍为空；无新增项目引用（权限/审计/会话接口都已在 `Core/Abstractions`）
- [ ] 写点位解析与校验的单元测试：非法功能码、缺失必需写点、地址格式错误 → 构造失败且消息含元件与字段名
- [ ] 空命令表路径的测试：无命令元件（传感器）执行命令 → 拒绝，不抛异常
- [ ] 命令契约与 PLCopen 单轴块的**命名/参数/输出对照表**评审通过（D2/D6）
- [ ] 构建 0 警告；测试 ≥ 991
- [ ] `CLAUDE.md` 文档地图登记本文件、`SRS.md` 与 `axis-command-interface.md`

**风险**：契约面别做肥。描述符只上 UI 真正需要渲染的字段；"命令模板/脚本化编排"等不做。

---

### 里程碑 B — 命令执行内核（Devices，元件无关部分）

**目标**：一条命令从发起到完成的**全部横切**只实现一次（沿用基类理由：四元件各写一遍必然分叉）。

**交付物**

- `ElementBase`：有界命令队列、`ExecuteCommandAsync`（权限 → 互锁 → 离线校验 → 入队）、
  轮询线程侧出队执行写阶段、期望谓词观察与超时、TCS 完成、注销/停机时未决命令取消。
- **中断语义**：`BufferMode=Aborting`（默认）下新命令作废未执行者、以 `CommandAborted` 结束执行中者（D3）。
- `ModbusElementBase`：按 `WritePoints` 解析出的写辅助（单寄存器/多寄存器/线圈）。
- 审计与日志接线：每命令一条审计（含操作者/参数/结果）+ Information 级结构化日志。

**验收标准**（里程碑 B 收口记录，2026-10-05）

- [x] Mock 通道全链路测试：发起 → 入队 → 写出（断言功能码/地址/值）→ 回读满足 → `Done`
      （写辅助断言见 `ElementCommandPipelineTests` 的写点位用例；管道断言见同文件 ——
      真实 `ServoElement` 的端到端在里程碑 C 接上命令语义后成形）
- [x] 拒绝路径测试：离线拒绝 / 参数非法 / 未知命令 / 无权限抛 `UnauthorizedAccessException`（且不产生写出）
      ⚠️ **"队列满拒绝"在 Aborting 模式（当前唯一模式）下不可达** —— 新命令替代旧命令而非排队；
      `COMMAND_BUSY` 错误码为 Buffered 模式预留（同 B4 顺延）
- [x] 中断路径测试：执行中新命令到达 → 旧命令 `CommandAborted`，新命令同轮启动（`ElementCommandPipelineTests`）
- [x] 超时路径：期望永不满足 → `Timeout`，不抛异常，消息含期望与实际
- [x] 注销与停机：未决命令全部以"已取消"完成（无永久挂起的 Task；两条真实路径各有用例）
- [x] 并发测试：命令 flood + 泵送并发下 —— **每条命令恰好一个终态（审计条数=命令数）+ 写阶段串行**
      （`ElementCommandConcurrencyTests`）。状态机 `DurationMs` 由架构保证不受破坏：
      命令路径不触碰状态机，全部转移都在轮询线程（单写者）
- [x] 轮询热路径未新增锁：`DequeuePendingCommand` 在"无待执行命令"（绝大多数轮次）时经
      `_pendingCount` 快速路径不取锁；`PollAsync` 热路径零改动
- [x] 构建 0 警告；测试 **1077**（基线 991，本里程碑 +86）

**风险**：队列与 TCS 的生命周期是长稳泄漏的高发点（命令对象、TCS 延续）——测试与 E 的长稳专门盯。

---

### 里程碑 C — 伺服命令闭环（PLCopen 单轴）

**目标**：伺服成为第一个可被完整操作的元件，命令集与 PLCopen 单轴块、与 `0004.PLC` 的轴控类型逐项对齐。

**命令表（必要子集 8 条，详见 D2；参数对齐图 3-4 与 `ST_ServoTarget`）**

| 命令 | PLCopen 类型 | C# 参数 | 期望（回读） | 互锁（示例） |
|---|---|---|---|---|
| `MC_Power` | Enable 级 | `Enable`(bool) | 使能位 = `Enable` | `Fault` 未复位时拒绝使能 |
| `MC_Reset` | Execute | — | 报警/故障位清零，`ErrorID=0` | — |
| `MC_Home` | Execute | `HomingMethod`(int) | `Homing` → 完成（`HomingStatus.Completed`） | 需已使能；运动中拒绝 |
| `MC_MoveAbsolute` | Execute | `Position` / `Velocity` / `Accel` / `Decel` / `Jerk?` / `Direction` / `BufferMode` | `Positioning` → `InPosition` | 需已使能、无报警 |
| `MC_MoveRelative` | Execute | `Distance`（其余同上） | 同上 | 同上 |
| `MC_MoveVelocity` | Execute | `Velocity` / `Direction` / `Accel` / `Decel` / `BufferMode` | 速度回读达到指令值 | 同上 |
| `MC_Halt` | Execute | `Decel` / `Jerk?` / `BufferMode` | 速度 = 0，**保持使能** | — |
| `MC_Stop` | Execute | `Decel` / `Jerk?` | 停止且轴锁定（恢复需重新使能/复位） | — |

**只读不新增接口**：`MC_ReadStatus` / `ReadActualPosition` / `ReadActualVelocity` / `ReadAxisError`
由既有数据点与状态机覆盖（D2）。

**交付物**

- 控制字/触发位定义（常量 + `WritePoints` 配置面）、8 条命令实现、互锁表、报警码→文本映射（`ErrorID` 文本化）；
- **`docs/axis-command-interface.md` 定稿**（与 `0004.PLC` 评审：命令寄存器 ↔ `E_ServoMode`/`E_Cia402Command`/
  `ST_ServoTarget`；状态寄存器 ↔ `ST_AxisStatus`/`ST_HomingStatus`）；
- `06-devices.md` 新增"操作与命令（PLCopen 对齐）"章节（命令表 + 互锁表 + 写点位示例 + 与 PLC 侧映射）。

**验收标准**

- [ ] 命令键/参数/语义/输出与 PLCopen 单轴块**逐条对照表**评审通过；与 `0004.PLC` 的类型映射逐条核对
- [ ] 每条命令：**正常 / 互锁拒绝 / 超时 / 被新命令中断（`CommandAborted`）** 四类用例
- [ ] "写成功但回读不符"用例 → `Timeout`/`Error`，不是 `Done`
- [ ] 状态仍由读数推导：命令只影响"期望与超时"，不直接改状态机（代码评审项）
- [ ] 参数越界在**入队前**被拒（不发出任何报文）
- [ ] 构建 0 警告；测试 ≥ 991

**风险**：控制字位定义随品牌不同——一期只做现有 `ServoElement` 的映射；变体走派生类（F 验证）。
跨项目接口表与实际 PLC 侧实现可能有出入——接口表定稿必须双方评审，不能单方面写。

---

### 里程碑 D — 伺服操作台 UI（App，生于 AntdUI）

**目标**：操作工看得见、点得动——HMI 替代的核心界面。

**交付物**

- `ElementOverviewPage`：元件总览（机台分组、状态色、离线/报警置顶、关键值）；
- `ElementOperationPage`（伺服）：实时位置/速度/扭矩/使能/报警码（`ErrorID` 文本）+ **命令区按描述符生成**：
  - `MC_Power` → 使能开关（电平）；`MC_Reset` → 复位按钮；
  - `MC_Home` → 回零按钮（Method 可选）；`MC_MoveAbsolute`/`MC_MoveRelative` → 定位参数组
    （`Position`/`Distance`/`Velocity`，带范围校验）；
  - `MC_MoveVelocity` → 速度运行（方向 + 速度）；`MC_Halt` / `MC_Stop` → 停止按钮（区分文案与后果）；
  - 点动 = 按住 `MC_MoveVelocity` / 松开 `MC_Halt`（D2），**看门狗**：UI 失焦/窗体失活/断连自动 `MC_Halt`；
  - 禁用项显示互锁原因；执行中显示 `Busy`，被替代显示 `CommandAborted`，失败显示 `ErrorID` 文本。
- 二次确认对话框（含"将写入的地址与值"）；`App/Controls/` 目录落地（07-app.md §3 已承诺）。

**验收标准**

- [ ] 刷新用 UI 定时器拉 `GetSnapshot()`（与 MesPushPage 现状一致），**不订阅元件事件**（规避泄漏与节流缺失）
- [ ] AntdUI 新控件**探针测试先行**，再实现（表格/数字输入等）
- [ ] **实跑清单**（改编自 `09-ui-migration.md §4`）：布局、Tab 顺序、回车、取消路径、禁用态原因、二次确认文案
- [ ] 点动看门狗实测：模拟 UI 卡顿/断连，点动必须自动停止
- [ ] 权限：无 `MachineOperate` 时按钮不可见；**绕过 UI 直接调服务仍被拒**（这是业务层校验的证明）
- [ ] 重复开关页面/主窗体 100 次无内存增长（事件与控件订阅已正确处理）
- [ ] 关闭主窗体后进程干净退出；构建 0 警告；测试 ≥ 991

**风险**：本里程碑是"测试全绿 ≠ UI 可用"的重灾区（阶段十教训）——**每条验收都必须实跑**，不接受仅构建/测试证明。

---

### 里程碑 E — 模拟器联调、安全收口与长稳

**目标**：从"能跑"到"敢给现场试用"。

**交付物**：Modbus 从站模拟器联调记录（全命令 + 异常注入 + `CommandAborted` 场景）；审计查询可见（复用既有审计查看）；
`06-devices.md` / `roadmap.md` / 本文件验收项勾销。

**验收标准**

- [ ] 模拟器（含写入与回读）全命令联调通过；记录命令报文（功能码/地址/值）
- [ ] 断线、超时、设备无响应场景：降级不崩溃，未决命令有明确结果，恢复后无需重启
- [ ] 长稳 ≥ 8h（命令 + 轮询并发）：无内存/句柄增长、无未决命令堆积、命令队列无泄漏
- [ ] 审计可查：操作者、命令、参数、结果、时间齐全
- [ ] 构建 0 警告；测试 ≥ 991（记录新基线）

**风险**：现场设备差异只有真实联调才能暴露——模拟器通过不等于现场通过，**试用期设观察窗口**。

---

### 里程碑 F — 气缸/传感器铺开与复用验证

**目标**：证明"品牌/类型复用"不是口号。

**交付物**

- 气缸：Extend / Retract（+ Stop）命令、与限位状态机互动、动作超时诊断（沿用 B 的内核，不改动）；
- 传感器：监控深化（量程/阈值配置面展示、质量码与去抖结果可见）；**空命令表路径自然复用**；
- **复用验证**：新增一个伺服变体（不同控制字/字序/地址布局）——**只加元件类 + 配置**，Core/App 零改动，
  以测试与配置样例固化；
- `06-devices.md` 扩展点表更新：新增元件类型的步骤含"命令表 + 写点位 + 接口表登记"标准动作。

**验收标准**

- [ ] 气缸命令的 正常/互锁/超时 用例齐全
- [ ] 伺服变体接入：不修改 `Core`、`App`、`Communication` 任何文件（用 diff 证明）
- [ ] 传感器无命令路径无回归（空命令表 + 全量既有测试）
- [ ] 构建 0 警告；测试 ≥ 991

---

### 里程碑 G — 现场化形态（可裁剪，鼠标键盘）

**目标**：从"界面"到"操作工长时间盯守的操作界面"。

**内容**：**鼠标键盘操作（SRS §4.4 明确无触摸屏）**——按钮按点击友好设计，不做触摸尺寸假设；
全屏 / 高 DPI 实跑（1366×768 与 1920×1080 两档）；报警横幅；
自动登录/操作模式（如需要——安全权衡书面记录）；多显示器适配。
按现场试用反馈裁剪或顺延，**取舍必须记录进本文档**。

---

## 7. 横切约定

| 项 | 约定 |
|---|---|
| **依赖不变量** | 本变更**不新增任何项目引用与 NuGet 包版本**（权限 `IPermissionService`、审计 `IAuditService`、会话 `ISessionProvider` 均已在 `Core/Abstractions`）。`dependency-rules` 验证照常跑 |
| **跨项目契约** | `docs/axis-command-interface.md` 是上位机 ↔ `0004.PLC` 的**共用契约**：一方修改必须同步另一方并双方评审；寄存器表变更等同 SQL 脚本变更的严肃度（现场设备会照它动作） |
| **需求基线** | `docs/SRS.md` 是需求基线：本计划的范围/验收与其 §15.1 追踪矩阵对账（§2.3）；需求增删改必须同步 SRS §15.2 变更记录，并由 [甲方/负责人] 批准 |
| **契约传播** | 每个里程碑完工时四件事齐备：实现方、消费方、`docs/module-design/*.md`、`architecture.md §3.1`（如矩阵变化）。文档不销账 = 变更未完成 |
| **测试基线** | 991（2026-10-04）。每个里程碑跑 `verify-all.sh`（构建 0 警告 → 全量测试 ≥ 基线 → 依赖不变量），记录新基线 |
| **UI 实测** | 凡 UI 改动必须实跑（阶段十实测教训：`PerformClick` 在挂载后静默失效，全部测试仍绿） |
| **已发现的文档滞后**（顺手销账，不扩大范围） | ① `07-app.md §6` 承诺 `PostThrottled`，`UiDispatcher` 实际只有 `Post`；② `architecture.md §2` / `07-app.md` 仍写 SunnyUI，实际为 AntdUI（阶段十已记录）；③ `architecture.md §3.1` 矩阵缺 `Devices → Data`、无 `Mes` 列（CLAUDE.md 已登记） |

---

## 8. 风险登记

| # | 风险 | 影响 | 对策 |
|---|---|---|---|
| 1 | **误写引发设备动作**（本变更最高风险） | 设备/人身 | D8 安全基线；模拟器先行；二次确认含地址与值；审计可追溯；现场试用观察窗口 |
| 2 | **跨项目契约漂移**（上位机命令寄存器表 ↔ PLC 轴控程序） | 命令写到错误寄存器、两侧语义不一致 | 接口表双方评审、版本化；联调（E）逐条核对报文；变更同步写进两侧文档 |
| 3 | 命令引入状态机第二写者 | 状态/时长统计错乱，难排查 | D3 队列机制；并发用例（B）守卫 |
| 4 | 点动失控（UI 卡死/断连未发 `MC_Halt`） | 设备持续动作 | 看门狗（里程碑 D）；超时自动停止写入互锁表 |
| 5 | 未决命令挂起（注销/停机/中断竞态） | UI 永久等待、内存滞留 | B 的取消与 `CommandAborted` 路径 + E 的长稳 |
| 6 | AntdUI 未验证控件差异 | 隐性 UI 缺陷（阶段十同款） | 探针测试先行；实跑清单（里程碑 D） |
| 7 | 模拟器对写功能码/回读支持不足 | 联调受阻 | E 开工前先验证模拟器能力，不足则用脚本从站替代 |
| 8 | 伺服控制字定义随品牌漂移 | 复用承诺落空 | F 用"变体接入"可验证验收固定 |

---

## 9. 不在本期范围

- **PLCopen 单轴扩展块**（本期明确裁剪，需要时按同一契约追加）：
  `MC_MoveAdditive`、`MC_MoveSuperimposed`、`MC_MoveContinuousAbsolute/Relative`、`MC_MoveJog`、
  `MC_SetPosition`、`MC_TouchProbe` / `MC_AbortTrigger`、`MC_DigitalCamSwitch`、
  位置/速度/加速度 Profile 类、`MC_ReadParameter` / `MC_WriteParameter`（伺服参数读写，含 Pn 参数表）。
- **多轴与协调块**：`MC_GearIn/Out`、`MC_CamIn/Out`、`MC_GearInPos`、`MC_CombineAxes`、`MC_CamTableSelect`
  （PLC 侧的 XYZ 协调由 `0004.PLC` 的 PNP 框架负责，上位机只做单轴操作台）。
- **MQTT / OPC UA / EtherNet/IP 的写命令**：OpcUa/EtherNetIp 通道本身尚未实现（architecture.md §2.2）；
  命令契约保持协议无关，实现随通道成熟度排期。
- **命令编排/配方/脚本化批量动作**：先证明单命令安全闭环。
- **命令事件推送 MES**：命令引发的数据变化走既有出口即可；"命令流水推 MES"另行评估。
- **权限点细分**：`MachineOperate` 一枚权限点覆盖全部元件操作，暂不拆"按元件/按命令"粒度。
- **点表动态化**（改配置不重启生效 / 可视化点表编辑，SRS FR-012）：**后续独立变更**（2026-10-05 确认）。
  本变更只在里程碑 B 提供其前置路径——"注销/停机时取消未决命令"。

## 10. 相关文档

- [总体架构](architecture.md) ｜ [编码规范](conventions.md) ｜ [开发计划](roadmap.md)
- [需求规格说明书（SRS）](SRS.md)（需求基线，§2.3 对账表）
- [Core 模块](module-design/01-core.md)（命令契约落点）
- [Devices 模块](module-design/06-devices.md)（写点位、命令语义、互锁）
- [App 模块](module-design/07-app.md)（操作台、UI 安全要求）
- [AntdUI 替换清单](module-design/09-ui-migration.md)（新界面沿用其策略与实跑清单）
- `docs/axis-command-interface.md`（上位机 ↔ PLC 轴控接口表，里程碑 A 起草）
- 依据（仓库外）：`0004.PLC` 轴控项目（`library/drivers/servo/`、`library/services/pnp/types/`、
  `docs/reference/vendor_library_mappings.md`）；`运动控制2.pdf` 第 3 章（PLCopen 运动控制规范）
