# 轴控开发框架交接说明

> 状态：**交接基线**（2026-10-29）
> 范围：`src/EquipmentMonitor.Common`、`src/EquipmentMonitor.Core`、`src/EquipmentMonitor.Infrastructure`、`src/EquipmentMonitor.Desktop`
> 目标：建立可复用的通用轴控开发框架；轴控**接口形状**已基本定型，实现层处于骨架阶段。
> 本文只记录事实与判断，不替代设计文档；与代码冲突时以代码和 `.csproj` 为准。

---

## 1. 一页结论

| 维度 | 现状 | 证据 |
|---|---|---|
| 新框架构建 | **可编译**：0 错误，33 条可空性警告 | `dotnet build AxisFramework.slnx -t:Rebuild` |
| 全解决方案构建 | **不可编译**：166 个错误，全部在旧 `EquipmentMonitor.Core` | `dotnet build EquipmentMonitor.slnx` |
| 轴控接口 | 已定义：`IAxisController`、`IElementAxis`、`IRegisterController`、`AxisConfig`、状态模型 | `src/EquipmentMonitor.Core/Abstractions`、`src/EquipmentMonitor.Infrastructure/Hardware/Entities` |
| 轴控实现 | 模拟轴 + `AxisElement` 已闭环；Modbus 轴（异步）+ 同步适配桥已实现；雷赛轴部分可用；固高仍是骨架 | 见 §5 |
| 元件层 | `AxisElement` 已实现（模拟轴/Modbus 均可接）；`IElementCylinder` 仍只有接口 | `Infrastructure/Axis/AxisElement.cs` |
| 配置映射 | `AxisConfig → AxisElementConfig` / `ToModbusMap` 与 `AxisFrameworkLoader` 已实现；**尚未从数据库读取配置** | `Infrastructure/Axis/` |
| 桌面宿主 | **R1 + R2 已交付**：`EquipmentMonitor.Desktop` 主壳 + 只读总览 + 伺服操作台（9 条命令、权限/审计入口、点动看门狗） | `src/EquipmentMonitor.Desktop`、`docs/module-design/10-desktop.md` |
| 用户权限 | **本地账号 + 用户管理**：5 角色、JSON 用户目录、可选口令、登录门禁与用户管理页；Windows/远端留接口 | `Core/Enums/UserRole.cs`、`Infrastructure/Security/`、`src/EquipmentMonitor.Desktop/Forms/UserManagementPage.cs` |
| 测试 | 新框架 **152 条全绿**（轴框架 + 桌面）；旧 991 条基线当前仍无法运行 | `tests/EquipmentMonitor.AxisFramework.Tests`、`tests/EquipmentMonitor.Desktop.Tests` |
| DI | 两个会直接抛异常的注册已修；配置/日志/数据库仍未闭环 | 见 §7.3 |

**一句话交接**：当前已完成可测试的纵向切片（模拟轴/Modbus 轴 → `AxisElement` → 状态/命令下发），下一步是接真实 PLC、雷赛/固高动作与宿主轮询。

### 1.1 本轮新增（2026-10-29 第二轮）

- `SimulatedAxisController`：内存轴，支持使能/回零/绝对/相对/点动/停止/复位/状态字，无需硬件；
- `AxisElement`：把 `IAxisController` 包装成 `IElementAxis`；命令方法只负责“命令下发”，参数合法性在元件层，**软限位由轴卡/PLC 兜底**；**不等待到位**，到位由业务通过 `RefreshAsync` 轮询 `Status` 判断；
- `ModbusRegisterController`：`IRegisterController` 的 Modbus 实现，写后回读校验，含线圈读写；
- `IAsyncAxisController`（**internal**）+ `ModbusAxisController`（internal）：Modbus 轴的异步实现，按状态区 9 寄存器/命令区 10 寄存器 + Execute 线圈工作；
- `AsyncAxisControllerAdapter`（internal）：**唯一允许 `.GetAwaiter().GetResult()` 的同步适配桥**，把异步 Modbus 接到 `IAxisController`；对外不可见；
- `ModbusAxisControllerFactory` + `AxisConfig.ToModbusMap`：按 `ControllerConfig` 从协议池取连接，按 `AxisConfig` 建每轴映射；
- `AxisFrameworkOptions` + `AxisFrameworkOptionsLoader`：启动时从 JSON 文件读取控制器/轴配置；
- `AxisFrameworkBootstrapper`：配置读取 → 装配 → 返回已初始化的控制器与轴元件；
- `AxisFrameworkLoader`：按 `ControllerConfig`/`AxisConfig` 创建控制器与轴元件，汇总所有装配错误；
- 55 条测试：轴命令下发、故障复位、寄存器转换、Modbus 寄存器/线圈、Modbus 轴命令、适配桥、机台聚合、配置文件读取与装配；
- 清理重复契约：删除未使用的 `HoldingRegisterController`、`RegisterDataType`、`IoPointType`、三个品牌工厂，`RegisterPointConfig` 改用 `Common.RegisterWordOrder`；
- 修正 `IElementAxis.ReadStateWordAsync` 的 `out` 参数（异步方法不能用 `out`）。

### 1.2 本轮新增（2026-10-29 第三轮：R1 桌面宿主）

- 新增 `src/EquipmentMonitor.Desktop`（`net8.0-windows`）：本仓库唯一允许 `System.Windows.Forms` 的 UI 宿主；
- `Program`：Generic Host + Serilog + 全局异常兜底；引导期装配 `AxisFrameworkBootstrapper`，失败弹窗并返回码 2；
- `AxisMonitorService`（`BackgroundService`，200ms）：后台刷新全部轴，**单轴失败不中断整轮**；
- `AxisSnapshotStore` + `AxisMonitorSnapshot` / `AxisSnapshot`：不可变快照 + `Volatile` 发布，UI 无锁读；
- `MainForm`：导航壳 + 状态栏（轴数 / 刷新时间 / 采集错误）；
- `ElementOverviewPage`：AntdUI 表格只读总览，故障/警告置顶，500ms 拉快照（不订阅元件事件）；
- 9 条测试：容器装配、模拟轴引导、快照发布、页面排序与错误提示、主窗体解析；
- 命名统一：`Infrastructure.csproj` → `EquipmentMonitor.Infrastructure.csproj`（文件夹名 = 项目名）；
- 新增文档 `docs/module-design/10-desktop.md`（UI 职责边界、线程模型、R1 验收、R2 前置）。

> R2（伺服操作台）在本轮之后完成，见 §1.3。

---

### 1.3 本轮新增（2026-10-29 第四轮：R2 伺服操作台）

- **Core 命令契约**：`IElementCommandTarget`、`ElementCommandDescriptor`、`CommandParameterDescriptor`、`ElementCommandRequest/Result`、`CommandAvailability`、`CommandValue`、`CommandRejectionCodes`；`CommandOutcome` 增加 **`Accepted`**（已下发、未到位），避免把“写成功”冒充实为 `Done`；
- **权限 / 审计**：`IPermissionService`、`IAuditService`、`AuditEvent`；`DevelopmentPermissionService`（全放行 + 启动告警）与 `LogAuditService`（写结构化日志）；`AxisElement` 命令入口统一调用 `RequirePermission(MachineOperate)`，每条命令成败写审计；
- **`AxisElement` 9 条命令**：`MC_Power` / `MC_Reset` / `MC_Halt` / `EStop` / `JogForward` / `JogBackward` 等；单一入口做「权限 → 参数 → 互锁 → 下发 → 审计」，控制器调用在 `Task.Run` 中执行，UI 线程不阻塞；
- **软限位**：从 `AxisConfig` 映射到 `AxisElementConfig`，作为 `MC_MoveAbsolute` 的参数范围；限位缺失/颠倒时只忽略范围提示（硬件仍兜底），不在元件层炸配置；
- **`ElementOperationPage`**：按描述符生成命令区、危险命令二次确认、互锁置灰 + 原因；点动按压 + `MouseLeave` / 窗体失活 / 窗体关闭三路看门狗；
- **测试 84 条**（轴框架 64 + 桌面 20），含「页面 → 元件」的真实消息泵用例、「选中 → 轴控面板绑定」、相对定位与设计器分部类守卫。

