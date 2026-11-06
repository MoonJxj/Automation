# 11 — 用户权限（新框架：本地账号 + 用户管理）

> 状态：**本地账号与用户管理已交付**（2026-10-31）
> 范围：`src/EquipmentMonitor.Core`（契约/模型/枚举）、`src/EquipmentMonitor.Infrastructure/Security`（实现）、
> `src/EquipmentMonitor.Desktop`（登录、用户管理页、状态栏、导航显隐）
> 与旧体系 [04-security.md](04-security.md)（四角色 + 三级安全等级 + 三种认证策略）的关系：
> 本文描述**新框架**（`AxisFramework.slnx`）的授权子系统；旧体系仍是需求基线，两者**尚未统一**（见 §9）。
> 桌面宿主的装配关系见 [10-desktop.md](10-desktop.md)。

---

## 1. 一页结论

| 维度 | 现状 | 证据 |
|---|---|---|
| 角色 | **5 个**：观察员 / 操作员 / 工艺工程师 / 设备工程师 / 管理员 | `Core/Enums/UserRole.cs` |
| 权限点 | **6 个**：查看、操作、设备配置、**工艺参数**、用户管理、日志 | `Core/Enums/Permission.cs` |
| 账号 | 本地用户目录 `users.json`；首次启动生成 5 个演示账号（均未设口令） | `LocalUserDirectory` |
| 认证 | **本地账号**；口令**可选**：设置后登录必须校验，未设置按工位信任放行 | `LocalAccountProvider` |
| 口令存储 | PBKDF2-HMAC-SHA256，210,000 次迭代，自描述格式，恒定时间比较 | `PasswordHasher` |
| 用户管理 | 列表 / 新建 / 编辑（显示名、角色）/ 启停 / 设置或清除口令 / 删除 | `UserManagementPage` |
| 防锁死 | 不能停用/删除当前登录账号；不能停用/删除/降级最后一个启用中的管理员 | `LocalUserDirectory` |
| 会话 | 进程内单例、不可变快照、变更事件 | `SessionProvider` |
| 权限判定 | 会话 + 角色权限表；**无会话 = 全部拒绝** | `PermissionService` / `RolePermissionPolicy` |
| 审计 | 登录/失败/登出 + 用户增删改/启停/口令变更，均带操作者 | `AuthenticationService` / `LocalUserDirectory` / `LogAuditService` |
| 未来认证方式 | 接口已定，新增实现即可接入，**不改业务调用点** | `IAuthenticationProvider` |
| 测试 | 轴框架 148 条 + 桌面 53 条 = **201 条全绿** | `dotnet test AxisFramework.slnx` |

**一句话**：从“启动时自选角色”升级为“管理员维护账号、登录按账号取角色、口令可选”，
并且把“换成 Windows / 远端认证”继续收敛为一个 provider 的替换。

---

## 2. 目标、边界与不做什么

### 2.1 交付目标

1. 建立**认证 → 会话 → 授权**的完整闭环，让 `RequirePermission` 真正会拒绝；
2. 用 5 个角色覆盖现场岗位分工（操作、工艺、设备、管理、只读）；
3. 把“角色分配”交给**用户管理页**，而不是让使用者在登录界面自选；
4. 把“未来认证方式”的接缝定在接口上，而不是散落在业务代码里的分支。

### 2.2 明确不做（本阶段）

| 不做 | 原因 / 去向 |
|---|---|
| 自助改密界面（用户自己改密） | 当前只有管理员“设置/清除口令”；自助改密需要旧口令校验与审计，见 §11 |
| 口令锁定、复杂度策略、强制首次改密 | 属于本地账号的完整策略，见 §11 |
| 安全等级（Low/Medium/High） | 旧体系的概念；新框架先按角色授权，统一时再评估（§9） |
| Windows / 远端认证实现 | 接口已就绪；现场交付前必须接入其中一种（§2.3） |
| 审计落库、权限拒绝自动审计 | 新框架尚未接数据库；现在写结构化日志 |
| 会话超时 | 监控画面常整班挂着，超时策略需与现场确认（旧文档 §10.2 的同一问题） |

