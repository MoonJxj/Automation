# 编码规范

> 规范的目的不是约束风格，而是减少多人协作时的沟通成本与低级错误。
> 本文档只列**本项目特有的、容易出错的**约定；语言级通用规范由 `.editorconfig` 与 Roslyn 分析器强制执行。

## 1. 项目与命名空间

- 解决方案：`AxisFramework.slnx`（唯一入口，含桌面宿主 `EquipmentMonitor.Desktop`）；旧体系
  `EquipmentMonitor.slnx` 与 9 个旧模块已于 2026-11-01 整体移入 Windows 回收站。
- 项目命名：`EquipmentMonitor.<模块名>`，命名空间与项目名一致（不额外追加层级）。
- **文件夹名 = 项目文件名**（含测试项目）。新增项目不得出现 `src/Foo/Foo.Bar.csproj` 这种错位。
- 一个项目一个根命名空间；**禁止**在模块项目内使用与模块名不符的命名空间。
- 程序集版本统一由 `Directory.Build.props` 定义，**不在单个 `.csproj` 中重复写版本号**。

> **命名已统一（2026-11-01）**：`src/EquipmentMonitor.Common/`、`src/EquipmentMonitor.Core/` 的文件夹与
> 项目文件都已加 `EquipmentMonitor.` 前缀，程序集为 `EquipmentMonitor.Common.dll` / `EquipmentMonitor.Core.dll`。
> 遗留项：`src/EquipmentMonitor.Common` 下仍有 20 个文件的命名空间写作 `Common`（历史写法），
> 重构时按 `EquipmentMonitor.Common.*` 迁移；`Core` 的硬件契约（`IAxisController` 等）已统一到
> `EquipmentMonitor.Core.Abstractions`。

### 1.1 文件编码（2026-11-01 强制）

- 源码、项目文件、文档统一 **UTF-8 with BOM**；`.json` 与 `.sh` 用 **UTF-8 无 BOM**（JSON 标准、shell shebang）。
- **禁止**用系统默认编码（GBK/GB18030）读写中文。PowerShell 写文件必须显式：
  `[System.Text.UTF8Encoding]::new($true)`，不要用 `Set-Content -Encoding Default`。
- Visual Studio 在中文系统上会把**无 BOM 的 UTF-8** 按 GB18030 打开，表现为 `鐗堟潈` / `鏂版` 这类乱码；
  这不是文件内容丢失，是编码判定错误。遇到时先看字节，不要直接重新输入中文。
- 每次新增/修改文本文件后运行：

  ```powershell
  powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-encoding.ps1
  ```

  守卫会检查非法 UTF-8、缺少 BOM（`.json`/`.sh` 除外）与替换字符。
- 执行清单与 PowerShell 写文件模板见 DSH 技能 `.dsh/skills/file-encoding/SKILL.md`。
- 已知历史损坏：`docs/handover-axis-framework.md` 含 809 个替换字符（迁移期造成），
  守卫对其只告警、不阻断；重写该文档时应一并修复。


## 2. 目录结构约定

各模块项目内部采用一致的目录语义：

| 目录 | 存放内容 | 是否可含业务逻辑 |
|---|---|---|
| `Abstractions/` | 接口定义（**仅 `Core` 有此目录**） | 否 |
| `Models/` | 数据模型、DTO、配置模型 | 否（纯数据） |
| `Enums/` | 枚举 | 否 |
| `Events/` | 事件参数类型 | 否 |
| `Extensions/` | 扩展方法 | 否（工具） |
| `Constants/` | 常量与魔法值集中定义 | 否 |
| `Services/` 或模块根 | 业务服务实现 | 是 |

## 3. 依赖规则（最重要的约束）