> **与 ChangePlan D4 的偏差（已记录）**：新框架的命令成功态 = `Accepted`（已下发），不是 `Done`；到位靠 150ms 状态刷新观察；二次确认不含寄存器地址与值（地址封装在控制器内部）。详见 `docs/module-design/10-desktop.md` §7/§8。

### 1.4 本轮新增（2026-10-31 第五轮：本地用户权限最小闭环）

- **Core 契约**：`UserRole`（角色）、`AuthenticationSource`（本地账号；Windows/远端为扩展位）、`UserSession` / `SignInRequest` / `AuthenticationProviderInfo`、`UserRoleCatalog` / `PermissionCatalog`、`SessionChangedEventArgs`；新增权限点 `Permission.ProcessConfig`（工艺参数），使「工艺」与「设备」两个岗位可分权；
- **接口**：`IAuthenticationProvider`（认证方式扩展点）、`IAuthenticationService`（登录门面）、`ISessionProvider`（会话持有）、`IPermissionPolicy`（角色→权限，未来可换远端实现）、`IAuditService` 增加登录/登出/权限拒绝事件类型；
- **实现**：`RolePermissionPolicy`（五角色单调递增）、`SessionProvider`、`PermissionService`（无会话=拒绝）、`AuthenticationService`（唯一登录路径 + 审计）；`LogAuditService` 补操作者；**删除** `DevelopmentPermissionService`（全部放行）；
- **桌面**：新增 `SignInForm`（登录门禁，取消返回 -3）；`MainForm` 状态栏显示用户/角色、导航按权限显隐、可切换用户；退出时写 `SIGN_OUT`；
- **测试 12 条新增**（轴框架 +6、桌面 +6），总计 **127 条全绿**；
- **文档**：新增 `docs/module-design/11-authorization.md`（角色矩阵/接口/扩展路线/安全边界）。

> **安全边界**：本地角色选择没有凭据，只防误操作、留操作者痕迹，**不是安全边界**。现场交付前必须接入 Windows 身份或远端权限服务——接口已定，替换 provider 即可，不改业务调用点。

### 1.5 本轮新增（2026-10-31 第六轮：本地账号与用户管理 UI）

- **用户目录**：`LocalUserDirectory`（JSON + 原子写 + 首次启动生成 5 个演示账号）；写操作要求 `UserManage`，并强制“不能停用/删除当前登录账号”“不能停用/删除/降级最后一个启用中的管理员”；
- **可选口令**：`PasswordHasher`（PBKDF2-HMAC-SHA256，10,000 次，恒定时间比较）；未设置口令的账号按工位信任放行，设置后登录必须校验；
- **认证**：`LocalAccountProvider` 取代 `LocalRoleSelectionProvider`；`SignInRequest` 删除 `RequestedRole`，角色一律来自用户目录；
- **契约**：`IUserDirectory`、`UserAccount`、`CreateUserRequest` / `UpdateUserRequest`、`UserRules` / `PasswordRules` / `SecurityErrorCodes`；
- **用户管理 UI**：`UserManagementPage`（账号列表 / 新建 / 编辑角色 / 启停 / 设置或清除口令 / 删除）+ `UserEditDialog` + `PasswordDialog`；登录窗体改为“账号下拉 + 可选口令”；
- **测试 25 条新增**（轴框架 +15、桌面 +10），总计 **152 条全绿**；
- **文档**：更新 `11-authorization.md`（账号/用户目录/用户管理/验收）与 `10-desktop.md`。

> **安全边界（更新）**：演示账号默认没有口令，仍是工位信任；设置口令后才有真实校验。现场交付前必须为相关账号设置口令，或接入 Windows / 远端认证。

---

## 2. 仓库地图：两套体系并存

当前仓库里同时存在两套代码，**不要把它们当成同一套**：

```
旧体系（docs/ 描述的对象，迁移中断，当前不可编译）
  EquipmentMonitor.App / Core / Shared / Logging / Security
  Communication / Devices / Data / Mes
  ├─ EquipmentMonitor.Shared 已被清空（只剩空项目）
  ├─ 旧 Core 缺 Result / IMachine / ElementState / PubSubEvent
  └─ 仍被 EquipmentMonitor.slnx 引用 → 整个解决方案红

新体系（本次接手对象，可独立编译）
  Common  ──►  Core  ──►  Infrastructure
  （配置/日志/Result/转换器） （接口/模型/机台） （硬件/协议/数据库/DI/实时引擎）
```

判断依据：

- `src/EquipmentMonitor.Shared/` 下已没有 `.cs` 文件，但旧 Core 大量使用 `Result<T>`、`IMachine`、`ElementState`；
- 旧 Core 的 `UserChangeEvent` 仍 `using Prism.Events`，而 `Directory.Packages.props` 没有 Prism；
- 新 `Common` 提供了 `EquipmentMonitor.Common.Result`，但旧代码按 `EquipmentMonitor.Shared` 书写；
- 新 `Core` 的 `IElement` / `IMachine` 与旧 `EquipmentMonitor.Core` 的同名接口不是同一类型。

**结论**：在明确“旧体系是归档还是继续迁移”之前，不要试图在旧解决方案上做局部修补；先把新框架作为唯一交付物推进。对应的可验证入口是新增的 `AxisFramework.slnx`。

---

## 3. 新框架分层与依赖

```
┌──────────────────────────────────────────────┐
│ Infrastructure                                │
│  Hardware（轴/IO/控制器工厂）                  │
│  Protocol（Modbus/TCP/串口）                  │
│  Database（SqlSugar 实体/仓储/初始化）         │
│  Realtime（轮询引擎）                          │
│  DI（RegisterInfrastructure）                 │
├──────────────────────────────────────────────┤
│ Core                                          │
│  Abstractions（IAxisController / IElement…）   │
│  Models（AxisStatus / MachineMetadata…）       │
│  Enums（MachineState / ElementState…）        │
│  Machine                                      │
├──────────────────────────────────────────────┤
│ Common                                        │
│  Result / RegisterValueConverter              │
│  Config（INI/JSON/XML）/ Logger / DI 特性      │
└──────────────────────────────────────────────┘
```

实际引用（以 `.csproj` 为准）：

| 项目 | 引用 | 包 |
|---|---|---|
| `Common` | 无 | CommunityToolkit.Mvvm、Microsoft.Extensions.Configuration.Ini、DI.Abstractions、Newtonsoft.Json、Serilog、System.Configuration.ConfigurationManager |
| `Core` | `Common` | 无直接包（会传递拿到 Common 的包） |
| `Infrastructure` | `Common`、`Core` | NModbus、NModbus.Serial、Serilog、SqlSugarCore、System.IO.Ports |

### 3.1 对旧硬约束的影响

旧 `CLAUDE.md` 的“`Core` 零 `PackageReference`”在新体系下**名存实亡**：新 `Core` 通过 `Common` 传递依赖 Serilog、Newtonsoft 等。是否接受这一点，需要作为架构决策记录（见 §9-D2），不要继续按旧规则检查而不改规则。