### 2.3 安全边界（必须如实告知）

> **“未设置口令”的账号是工位信任，不是安全边界。**

- 首次启动生成的 5 个演示账号**都没有口令**，任何人都能选中并以该身份进入；
- 管理员给账号设置口令后，该账号登录**必须**校验口令（PBKDF2 + 恒定时间比较）；
- 界面上的角色、账号列表都是公开信息；**真正的拦截在业务入口**（§8.4）；
- 能够修改本机文件、替换程序或直接启动另一个进程的人，不受本机制约束 ——
  这与旧体系 §8.1 的结论一致：数据库/配置文件权限才是最后一道防线。

**现场交付前**：给需要防越权的账号设置口令，或接入 Windows 身份 / 远端权限服务。

---

## 3. 五个角色

### 3.1 角色表

| 角色 | 枚举 | 显示名 | 能做什么 | 典型岗位 |
|---|---|---|---|---|
| 只读 | `Viewer` | 观察员 | 查看状态、报警、日志 | 班组长巡检、参观 |
| 操作 | `Operator` | 操作员 | + 设备启停、伺服使能/定位/点动 | 机台操作工 |
| 工艺 | `ProcessEngineer` | 工艺工程师 | + 调整工艺参数与配方 | 工艺员 |
| 设备 | `Engineer` | 设备工程师 | + 通道/轴参数、手动调试、诊断 | 设备/电气工程师 |
| 管理 | `Admin` | 管理员 | + 用户与权限管理（并拥有以上全部） | 系统管理员 |

### 3.2 两条设计决定

**① 能力单调递增。** `Viewer ⊂ Operator ⊂ ProcessEngineer ⊂ Engineer ⊂ Admin`。
高角色一定拥有低角色的全部权限 —— 升级角色不会让某人已经能做的工作突然做不了；
测试逐级校验包含关系，新增权限点时若只加给中间某个角色会立刻失败。

**② 工艺与设备必须分权。** 旧体系的四角色里没有“工艺”。
但现场“调工艺参数”和“改通道地址/轴卡参数”是两类风险完全不同的操作：
前者可以试错，后者改错会让整台设备失联。因此新增权限点 `ProcessConfig`，
并让 `ProcessEngineer` 拥有它而没有 `MachineConfig`。

### 3.3 角色从哪里来

角色由管理员在 **用户管理页** 分配，登录时以用户目录中的角色为准。
登录请求里**没有角色字段** —— 早期版本允许客户端传 `RequestedRole`，那等于“客户端说自己是管理员”，已删除。

### 3.4 与旧体系角色集的差异（待统一）

| 旧 `EquipmentMonitor.Core.Enums.UserRole` | 新 `EquipmentMonitor.Core.Enums.UserRole` |
|---|---|
| `Viewer` / `Operator` / `Engineer` / `Admin` | `Viewer` / `Operator` / `ProcessEngineer` / `Engineer` / `Admin` |
| 另有三级 `SecurityLevel`，权限表按（等级 × 角色）12 条组合 | 暂无安全等级，权限表按角色 5 条 |
| 本地密码 / Windows / 远程三种认证策略 | 本地账号（口令可选）；另外两种是已定型的扩展位 |

两者是**同名不同义**的类型（不同程序集），不要互相赋值。
统一方案见 §9，属于需求变更，须走 SRS §15.2 的记录。

---

## 4. 权限点与权限矩阵

### 4.1 权限点

