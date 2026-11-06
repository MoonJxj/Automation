# Security 模块设计

> 项目：`EquipmentMonitor.Security` ｜ 依赖：`Core`、`Data`、`Shared`
> 密码哈希使用 .NET 8 BCL 的 `Rfc2898DeriveBytes.Pbkdf2`，无第三方加密库

> ⚠️ **本文档在 M2 阶段被整体重写。**
> 早期设计稿把 `SecurityLevel` 定义为 `Viewer/Operator/Engineer/Administrator` 四级，
> 那实际上混淆了「角色」与「等级」两个概念。
> 现已拆分为 `SecurityLevel`（三级）与 `UserRole`（四角色），详见 §3。

## 1. 职责边界

**做什么**：认证（三种可插拔策略）、会话管理、基于决策表的权限判定、用户 CRUD、审计落库。

**不做什么**：

- ❌ 不实现用户数据访问（`UserRepository` 在 `Data` 模块）
- ❌ 不引用任何 UI 类型
- ❌ 不做设备/通讯相关的授权（只提供判定能力，由业务模块调用）

## 2. 依赖关系

```
EquipmentMonitor.Security ──► Core, Data, Shared
```

### 2.1 对架构文档的一处修改

**`Core` 现在引用 `Shared`。** 架构文档原先声明「Core 与 Shared 互不依赖，两者都是叶子节点」，
这条约束在 M2 落地时被证明站不住：`IAuthService` / `IPermissionService` 的返回值
需要表达「预期内的失败」（密码错误、权限不足、账号锁定），
而这是全项目统一的 `Shared.Result<T>` 的职责。

替代方案都被否决了：在 `Core` 里再定义一套 `Result` 会产生两种失败表示；
认证失败改用抛异常违反「预期内失败不抛异常」的项目约定，且异常容易被 `catch` 吞掉而丢失审计记录。

`Shared` 仍是叶子节点，依赖图依然无环。**`docs/architecture.md` 的依赖矩阵需同步更新。**

## 3. 认证策略与安全等级（正交的两个维度）

### 3.1 核心设计原则

| 维度 | 回答的问题 | 由什么决定 |
|---|---|---|
| **认证策略** `AuthStrategy` | 你怎么进来的 | 用户选择的登录方式 |
| **角色** `UserRole` | 你是谁 | 用户配置 |
| **安全等级** `SecurityLevel` | 进来后能做什么 | 认证方式 + 角色，运行时可调 |

三者正交。一个 Admin 用 Windows 自动登录进来，角色仍是 Admin，但等级只有 Medium。

### 3.2 认证流程图

```
                      ┌──────────────────────────┐
   用户选择认证方式 ───►│      AuthService         │
                      │  按配置顺序调度策略        │
                      └───────────┬──────────────┘
                                  │
        ┌─────────────────────────┼─────────────────────────┐
        ▼                         ▼                         ▼
┌───────────────┐        ┌───────────────┐        ┌───────────────┐
│ LocalAuth     │        │ WindowsAuth   │        │ RemoteAuth    │
│ 查库 + PBKDF2 │        │ 读进程身份     │        │ HTTPS 调用    │
│ 【强凭据】     │        │ 【仅识别】     │        │ 【默认禁用】   │
└───────┬───────┘        └───────┬───────┘        └───────┬───────┘
        │                        │                        │
        │ 失败：计数 +1           │ 不在允许列表 → 拒绝      │ 超时/不可达
        │ 达阈值 → 锁定           │ 未加域 → 组检查降级      │ → 快速失败
        ▼                        ▼                        ▼
   AuthOutcome(User, GrantedLevel, StrategyName)
                                  │
                                  ▼
                    ┌──────────────────────────┐
                    │  由策略决定等级上限        │
                    │  （见 §3.3 判定规则）      │
                    └───────────┬──────────────┘
                                ▼
                    ┌──────────────────────────┐
                    │  AuthService 建立 Session │
                    │  → ISessionProvider.Set   │
                    └──────────────────────────┘
```

### 3.3 安全等级判定规则