### 3.2 命名空间现状（必须先统一）

同一个项目里混用了多个根命名空间：

| 项目 | 文件 | 实际命名空间 |
|---|---|---|
| `Common` | `Result.cs` / `Tools/RegisterValueConverter.cs` | `EquipmentMonitor.Common` |
| `Common` | `Config/*`、`DI/*`、`logger/*` | `Common` |
| `Common` | `Constants/ModuleNames.cs` | `EquipmentMonitor.Shared.Constants` |
| `Core` | `IElement.cs`、`IMachine.cs`、`Machine.cs` | `EquipmentMonitor.Core.*` |
| `Core` | `IAxisController.cs`、`IControllerBase.cs`、`IRegisterController.cs` | `EquipmentMonitor.Infrastructure`（**在 Core 程序集里定义 Infrastructure 命名空间**） |
| `Core` | `IElementAxis.cs`、`IElementCylinder.cs` | `Core.Abstractions` |
| `Core` | `CylinderStatus.cs` | `Core.Models` |
| `Core` | `UniaxialState.cs`、`AxisStatus.cs` | `EquipmentMonitor.Core` |
| `Core` | `Abstractions/Realtime/*` | `EquipmentMonitor.Core.Abstractions` |
| `Infrastructure` | 通用 | `EquipmentMonitor.Infrastructure` |
| `Infrastructure` | `Axis/*` | `EquipmentMonitor.Infrastructure.Axis` |
| `Infrastructure` | `Hardware/PLC/Modbus/*` | `EquipmentMonitor.Infrastructure.Hardware.PLC.Modbus` |

这是当前**最高优先级**的债务：每多写一个实现，错误命名就多固化一处。

### 3.3 目录职责（为什么 Axis 与 Hardware/PLC/Modbus 分开）

| 目录 | 职责 | 不应该包含 |
|---|---|---|
| `Infrastructure/Axis/` | **协议无关**的轴框架：`AxisElement`、`AxisFrameworkLoader/Bootstrapper`、`AxisConfigExtensions`（只到 `AxisElementConfig`）、`IAsyncAxisController` + 适配桥 + 适配工厂接口 | Modbus 寄存器布局、IP/端口、NModbus 类型 |
| `Infrastructure/Hardware/PLC/Modbus/` | **Modbus 专属**：`ModbusRegisterController`、`ModbusAxisController`、`ModbusAxisRegisterMap`、`ModbusAxisConfigExtensions`、连接/寄存器工厂、Modbus 轴工厂 | `IElementAxis`、机台聚合、UI |
| `Infrastructure/Protocol/` | 原始传输：Modbus TCP/RTU、TCP/IP、串口 | 轴/IO 语义 |
| `Infrastructure/Realtime/` | 实时引擎实现；接口在 `Core/Abstractions/Realtime` | 轴/协议语义 |

依赖方向：

```text
Protocol（原始通讯）
   ↑
Hardware/PLC/Modbus（Modbus 能力 + 轴寄存器适配）
   ↑ 提供 IRegisterController
Axis（轴元件/装配/异步桥）
```

`ModbusAxisControllerFactory` 位于 Modbus 目录、实现 `Axis` 里的 `IAxisControllerAdapterFactory`；这样 `AxisFrameworkLoader` 只认适配工厂接口，不需要知道 Modbus 的存在。

---

## 4. 轴控契约（已定型部分）

### 4.1 硬件抽象

`src/EquipmentMonitor.Core/Abstractions/IAxisController.cs`（命名空间为 `EquipmentMonitor.Infrastructure`）：

```csharp
public interface IAxisController : IControllerBase
{
    int AxisCount { get; }
    void GetAxisCount(int ControllerNo, ref uint axisCount);
    bool ServeOn(int ControllerNo, int axisNo);
    bool ServeOff(int ControllerNo, int axisNo);
    bool Home(int ControllerNo, int axisNo);
    bool MoveAbs(int ControllerNo, int axisNo, double position);
    bool MoveRel(int ControllerNo, int axisNo, double distance);
    bool MoveInPos(int ControllerNo, int axisNo);
    bool Jog(int ControllerNo, int axisNo, bool direction);
    bool Stop(int ControllerNo, int axisNo);
    bool ResetAxis(int ControllerNo, int axisNo);
    bool ResetAll();
    double GetPos(int ControllerNo, int axisNo);
    double GetVel(int ControllerNo, int axisNo);
    double GetTorque(int ControllerNo, int axisNo);
    bool ReadStateWord(int ControllerNo, int axisNo, string type, out int state);
    AxisOperationError? GetLastError(int ControllerNo, int axisNo) => null;
}
```

特点与后果：

- **同步 + `bool`**：厂商板卡库（雷赛 LTDMC、固高）确实是同步 API，这样封装最直接；
- 失败时通过 `GetLastError` 取详细信息（实现类记录；默认返回 `null`），不再只有一句“命令被拒绝”；
- `IAxisController` 本身不加 `CancellationToken`（厂商 DLL 同步调用）；`IElementAxis` 的异步方法已统一加 `CancellationToken`；
- `GetAxisCount` 用 `ref uint` 出参，`ReadStateWord` 用 `string type` 做弱类型分派；
- `IControllerBase` 的 `Initialize()` 也是同步 `bool`，且实现可能在构造函数里就初始化硬件。

`IRegisterController`（`src/EquipmentMonitor.Core/Abstractions/IRegisterController.cs`）则是另一种风格：

```csharp
Task<Result<ushort[]>> ReadRegistersAsync(int controllerNo, ushort startAddress, ushort count, CancellationToken ct = default);
Task<Result> WriteRegistersAsync(int controllerNo, ushort startAddress, ushort[] values, CancellationToken ct = default);
Task<Result<T>> ReadValueAsync<T>(...) where T : struct;
Task<Result> WriteValueAsync<T>(...) where T : struct;
```

**同一框架里存在“同步 bool”和“异步 Result”两套失败哲学。** 当前分工：`IAxisController` 保持同步、作为厂商适配层，失败用 `GetLastError` 补充；`IElementAxis` 的命令方法统一用异步 `Result`，但语义是“命令已下发”，到位由业务轮询 `RefreshAsync` 判断。

### 4.2 元件抽象

```csharp
public interface IElement
{
    ElementMetadata ElementMetadata { get; }
    ElementState State { get; }
    bool IsInitialized { get; }
    Task<Result> InitializeAsync(CancellationToken ct = default);
    Task<Result> RefreshAsync(CancellationToken ct = default);
    Task<Result> ResetAsync(CancellationToken ct = default);
}

public interface IElementAxis : IElement
{
    int ControllerNo { get; init; }
    int AxisNo { get; init; }
    AxisStatus Status { get; }
    AxisOperationError? LastError { get; }
    Task<Result> ServeOnAsync(CancellationToken ct = default);
    Task<Result> ServeOffAsync(CancellationToken ct = default);
    Task<Result> HomeAsync(CancellationToken ct = default);
    Task<Result> MoveAbsAsync(double position, CancellationToken ct = default);
    Task<Result> MoveRelAsync(double offset, CancellationToken ct = default);
    Task<Result> ForwardJogAsync(CancellationToken ct = default);
    Task<Result> AfterJogAsync(CancellationToken ct = default);
    Task<Result> StopAsync(CancellationToken ct = default);
    Task<Result> EStopAsync(CancellationToken ct = default);
    new Task<Result> ResetAsync(CancellationToken ct = default);
    Task<Result<int>> ReadStateWordAsync(string type, CancellationToken ct = default);
}
```