| 权限点 | 含义 | 会直接导致设备动作 |
|---|---|---|
| `MachineView` | 查看设备状态、监控画面 | 否 |
| `MachineOperate` | 启停、伺服使能/定位/点动、写点位 | **是**（需二次确认 + 审计） |
| `ProcessConfig` | 修改工艺参数与配方 | 间接（影响之后的动作） |
| `MachineConfig` | 修改通道/轴参数、点位映射、轮询周期 | 间接（可能让设备失联） |
| `UserManage` | 用户管理：增删改用户、分配角色、重置密码 | 否 |
| `LogView` | 查看系统日志与审计日志 | 否 |
| `AlarmAcknowledge` | 确认报警：标记「已知悉」，不改变设备状态 | 否 |

> `AlarmAcknowledge` 由 [12-alarm.md](12-alarm.md) 定义，**P0 已落地（2026-11-01）**：
> `Permission` 枚举、`RolePermissionPolicy`（操作员起）、`PermissionCatalog` 与报警页确认入口均已接入。

### 4.2 矩阵（`RolePermissionPolicy` 的唯一真相）

| 权限点 | 观察员 | 操作员 | 工艺工程师 | 设备工程师 | 管理员 |
|---|:---:|:---:|:---:|:---:|:---:|
| `MachineView` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `LogView` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `MachineOperate` | — | ✓ | ✓ | ✓ | ✓ |
| `ProcessConfig` | — | — | ✓ | ✓ | ✓ |
| `MachineConfig` | — | — | — | ✓ | ✓ |
| `AlarmAcknowledge` | — | ✓ | ✓ | ✓ | ✓ |
| `UserManage` | — | — | — | — | ✓ |

规则写死在代码里（`RolePermissionPolicy`），**不做成运行时配置**：
这张表就是本阶段的安全模型，一次配置失误就会变成“某个角色在现场突然什么都不能做”，
而那种问题只有到现场才会被发现。远端权限管理接管时替换的是 `IPermissionPolicy` 实现，不是改这张表。

---

## 5. 结构：谁依赖谁

```text
┌──────────────────────── Desktop（唯一允许 WinForms 的宿主）────────────────────────┐
│ SignInForm          UserManagementPage（+ 编辑/口令对话框）   MainForm / 操作面板     │
└───────┬───────────────────────┬──────────────────────────────┬─────────────────────┘
        │ SignInAsync           │ List/Create/Update/…          │ HasPermission / RequirePermission
        ▼                       ▼                              ▼
 IAuthenticationService    IUserDirectory               IPermissionService  ◄── 业务入口
        │                       │                              │
        │ 选 provider           │ 读写 users.json               │ 读会话 + 查策略
        ▼                       ▼                              ▼
 IAuthenticationProvider   LocalUserDirectory            ISessionProvider ◄── 登录写入
 （LocalAccount）           （唯一持有口令哈希）
        ▲                       ▲
        │ 未来：WindowsAccount / RemoteService
        └─────────────── 共享用户目录 / 远端目录
   IPermissionPolicy（当前 RolePermissionPolicy；未来可换远端权限表 + 本地缓存）
```

关键点：

1. **只有一条登录路径**：`IAuthenticationService`。界面不直接调 provider，否则审计/会话写入会各写一份。
2. **会话是唯一的“当前身份”**：权限服务、界面、审计都从这里读，不存在第二份状态。
3. **业务入口只认 `IPermissionService`**：`AxisElement` 的 9 条命令、通讯管理器等已有调用点一行不用改。
4. **用户管理的写入口在 `IUserDirectory`**：页面只负责收集输入；权限校验、口令哈希、文件落盘、审计都在实现里。

---

## 6. 接口与模型清单（Core）

