# EquipmentMonitor

.NET 8（`net8.0` / App 为 `net8.0-windows`）WinForms 工业设备监控上位机：多协议接入 PLC / 伺服 / 气缸 / 传感器，
实时状态采集 + 协议调试 + 设备配置 + 用户权限。**文档驱动开发**——`docs/` 是规格，代码是它的实现。

> 本文件是**常驻上下文**，只写永远为真的硬约束与索引；流程与细节在技能里——
> DSH 自动加载 `.dsh/skills/`；`.claude/skills/` 是 Claude Code 约定（历史四项）。
> 本目录**不是 git 仓库**（无 `.git`），不要执行 git 操作。

## 仓库布局

> **2026-11-01 清理**：旧体系（`EquipmentMonitor.slnx`、9 个 `src/EquipmentMonitor.*` 模块、
> 6 个旧 xUnit 测试项目）已整体移入 Windows 回收站，仓库只剩下面这套新框架。
> `docs/` 里描述旧模块的章节（`module-design/00–07`、`architecture.md` 依赖矩阵、`09-ui-migration.md` 等）
> 尚未改写，读到时按“历史记录”理解；现行约束、缺陷与路线以 `docs/handover-axis-framework.md` 为准。

- `AxisFramework.slnx` —— **唯一**解决方案与构建入口（4 个 src 项目 + 2 个测试项目 + 1 个外部适配样例），当前 0 错误
- `src/EquipmentMonitor.Common/`、`src/EquipmentMonitor.Core/`、`src/EquipmentMonitor.Infrastructure/` —— 新框架三项目（轴控开发框架）
- `src/EquipmentMonitor.Desktop/` —— 新框架桌面宿主（WinForms + AntdUI；R1 总览 + R2 伺服/气缸操作 + 设备状态/启停 + 通用参数页模板）
- `tests/EquipmentMonitor.AxisFramework.Tests/` —— 新框架测试（163 条，覆盖模拟轴/轴元件/气缸/寄存器/机台/命令契约/报警服务/五角色权限/本地用户目录/轴与气缸参数契约/领域校验/工位屏蔽服务）
- `tests/EquipmentMonitor.Desktop.Tests/` —— 桌面宿主测试（79 条：容器装配、采集快照、轴控/气缸面板、设备状态与机台启停、工位过滤、选中绑定、权限/登录、用户管理页与对话框、报警页与采集、参数编辑器、功能选择页、设计器分部类守卫）
- 根目录 `Directory.Build.props`（统一 `TargetFramework` / `Nullable` / **`TreatWarningsAsErrors`**）
  与 `Directory.Packages.props`（Central Package Management，**所有包版本只在这里**）

> 接手新框架时不要把旧体系的约束生搬硬套（尤其是“`Core` 零包”——新 `Core` 通过 `Common`
> 传递依赖第三方包，见 `docs/handover-axis-framework.md` §3.1）。

## 构建 / 测试 / 运行（从仓库根执行）

```bash
dotnet build AxisFramework.slnx -t:Rebuild   # 唯一入口：当前 0 错误 / 33 条空引用警告
dotnet test  AxisFramework.slnx              # 当前 242 条全绿（轴框架 163 + 桌面 79）
dotnet run --project src/EquipmentMonitor.Desktop/EquipmentMonitor.Desktop.csproj   # 启动 GUI（模拟轴）
```

## 硬约束（永不违反）

1. **`Core` 零 `<PackageReference>`**（BCL 除外）；`Shared` 零依赖；`Core → Shared` 是唯一被批准的边。
2. 业务模块禁止反向引用 UI 宿主、禁止成环；**`Desktop` / `App` 之外禁止 `System.Windows.Forms`**。
3. `Logging` 不依赖 `Data`（日志不落库）；`UserRepository` 等数据访问接口只在 `Data/Repositories/`。
4. 包版本只写在 `Directory.Packages.props`；新增包必须**实读 nuspec/包内 LICENSE** 且为
   MIT / Apache-2.0 / BSD 级别，拒绝理由写进注释。**FluentAssertions 锁 7.2.2，禁升 8.x**（商业授权）。
5. 接口下沉：服务接口在 `Core/Abstractions`，实现在所属模块，`App` 绑定 DI。
6. 通道只经 `CommunicationManager` + `ChannelId` 访问；禁止 `new` 协议客户端（唯一例外是 DI 工厂 `ChannelBuilders`）。
7. 权限：业务方法入口必须 `RequirePermission(...)`（失败抛异常）；UI 只用 `HasPermission(...)` 判断显隐。
8. 配置用 `IOptions<T>`；业务代码禁止直接读 `IConfiguration`（Options 校验器是唯一豁免）。
9. 预期内失败用 `Result.Fail`，编程错误才 `throw`；同步阻塞（`.Result`/`.Wait()`）只允许在 `Program` 引导期。
10. **文本文件统一 UTF-8 with BOM**（`.json` / `.sh` 例外，用 UTF-8 无 BOM）；禁止用系统默认编码（GBK/GB18030）写中文。新增/修改后跑 `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-encoding.ps1`，详见 `docs/conventions.md §1.1`；写文件前加载 `.dsh/skills/file-encoding/SKILL.md`。

## 代码约定（细节见 `docs/conventions.md` → 技能 `coding-standards`；窗体见技能 `winforms-designer`）