问题：

- `IElementAxis` 已由 `AxisElement` 实现；`ControllerNo`/`AxisNo` 用 `init` 放在接口上，实现类仍需重复声明，后续可收进配置对象；
- `new Task<Result> ResetAsync(...)` 只是隐藏基接口同名方法，没有增加语义；
- `AfterJogAsync` 命名不明确（应为 `JogNegativeAsync` 或 `JogBackwardAsync`）；
- `IElementCylinder.StatusStatus` 已改为 `Status`；但它仍在 `Core.Abstractions` 命名空间，尚未统一；
- `IElementAxis` 已加 `CancellationToken`；`IAxisController` 是同步厂商契约，不加 `CancellationToken`。

### 4.3 状态模型

| 类型 | 位置 | 用途 | 问题 |
|---|---|---|---|
| `AxisStatus` | `src/EquipmentMonitor.Core/Models/AxisStatus.cs` | 轴的状态快照 | ✅ 已改为可构造 record；`UniaxialState` 由映射器推导；`ToString` 已补分隔符 |
| `AxisFeedback` | `Infrastructure/Hardware/Entities/AxisFeedback.cs` | 控制器层的一次性反馈快照 | 与 `AxisStatus` 职责已分开，映射见 `AxisFeedbackExtensions` |
| `AxisStatusWord` | 同目录 | 16 位状态位定义 | 与 `docs/axis-command-interface.md §2.1` 对齐；但 `IAxisController.ReadStateWord` 返回 `int`，没有强制使用该枚举 |
| `UniaxialState` | `Core/Enums/UniaxialState.cs` | 单轴运动状态 | 命名空间是 `EquipmentMonitor.Core`，不在 `Enums` 下；没有状态迁移规则 |
| `CylinderStatus` | `Core/Models/CylinderStatus.cs` | 气缸状态 | 命名空间 `Core.Models`；同样没有构造函数 |

`AxisStatus` 与 `AxisFeedback` 的关系需要一次明确决策：建议 `AxisFeedback` 作为厂商原始层，`AxisStatus` 作为领域快照，增加 `AxisStatus.From(AxisFeedback)` 映射并把 `UniaxialState` 推导集中到一处。

### 4.4 配置实体

`src/EquipmentMonitor.Infrastructure/Database/Entities/`：

| 实体 | 关键字段 | 说明 |
|---|---|---|
| `ControllerConfig` | `Brand`、`ControllerNo`、`ConnectionType`、IP/串口、`SlaveAddress`、`TimeoutMs`、`SortOrder` | 一行一个启动控制器；品牌键与 DI 的 key 对应 |
| `AxisConfig` | `ControllerConfigId`、`AxisNo`、`Name`、`Unit`、5 个 Scale、软限位、`MaxVelocity`、默认速度曲线、`HomeMethod` | 轴的身份与默认参数 |
| `DeviceConfig` | `ControllerConfigId`、`DeviceNo`、`DeviceType` | PLC 伺服/气缸等逻辑设备 |
| `IoPointConfig` | `PointNo`、`Kind`、`IsInverted`、`InitialState` | IO 点 |
| `RegisterPointConfig` | `Address`、`DataType`、`Access`、`WordOrder`、`Scale` | Modbus 保持寄存器点 |

问题：

- `AxisConfig` 用 `VelocityScale`，而 `docs/axis-command-interface.md §5-Q4` 明确否决了单独的 `VelocityScale`、要求读写共用 `SpeedScale`——**文档与实现冲突**；
- `RegisterPointConfig` 的字序已统一为 `Common.RegisterWordOrder`；`RegisterValueType` 仍与旧 `RegisterDataType` 语义重复（后者已删除）；
- 所有实体用 `DateTime`（本地时间），而框架其他位置用 `DateTimeOffset`/UTC，时间语义不统一；
- `AxisElementConfig.Validate` 只校验元件参数；**软限位不在上层校验**，由轴卡/PLC 兜底；
- 配置加载器已实现 JSON 读取（`AxisFrameworkOptions`），但没有独立的字段级校验器（Scale 非 0、地址冲突等）。

### 4.5 机台

`src/EquipmentMonitor.Core/Machine.cs` 在本次交接前是纯 `NotImplementedException` 骨架且无法编译。本次已补到**可编译基线**：

- 构造函数注入 `MachineMetadata`、`IElement` 集合、可选 `TimeProvider`；
- `GetElement<T>` 按 `ElementId` 查找并做类型检查；
- `InitializeDeviceAsync` / `RefreshAsync` 遍历启用元件、汇总失败、聚合状态；
- 状态变化时触发 `StateChanged`（锁外触发）；
- `Start/Stop/Pause/Maintenance/Reset` **显式抛 `NotSupportedException`**，避免“只改状态位却假装设备已动作”。

聚合规则是临时默认值（Error > Warning > Running > 全 Ready=Idle），设计文档要求配置驱动，后续要替换。

### 4.6 启动配置与装配

轴控框架在初始化时从 JSON 配置文件读取控制器连接参数、每轴寄存器/运动参数，然后装配成运行期对象：

```text
AxisFrameworkBootstrapper.InitializeAsync(path)
  → AxisFrameworkOptionsLoader.Load(path)          // JSON → AxisFrameworkOptions
  → AxisFrameworkLoader.BuildAsync(...)            // 控制器初始化 + 轴元件创建
      ├─ 同步控制器：IControllerFactory（keyed DI，如 Sim/LeadShine/GoogolTech）
      └─ Modbus：ModbusAxisControllerFactory
             → ModbusProtocol（IP/Port/串口/从站号）
             → ModbusRegisterController
             → ModbusAxisController（internal）+ AsyncAxisControllerAdapter（internal）
             → IAxisController
  → AxisFrameworkBuildResult { Controllers, Elements }
```

- 配置模型：[AxisFrameworkOptions.cs](src/EquipmentMonitor.Infrastructure/Configuration/AxisFrameworkOptions.cs) 复用 `ControllerConfig`/`AxisConfig`，避免同一组字段维护两份；
- 读取器：`AxisFrameworkOptionsLoader.Load` 返回 `Result`，文件缺失/JSON 错误都按启动失败处理；
- 默认文件名：`axis-framework.json`，放在程序运行目录（`AppContext.BaseDirectory`）；相对路径按该目录解析，避免快捷方式/服务启动时工作目录不同；
- 装配器：`AxisFrameworkLoader` 汇总所有控制器/轴错误，不返回半套可用对象；
- 示例：项目内的默认配置 [axis-framework.json](src/EquipmentMonitor.Infrastructure/Configuration/axis-framework.json)（Sim，随构建复制到输出目录），带 Modbus TCP 的参考 [axis-framework.modbus.sample.json](src/EquipmentMonitor.Infrastructure/Configuration/axis-framework.modbus.sample.json)。

> 注意：`ControllerConfig.Id` 与 `AxisConfig.ControllerConfigId` 必须在配置文件中手工对应；
> `AxisConfig` 的 `StateStartAddress`/`CommandStartAddress`/`ExecuteCoilAddress` 只对 Modbus 轴有意义。
>
> **时序边界**：`RefreshAsync` 典型周期是 0.1s 级，只用于监控/业务状态，不参与实时安全判断。
> 软限位、急停、到位窗口等实时职责由轴卡/PLC/FPGA 兜底；上位机不能用 0.1s 轮询去替代它们。

---

## 5. 实现状态矩阵