| 文件 | 类型 | 职责 / 扩展点 |
|---|---|---|
| `Enums/UserRole.cs` | `UserRole` | 5 角色；数值只用于排序与存储 |
| `Enums/AuthenticationSource.cs` | `AuthenticationSource` | `LocalAccount` / `WindowsAccount` / `RemoteService` |
| `Enums/Permission.cs` | `Permission` | 6 个权限点；含 `ProcessConfig` |
| `Models/UserAccount.cs` | `UserAccount` | 账号只读快照；`HasPassword` 表示是否设置了口令，**不含哈希** |
| `Models/CreateUserRequest.cs` / `UpdateUserRequest.cs` | 请求模型 | 新建；编辑显示名与角色（账号名不可改） |
| `Models/UserRules.cs` | `UserRules` | 账号名/显示名校验（界面与服务共用） |
| `Models/PasswordRules.cs` | `PasswordRules` | 口令长度规则（≥8、≤128） |
| `Models/SecurityErrorCodes.cs` | `SecurityErrorCodes` | 用户管理与认证的失败码 |
| `Models/UserSession.cs` | `UserSession` | 不可变会话快照；无凭据字段 |
| `Models/SignInRequest.cs` | `SignInRequest` | 登录输入：账号 + 可选凭据；**没有角色字段** |
| `Models/AuthenticationProviderInfo.cs` | `AuthenticationProviderInfo` | 登录界面列出的认证方式 |
| `Models/UserRoleCatalog.cs` | `UserRoleCatalog` | 角色顺序、中文名、一句话职责 |
| `Models/PermissionCatalog.cs` | `PermissionCatalog` | 权限点中文名 |
| `Events/SessionChangedEventArgs.cs` | `SessionChangedEventArgs` | 旧/新会话与原因 |
| `Abstractions/IAuthenticationProvider.cs` | `IAuthenticationProvider` | **认证方式的唯一扩展点** |
| `Abstractions/IAuthenticationService.cs` | `IAuthenticationService` | 登录门面：分派、建会话、写审计 |
| `Abstractions/IUserDirectory.cs` | `IUserDirectory` | 账号查询与用户管理（写操作要求 `UserManage`） |
| `Abstractions/ISessionProvider.cs` | `ISessionProvider` | 当前会话持有 + 变更事件 |
| `Abstractions/IPermissionPolicy.cs` | `IPermissionPolicy` | 角色 → 权限（授权决策唯一来源） |
| `Abstractions/IPermissionService.cs` | `IPermissionService` | 业务入口 `RequirePermission` / UI `HasPermission` |
| `Abstractions/IAuditService.cs` | `IAuditService` + `AuditEventTypes` | 登录/登出/权限拒绝 + 用户管理事件 |

### 6.1 关键契约

```csharp
public interface IAuthenticationProvider
{
    AuthenticationSource Source { get; }
    string DisplayName { get; }
    bool IsAvailable { get; }                       // 不可用就置灰，不要点了再报错
    Task<Result<UserSession>> AuthenticateAsync(SignInRequest request, CancellationToken ct = default);
}

public interface IUserDirectory
{
    Task<Result<IReadOnlyList<UserAccount>>> ListAsync(CancellationToken ct = default);
    Task<Result<UserAccount>> FindAsync(string userName, CancellationToken ct = default);
    Task<Result<UserAccount>> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<Result> UpdateAsync(UpdateUserRequest request, CancellationToken ct = default);
    Task<Result> SetEnabledAsync(string userName, bool enabled, CancellationToken ct = default);
    Task<Result> SetPasswordAsync(string userName, string password, CancellationToken ct = default);
    Task<Result> ClearPasswordAsync(string userName, CancellationToken ct = default);
    Task<Result> DeleteAsync(string userName, CancellationToken ct = default);
}
```

**为什么 `ListAsync` / `FindAsync` 不校验权限**：登录界面要在会话建立之前列出账号。
**为什么写操作在实现里校验**：界面按钮可以绕过；只有业务方法入口的 `RequirePermission` 才是不可绕过的那一层。

**为什么 `AuthenticateAsync` 返回 `Result` 而不是抛异常**：账号不存在、口令错误、账号停用
都是预期内失败，且需要可读提示；按项目约定用 `Result`。真正需要抛的是实现自身的编程错误。

