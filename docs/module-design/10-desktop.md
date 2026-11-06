# 10 — 桌面宿主与元件操作界面（EquipmentMonitor.Desktop）

> 状态：**R1 + R2 已交付**（2026-10-29）：宿主 + 主壳 + 只读总览 + 伺服操作台。
> 2026-11-02：自动监控页增加设备状态汇总与机台级启停（§5.7）。
> R3（正式权限/审计、到位观察、地址级预演）见 §7。
> 本文只写 UI 层的职责、线程与装配约定；业务契约见 [01-core.md](01-core.md)，轴控实现见 [06-devices.md](06-devices.md) 与
> [交接文档](../handover-axis-framework.md)。

---

## 1. 职责边界

| 做 | 不做 |
|---|---|
| 导航、布局、渲染快照 | 轮询、协议读写、状态机 |
| 把用户意图转成服务调用（R2） | 命令排队、超时判定、互锁判定（业务层） |
| 权限的**显隐**判断（R2） | 权限的**校验**（业务层 `RequirePermission`） |
| 启动装配与异常兜底 | 任何形式的寄存器/地址计算 |

- 依赖方向：`Desktop → Core`、`Desktop → Infrastructure`。**禁止**任何项目反向引用 `Desktop`。
- `Desktop` 是本仓库**唯一允许**引用 `System.Windows.Forms` 的项目（对应旧的“App 之外禁止 WinForms”约束）。
- 窗体不写业务逻辑：页面只读快照、只发命令，不自己算状态、不自己重试。

---

## 2. 目录结构

| 目录 | 内容 | 说明 |
|---|---|---|
| `Forms/` | 窗体与页面 | 只做渲染与交互 |
| `Controls/` | 可复用 UserControl（如 `AxisOperationPanel`） | 布局 + 交互，同样由 VS 设计器维护 |
| `Monitoring/` | 采集服务、快照模型、快照仓 | **宿主级编排**，不是业务逻辑；元件的读写仍由 `AxisElement` 完成 |
| `Operation/` | 机台级启停控制与设备状态汇总 | 把启停/复位翻译成元件命令，产出 `MachineRunSnapshot` / `DeviceStatus`；不替代 PLC 自动流程与安全回路 |

---

## 3. 线程模型（本层最重要的一条）

```text
后台采集线程（AxisMonitorService，200ms）
  → AxisElement.RefreshAsync（同步控制器调用发生在这里）
  → 构造 AxisMonitorSnapshot（不可变）
  → AxisSnapshotStore.Publish（Volatile 写）

UI 线程（WinForms Timer，500ms / 1s）
  → AxisSnapshotStore.Current（Volatile 读）
  → 渲染表格 / 状态栏
```

**硬约束**：

1. UI 线程**禁止**直接调用 `AxisElement.RefreshAsync` 或任何命令方法。
   `AsyncAxisControllerAdapter` 内部会 `.GetAwaiter().GetResult()`，在 UI 线程调用就是界面冻结。
2. 不在窗体里订阅元件事件；一律“拉快照”。订阅是页面反复开关时的泄漏高发点。
3. 快照不可变、整体替换；UI 不加锁。
4. 定时器只在页面可见时运行（`VisibleChanged`），不可见时停止。

---

## 4. 启动流程

```text
Program.Main
  ApplicationConfiguration.Initialize()
  RegisterGlobalExceptionHandlers()
  BuildHost(args)                         // Host.CreateApplicationBuilder + Serilog + DI
  SignInForm.ShowDialog()                 // 登录：建立会话；取消 → 返回码 3，不初始化硬件
  AxisFrameworkBootstrapper.InitializeAsync()
      ├─ 失败 → MessageBox + 返回码 2（不进入主界面）
      └─ 成功 → AxisSnapshotStore.Attach(elements)
  host.Start()                            // 启动后台采集
  Application.Run(MainForm)
  Shutdown()                              // 停采集 → 写登出审计 → 释放轴元件 → 冲刷日志
```