| 能力 | 状态 | 位置 | 说明 |
|---|---|---|---|
| 雷赛轴：初始化/回零 | 部分可用 | `Hardware/Card/LeadShine/LeadShineAxisController.cs` | 构造即 `dmc_board_init`，失败抛异常；无卡环境会直接崩 |
| 雷赛轴：使能/绝对/相对/点动/停止/复位 | **空实现** | 同上 | 返回 `false`，没有调用 LTDMC |
| 雷赛轴：位置/速度/扭矩 | **空实现** | 同上 | 返回 `0` |
| 雷赛轴：状态字 | **空实现** | 同上 | 本次补了接口方法，返回 `false` |
| 雷赛 IO：读输入/读输出/写输出 | 可用 | `LeadShineIoController.cs` | 直接调用 LTDMC；`ReadIn` 的注释 `//npn` 说明极性未配置化 |
| 固高轴/IO | **全部骨架** | `GoogolTech*.cs` | 全部 `NotImplementedException` |
| Modbus 轴/IO | **轴可用（异步） / IO 骨架** | `Hardware/PLC/Modbus/*.cs` | `ModbusRegisterController` + `ModbusAxisController`（异步）；轴未注册进 keyed DI（需 `IRegisterController` + 每轴映射），由 `ModbusAxisControllerFactory` 按配置构造 |
| Modbus TCP/RTU 协议 | 可用但有缺陷 | `Protocol/Protocols/ModbusProtocol.cs` | 单例 + 可变连接参数；异常被吞掉后返回 default |
| TCP/IP 协议 | 可用但有缺陷 | `Protocol/Protocols/TcpIpProtocol.cs` | 客户端/服务端双模式；发送锁未放在 `finally` |
| 串口协议 | 可用但有缺陷 | `Protocol/Protocols/SerialPortProtocol.cs` | 未连接时只记日志仍继续发送 |
| 实时引擎 | 可用 | `Core/Abstractions/Realtime/*` + `Realtime/*` | 接口在 Core、实现在 Infrastructure；`PeriodicTimer` 防重入；任务异常隔离 |
| 数据库实体/仓储 | 可用骨架 | `Database/` | SqlSugar CodeFirst；实体扫描用 `[SugarTable]`；无迁移脚本 |
| 数据库初始化 | **已删除** | — | 不再用反射扫描/CodeFirst 自动建表；表结构交给后续 SQL 脚本/显式迁移 |
| 配置（INI/JSON/XML） | JSON 可用；INI/XML 未接 | `Common/Config/*`、`Infrastructure/Configuration/AxisFrameworkOptions.cs` | 轴控启动用 JSON；`Common.Config` 的 DI 仍未闭环 |
| 日志 | 可用但未接线 | `Common/logger/*` | `LoggerFactory` 需要 `Func<string, ILoggerManager>`，未注册 |
| 控制器工厂 | **已收敛** | `DefaultControllerFactory`、`IAxisControllerAdapterFactory` | 删除了三个品牌工厂；同步走 keyed DI，异步适配工厂返回 `IAxisController` |
| 模拟轴 | **可用** | `Hardware/Simulation/SimulatedAxisController.cs` | 无硬件跑通使能/回零/定位/点动/停止/复位 |
| 轴元件 `AxisElement` | **可用（模拟轴/Modbus）** | `Infrastructure/Axis/AxisElement.cs` | 命令只下发、不等到位；参数合法性；`LastError`；`CancellationToken`；软限位由硬件兜底 |
| 异步轴契约 | **internal** | `Infrastructure/Axis/IAsyncAxisController.cs` | 只在 Infrastructure 内部使用，不是公共入口 |
| 同步适配桥 | **internal（唯一阻塞点）** | `Infrastructure/Axis/AsyncAxisControllerAdapter.cs` | 把异步控制器接到 `IAxisController`；其他代码不出现 `.GetAwaiter().GetResult()` |
| Modbus 协议池 | **可用** | `Hardware/PLC/Modbus/ModbusProtocolPool.cs` | 同一 PLC（端点 + 从站号）复用一个 `ModbusProtocol` |
| Modbus 能力 | **可用** | `Hardware/PLC/Modbus/ModbusRegisterControllerFactory.cs` | 独立功能：从协议池取连接，创建 `IRegisterController`，不关心轴语义 |
| Modbus 轴适配 | **可用** | `Hardware/PLC/Modbus/ModbusAxisControllerFactory.cs` | 只负责 `AxisConfig` → 每轴映射 + 轴控制器 + 适配；消费 Modbus 能力 |
| 轴配置映射 | **可用** | `Infrastructure/Axis/AxisConfigExtensions.cs` + `Hardware/PLC/Modbus/ModbusAxisConfigExtensions.cs` | `AxisConfig` → `AxisElementConfig`；Modbus 映射单独在 Modbus 目录 |
| 轴装配器 | **可用** | `Infrastructure/Axis/AxisFrameworkLoader.cs` | 按控制器/轴配置创建并初始化；支持同步/异步控制器 |
| 启动入口 | **可用** | `Infrastructure/Axis/AxisFrameworkBootstrapper.cs` | 读 JSON → 装配；文件缺失/JSON 错误返回 `Result` |
| 配置加载器 | **部分** | `Infrastructure/Configuration/AxisFrameworkOptions.cs` | JSON 已实现；SqlSugar/INI/XML 未接 |
| 桌面宿主 | **可用（R1）** | `EquipmentMonitor.Desktop` | 主壳 + 只读总览 + 200ms 后台采集；操作台（R2）未开始 |
| 测试 | **64 条全绿** | `tests/EquipmentMonitor.AxisFramework.Tests`、`tests/EquipmentMonitor.Desktop.Tests` | 轴命令/故障/寄存器/Modbus 轴/机台 + 宿主装配/快照/页面渲染 |

---

## 6. 构建与验证

### 6.1 新框架（推荐日常入口）

```powershell
dotnet build AxisFramework.slnx -t:Rebuild
```

当前结果：

- **0 错误**
- **33 条可空性警告**（`CS8600/CS8602/CS8603/CS8604/CS8618/CS8619/CS8625`）
- 警告集中在 `Common/Config` 与 `Infrastructure/Protocol`，原因是两个 `.csproj` 把空引用系列加入了 `WarningsNotAsErrors`，绕过了“警告即失败”

> 旧 `CLAUDE.md` 的 0 警告门禁在新框架尚未成立。要么修完警告，要么把“允许空引用警告”的理由写进项目文档；不要假装它是 0 警告。

### 6.2 全解决方案（当前红）

```powershell
dotnet build EquipmentMonitor.slnx
```

当前结果：**83 个错误**，全部来自旧 `EquipmentMonitor.Core`（缺 `Result`、`IMachine`、`IElement`、`ElementState`、`PubSubEvent` 等）。这是旧体系迁移中断导致的，不是本次新框架改动引入的。

### 6.3 测试

```powershell
dotnet test AxisFramework.slnx
dotnet test EquipmentMonitor.slnx   # 旧基线，当前无法运行
```

当前结果：新框架 **64 条测试全绿**（轴命令、故障复位、寄存器转换、Modbus 读写、机台聚合、宿主装配、快照发布、页面渲染）。
旧基线 991 条仍无法运行；在恢复旧体系之前，不要把 991 当作新框架的门禁。

---

## 7. 关键缺陷与风险

### 7.1 P0：继续写实现前必须处理