**为什么 `IPermissionService.RequirePermission` 仍然抛异常**：它表达“不该走到这里”
（界面已经判过），异常会带完整堆栈，便于排查越权调用 —— 与旧体系 §4.4 的结论一致。

---

## 7. 实现（Infrastructure）

| 类型 | 职责 | 关键实现决定 |
|---|---|---|
| `LocalUserDirectory` | 本地用户库（JSON） | 默认 5 个演示账号；原子写；最后管理员/当前账号保护；写操作审计 |
| `PasswordHasher` | 口令哈希 | PBKDF2-HMAC-SHA256 / 210,000 次 / 16 字节盐 / 32 字节哈希 / 恒定时间比较 |
| `LocalAccountProvider` | 本地账号认证 | 角色来自用户目录；口令校验委托给用户目录（provider 拿不到哈希） |
| `AuthenticationService` | 登录门面 | 只有它能写会话；失败**不覆盖**现有会话；审计不含凭据 |
| `SessionProvider` | 会话持有 | 锁内换引用、**锁外**触发事件；不做过期检查 |
| `RolePermissionPolicy` | 角色权限表 | 写死；按累积构造保证单调；未知角色返回空（拒绝） |
| `PermissionService` | 权限判定 | 无会话 / 已过期 / 无授权 → 拒绝 |
| `LogAuditService` | 审计 | 从 `ISessionProvider` 补操作者；失败只记日志，不影响业务 |

### 7.1 用户文件

- 路径：`users.json`，默认在程序运行目录（`AppContext.BaseDirectory`），可由 `UserDirectoryOptions` 改；
- 格式：`{ "version": 1, "users": [ { "userName", "displayName", "role", "isEnabled", "passwordHash", "createdAt" } ] }`；
- **口令哈希可为 `null`**：表示工位信任；
- 首次启动（文件不存在）生成 5 个演示账号：`viewer/operator/process/engineer/admin`，均无口令；
- 删除文件 = 重置为演示账号（审计记录仍在日志里）。

### 7.2 用户管理规则（由 `LocalUserDirectory` 强制）

| 规则 | 失败码 | 理由 |
|---|---|---|
| 账号名 2–32 位、字母/数字开头、允许 `. _ -` | `USER_INVALID_NAME` | 账号名是审计的主键，不能是任意文本 |
| 显示名 1–40 字 | `USER_INVALID_DISPLAY` | 过长会撑破列表与确认框 |
| 账号名唯一（大小写不敏感） | `USER_DUPLICATE` | 避免“Admin 与 admin 是两个账号”的冒充 |
| 不能停用/删除**当前登录账号** | `USER_SELF_PROTECTED` | 防一次误操作把正在操作的人锁在门外 |
| 不能停用/删除/降级**最后一个启用中的管理员** | `USER_LAST_ADMIN` | 否则再也没人能做用户管理 |
| 口令 ≥8 字符（仅长度规则） | `USER_INVALID_PASSWORD` | 组合复杂度规则往往导致口令被写在显示器上 |

**变更何时生效**：角色、启停、口令在**下一次登录**时生效；当前会话不会被即时踢掉
（避免管理员改权限时把现场正在操作的人瞬间降权）。

### 7.3 口令

- 格式：`pbkdf2.sha256.{iterations}.{base64Salt}.{base64Hash}`；
- 迭代次数写在串里，将来提高强度不影响旧口令验证；
- 只在 `LocalUserDirectory.VerifyAsync`（认证）与 `PasswordHasher`（哈希）之间流转；
- **明文口令不进日志、不进审计、不进模型**；界面把明文交给目录后立即哈希；
- 本阶段**没有**：首次强制改密、失败锁定、口令过期、口令历史。

### 7.4 DI 注册与生命周期（`InfrastructureModuleRegister`）