- 同步阻塞只允许出现在 `Program` 引导期（`GetAwaiter().GetResult()`），与 `docs/conventions.md` 一致。
- **先登录再初始化轴卡**：取消登录不应因为已经初始化硬件而留下副作用；登录实现见
  [11-authorization.md](11-authorization.md)。
- `ContentRootPath = AppContext.BaseDirectory`：避免从快捷方式启动时配置文件找不到。
- 配置文件：`axis-framework.json` 链接自 Infrastructure 的样例，随构建复制到输出根目录（唯一真相源，不在两处各放一份）。

---

## 5. 命名约定

- 项目 / 文件夹：`EquipmentMonitor.Desktop`（文件夹与项目名一致）。
- 命名空间：`EquipmentMonitor.Desktop`、`EquipmentMonitor.Desktop.Forms`、`EquipmentMonitor.Desktop.Monitoring`。
- 页面：`ElementOverviewPage` / `ElementOperationPage` / `AxisOperationPanel`（轴），
  `CylinderOverviewPage` / `CylinderOperationPage` / `CylinderOperationPanel`（气缸）。
- 轴模式组：`_jogGroup` / `_positionGroup` / `_homeGroup` 统一在 `AxisOperationPanel.Designer.cs` 内，坐标由 VS 设计器维护；不再拆子 UserControl。
- 登录：`SignInForm`（启动门禁；角色选择与登录说明见 [11-authorization.md](11-authorization.md)）。
- 控件别名：`AntdXxx` 前缀（阶段十策略 2），别名只在 `.csproj` 里声明。

### 5.2 R2 轴控面板（参照 TIA Portal 轴控制面板）

`AxisOperationPanel` 是 `ElementOperationPage` 弹窗内的 UserControl（弹窗只负责承载与注入权限），布局按现场习惯组织，命令映射如下：

| 界面元素 | 下发命令 | 说明 |
|---|---|---|
| 主控 激活 / 禁用 | 无 | **仅界面使能**，不向设备下发命令；用于防止误操作 |
| 轴 启用 / 禁用 | `MC_Power`（Enable=true/false） | 真正的伺服使能 |
| 命令：点动 | `JogBackward` / `JogForward` | 按住走、松开发 `MC_Halt`；含失焦/关闭看门狗 |
| 命令：定位（绝对） | `MC_MoveAbsolute` | 目标位置来自“目标位置”输入，受软限位参数校验 |
| 命令：定位（相对） | `MC_MoveRelative` | “相对”单选 + “相对位移”输入；从当前位置移动指定距离 |
| 命令：回原点 | `MC_Home` | 危险命令，二次确认 |
| 停止 | `MC_Halt` | 任意模式下可用 |
| 确认 | `MC_Reset` | 仅在故障时可用 |
| 速度 / 加速度 / 加加速度 | 无 | 来自 `AxisConfig`，只读显示；单次命令覆盖需扩展控制器契约 |

状态指示：已启用、已归位、就绪、轴错误、驱动装置错误（绿/灰/红）；
当前值：位置、速度（只读高亮）；信息性消息显示状态或最近一条命令结果（保留 3 秒）；错误消息显示 `LastError`。

### 5.3 气缸控制（R2 扩展）

气缸与轴是**两类元件**，不共用操作台：轴的命令是“位置/速度”，气缸的命令是“伸出/缩回 + 限位反馈”。

| 界面元素 | 下发命令 | 说明 |
|---|---|---|
| 伸出 | `Cylinder_Extend` | 双控先断另一侧再置位；单控置位伸出 |
| 缩回 | `Cylinder_Retract` | 双控先断另一侧再置位；单控断阀靠弹簧复位 |
| 断阀 | `Cylinder_Halt` | 两阀都断开；单控会缩回，双控停在原地（中位机能由阀决定） |
| 复位 | `Cylinder_Reset` | 清除超时/限位矛盾故障 |