1. **命名空间分裂**（§3.2）。同一个 `Core` 程序集同时定义 `EquipmentMonitor.Core`、`Core.Abstractions`、`EquipmentMonitor.Infrastructure`。继续写实现会把错误命名固化。
2. **重复契约**（本轮已清掉三项，剩余两项）：
   - ✅ `Common.RegisterWordOrder` 与 `Infrastructure.RegisterWordOrder` 已合并（`RegisterPointConfig` 改用 Common 版本）；
   - ✅ `RegisterDataType` 已删除，保留 `RegisterValueType`；
   - ✅ `IoPointType` 已删除，统一为 `IoPointKind`；
   - ❗ `AxisStatus` vs `AxisFeedback`：语义重叠，尚无映射器；
   - ❗ 新旧 `IElement` / `IMachine` 同名不同义（跨项目引用时必冲突）。
3. **DI 组合根不完整**：
   - `CommonModuleRegister.AddCommonModule` 注册 `IConfigManager → ConfigManager`，但 `ConfigManager` 需要 `IConfigSerializer` 和 `string fullPath`，容器无法解析；
   - `LoggerFactory` 需要 `Func<string, ILoggerManager>`，未注册；`CommonModuleRegister` 里的注册被注释掉；
   - `DbContext` 需要连接字符串，当前未注册；`DatabaseInitializer` 已删除，不再自动建表；
   - `LeadShineAxisController` 构造函数直接初始化板卡，失败抛异常——无卡开发机解析该服务即崩；
   - 已修：`RealtimeEngine` 不能直接 `AddSingleton`（构造函数需要 `name`/`interval`），改由 `RealtimeEngineManager` 内部创建；
   - 已修：`LeadShineIoController` 的 keyed 注册改为工厂，显式取同 key 的 `IAxisController`。
4. ✅ **配置加载器已补**：`AxisFrameworkOptionsLoader` 读 JSON，`AxisFrameworkBootstrapper` 装配；`SimulatedAxisController` + `AxisElement` 让无硬件开发可行。
5. **同步 `bool` 丢失失败原因**：`IAxisController` 新增 `GetLastError`，`AxisElement` 会把失败转成带错误码的 `Result`；默认返回 `null`，厂商实现可逐步补齐。Modbus 的异步桥仍保留错误码。

### 7.2 P1：会在联调时爆

6. ✅ **工厂已收敛**：删除了 `LeadShineFactory` / `GoogolTechFactory` / `ModbusEquipmentFactory`；
   同步控制器统一走 `DefaultControllerFactory` + keyed DI，Modbus 走 `IAxisControllerAdapterFactory`（返回适配后的 `IAxisController`）。
7. **协议层吞错**：`ModbusProtocol.ReadAsync` 捕获异常后返回 `default`；`WriteAsync` 捕获后仅记日志、调用方以为成功。轴控写命令若走这条路，会出现“界面显示已下发、设备没动”。
8. **协议对象仍带可变连接参数**：`ModbusProtocolPool` 现在按 PLC 端点复用协议，同一 PLC 不再每条轴/控制器配置各建连接；但 `ModbusProtocol` 的 `IP/Port/SlaveAddress` 仍是 `set` 属性，建议后续改为构造参数/不可变选项。
9. **发送锁未放在 `finally`**：`TcpIpProtocol.SendAsync` 在 `GetStream()` 抛异常时不会 `Release`，后续发送永久阻塞。
10. ✅ **`AxisStatus` 已可构造**：改为 `sealed record` + `init` 属性，测试与映射器都能创建快照。
11. **配置语义冲突**：`AxisConfig.VelocityScale` vs 文档的 `SpeedScale`；`RegisterPointConfig` 的字序枚举已统一，但缩放字段仍需定稿。
12. **时间语义**：`Result.Timestamp` 用 `DateTimeOffset.Now`（本地），实体用 `DateTime`，机台事件用 UTC。统计/排序会混。
13. **Modbus 同步桥的线程约束**：`AsyncAxisControllerAdapter` 会在同步调用里阻塞等待异步通讯。
    它只能从后台线程/线程池调用（`AxisElement` 满足），**禁止从 WinForms UI 线程直接调用**；
    后续如果 UI 要直接读状态，应走异步接口而不是这个桥。

### 7.3 P2：债务与卫生

13. ✅ `RegisterController.cs` 里的 `HoldingRegisterController` 已删除（与 `IRegisterController` 重复）。
14. ✅ `DatabaseInitializer` 及 `Iniltializer` 目录已删除：不再反射扫描/CodeFirst 自动建表，默认管理员 `123` 也一并移除。
15. 厂商 P/Invoke 文件 `LTDMC.cs` 约 31 万字节，随源码提交；建议确认是否应改为 NuGet/原生依赖。
16. `axisNo` 小写、`AfterJogAsync` 命名仍不清晰（本轮已修 `IElementCylinder.StatusStatus`）。
17. ✅ `AxisStatus.ToString()` 已补分隔符与右括号。
18. `ModuleNames.All` 不含 `Infrastructure`/`Common`，新框架日志无法按模块分流。
19. `WarningsNotAsErrors` 把空引用系列全部降级，等于放弃可空性检查；33 条警告会继续增长。

---

## 8. 建议路线（按纵向切片，而不是按文件铺开）

### 阶段 0：契约冻结（部分完成）

目标：让后续实现只写一次。

已完成：`AxisStatus` 改为可构造 record；删除重复的 `RegisterWordOrder`/`RegisterDataType`/`IoPointType`；`IElementAxis.ReadStateWordAsync` 改为 `Result<int>`；`IElementCylinder.StatusStatus` 改为 `Status`。

仍需做：

1. 统一命名空间：`EquipmentMonitor.Common` / `EquipmentMonitor.Core` / `EquipmentMonitor.Infrastructure`；
   - `IAxisController` 等硬件接口移到 `EquipmentMonitor.Core.Abstractions`（或明确保留在 Infrastructure 并改项目归属）；
   - 删除 `Core.Abstractions`、`Core.Models` 两个临时根。
2. ✅ `AxisStatus`/`AxisFeedback` 已定主从：`AxisFeedback` 原始层、`AxisStatus` 领域层，映射在 `AxisFeedbackExtensions`。
3. 给 `IAxisController` 的失败补错误码（例如 `AxisOperationResult` 或 `Result` 适配器），不改厂商同步调用方式。
4. 决定 `AxisConfig.VelocityScale` 与文档 `SpeedScale` 谁为准，改代码或改文档。
5. 把 `WarningsNotAsErrors` 的豁免范围缩小，至少新代码 0 警告。

**验收**：`dotnet build AxisFramework.slnx -t:Rebuild` 0 错误；命名空间清单只剩三个根；重复类型清单归零。

### 阶段 1：可模拟的轴控闭环（基本完成）

目标：无硬件也能开发 UI 与业务。

已完成：

1. ✅ `SimulatedAxisController : IAxisController`：内存位置、速度、状态字，支持 MoveAbs/MoveRel/Home/Jog/Stop/Reset；
2. ✅ `AxisElement : IElementAxis`：组合 `IAxisController` + `AxisElementConfig`，实现命令下发、参数合法性、`LastError`、`CancellationToken` 与状态刷新；
3. ✅ `UniaxialState` 推导集中在 `AxisFeedbackExtensions`；
4. ✅ 单元测试：状态位映射、命令下发、运动过程中状态、故障复位、失败传播；
5. ⏳ 最小宿主/DI 装配尚未做；目前测试直接构造 `AxisElement`。

**验收**：55 条测试全绿；模拟轴能在无硬件环境完成“使能 → 回零 → 绝对定位 → 到位”。

### 阶段 2：Modbus 轴（基本完成）

