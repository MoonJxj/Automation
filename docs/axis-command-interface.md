# 轴控命令接口表（上位机 ↔ PLC）

> **状态：草案（V0.2，2026-10-06：上位机侧实现已对齐，待与 PLC 轴控项目（`0004.PLC`）双方评审后定稿）。**
> 定稿前**不得**作为现场接线/编程依据。
>
> 本表是**跨项目契约**：上位机（本仓库）按它写命令寄存器、读状态寄存器；
> PLC 轴控程序按它解释命令、回写状态。**一方修改必须同步另一方并双方评审**
> （变更计划 §7）。寄存器内容直接导致设备动作，修改的严肃度等同已发布的 SQL 脚本。
>
> 设计依据：PLCopen 运动控制规范（《运动控制》第 3 章）；`0004.PLC` 的
> `E_ServoMode` / `E_Cia402Command` / `ST_ServoTarget` / `ST_AxisStatus` / `ST_HomingStatus`；
> 上位机侧契约 `IElementCommandTarget` / `MotionParameters` / `AxisStatus`（里程碑 A1 已实现）。

## 1. 角色与总体约定

| 项 | 约定 |
|---|---|
| 通信 | Modbus TCP；**上位机 = Client/Master，PLC = Server/Slave** |
| 归属 | 每个**轴元件**占一段连续寄存器区：状态区（只读）+ 命令区（写） |
| 单位 | 位置 mm、速度 mm/s、加速度 mm/s²、加加速度 mm/s³（工程单位，**由上位机换算后写整数**） |
| 缩放 | 每个数值参数在**上位机配置**里有缩放系数（`Attributes`），PLC 侧只认整数寄存器 |
| 字节序 | 32 位量=两个寄存器，**高字在前**（与 `ServoElement` 现状一致） |
| 写语义 | 上位机的每条命令 = 一组合法寄存器写入序列；**写后必须回读状态确认**（上位机侧，决策 D4） |
| 缺省 | 未使用的寄存器写 0；PLC 侧不得依赖"寄存器未被写过"的初始值 |

## 2. 状态区（只读，9 个寄存器）

相对轴元件读块起始地址（如 `1:03:100:9`）。**+0～+5 为已实现布局**（`ServoElement` 现状），
+6～+8 为本次变更新增（供 `AxisStatus` / `HomingStatus` 快照）。

| 偏移 | 宽度 | 内容 | 上位机对应 |
|---|---|---|---|
| +0, +1 | 2 | 当前位置 i32（高字在前） | `AxisStatus.ActualPosition` |
| +2 | 1 | 当前速度 i16 | `AxisStatus`（速度显示） |
| +3 | 1 | 当前扭矩 i16 | 扭矩显示 |
| +4 | 1 | **状态字**（见 §2.1） | `AxisStatus` 各布尔 / 命令终态判据 |
| +5 | 1 | 报警/故障码 u16（0=无） | `AxisStatus.FaultCode`、`ErrorID` |
| +6, +7 | 2 | **目标位置** i32（高字在前） | `AxisStatus.TargetPosition` |
| +8 | 1 | **回零方式回显** u16 | `HomingStatus.Method` |

### 2.1 状态字位定义

| 位 | 含义 | 来源 |
|---|---|---|
| bit0 | 使能中（Operation Enabled） | `FB_ServoControlSM.IsOperational` |
| bit1 | 移动中 | 轴控程序 |
| bit2 | 回零中 | `ST_HomingStatus.Busy` |
| bit3 | 定位中 | 轴控程序 |
| bit4 | 报警（可继续，需关注） | 驱动器状态字 |
| bit5 | 故障（不可运行，需复位） | 驱动器状态字 |
| bit6 | **到位**（InPosition） | 轴控程序（就绪 + 到位判据） |
| bit7 | **回零完成** | `ST_HomingStatus.Completed` |
| bit8 | **命令忙**（Busy） | 轴控程序 |
| bit9 | **命令完成**（Done，脉冲或电平，见 §5-Q3） | 轴控程序 |
| bit10 | **命令被中断**（CommandAborted） | 轴控程序 |
| bit11 | **命令错误**（Error） | 轴控程序 |
| bit12–15 | 保留 | — |

> bit0–bit5 与 `ServoElement` 的现有位定义**逐位一致**（现状沿用）；bit6–bit11 为本次新增，
> 对应 PLCopen 的 `Done / Busy / CommandAborted / Error` 输出与到位判据。

## 3. 命令区（写，逐轴）

相对轴元件命令区起始地址（如 `1:06:1000:1` 起，具体基址按现场配置）。
语义名即上位机 `WritePoints` 的键（`CommandParameterKeys` 之外的写法见各条备注）。

| 语义名（WritePoints 键） | 功能码 | 宽度 | 内容 | 对应 PLC 侧 |
|---|---|---|---|---|
| `ControlWord` | 06 | 1 | CiA402 控制字 | `E_Cia402Command` → `FB_ServoControlSM.CtrlWord` |
| `Mode` | 06 | 1 | 操作模式：1=PP 定位、2=PV 速度、6=HM 回零、0=无 | `E_ServoMode` → `ST_ServoTarget.Mode` |
| `TargetPosition` | 16 | 2 | 目标位置 i32（高字在前） | `ST_ServoTarget.Position` |
| `Relative` | 06 | 1 | 0=绝对、1=相对 | `ST_ServoTarget.Relative` |
| `TargetVelocity` | 06 | 1 | 目标速度 u16（缩放后整数，**正负由方向位或符号约定**，见 §5-Q2） | `ST_ServoTarget.Velocity` |
| `Accel` | 06 | 1 | 加速度 u16 | `ST_ServoTarget.Accel` |
| `Decel` | 06 | 1 | 减速度 u16 | `ST_ServoTarget.Decel` |
| `Jerk` | 06 | 1 | 加加速度 u16（0=不使用） | `ST_ServoTarget.Jerk` |
| `HomingMethod` | 06 | 1 | 回零方式 | `ST_HomingStatus.Method` |
| `Execute` | **05（线圈）** | 1 | **命令触发脉冲**（见 §5-Q1） | 上升沿 = 执行一条命令 |