规则实现于 `SecurityLevelResolver`，**刻意写成代码而非配置** ——
它们定义的是安全模型本身，做成配置项等于允许一次配置失误就瓦解整个模型。

| 认证方式 | 角色 | 授予等级 | 理由 |
|---|---|---|---|
| 本地密码 | Admin / Engineer | **High** | 本地密码是系统中唯一的强凭据 |
| 本地密码 | Operator | **Medium** | 可操作设备，不能改配置 |
| 本地密码 | Viewer | **Low** | 只读 |
| Windows 身份 | Admin / Engineer | **Medium** | 见下方说明 |
| Windows 身份 | Operator / Viewer | **Low** | 只读 |
| 远程服务 | 按角色映射 | 同本地密码 | 依赖远程服务可信 |

> **Windows 会话的等级上限硬性钳制在 Medium。**
> 配置里写 `High` 会被忽略并记录告警。
> 理由有二：
>
> 1. `WindowsIdentity.GetCurrent()` 返回的是**当前进程**的身份，
>    它只能证明「这台机器上有个已登录的会话」，**无法证明此刻坐在键盘前的人是谁**。
>    操作工离开工位没锁屏，下一个人直接就能用他的身份进入系统。
> 2. 该会话**没有本地密码**，而 High 的操作需要二次确认（重新输入密码）。
>    允许它到达 High 会产生「需要确认却无法确认」的死结。

### 3.4 三种策略的实现要点

#### LocalAuthStrategy

- **不泄露用户是否存在**：「用户不存在」与「密码错误」返回**完全相同**的提示，
  且对不存在的用户也执行一次等价开销的哈希运算以抹平响应时间差
- **账号锁定**：连续失败达阈值后锁定，防在线暴力破解
- **渐进式升级**：迭代次数提高后，下一次成功登录时透明地重算并落库
- **绝不记录密码**：失败日志只含用户名与原因

#### WindowsAuthStrategy

- `IsAvailable()` 在非 Windows 平台、未配置、身份取不到时都返回 `false`，**不抛异常**
- 允许列表为空时**拒绝所有人** —— 「未配置」等价于「未授权」
- 未加域时 AD 组查询失败会降级为「按用户列表判断」并告警，不中断认证
- 给 Windows 账户一个低等级会话总比让它崩溃好

#### RemoteAuthStrategy

- **默认禁用**。现场部署不启用，不影响任何安全等级的判定
- 强制 HTTPS，非 HTTPS 端点直接拒绝发起请求
- 用 `IHttpClientFactory` 管理 `HttpClient`，避免 socket 耗尽
- 超时（默认 5 秒）与「网络不可达」分开处理，给出不同的用户提示
- 远程返回的未知角色**必须拒绝**，不能默认给某个角色

> **Fake 实现的双重门禁**：`FakeRemoteAuthStrategy` 接受任意非空凭据，
> 因此被 `#if DEBUG` 包裹（Release 构建中根本不存在），
> 且需要 `Security:RemoteAuth:UseDevelopmentFake = true` 才注册。

## 4. 权限模型

### 4.1 权限点与角色

| 权限点 | 说明 |
|---|---|
| `MachineView` | 查看设备状态、监控画面 |
| `MachineOperate` | 启停设备、手动调试、写入点位 ⚠️ 会直接导致设备动作 |
| `MachineConfig` | 修改通道参数、点位映射 |
| `UserManage` | 增删改用户、分配角色、重置密码 |
| `LogView` | 查看系统日志与审计日志 |

角色：`Viewer` / `Operator` / `Engineer` / `Admin`

### 4.2 决策表

决策表按 **(安全等级, 角色)** 组织，共 **3 × 4 = 12 条**，
配置在 `appsettings.json` 的 `Security:AccessRules`。

**为什么不是只按角色建表**：同一角色在不同等级下的权限**本来就不同**。
Admin 在 Low 下与 Viewer 一样只能看 —— 这正是「临时降权」能生效的原因。