1. ✅ 定义轴寄存器布局（状态区 9 + 命令区 10 + Execute 线圈），见 `ModbusAxisRegisterLayout`；
2. ✅ `ModbusAxisController` 基于 `IRegisterController` 实现内部 `IAsyncAxisController`，经内部 `AsyncAxisControllerAdapter` 转成 `IAxisController` 后交给元件；
3. ✅ `ModbusRegisterController` 实现 `IRegisterController`（异步 + `Result`，写后回读，含线圈）；
4. ✅ 地址/缩放从 `AxisConfig` 读取（`ToModbusMap`）；
5. ⏳ 用 Modbus 从站模拟器（或 NModbus slave）做集成测试；目前用假寄存器层验证命令/回读。

**验收**：55 条测试全绿；命令写到正确寄存器、反馈解析正确、适配桥只在边界阻塞。

### 阶段 3：厂商板卡

- 雷赛：补齐使能/绝对/相对/点动/停止/复位/位置/速度/扭矩/状态字；
- 固高：按同一 `IAxisController` 契约实现；
- 每个实现都先写“无卡时可注入的 fake”，再写真实 P/Invoke。

### 阶段 4：配置与宿主

- `ControllerConfig`/`AxisConfig` 加载器 + 校验器；
- `InfrastructureModuleRegister` 按配置创建控制器与元件；
- 机台 `Machine` 接入元件命令管线与审计；
- 日志/配置/数据库 DI 闭环。

> **2026-10-29 进展**：宿主部分已由 `EquipmentMonitor.Desktop`（R1）接上 ——
> 配置读取 → 装配 → 后台采集 → 主窗体。仍缺：配置校验器、`Machine` 装配、日志/数据库 DI 闭环。

---

## 9. 待确认问题（需要原负责人/用户拍板）

| # | 问题 | 建议 | 影响 |
|---|---|---|---|
| D1 | 旧 `EquipmentMonitor.*` 九模块是归档还是继续迁移？ | **归档**，新框架独立演进；旧代码只作参考 | 决定是否继续修 `EquipmentMonitor.slnx`，以及 991 条旧测试的去留 |
| D2 | 新 `Core` 是否允许通过 `Common` 传递第三方包？ | 允许，但把规则写进新架构文档；不再套用旧“Core 零包” | 决定 `Common` 的定位是“基础设施”还是“纯工具” |
| D3 | `IAxisController` 保持同步 `bool`，还是改成异步 `Result`？ | ✅ 已定：保持同步作为唯一硬件入口；Modbus 用 internal `IAsyncAxisController` + internal 适配桥，对外仍只暴露 `IAxisController` | 已落地，不再改接口 |
| D4 | 轴状态以 `AxisStatus` 还是 `AxisFeedback` 为准？ | ✅ 已定：`AxisFeedback` 原始层、`AxisStatus` 领域层 + 映射器 | 已落地 |
| D5 | 配置主数据放 SQLite 还是 INI/JSON？ | ✅ 轴控启动配置用 JSON（`AxisFrameworkOptions`）；点表/大数据后续再接 SQLite | 已落地 |
| D6 | 第一优先硬件是雷赛、固高还是 Modbus？ | ✅ Modbus 已完成异步链路；下一步雷赛 | 决定阶段 3 顺序 |

---

## 10. 本次交接已做的修改（透明记录）

> 目的：把“不可编译”修到“新框架可编译”，不改变厂商动作语义。

| 修改 | 文件 | 原因 |
|---|---|---|
| 修正解决方案里的项目路径 | `EquipmentMonitor.slnx` | 原路径指向不存在的 `EquipmentMonitor.Infrastructure.csproj`；当时实际文件是 `Infrastructure.csproj`，2026-10-29 已统一命名为 `EquipmentMonitor.Infrastructure.csproj`（文件夹与项目名一致） |
| 新增独立构建入口 | `AxisFramework.slnx` | 全解决方案被旧体系拖红，新框架需要可验证基线 |
| 拆分实时引擎 | `Core/Abstractions/Realtime/*`（接口）+ `Infrastructure/Realtime/*`（实现） | 原文件放在旧 Core 却写成 `EquipmentMonitor.Infrastructure` 命名空间；按“接口归 Core、实现归 Infrastructure”拆开 |
| 修复多余右括号 | `Core/Abstractions/Realtime/IRealtimeTask.cs` | 原文件语法错误（CS1022） |
| 补 `Machine` 可编译基线 | `Core/Machine.cs` | 原文件字段未赋值、事件未使用，7 个编译错误；现实现初始化/刷新/聚合/取元件，管理动作显式 `NotSupportedException` |
| 删除 `IEntity` 标记 | `Database/Entities/IEntity.cs` + 仓储 | 仓储约束改为 `class, new()`；实体扫描直接用 `[SugarTable]`，少一个无行为标记 |
| 删除自动建表器 | `Database/Iniltializer/*` | 不再反射扫描/CodeFirst 自动建表，避免启动期扫描与默认管理员 `123`；表结构交给 SQL 脚本/显式迁移 |
| 补 `ReadStateWord` | `LeadShineAxisController`、`GoogolTechAxisController` | 接口有该方法，两个实现缺失（CS0535） |
| 修正固高 IO 工厂 | `GoogolTechFactory.CreateIoController` | 原来返回 `ModbusIoController`，品牌错配 |
| 修正 DI 注册 | `InfrastructureModuleRegister` | `RealtimeEngine` 不能直接注册；`LeadShineIoController` 的 keyed 依赖改为显式工厂；补 `IControllerFactory` |
| 补雷赛 IO 计数赋值 | `LeadShineIoController.GetIOCount` | 字段从未赋值（CS0649，警告即错误） |
| 新增模拟轴 | `Hardware/Simulation/SimulatedAxisController.cs` | 无硬件开发/测试的控制器实现 |
| 新增轴元件 | `Infrastructure/Axis/AxisElement.cs` | `IAxisController` → `IElementAxis`；命令下发、参数合法性、LastError、CT |
| 新增轴配置映射 | `Infrastructure/Axis/AxisConfigExtensions.cs` | `AxisConfig` → `AxisElementConfig` |
| 新增轴装配器 | `Infrastructure/Axis/AxisFrameworkLoader.cs` | 按控制器/轴配置创建并初始化，汇总所有装配错误 |
| 注册模拟轴/装配器 | `InfrastructureModuleRegister.cs` | keyed `"Sim"` 模拟轴；`AxisFrameworkLoader` 可 DI 解析 |
| 新增 Modbus 寄存器层 | `Hardware/PLC/Modbus/ModbusRegisterController.cs` | `IRegisterController` 的 Modbus 实现，写后回读，含线圈 |
| 新增异步轴契约 | `Infrastructure/Axis/IAsyncAxisController.cs` | **internal**；Modbus 等异步控制器的内部契约，不进入公共 API |
| 新增同步适配桥 | `Infrastructure/Axis/AsyncAxisControllerAdapter.cs` | **internal**；异步 → 同步；**唯一允许 `.GetAwaiter().GetResult()` 的文件** |
| 新增 Modbus 轴 | `Hardware/PLC/Modbus/ModbusAxisController.cs` | **internal**；状态区/命令区/Execute 线圈的异步实现 |
| 新增 Modbus 能力工厂 | `Hardware/PLC/Modbus/ModbusRegisterControllerFactory.cs` | 独立功能：按 `ControllerConfig` 创建协议 + `IRegisterController` |
| 新增适配工厂 | `Infrastructure/Axis/IAxisControllerAdapterFactory.cs` + `Hardware/PLC/Modbus/ModbusAxisControllerFactory.cs` | 接口在 Axis、实现在 Modbus；返回 `IAxisController`，只做轴映射与适配 |
| 扩展轴实体 | `AxisConfig` | 新增 Modbus 状态/命令/Execute 地址与 `TorqueScale` |
| 新增启动配置 | `Infrastructure/Configuration/AxisFrameworkOptions.cs` | JSON 读取控制器/轴配置，返回 `Result`；默认文件 `Configuration/axis-framework.json` 随构建复制 |
| 新增启动入口 | `Infrastructure/Axis/AxisFrameworkBootstrapper.cs` | 配置读取 → `AxisFrameworkLoader` → 已初始化控制器/轴元件 |
| 清理重复契约 | `RegisterController.cs`、`RegisterDataType.cs`、`IoPointType.cs`、品牌工厂、`RegisterPointConfig.cs` | 删除未使用类型/工厂；统一 `RegisterWordOrder` / `IoPointKind` |
| 新增测试项目 | `tests/EquipmentMonitor.AxisFramework.Tests` | 55 条测试覆盖轴命令下发/寄存器/Modbus 轴/机台 |
| 修正异步签名 | `IElementAxis.ReadStateWordAsync`、`IElementCylinder.Status` | 异步方法不能带 `out`；笔误修正 |