异步全链路 + `CancellationToken` 向下传；结构化日志且必带 `SourceContext`（禁裸 `Serilog.Log.*`）；
UI 更新一律 `BeginInvoke`；通道实例读写串行化；`IDisposable` 必释放、事件订阅必退订；
跨线程传不可变快照；窗体不写业务逻辑。

## 模块边界

分层图与依赖矩阵：`docs/architecture.md §3`。
**实际依赖以各 `.csproj` 的 `ProjectReference` 为准**——文档矩阵已滞后（无 `Mes` 列、缺 `Devices → Data`），
发现漂移要**记录进文档**，绝不按旧表删真实引用。

## 变更流程

开工先读 `docs/module-design/<NN>-<模块>.md`；契约变更必须传播到所有实现方、消费方与文档；
完工跑三段验证（构建 0 警告 → 全量测试 ≥ 基线 → 依赖不变量）并做文档销账。详见技能 `change-workflow`。

## 文档地图

| 文档 | 管什么 |
|---|---|
| `docs/architecture.md` | 分层、依赖矩阵、技术选型与许可证结论、线程模型、待决问题 |
| `docs/conventions.md` | 编码规范（依赖 / 异步 / 并发 / 资源 / 日志 / 配置 / 测试） |
| `docs/module-design/00–07` | 各模块职责边界、接口契约、扩展点、待细化事项 |
| `docs/module-design/09-ui-migration.md` | 阶段十 AntdUI 替换作业指导（当前 1/8，含逐窗体检查清单） |
| `docs/module-design/10-desktop.md` | **新框架桌面宿主**：UI 职责边界、线程模型、启动流程、R1/R2 验收与 R3 缺口 |
| `docs/module-design/11-authorization.md` | **新框架用户权限**：5 角色/权限矩阵、本地账号与用户管理、认证/会话/授权接口、Windows/远端扩展路线与安全边界 |
| `docs/module-design/13-axis-parameters.md` | **轴参数设计**：参数描述符/服务/存储/控制器能力、权限与生效方式 |
| `docs/module-design/14-external-axis-adapter.md` | **外部轴适配者接入**：只依赖 Core 实现 `IAxisController`、错误语义与样例 |
| `docs/module-design/15-cylinder-parameters.md` | **气缸参数设计**：与轴共用 `ElementParameter*`，IO/超时/权限与生效方式 |
| `docs/module-design/16-function-selection.md` | **功能选择页**：六入口目录、卡片权限置灰、工位屏蔽示例（权限/审计/内存实现边界） |
| `docs/roadmap.md` | M0–M6 里程碑与验收标准 |
| `docs/SRS.md` | 需求基线（功能/性能/接口/权限/验收；§15.1 需求追踪矩阵 = 唯一对账表） |
| `docs/ChangePlan.md` | 本次变更：元件操作接口 + HMI 替代；里程碑 A–G、决策 D1–D8、开发计划表（§5.1 WBS） |
| `docs/axis-command-interface.md` | 上位机 ↔ PLC 轴控寄存器契约（**草案**，双方评审后定稿） |
| `docs/handover-axis-framework.md` | **新轴控框架交接**：现状、契约、缺陷、路线与待决问题 |

部分文档滞后于代码（`README.md` 状态行、`03-data.md` 表结构、`05-communication.md §7.1`、
`06-devices.md §12`、`09-ui-migration.md §1`）。**以代码和 `.csproj` 为准**，核实后把偏差记入文档。

## 技能

> **DSH 自动加载 `.dsh/skills/`**（本项目唯一会被 DeepSeek Harness 扫描的技能目录，rank 100）；
> `.claude/skills/` 是 Claude Code 的约定，DSH **不扫描**，目前保留历史四项与 `winforms-designer` 的指针。
> 新增技能请放 `.dsh/skills/<name>/SKILL.md`；若同时服务 Claude Code，在 `.claude/skills/` 放一个指向它的指针，不复制内容。

| 技能 | 路径 | 何时用 |
|---|---|---|
| `winforms-designer` | `.dsh/skills/` | **新建/修改任何窗体、`*.Designer.cs`、布局/控件，或出现 CS0102/CS0111/CS0229 时** |
| `file-encoding` | `.dsh/skills/` | **新建/修改任何文本文件、中文乱码、编码检查失败时** |
| `coding-standards` | `.claude/skills/` | 写 / 改 / 评审任何 `.cs` 文件时 |
| `dependency-rules` | `.claude/skills/` | 增删改项目引用、NuGet 包、`Directory.*.props`、slnx 时 |
| `module-contract` | `.claude/skills/` | 跨模块调用、`Core` 接口、DI 注册与生命周期、配置面 / 事件面变更时 |
| `change-workflow` | `.claude/skills/` | 开始或收尾一个功能 / 重构 / 契约变更时 |

## 交流风格（用户偏好，2026-11-01 记录）

- 中文回答；**结论先行** → 证据（命令 / 输出 / 文件路径）→ 下一步。
- 保留少量人味：可以用短句、可以直接认错；但不铺陈情绪、不写「希望对你有帮助」这类套话、不把自己的输出问题抛回给用户做选择题。
- 分析要**客观理性**：分清「观察到的事实」与「我的推测」；不确定就说不确定，不为了圆场编解释。
- 被指出问题时先修正事实，不辩解；出了错按「哪一步错 / 影响什么 / 怎么补」交代。