**穷尽性校验**：缺少任何一条组合都会导致 `SecurityLevelPolicy` 构造失败、程序无法启动。
安全决策不允许存在「未定义行为」—— 若缺失时默认拒绝，
一个配置笔误就会变成「某个角色在现场突然什么都不能做」，而那只有到现场才会被发现。

### 4.3 权限矩阵（默认配置）

| 等级 | 角色 | 权限 | 需二次确认 |
|---|---|---|---|
| **Low** | 全部（含 Admin） | MachineView, LogView | — |
| **Medium** | Viewer | MachineView, LogView | — |
| **Medium** | Operator / Engineer / Admin | MachineView, LogView, MachineOperate | MachineOperate |
| **High** | Viewer † | MachineView, LogView | — |
| **High** | Operator † | MachineView, LogView, MachineOperate | MachineOperate |
| **High** | Engineer | MachineView, LogView, MachineOperate, MachineConfig | MachineOperate, MachineConfig |
| **High** | Admin | 全部 5 项 | MachineOperate, MachineConfig, UserManage |

† `High/Viewer` 与 `High/Operator` 实际不可达（等级提升校验会拒绝），
但决策表仍必须完整填写 —— 不能依赖「它到不了」这种隐含假设。

### 4.4 两种调用风格

```csharp
// 1. 界面呈现：返回 bool，调用方处理 false 分支
if (permissionService.HasPermission(Permission.UserManage)) { 显示按钮 }

// 2. 业务方法入口：失败抛 UnauthorizedAccessException
permissionService.RequirePermission(Permission.UserManage);
```

**为什么 `RequirePermission` 抛异常而不是返回 `Result`**：
本项目的一般约定是「预期内失败用 `Result`」，但权限守卫表达的是「**不该走到这里**」——
正常流程中调用方已经用 `HasPermission` 判断过了。
它失败意味着代码有缺陷或存在越权调用，抛异常更合适，
且能被全局兜底捕获并留下完整堆栈（那正是排查越权时需要的）。

> **UI 隐藏不等于权限控制。**
> 按钮禁用只是体验优化，真正的校验必须在业务层通过 `RequirePermission` 执行。
> `UserManagementForm` 上体现为三层：打开窗体前判权限（第 1 层）、
> 按钮 `Enabled` 状态（第 2 层，可绕过）、**每个操作入口的 `RequirePermission`（第 3 层，不可绕过）**。

### 4.5 等级切换

- **降权**：总是允许，无需二次确认，但必须审计
- **升级**：需要角色资格 + 二次确认（重新输入密码）
- **Windows 会话**：不能升到 High
- **审计**：`OldLevel`、`NewLevel`、`Operator`、`Reason`、`Timestamp`

`Reason` 是必填的 —— 「为什么降的权」往往比「降到了几级」更重要。

## 5. 会话管理

```
ISessionProvider（单例，全进程唯一）
├── Current : Session?        ← volatile 引用
├── Set / Clear               ← 加锁，事件在锁外触发
├── Touch()                   ← 滑动过期续期（不触发事件）
└── ChangeLevel()             ← 只改状态，不做校验

SessionExpiryService（IHostedService，每 30 秒检查一次）
└── 过期 → ISessionProvider.Clear("会话超时") → 界面响应 SessionChanged
```

**为什么需要后台服务而不是惰性检查**：若只在权限判定时检查过期，
界面不会自己变成「已登出」——用户离开后回来看到的仍是上次的画面。
超时登出的意义恰恰是**遮住屏幕上的内容**，惰性检查做不到。

**`Session` 不可变**：会被 UI 线程与后台线程同时读取，
可变对象会产生「检查通过 → 降权 → 操作执行」的窗口期。

## 6. 密码哈希

### 6.1 存储格式

```
pbkdf2.sha256.600000.{base64Salt}.{base64Hash}
 └──┬──┘ └─┬──┘ └──┬─┘ └───┬────┘ └───┬────┘
  算法族   摘要   迭代次数   16字节盐   32字节哈希
```

自描述格式的意义：**参数升级无需改动表结构**。旧的哈希带着它自己的迭代次数，
换用更高参数的校验器仍能正确验证，并在下一次成功登录时透明重算。