未做的事（有意留给下一阶段）：

- 没有改雷赛/固高的核心动作实现（除补接口缺项外仍是骨架）；
- 没有统一命名空间（属于阶段 0 的契约决策，仍需做）；
- 没有修旧 `EquipmentMonitor.*` 的 **166** 个错误；
- ~~没有把 `AxisFrameworkBootstrapper` 接到 App/宿主启动流程~~ —— 已由 `EquipmentMonitor.Desktop` 接上（R1）；
- 没有用真实 Modbus 从站做集成测试（当前用假寄存器层验证命令/回读）；
- ~~没有做 UI 宿主装配~~ —— R1 已交付（主壳 + 只读总览）；**操作台（R2）仍未开始**。

---

## 11. 快速上手命令

```powershell
# 只构建新框架（推荐）
dotnet build AxisFramework.slnx -t:Rebuild

# 跑新框架测试
dotnet test AxisFramework.slnx

# 单独构建
dotnet build src/EquipmentMonitor.Common/Common.csproj
dotnet build src/EquipmentMonitor.Core/Core.csproj
dotnet build src/EquipmentMonitor.Infrastructure/EquipmentMonitor.Infrastructure.csproj

# 看依赖图
dotnet list src/EquipmentMonitor.Infrastructure/EquipmentMonitor.Infrastructure.csproj reference

# 全解决方案（当前预期红，旧体系）
dotnet build EquipmentMonitor.slnx
```

---

## 12. 关键文件索引

| 主题 | 入口 |
|---|---|
| 轴硬件契约 | `src/EquipmentMonitor.Core/Abstractions/IAxisController.cs` |
| 寄存器契约 | `src/EquipmentMonitor.Core/Abstractions/IRegisterController.cs` |
| 轴元件契约 | `src/EquipmentMonitor.Core/Abstractions/IElementAxis.cs` |
| 轴状态 | `src/EquipmentMonitor.Core/Models/AxisStatus.cs`、`Infrastructure/Hardware/Entities/AxisFeedback.cs`、`AxisStatusWord.cs` |
| 轴配置 | `Infrastructure/Database/Entities/AxisConfig.cs`、`ControllerConfig.cs` |
| 雷赛实现 | `Infrastructure/Hardware/Card/LeadShine/LeadShineAxisController.cs` |
| 模拟轴 | `Infrastructure/Hardware/Simulation/SimulatedAxisController.cs` |
| 轴元件 | `Infrastructure/Axis/AxisElement.cs` |
| 轴装配 | `Infrastructure/Axis/AxisFrameworkLoader.cs`、`AxisConfigExtensions.cs` |
| 启动配置 | `Infrastructure/Configuration/AxisFrameworkOptions.cs`、`Configuration/axis-framework.json` |
| 启动入口 | `Infrastructure/Axis/AxisFrameworkBootstrapper.cs` |
| 异步轴契约/桥（internal） | `Infrastructure/Axis/IAsyncAxisController.cs`、`AsyncAxisControllerAdapter.cs` |
| Modbus 能力 | `Infrastructure/Hardware/PLC/Modbus/ModbusRegisterControllerFactory.cs`、`ModbusRegisterController.cs` |
| Modbus 轴 | `Infrastructure/Hardware/PLC/Modbus/ModbusAxisController.cs`、`ModbusAxisControllerFactory.cs`、`ModbusAxisRegisterMap.cs`、`ModbusAxisConfigExtensions.cs` |
| Modbus 协议 | `Infrastructure/Protocol/Protocols/ModbusProtocol.cs` |
| DI 组合根 | `Infrastructure/DI/InfrastructureModuleRegister.cs`、`Common/DI/CommonModuleRegister.cs` |
| 实时引擎 | `Infrastructure/Realtime/RealtimeEngine.cs` |
| 机台 | `src/EquipmentMonitor.Core/Machine.cs` |
| 跨项目轴寄存器契约（文档） | `docs/axis-command-interface.md` |

---

## 13. 旧代码迁移 / 删除清单

原则：**先在新框架里跑通等价实现，再删旧文件**；不要一边保留旧实现、一边在新实现里复制逻辑，否则两套行为会分叉。

| 旧代码 | 可复用部分 | 处理建议 |
|---|---|---|
| `Devices/Elements/ServoElement.cs` | 9 寄存器状态映射、8 条命令的写入序列、到位/回零“先看到运动”的业务判据、超时策略 | 寄存器映射迁移到 `ModbusAxisController`；到位判据/超时属于业务轮询层，不要塞回 `AxisElement` |
| `Devices/Protocol/ModbusElementBase.cs` | 寄存器块读取、位解析、缩放 | 已被 `ModbusRegisterController` + `AxisElement` 取代；确认无遗漏后删除 |
| `Devices/ElementBase.cs`、`ElementCommandPipeline` | 命令权限、审计、命令队列、可用性判断 | 新 `IElementAxis` 目前是直接方法，没有命令管线；若需要“操作台命令描述/权限/审计”，再整体迁移，不要拆一半 |
| `Devices/StateMachines/*` | 元件/机台状态机与聚合规则 | 新 `Machine` 目前是临时聚合；需要 OEE/维护/暂停语义时迁移 |
| `Devices/Configuration/DeviceConfigurationLoader.cs`、`DevicesOptionsValidator.cs` | 配置校验与装配思路 | 迁移成新 `AxisConfig`/`ControllerConfig` 加载器与校验器 |
| `Communication/*` | 通道串行化、重连、健康度 | 新 `Infrastructure/Protocol` 已有 Modbus/TCP/串口，但缺通道管理与重连；按需迁移 |
| `Security/*`、`Data/*`、`Mes/*`、`App/*` | 认证、仓储、MES、UI | 不属于本轮轴控框架；等轴控闭环稳定后按模块迁移 |
| `EquipmentMonitor.Shared`（空项目） | 无 | 旧项目全部迁走后删除 |
| `tests/EquipmentMonitor.*.Tests` | 旧业务测试（991 条） | 旧模块删除时同步删除；新框架测试独立在 `EquipmentMonitor.AxisFramework.Tests` |

**不要删除**：旧 `ServoElement`、`DeviceConfigurationLoader`、`ServoCommandMap`、`ServoAlarmTexts` 在迁移完成前仍是寄存器语义的唯一参考。