## 4. 命令 → 寄存器写入序列（上位机实现依据）

上位机按本表实现 8 条命令（决策 D2）；PLC 侧按本表解释。
每条命令的"期望"由上位机**回读状态字**判定（不满足 = Timeout，决策 D4）。

| 命令 | 写入序列 | 期望（回读） |
|---|---|---|
| `MC_Power`（Enable=true） | `ControlWord` ← 0x000F | 状态字 bit0 = 1 |
| `MC_Power`（Enable=false） | `ControlWord` ← 0x0007 | 状态字 bit0 = 0 |
| `MC_Reset` | `ControlWord` ← 0x0080，随后回写 0x000F（若需保持使能） | bit4/bit5 = 0 且报警码 = 0 |
| `MC_Home` | `HomingMethod` ← n；`Mode` ← 6；`Execute` 脉冲 | bit2（回零中）→ bit7（回零完成）；失败 → bit5 或报警码 |
| `MC_MoveAbsolute` | `TargetPosition`、`TargetVelocity`、`Accel`、`Decel`、`Jerk?`、`Relative` ← 0；`Mode` ← 1；`Execute` 脉冲 | bit3（定位中）→ bit6（到位） |
| `MC_MoveRelative` | 同上，`Relative` ← 1 | 同上 |
| `MC_MoveVelocity` | `TargetVelocity`、`Accel`、`Decel`；`Mode` ← 2；`Execute` 脉冲 | 速度回读 ≈ 指令值 |
| `MC_Halt` | `Mode` ← 2；`TargetVelocity` ← 0；`Execute` 脉冲 | 速度回读 → 0，**bit0 保持 1** |
| `MC_Stop` | `ControlWord` ← 0x0002（QUICK_STOP） | 停止且 bit0 = 0（恢复需重新 `MC_Power`） |

> **中断语义**（决策 D3，BufferMode=Aborting）：新命令的 `Execute` 脉冲到达时，
> PLC 侧应中断执行中的运动（回读 bit10 = 1 表示被中断），并尽快接受新命令。
> 上位机侧队列深度默认为 1，不依赖 PLC 侧排队。

## 5. 待确认问题（双方评审时逐条敲定）

| # | 问题 | 提议 | 影响 |
|---|---|---|---|
| Q1 | **命令触发方式**：`Execute` 线圈的"脉冲"如何可靠送达 | ✅ **上位机已按提议实现**（2026-10-06）：每条命令先写 0 再写 1，PLC 侧只需检测上升沿 | 触发丢失 = 命令不执行，属"设备不动"类难排查故障；**PLC 侧待确认** |
| Q2 | 速度方向表达：`TargetVelocity` 用符号（i16）还是独立方向位 | ✅ **上位机已按提议实现**：i16 有符号 | PLC 侧待确认 |
| Q3 | bit9（Done）是脉冲还是电平：脉冲会被轮询错过 | 提议：**电平保持到下一次命令**（上位机轮询周期 100–200ms，脉冲宽度无法保证） | 漏读 Done 会把成功判成超时；**PLC 侧待确认** |
| Q4 | 缩放系数与量程：各参数在上位机 `Attributes` 里的键名与默认值 | ✅ **上位机已实现**：速度**读/写共用 `SpeedScale`**（否决单独的 `VelocityScale`——两个必须互相匹配的系数是配置的坑）、`AccelScale`/`DecelScale`/`JerkScale` 默认 1.0 | 写错缩放 = 速度差 10 倍 |
| Q5 | 报警码 → 文本对照表由哪一方维护 | 提议：PLC/驱动器手册为准，上位机侧配置映射 | 现场排障可读性 |
| Q6 | 多轴地址分配（X/Y/Z 各自的读块与命令区基址） | 提议：每轴连续一段，基址按现场点表；上位机用模板 + `Overrides` 表达 | 现场点表编制 |
| Q7 | PLC 侧是否需要写保护（拒绝非法序列，如未使能时直接定位） | 提议：**两侧都做**——上位机互锁（决策 D8）+ PLC 侧兜底 | 安全边界：本软件不承担安全功能，PLC 侧兜底是最后防线 |

## 6. 变更记录

| 日期 | 版本 | 变更内容 | 提出方 | 双方确认 |
|---|---|---|---|---|
| 2026-10-05 | V0.1 | 初稿（依 PLCopen 单轴 + `0004.PLC` 类型提出） | 上位机侧 | [待评审] |
| 2026-10-06 | V0.2 | 上位机侧 8 条命令实现完成并与本表对齐（Q1/Q2/Q4 按提议落地；状态区 9 寄存器已实现）；Q3/Q5/Q6/Q7 仍待 PLC 侧确认 | 上位机侧 | [待评审] |

## 7. 相关文档

- [变更计划](ChangePlan.md)（决策 D2/D4/D6、里程碑 A4/C9）
- [Devices 模块设计](module-design/06-devices.md)（写点位、命令语义）
- [需求规格说明书](SRS.md)（§6.4 通信接口、§10.1 点表）
- `0004.PLC`：`library/drivers/servo/`（`FB_ServoControlSM` / `E_ServoMode` / `ST_ServoTarget`）、
  `library/services/pnp/types/ST_AxisStatus.st`