> 完整依赖矩阵见 [architecture.md §3.1](architecture.md#31-依赖矩阵)

**绝对禁止**：

1. ❌ 任何模块反向引用 `App`。
2. ❌ `Core` 或 `Shared` 引用任何其他项目。
3. ❌ `Core` 引用任何第三方 NuGet 包（BCL 除外）。**这条一旦破例，分层就失去意义**。
4. ❌ 业务模块之间形成环（如 `Devices → Communication → Devices`）。
5. ❌ 在 `App` / `Desktop` 之外的任何项目中引用 `System.Windows.Forms`。

第 5 条尤其重要：业务模块必须与 UI 框架解耦，否则无法进行单元测试，
也无法在将来迁移到 WPF / Avalonia 或改为无界面的后台服务。
**`Desktop` 是新框架（`AxisFramework.slnx`）唯一允许引用 WinForms 的宿主**，
与旧 `App` 并列；它与业务模块的关系仍是单向依赖（见 [10-desktop.md](module-design/10-desktop.md) §1）。

**强制手段**：在 CI 中加入依赖检查（可用 `dotnet list reference` 或专门的架构测试库如 `NetArchTest`）。

## 4. 异步编程

- **异步全链路**：一旦某方法内部有异步操作，整条调用链就用 `async`/`await`，禁止混用 `.Result` 或 `.Wait()`。
- 阻塞式等待在 WinForms 中极易造成**死锁**（UI 线程等待后台线程，后台线程等待 UI 线程）。
- 所有可能长时间运行的公共方法**必须接受 `CancellationToken`** 并向下传递：

  ```csharp
  Task<Result<DeviceState>> ReadStateAsync(CancellationToken ct = default);
  ```

- 后台循环使用 `Task.Delay(interval, ct)` 而非 `Thread.Sleep`，前者可被取消。
- 仅在**库代码**（非 UI）中使用 `ConfigureAwait(false)`；WinForms 应用层代码不使用。

## 5. 线程与并发

这是上位机项目最容易出问题的地方，单列一节。

| 场景 | 约定 |
|---|---|
| 更新 UI | 一律 `Control.BeginInvoke`（异步，不阻塞后台线程）。**不要用 `Invoke`**——它会在 UI 线程繁忙时阻塞采集线程 |
| 共享状态 | 优先用 `Interlocked` / `volatile`；需要复合操作时用 `lock` 且锁粒度尽可能小 |
| 通道读写 | **同一 `ICommunicationChannel` 实例上的读写必须串行化**，用 `SemaphoreSlim(1,1)` 或专用单线程队列 |
| 集合跨线程 | 用 `ConcurrentDictionary` / `BlockingCollection`，或复制后传递，禁止直接跨线程遍历 `List<T>` |
| 事件订阅 | 订阅方必须在 `Dispose` 时**取消订阅**，否则造成内存泄漏（上位机长周期运行，这类泄漏最终会耗尽内存） |

**关于 `ICommunicationChannel` 串行化的原因**：Modbus RTU 等串口协议物理上不支持并发请求；
libplctag 的单个 Tag 实例也非线程安全。上层不应假设通道是线程安全的。

## 6. 资源管理

- 上位机需连续运行数周，**任何 `IDisposable` 都必须被释放**：串口、Socket、`SemaphoreSlim`、定时器、`CancellationTokenSource`。
- 长生命周期对象实现 `IDisposable`，由 DI 容器管理生命周期（`Singleton` 由容器负责释放）。
- **禁止**在循环内创建 `HttpClient` / `TcpClient` / `SerialPort` 之类的重资源对象，
  应复用长连接并在断线时重建。

## 7. 错误处理

- 使用 [`Result` / `Result<T>`](module-design/00-shared.md) 表达**预期内的失败**：
  设备离线、校验不通过、Tag 不存在、用户无权限。
- 使用 `throw` 表达**预期外的异常**：空引用、索引越界、配置缺失等属于编程错误的场景。
- **边界兜底**：通讯层的任何异常都不得冒泡到 UI 线程。
  单次采集失败只应记录日志 + 标记通道不健康，由 `CommunicationManager` 按退避策略重连。
- 禁止空的 `catch { }`。捕获后至少记录日志；确实需要忽略时，写明理由注释。

  ```csharp
  catch (IOException ex)
  {
      // 串口在设备断电时抛 IOException，属预期情况，交由重连机制处理
      Log.Warning(ex, "通道 {ChannelName} IO 异常，触发重连", channel.Name);
      return Result.Fail($"通道 {channel.Name} 连接中断", ex);
  }
  ```

## 8. 日志

- 使用**结构化日志**，禁止字符串插值拼接：

  ```csharp
  // ✅ 正确 —— 参数被结构化，可被检索与聚合
  Log.Information("元件 {ElementName} 状态由 {OldState} 迁移至 {NewState}", name, oldState, newState);

  // ❌ 错误 —— 参数被拍平进消息文本，无法检索
  Log.Information($"元件 {name} 状态变为 {newState}");
  ```

- 日志级别使用约定：

  | 级别 | 使用场景 |
  |---|---|
  | `Verbose` | 逐帧的协议报文、寄存器原始值（**默认关闭**，仅现场排障时开启） |
  | `Debug` | 轮询周期内的正常读写、状态机内部迁移 |
  | `Information` | 状态变化、用户登录、配置变更、通道连接/断开 |
  | `Warning` | 单次采集失败、重试、降级、响应缓慢 |
  | `Error` | 通道持续失败、数据解析异常、业务操作失败 |
  | `Fatal` | 不可恢复的错误，进程即将退出 |

- **不要在日志中输出**：用户密码、完整的连接字符串、大块二进制报文。

## 9. 配置

- 通过 `IOptions<T>` 强类型绑定注入，**禁止在业务代码中直接使用 `IConfiguration`**。
- 配置模型类定义在**使用它的模块内**，`App` 在启动时注册绑定。
- 配置项需提供合理的默认值，避免现场因缺少某个字段而无法启动。

## 10. UI 层约定

- 窗体中**不写业务逻辑**。窗体职责仅限：收集输入 → 调用服务 → 渲染结果。
- 业务逻辑下沉到 `Devices` / `Communication` 等模块，使其可被单元测试覆盖。
- 耗时的界面操作（如批量读取 200 个 Tag）必须显示进度并提供取消入口，不得冻结界面。
- 现场环境下主机分辨率差异大，窗体布局需支持 DPI 缩放（`AutoScaleMode.Font`）。
- **窗体一律分部类，保持 VS 设计器可解析**：
  - `X.cs` 只写依赖字段、构造、事件与业务调用；`X.Designer.cs` 放全部控件字段 + `InitializeComponent`；
  - 控件字段只在一处声明，且 **Designer 字段不要 `readonly`**；
  - 无参构造只 `InitializeComponent()`（设计器实例化用），DI 构造写 `: this()`；
  - `InitializeComponent` 内不解析服务、不写循环/IO；导航按钮、命令区、表格列等动态内容放 `.cs`；
  - 守卫：`dotnet test tests\EquipmentMonitor.Desktop.Tests --filter DesignerConventionTests`。
  - 详细流程见技能 `winforms-designer`。

## 11. 测试

- 测试框架：**xUnit** + **FluentAssertions** + **NSubstitute**。
- 测试命名：`方法名_场景_预期结果`，例如 `PollAsync_设备离线_返回失败结果并标记通道不健康`。
- 硬件相关代码通过 `ICommunicationChannel` 接口 Mock，**单元测试不得依赖真实设备**。
- `App`（WinForms）不做自动化 UI 测试（详见 [architecture.md §8](architecture.md#8-测试策略)）。

## 12. 版本管理与包管理

- 采用 **Central Package Management**：所有 NuGet 包版本集中声明在根目录 `Directory.Packages.props`，
  各 `.csproj` 只写 `<PackageReference Include="..." />`，**不写 `Version`**。
- 所有项目共享的编译属性（`TargetFramework`、`Nullable`、`LangVersion`、`TreatWarningsAsErrors` 等）
  集中声明在 `Directory.Build.props`。
- 提交信息遵循 Conventional Commits：`feat:` / `fix:` / `refactor:` / `docs:` / `test:` / `chore:`。
- `bin/`、`obj/`、`.vs/` 必须加入 `.gitignore`。

## 13. 相关文档

- [总体架构](architecture.md)
- [开发计划](roadmap.md)
- [模块设计索引](module-design/)