| 服务 | 生命周期 | 说明 |
|---|---|---|
| `UserDirectoryOptions` | Singleton（实例） | 用户文件路径 |
| `LocalUserDirectory` | Singleton | 唯一持有口令哈希的实例 |
| `IUserDirectory` | Singleton（同一实例） | 界面/业务通过接口使用 |
| `ISessionProvider` → `SessionProvider` | Singleton | 全进程唯一 |
| `IPermissionPolicy` → `RolePermissionPolicy` | Singleton | 无状态 |
| `IPermissionService` → `PermissionService` | Singleton | 无状态 |
| `IAuthenticationProvider` → `LocalAccountProvider` | Singleton | 可注册多个，按注册顺序 |
| `IAuthenticationService` → `AuthenticationService` | Singleton | 组合根保证至少一个 provider |
| `IAuditService` → `LogAuditService` | Singleton | 现场替换为落库实现 |

> **已删除**：`DevelopmentPermissionService`、`LocalRoleSelectionProvider`（自选角色的过渡实现）。

---

## 8. 桌面接入

### 8.1 启动流程（变化点）

```text
Program.Main
  ApplicationConfiguration.Initialize()
  RegisterGlobalExceptionHandlers()
  BuildHost(args)
  ┌───────────────────────────────────────┐
  │ SignInForm.ShowDialog()               │ ← 新增：账号列表来自用户目录
  │ 取消 → 返回码 3（不初始化轴）          │
  └───────────────┬───────────────────────┘
  AxisFrameworkBootstrapper.InitializeAsync()
  失败 → MessageBox + 返回码 2
  AxisSnapshotStore.Attach(elements)
  host.Start()
  Application.Run(MainForm)
  Shutdown()  → 停采集 → 写登出审计 → 释放元件 → 冲刷日志
```

先登录再初始化轴卡的原因：取消登录不应因为已经初始化硬件而留下副作用。

### 8.2 登录窗体

- 账号下拉：只列**启用中**的账号（`显示名（账号）`）；
- 角色说明：`角色：管理员` + 一句职责，**不可自选**；
- 口令框：账号未设置口令时禁用并提示“工位信任”；设置后必须输入；
- 失败提示直接展示 `Result.Error`（账号不存在 / 已停用 / 口令错误 / 用户文件不可用）。

### 8.3 用户管理页（`UserManagementPage`）

| 区域 | 内容 |
|---|---|
| 工具栏 | 新建用户 / 编辑 / 启用·停用 / 设置·重置口令 / 删除账号 / 刷新 |
| 表格 | 账号、显示名、角色、口令（已设置/未设置）、状态、创建时间；双击行直接编辑 |
| 底部 | `共 N 个账号；当前登录：xxx（不能停用/删除自己）` + 最近一次操作结果 |

- 需要 `UserManage` 才能看到导航；没有权限时工具栏全部禁用并提示；
- 选中**当前登录账号**时，停用/删除按钮禁用（目录层还有第二道保护）；
- 新建/编辑走 `UserEditDialog`（账号名创建后只读；角色默认观察员）；
- 口令走 `PasswordDialog`（设置/重置；已有口令时可勾选“清除口令”回到工位信任）；
- 每次操作成功都会 `RefreshAsync` 并保持选中行，失败把原因写到提示栏。

### 8.4 三层防护（对应旧体系 §4.4）

| 层 | 位置 | 是否可绕过 |
|---|---|---|
| ① 入口显隐 | `MainForm` 导航按钮、用户管理页工具栏 | 可（界面层） |
| ② 窗体动作前判断 | 按钮 `Enabled`、二次确认 | 可 |
| ③ 业务方法入口 | `AxisElement.RequirePermission`、`IUserDirectory` 写方法 | **不可** |

---

## 9. 与旧体系 / SRS 的关系

### 9.1 为什么不直接迁移旧 `EquipmentMonitor.Security`