### 6.2 参数

| 项 | 值 | 说明 |
|---|---|---|
| 算法 | PBKDF2-HMAC-SHA256 | BCL 内置，零第三方依赖 |
| 迭代次数 | 可配置，默认 **600,000** | OWASP 当前推荐值，目标耗时 200–500ms |
| 强制下限 | **210,000** | 低于此值会被抬升并告警 |
| 盐 | 16 字节，每用户独立 | `RandomNumberGenerator`（**不是** `Random`） |
| 输出 | 32 字节 | 与 SHA-256 输出宽度一致 |
| 比较 | `CryptographicOperations.FixedTimeEquals` | 恒定时间，防时序攻击 |

**关于 `NeedsRehash` 与渐进式升级**：只调整配置而不重算已有哈希，
等于升级只对新用户生效，老用户的弱哈希会长期留在库里。
用户成功登录的那一刻是唯一能拿到明文密码的时机，错过就要等下一次。

## 7. 审计

### 7.1 与诊断日志的分工

| | 诊断日志（Serilog） | 审计记录（`audit_logs` 表） |
|---|---|---|
| 用途 | 排查问题 | 问责 |
| 存储 | 文件 | 数据库 |
| 量 | 大 | 小 |
| 保留 | 30 天，自动清理 | **永久** |
| 可查 | 文本搜索 | SQL 查询 |

两者刻意不做成同一份：诊断日志的保留策略会自动清理，
而审计记录如果跟着一起被删掉，就等于没有审计。

### 7.2 必须记录的事件

| 事件 | 级别 | 必含字段 |
|---|---|---|
| 登录成功 | Information | UserName、Role、SecurityLevel、Strategy |
| 登录失败 | **Warning** | UserName、失败原因（**不含密码**） |
| 账号锁定 | **Warning** | UserName、锁定时长 |
| 登出 / 超时 | Information | UserName、原因 |
| 权限校验未通过 | **Warning** | UserName、RequiredPermission、ActualRoles |
| 等级变更 | Information | OldLevel、NewLevel、Operator、Reason |
| 用户增删改 | Information | Operator、目标用户、操作类型 |
| 密码重置 | Information | Operator、目标用户（**不含密码**） |

**禁止记录**：密码明文、密码哈希、Token、完整连接字符串。

## 8. 威胁模型简析

| 威胁 | 缓解措施 | 残留风险 |
|---|---|---|
| **离线破解数据库** | PBKDF2 600k 迭代 + 每用户随机盐 | 工控机 CPU 若太慢会调低迭代次数，削弱强度 |
| **在线暴力破解** | 账号锁定（5 次 / 15 分钟） | 多账号轮换攻击；共享账号场景下锁定是可用性问题 |
| **用户名枚举** | 统一失败提示 + 等价哈希运算抹平耗时 | 无 |
| **时序攻击** | `FixedTimeEquals` 比较哈希 | 无 |
| **认证绕过** | 无「记住我跳过登录」；假实现在 Release 中不存在 | 无 |
| **越权操作** | 决策表 + 业务层 `RequirePermission` | 新增业务方法若忘记加守卫则失效 |
| **物理接触机器** | Windows 会话等级限制在 Medium 以下 | **未锁定屏幕时，任何人都能以该身份进入（只读）** |
| **SQL 注入** | 全部参数化，动态排序/表名走白名单 | 无 |
| **凭据泄露到日志** | `UserInfo` / `Session` / `LoginRequest` 显式脱敏 `ToString` | 新增敏感字段时需人工检查 |
| **数据库被手工篡改** | 哈希串损坏时 `Verify` 返回 false 而非抛异常 | 可直接改 `is_enabled` 或 `role` 提权——**数据库文件权限是最后一道防线** |

### 8.1 关键的安全边界

> **本系统不防御「拥有本机文件系统写权限」的攻击者。**

能够直接编辑 SQLite 文件的人可以：
- 把自己改成 Admin
- 替换任意用户的密码哈希
- 删除或伪造审计记录