- 位置由两个限位推导：伸出到位 / 缩回到位 / 动作中 / 中间或矛盾 / 故障；
- 超过 `actionTimeoutSeconds` 未到位 → 置故障，必须“复位”；
- 配置在 `axis-framework.json` 的 `cylinders` 段（阀输出、限位输入、超时、单/双控）；
- 采集独立：`CylinderMonitorService` 200ms 刷新，`CylinderSnapshotStore` 发布不可变快照；
- 界面：`CylinderOverviewPage`（列表）→ `CylinderOperationPage` 弹窗 → `CylinderOperationPanel`。

**边界**：本类不替代安全回路；急停、安全门、机械硬限位由 PLC/电气兜底。

### 5.4 骨架导航（六模块 + 工位1/工位2）

按现场 HMI 的任务结构固定六个一级模块；骨架阶段先交付导航、权限与工位过滤，具体设备在页面内扩展。

| 导航 | 页面 | 权限 | 当前状态 |
|---|---|---|---|
| 自动监控 | `AutoMonitorPage`（设备状态 + 启停/复位 + 伺服轴 / 气缸两个只读页签） | `MachineView`（启停另需 `MachineOperate`） | **已可用**（2026-11-02）：设备状态汇总、启动/停止/复位轴故障；自动流程/节拍/I-O 待接 |
| 手动操作 | `ManualOperationPage`（工位1/工位2 × 气缸/伺服/机械手） | `MachineView` + 操作时 `MachineOperate` | 已可用；工位过滤后复用操作台弹窗 |
| 功能选择 | `FunctionSelectionPage` | `MachineView`（写操作另需 `MachineConfig`） | **示例已交付**（2026-11-02）：六张功能卡片；工位屏蔽 / 操作帮助已接，其余待接；见 [16-function-selection.md](16-function-selection.md) |
| 参数设置 | `ParameterSettingsPage` | `MachineConfig` | **模板已交付**：按描述符生成通用参数页；当前内存示例源，接真实服务后替换 `IParameterPageSource` |
| 报警信息 | `AlarmInfoPage` | `MachineView` | **已可用**（P0，2026-11-01）：活动报警/历史/确认/筛选；报警服务见 [12-alarm.md](12-alarm.md) |
| 用户管理 | `UserManagementPage` | `UserManage` | **已可用**：账号列表、新建/编辑角色、启停、设置/重置口令、删除；自保护与最后管理员保护（见 [11-authorization.md §8.3](11-authorization.md#83-用户管理页usermanagementpage)） |

- **工位**：`StationCatalog` 固定 `工位1`/`工位2`；轴/气缸配置带 `StationId`，`AxisSnapshot`/`CylinderSnapshot` 透传；
- **手动操作**：`ManualOperationPage` 把 `StationFilter` 设置到复用的 `ElementOverviewPage` / `CylinderOverviewPage`，切工位只改过滤并刷新，不重建页面；
- **机械手**：页签占位，等机械手控制器/点位契约落地；
- **单步**：暂未实现，建议后续做成 `自动/单步/手动` 模式，而不是工位同级入口。

### 5.6 功能选择页

一级导航里的“任务清单”：六张卡片由 `FunctionCatalog` 生成，点击后打开对应对话框/页面。

- **卡片常显、无权限置灰**：导航层已按权限隐藏整页，页内再隐藏会让现场误以为“程序缺功能”；
  置灰卡片显示“无权限”和缺哪个权限点，点击只更新底部状态栏。
- **未接入不打开空窗口**：`Planned` 入口显示“待接入”和缺失的契约，点击只给说明。
- **新增功能**：改 `FunctionCatalog` 一条 + 页面 `switch` 一个分支；不改导航与权限枚举。
- **示例功能**：`StationBypassDialog` 展示“卡片 → 弹窗 → 二次确认 → 服务校验/审计 → 回读”的完整链路；
  服务 `IStationBypassService` 当前为内存实现，接设备时替换实现即可。

### 5.7 自动监控页的设备级启停（R2 扩展）

页面顶部固定一块设备状态条：左侧显示设备状态（待机 / 运行中 / 动作中 / 警告 / 故障 / 停止未完成 / 采集异常）与
轴、气缸、报警摘要；右侧是“启动设备”“停止设备”“复位轴故障”。

| 操作 | 实际命令 | 前置条件 | 状态结果 |
|---|---|---|---|
| 启动设备 | 对每根启用轴 `MC_Power(true)` | 轴已初始化、无故障、无运动、设备未运行；`MachineOperate` | 全部成功 → `Running`；失败时回滚本次新使能的轴，回滚成功 → `Stopped`，回滚也失败 → `Faulted` |
| 停止设备 | 对每根启用轴 `MC_Halt` | `MachineOperate`；停止类命令不因故障被拦截 | 全部成功 → `Stopped`；任一失败 → `Faulted`（界面提示“停止未完成”） |
| 复位轴故障 | 对故障轴 `MC_Reset` | 非 `Running`；`MachineOperate` | 运行状态不变，只清轴故障标志 |

- **状态来源**：`Operation/MachineRunService` 维护运行状态，`Operation/DeviceStatusEvaluator` 汇总轴/气缸/报警/采集；
  页面只渲染，不自己推导状态。`MachineRunService` 的机台级操作另写审计（`MACHINE_RUN_*` / `MACHINE_AXIS_RESET`）。
- **权限**：按钮按 `HasPermission(MachineOperate)` 置灰；服务入口仍 `RequirePermission`，UI 隐藏不等于权限控制。
- **不是 PLC 自动流程**：当前没有自动循环点位，启动只完成伺服使能，不会启动物料/节拍；界面与确认框都明确写出这一限制。
- **停止的边界**：只下发 `MC_Halt`，轴保持使能；断使能仍走操作台的“软件急停”或现场安全回路。
- **状态语义**：这是上位机侧的运行状态，不冒充 Core 的完整 `MachineState`。接入 PLC 自动流程后应改为读回设备状态，
  不能继续靠本地状态位表示“设备正在自动运行”。

### 5.5 通用参数页模板

参数设置页不再为轴/气缸手写表单，而是按 Core 的 `ElementParameterDescriptor` 自动生成：

```
ParameterSettingsPage（通用页面）
├─ 左侧：ParameterTarget 列表
├─ 顶部：重新加载 / 保存 / 更新方式
└─ 右侧：ParameterEditorControl
   ├─ 按 descriptor.Group 生成 GroupBox
   ├─ Bool → CheckBox / Number → TextBox / Text+Choices → ComboBox
   ├─ 显示单位、Scope、ApplyMode、只读标记
   └─ 单参数范围校验 + 脏状态
```

| 文件 | 作用 |
|---|---|
| `Forms/ParameterSettingsPage.cs` / `.Designer.cs` | 页面模板：列表、工具栏、加载/保存流程 |
| `Controls/ParameterEditorControl.cs` / `.Designer.cs` | 通用编辑器：按描述符生成控件，不硬编码字段 |
| `Parameter/IParameterPageSource.cs` | 数据源端口：列表/读取/保存 |
| `Parameter/TemplateParameterPageSource.cs` | 骨架用内存示例源（带提示条，非生产存储） |

约定：

- **UI 不直接依赖轴/气缸服务**：页面只认 `IParameterPageSource`；真实实现包装
  `IAxisParameterService` / `ICylinderParameterService`。
- **描述符是唯一字段来源**：新增参数只扩展 `AxisParameterCatalog` / `CylinderParameterCatalog`，
  页面和编辑器不增加字段。
- **权限**：编辑器用 `HasPermission(descriptor.RequiredPermission)` 逐字段禁用；保存入口仍由服务校验。
- **生效方式**：每行显示 `立即生效 / 下一条命令 / 重启后生效`，保存结果若含 `RestartRequired` 会提示。
- **校验分层**：编辑器只做类型/范围；跨字段规则由服务/`AxisParameterValidator` / `CylinderParameterValidator` 负责。
- **示例数据**：当前 `TemplateParameterPageSource` 明确标注“内存数据，未持久化”；
  接真实服务时替换 `Program.ConfigureServices` 中的 `IParameterPageSource` 注册即可。

### 5.1 分部类与 VS 设计器

每个窗体拆成两个文件：

| 文件 | 内容 |
|---|---|
| `Xxx.cs` | 行为：事件、定时器、数据绑定、命令逻辑 |
| `Xxx.Designer.cs` | `InitializeComponent()` + 控件字段 + 静态布局 |

约定：

1. **无参构造只调用 `InitializeComponent()`**，供 VS 设计器实例化；
   DI 构造写成 `: this()`，复用同一份界面初始化（`ElementOverviewPage` 的无参构造额外加表格列）。
2. **不要在 `InitializeComponent` 里解析服务或写业务调用** —— 设计器会执行它。
3. 设计器可见的是**静态骨架**：
   - `MainForm`：导航面板、内容面板、状态栏；导航按钮在运行期按 `Navigation` 表生成。
   - `ElementOverviewPage`：顶部“打开操作台”工具栏 + 表格；双击行也可打开。
   - `ElementOperationPage`：弹窗壳，只放 `AxisOperationPanel` 并注入权限。
   - `AxisOperationPanel`：主控/轴使能、命令选择、点动/定位/回原点三个模式组、状态、停止、错误/提示全部在
     `AxisOperationPanel.Designer.cs` 内；代码只切 `_jogGroup` / `_positionGroup` / `_homeGroup` 的 `Visible`，
     不写模式控件坐标。
   - `CylinderOverviewPage` / `CylinderOperationPage` / `CylinderOperationPanel`：气缸列表、弹窗壳、气缸操作面板。
   - `UserManagementPage`：工具栏（新建/编辑/启停/口令/删除/刷新）+ 账号表格 + 提示栏。
   - `UserEditDialog` / `PasswordDialog`：新建/编辑账号、设置或清除口令；都是纯输入对话框，不解析服务。
   - `SignInForm`：账号下拉 + 角色说明 + 口令框；账号列表在 `Load` 时从用户目录读取。
4. 改静态布局请改 `*.Designer.cs`；改交互与命令请改 `*.cs`。
5. `.csproj` 已为每个窗体标注 `<SubType>Form</SubType>` 与 `DependentUpon`，
   VS 中可直接“查看设计器”，文件也会折叠在窗体节点下。
6. **控件字段只能声明在 `*.Designer.cs`，且不要加 `readonly`**。
   若 `.cs` 里还留着一份 `private readonly AntdLabel _title = new();`，会与设计器字段重名
   （CS0102；IDE 有时显示为 CS0229 二义性）。改造期间就踩过：VS 旧缓冲区把 `.cs` 存回了旧版本。
   遇到这类报错先确认磁盘上是否只有一份字段；若磁盘已正确，关闭文档/解决方案并删除 `.vs` 缓存后重开。
7. **守卫测试**：`DesignerConventionTests` 会在每次 `dotnet test` 时扫描 `Forms/` 与 `Controls/`，
   校验成对文件、字段归属、唯一 `InitializeComponent`、无参构造与 csproj 元数据。
   流程与模板见技能 [`.claude/skills/winforms-designer/SKILL.md`](../../.claude/skills/winforms-designer/SKILL.md)。


---

## 6. R1 验收记录

| 项 | 结果 | 证据 |
|---|---|---|
| 构建 | 0 错误；33 条空引用警告与基线持平（Desktop 0 警告） | `dotnet build AxisFramework.slnx -t:Rebuild` |
| 测试 | **215 条全绿**（轴框架 156 + Desktop 59；含参数编辑器 4 条、布局回归 1 条、布局源码守卫 1 条） | `dotnet test AxisFramework.slnx` |
| 实跑 | 先出现“登录”窗口；回车后主窗口标题 `设备监控上位机 — 轴控与气缸`；日志无 ERR/FTL | 实跑 + 窗口枚举 + `logs/desktop-*.log` |
| 登录 | 登录成功写 `SIGN_IN_SUCCEEDED`（含操作者/角色/方式）；退出写 `SIGN_OUT` | `AuthenticationServiceTests` / 实跑日志 |
| 用户管理 | 打开页面列出账号；新建用户后列表出现新行；设置口令后显示“已设置”且文件不含明文 | 实跑截图 + `users.json` + `UserManagementPageTests` |
| 参数页模板 | 按描述符生成轴/气缸参数行；单参数范围校验；权限禁用；保存示例源后状态更新 | `ParameterEditorControlTests` + 实跑截图 |
| 配置 | `axis-framework.json` 复制到输出根目录，模拟轴装配成功 | 输出目录文件清单 |
| UI 线程 | 采集与命令都不在 UI 线程执行 | `AxisMonitorService` / `AxisElement.ExecuteCommandAsync` |
| 命令区 | 9 条命令按面板固定布局呈现；相对定位构造 `MC_MoveRelative` 请求 | `AxisOperationPanelTests` |
| 气缸区 | 伸出/缩回/断阀/复位 + 限位/超时故障；面板与元件测试 | `CylinderElementTests` / `CylinderOperationPanelTests` |
| 气缸采集 | 200ms 独立刷新并发布快照；列表位置/限位/故障 | `CylinderOverviewPageTests` |
| 互锁 | 未使能时移动命令置灰并显示原因 | `AxisOperationPanelTests.RefreshView_未使能_提示先启用` |

尚未验证（R3 前补）：

- [ ] 页面反复开关 100 次无内存/句柄增长；
- [ ] 50 根轴 × 200ms 采集下的界面流畅度；
- [ ] 高 DPI（1366×768 / 1920×1080）；
- [ ] 真实设备上点动看门狗（失焦/断连自动停）。

---

## 7. R3 待办（已知缺口，按优先级）

R2 不是“再画一个页面”，而是先把 UI 需要的业务契约补齐；以下为 R2 已做与未做的分界：

| # | 能力 | 现状 | 说明 |
|---|---|---|---|
| 1 | 命令契约 | ✅ 基础版 | `IElementCommandTarget` + 描述符/参数/可用性/结果；9 条命令 |
| 2 | 命令执行 | ✅ 下发 | 控制器调用在 `Task.Run` 中执行，同一元件串行（信号量） |
| 3 | 权限 | ✅ **本地最小实现** | 5 角色（观察员/操作员/工艺/设备/管理员），会话 + 角色权限表；无口令，不是安全边界。入口已调用 `RequirePermission(MachineOperate)`，接口为 Windows/远端认证预留。见 [11-authorization.md](11-authorization.md) |
| 4 | 审计 | ⚠️ 日志版 | `LogAuditService` 写结构化日志并补操作者；未落库、不可查询 |
| 5 | 到位观察 | ❌ | 命令成功返回 `Accepted`（已下发），到位仍由 150ms 状态刷新观察；未实现“等待 Done/超时” |
| 6 | 写入计划（地址+值） | ❌ | 新框架的地址封装在控制器内部，元件不知道寄存器地址；二次确认只显示元件/命令/参数 |
| 7 | 中断/取消 | ❌ | 无命令队列；`CommandAborted` 语义保留但未实现 |
| 8 | 报警文本化 | ✅ **P0 已做** | `AlarmCatalog` 码表 + 报警页文本；轴面板仍显示 `FaultCode` 数字（P1 可统一） |
| 9 | 机台自动流程 | ❌ | 自动监控页的启动/停止只到“伺服使能 / `MC_Halt`”；没有 PLC 自动循环点位，不能声称“自动运行已开始” |

**R3 建议顺序**：审计落库（权限已本地化，见 [11-authorization.md](11-authorization.md)）→ 到位观察（Done/Timeout）→ 报警文本 → 写入计划（需控制器暴露地址映射）。

---

## 8. 已知取舍（R1/R2）

- 总览表格**不做状态配色**：AntdUI 单元格样式 API 尚未探针，宁可先无颜色，也不盲写。
- **不做机台分组**：新框架尚未装配 `Machine`，页面的“机台”列留到机台装配一起做。
- **设备状态是“上位机侧事实”**：`MachineRunService` 只根据启停序列是否成功维护 `Stopped/Running/Faulted`；
  它不是 Core 的完整 `MachineState`，也不读 PLC 的自动运行位。接入自动流程后必须改为回读设备状态。
- **停止不切断动力**：停止序列只下发 `MC_Halt`，轴保持使能；断使能走操作台的“软件急停”或现场安全回路。
  界面的“停止设备”不能写成“断电/安全停止”。
- 采集 200ms、界面 500ms：UI 看到的是**最近一帧**，不是实时值；这是监控界面的正常语义，不得用于安全判断。
- 状态栏每秒刷新一次，不参与告警判定。
- **命令成功 = `Accepted`（已下发）**，不是 `Done`：轴命令的到位由状态刷新观察，
  把已下发显示成“完成”会误导操作工（交接文档 §4.2）。
- **二次确认不含写入地址与值**：整数地址封装在控制器内部，元件拿不到；
  确认框显示元件、命令、参数与后果说明。地址级预演需要控制器先暴露映射（R3）。
- **权限是本地账号最小实现**（口令可选：未设置即工位信任），**审计只写日志**；两者都已有正式接口，
  换成 Windows / 远端认证或落库审计时只替换实现，不改界面与业务调用点。
- **登录先于硬件初始化**：取消登录返回码 3，不初始化轴卡；退出时写一条 `SIGN_OUT` 审计。
- **账号变更在下一次登录生效**：改角色/启停不会即时踢掉当前会话（避免管理员改权限时把正在操作的人降权）。
- **不能把自己锁在门外**：用户管理页禁用当前登录账号的停用/删除；最后一个启用中的管理员同样受保护。
- **演示账号无口令**：首次启动生成 5 个演示账号，现场交付前必须设置口令或接入 Windows/远端认证。
- **本地账号不是安全边界**：它防误操作、留操作者痕迹，不防有意越权 ——
  现场交付前必须接入 Windows 身份或远端权限服务（见 [11-authorization.md §2.3](11-authorization.md#23-安全边界必须如实告知)）。
- 点动看门狗：`MouseLeave` / 窗体失活 / 窗体关闭三路触发 `MC_Halt`；断连由元件拒绝兜底。
- **AntdUI 控件要显式给 `Height`**：`InputNumber`/`Switch`/`Select` 不设高度时可能为 0，
  表现为“标签后面空一块、找不到输入框”。
- **`AntdUI.Table` 的列宽会按 DPI 放大**：写死像素在 150% 缩放下会把最后几列挤出可视区
  （横向滚动条）。当前用 `ColumnsMode.Fill` + 较小基准宽度，并在 150% 下实跑截图核对。
- **`Table.SelectedIndex` 是 1 基**；`CellClick.RowIndex` 带表头偏移，`RowType` 过滤不可靠。
  点击行必须用 `e.Record` 取数据对象 → 反查 0 基行号 → 显式同步 `SelectedIndex`；
  表头/空白区的 `Record` 不是数据行，天然被排除。数据到达时仍默认选中第一行。
- **分部类字段只能出现在 `*.Designer.cs`**：若在 `*.cs` 里保留一份 `private AntdLabel _title = new();`，
  会与设计器字段重名（CS0102）。改造后两个页面曾因手工保留旧字段而编译失败，已修正。
- **AntdUI `Table.SelectedIndex` 是 1 基**（0 = 未选中）；`CellClick.RowIndex` 不能直接当 0 基用，
  **表头/空白区的 `CellClick` 也会触发**（`RowType=None`，`RowIndex` 会给 1）。
  不能靠 `RowType.Column` 过滤：数据行的 `RowType` 并不稳定。
  当前实现：`e.Record` 是数据行对象 → 按引用反查 0 基行号 → `SelectRow(index)`；
  数据到达时**默认选中第一行**（表格重绑后恢复选中）。