旧体系（`EquipmentMonitor.slnx`）当前**不可编译**（迁移中断，见 [交接文档](../handover-axis-framework.md)），
但它仍是完整的需求实现：四角色、三级安全等级、12 条决策表、本地密码/Windows/远程三种策略、审计落库。
新框架如果一次性搬过来，会同时引入 Data 依赖、SQLite 表结构和会话超时服务，
在轴控还没接真实 PLC 的阶段把风险叠在一起。

因此本阶段做**新框架内的最小闭环**：五角色 + 本地账号 + 可选口令 + 用户管理 + 会话 + 权限判定 + 审计日志。
接口按“将来能替换成旧体系那套”的形状设计（provider / directory / policy / session 分段）。

### 9.2 与 SRS §9 的偏差（必须对齐）

[SRS §9](../SRS.md) 的权限矩阵是**已冻结的需求基线**（4 角色 × 3 等级，共 12 条），
并声明“任何调整都属于需求变更，须走 §15.2 变更记录并由负责人批准”。
新框架当前的 5 角色、无等级、可选口令与该基线**不一致**：

- 本文只把它记为**过渡实现**，不宣称满足 AC-005；
- 统一时需要在以下三项中逐条决策：角色集合（4 还是 5）、是否保留安全等级、
  口令/Windows/远端三种认证的启用顺序；
- 决策完成后同步 SRS §9、`04-security.md` 与本文，并更新 §15.1 追踪矩阵。

> 在新旧统一之前，**不要把本文的矩阵当作现场验收依据**。

---

## 10. 测试

| 测试文件 | 覆盖 |
|---|---|
| `RolePermissionPolicyTests` | 五角色单调性、管理员拥有全部权限、观察员只读、工艺/设备分权、未知角色拒绝 |
| `PermissionServiceTests` | 无会话拒绝、未登录异常文案、各角色边界、越权消息、会话过期、登出后拒绝 |
| `SessionProviderTests` | 登录/切换/登出事件、旧值传递、未登录时空操作 |
| `LocalUserDirectoryTests` | 种子账号、新建/重复/非法名、改角色、最后管理员、当前账号保护、删除、口令设置与清除、无权限拒绝、审计 |
| `AuthenticationServiceTests` | 账号登录、不存在/停用/口令错误、失败不覆盖会话、登出审计、审计不含凭据 |
| `SignInFormTests` | 只列启用账号、角色说明、口令框按账号启用、无账号提示 |
| `UserManagementPageTests` | 列表渲染、无权限禁用、当前登录账号保护 |
| `UserEditDialogTests` | 默认最小权限、账号名校验、编辑模式账号名不可改 |
| `PasswordDialogTests` | 长度/确认校验、清除口令 |
| `DesktopHostTests` | 用户管理页与登录窗体可从容器解析 |

当前：**201 条全绿**（轴框架 148 + 桌面 53）。

---

## 11. 扩展路线

### 11.1 本地账号的剩余项

1. **自助改密**：新增 `ChangePasswordForm`，校验旧口令 + 新口令，成功后写 `USER_PASSWORD_CHANGED` 审计
   （旧体系已有完整实现，可参考 `ChangePasswordForm` 与 `PasswordPolicy`）；
2. **首次强制改密**：账号表加 `MustChangePassword`，登录成功后拦截并强制改密；
3. **失败锁定**：连续 N 次失败锁定 M 分钟；锁定状态入库/入文件并写审计；
4. **口令策略**：长度之外的规则（禁止与账号名相同、口令历史）按现场要求追加。

### 11.2 Windows 身份

1. `WindowsAuthenticationProvider`：`WindowsIdentity.GetCurrent()` 取身份，不需要口令；
2. 账号 → 角色映射来自用户目录（新增 `WindowsAccount` 字段）或配置；未映射默认 `Viewer`；
3. 建议沿用旧体系 §3.3 的钳制：**Windows 会话不能达到最敏感角色**
   （进程身份只能证明“这台机器有人登录”，无法证明“此刻在键盘前的人是谁”）；