这不是实现缺陷，而是单机部署架构的固有限制。
现场部署时应通过 **NTFS 文件权限**限制 `data/equipment.db` 的访问，
只允许运行上位机的账户读写。

## 9. 扩展点

| 需求 | 做法 |
|---|---|
| 新增认证方式（LDAP / OAuth） | 实现 `IAuthStrategy` + 在 `AddEquipmentMonitorSecurity` 中注册。**不改动 `AuthService`** |
| 新增权限点 | 加 `Permission` 枚举值 + 更新 12 条 `AccessRules`（缺一条会启动失败，这是刻意的） |
| 新增角色 | 加 `UserRole` 枚举值 + 更新 `AccessRules` |
| 调整权限矩阵 | 改 `appsettings.json`，无需重新编译 |
| 提高哈希强度 | 改 `PasswordHashIterations`。旧哈希在用户下次登录时自动升级 |
| 启用远程认证 | 配置 `RemoteAuth`。**先确认端点可信** —— 它返回的角色直接决定权限 |

## 10. 遗留问题与后续 TODO

### 10.1 未完成项

> **新框架已另建本地账号与用户管理**（2026-10-31）：`AxisFramework.slnx` 侧的五角色、本地用户目录、
> 可选口令、用户管理页与权限判定见 [11-authorization.md](11-authorization.md)。本文描述的四角色 /
> 三级等级 / 三种策略仍是旧体系实现与**需求基线**，两者尚未统一；本文的未完成项在统一前不影响新框架。

- [ ] **UI 控件库替换未落地**。窗体目前用标准 WinForms 控件实现。
      AntdUI（Apache-2.0）已引入并还原成功，但控件级替换需要对照该库的
      设计器与文档逐项完成，属于纯外观改动。
- [ ] **首次登录强制改密的界面未实现**。`must_change_password` 字段已在数据层落地，
      启动时也会告警，但登录后强制跳转改密对话框尚未实现。
- [ ] **审计查看界面未实现**。`IAuditService.GetRecentAsync` 已就绪，缺 UI。
- [ ] **二次确认对话框未接入业务操作**。`AccessDecision.RequireConfirm` 已能正确求值，
      但 `MachineOperate` / `MachineConfig` 的实际执行点尚未调用确认流程。

### 10.2 需要确认的设计问题

- [ ] **会话超时对监控画面的影响**。默认 30 分钟滑动过期，
      但上位机常需要长时间挂着监控画面无人操作。是否需要「监控模式例外」？
- [ ] **`SecurityLevelPolicy` 的判定与 `PermissionService` 的关系**。
      当前 `PermissionService` 完全委托给决策表，两者不是并列关系而是门面与实现。
      若将来需要「角色权限 ∩ 等级权限」的语义，需重新设计表结构。
- [ ] **远程认证返回 Admin 角色是否应被接受**。
      当前按角色直接映射（远程 Admin → High）。若接入外部供应商服务需重新评估。
- [ ] **审计记录的防篡改**。当前无哈希链或签名，数据库被改写无法察觉。
      若现场有合规要求需增加。

### 10.3 已知的工程债

- [ ] `AuthService` 中有一处 `_authService is AuthService concrete` 的向下转型
      （`LoginForm` 取 `DefaultStrategy`）。应把 `DefaultStrategy` 提到 `IAuthService` 接口上。
- [ ] `UserRepository.RecordFailedAttemptAsync` 的读-改-写在事务内完成，
      但高并发下仍可能因 SQLite 的写锁产生 `busy` 重试。当前依赖 `busy_timeout` 兜底。
- [ ] 缺少 WindowsAuthStrategy 与 RemoteAuthStrategy 的测试
      （前者需要真实 Windows 身份，后者需要 HTTP 端点模拟）。

## 11. 相关文档

- [总体架构](../architecture.md)（依赖矩阵需按 §2.1 更新）
- [新框架用户权限（五角色本地最小实现）](11-authorization.md)
- [Data 模块](03-data.md)
- [App 模块](07-app.md)
- [编码规范 §8 日志](../conventions.md#8-日志)