4. `IsAvailable` 在非 Windows 平台返回 `false`，不抛异常。

### 11.3 远端权限管理

1. `RemoteAuthenticationProvider`：HTTPS 换令牌 → 用户信息 + 角色；强制 HTTPS，
   用 `IHttpClientFactory`，超时与不可达分别提示；**远端返回的未知角色必须拒绝**；
2. `RemoteUserDirectory` 或 `RemotePermissionPolicy`：目录/权限表从服务端拉取并**本地缓存**；
   接口是同步的，不能把网络调用放进去；
3. 远端不可达时的降级策略必须在文档里写死（建议：沿用上次缓存 + 告警，不能默认放行）；
4. 令牌过期 → 重写 `UserSession.ExpiresAt`，界面按 `SessionChanged` 回到登录页。

### 11.4 其他待补

- **审计落库**：`IAuditService` 新实现；`PermissionDenied` 由业务入口或全局兜底成对写入；
- **会话超时**：`SessionExpiryService : IHostedService`（30s 检查），并定义“监控模式例外”。

---

## 12. 已知缺口（按优先级）

| # | 缺口 | 影响 | 处理建议 |
|---|---|---|---|
| 1 | 演示账号默认无口令 | 任何人可选管理员进入 | 现场交付前设置口令或接入 Windows/远端认证 |
| 2 | 与 SRS §9 的 4 角色基线不一致 | 不能直接用于验收 | 走需求变更统一（§9.2） |
| 3 | 无失败锁定/首次强制改密 | 口令可被在线暴力尝试 | 随本地账号完善（§11.1） |
| 4 | 审计只写日志、权限拒绝不自动审计 | 事后追责不完整 | 接数据库后补 |
| 5 | 无会话超时 | 离岗后界面仍可操作 | 与现场确认后加 `SessionExpiryService` |
| 6 | 变更在下一次登录生效 | 管理员改权限不会立即踢人 | 有意取舍；需要即时生效时再设计“强制刷新会话” |
| 7 | 登录/用户管理窗体的自动化覆盖有限 | 回车/Tab/取消路径未全覆盖 | 已有实跑截图；后续补消息泵用例 |

---

## 13. 验收记录（2026-10-31）

| 项 | 结果 | 证据 |
|---|---|---|
| 构建 | 0 错误；**33 条空引用警告与基线持平**，新文件 0 警告 | `dotnet build AxisFramework.slnx -t:Rebuild` |
| 测试 | **152 条全绿**（轴框架 110 + 桌面 42） | `dotnet test AxisFramework.slnx` |
| 登录 | 登录窗口列出 5 个演示账号；管理员账号密码框禁用；回车进入主界面 | 实跑截图 `登录` |
| 用户管理 | 打开“用户管理”即显示 5 个账号；当前登录 admin 的停用/删除按钮禁用 | 实跑截图 `用户管理` |
| 新建用户 | 填账号/显示名 → 保存 → 列表出现新行、自动选中、提示“已新建账号…” | 实跑 + `users.json` 落盘 |
| 设置口令 | 选择 operator → 设置口令 → 列表显示“已设置”、按钮变“重置口令” | 实跑 + `users.json` 含 `pbkdf2.sha256`、**不含明文** |
| 审计 | 登录/登出/用户增删改/口令变更均写结构化日志并带操作者 | `logs/desktop-*.log` |

---

## 14. 相关文档

- [桌面宿主](10-desktop.md)（启动流程、导航、用户管理页入口）
- [旧 Security 模块](04-security.md)（四角色 / 三级等级 / 三种策略的完整设计，迁移参考）
- [新框架交接](../handover-axis-framework.md)（两套体系并存、构建与测试基线）
- [需求基线 §9 权限需求](../SRS.md)（冻结矩阵，统一前的对账对象）
- [编码规范](../conventions.md)（Result/异常、日志、线程）