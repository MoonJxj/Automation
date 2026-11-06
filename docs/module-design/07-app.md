# App 模块设计

> 项目：`EquipmentMonitor.App` ｜ 依赖：全部 7 个模块
> 输出类型：`WinExe`，目标框架 `net8.0-windows`（建议迁移 `net10.0-windows`）
> UI 控件库：SunnyUI.NET

## 1. 职责边界

**做什么**：程序入口与宿主构建、DI 注册、配置绑定、窗体与导航、跨线程 UI 更新。

**不做什么**：

- ❌ **不写业务逻辑**。窗体的职责是"收集输入 → 调用服务 → 渲染结果"
- ❌ 不直接访问数据库（经 `Data` 的仓储）
- ❌ 不直接操作通道（经 `IComponentManager` / `ICommunicationManager`）

> **这是本模块最重要的约定**。WinForms 项目最常见的腐化方式，
> 就是把逻辑一点点写进窗体的按钮事件里，最终变成无法测试、无法维护的泥球。
> 判断标准：**如果一段代码无法脱离窗体运行，它就不该写在窗体里。**

## 2. 依赖关系

```
EquipmentMonitor.App ──► Core, Shared, Logging, Security, Communication, Devices, Data
```

NuGet 依赖：`SunnyUI`、`Microsoft.Extensions.Hosting`、`Serilog.Extensions.Hosting`、
`Serilog.Sinks.File`、`Serilog.Sinks.Console`、`Serilog.Sinks.Map`

## 3. 目录结构

```
EquipmentMonitor.App/
├── Program.cs                       # 入口：Host 构建、DI 注册、Serilog 初始化
├── Startup/
│   ├── ServiceRegistration.cs       # 各模块服务的 DI 注册扩展
│   └── ConfigurationBinding.cs      # appsettings.json → IOptions<T>
├── Forms/
│   ├── LoginForm.cs
│   ├── MainForm.cs                  # 导航容器
│   ├── UserManagementForm.cs
│   ├── DeviceConfigForm.cs
│   ├── DeviceDebugForm.cs
│   └── LogViewerForm.cs
├── Controls/                        # 自定义控件（元件状态卡片、通道健康灯等）
├── Helpers/
│   ├── UiDispatcher.cs              # BeginInvoke 封装与节流
│   └── FormNavigator.cs
└── appsettings.json
```

## 4. 启动流程

### 4.1 WinForms 与 Generic Host 的集成

> **这是本模块最容易踩坑的地方**：WinForms 使用 `Application.Run()` 接管消息循环，
> 而 Generic Host 的 `host.Run()` 也会阻塞。**两者不能同时调用**。

正确做法是：**只用 Host 做 DI 与配置，不调用 `Run()`，而用 `Start()` 启动托管服务**。

```csharp
[STAThread]
static void Main()
{
    ApplicationConfiguration.Initialize();          // DPI 与默认字体（必须最先调用）

    var builder = Host.CreateApplicationBuilder();

    // ① 日志必须最先配置 —— 后续任何失败都要能被记录
    builder.Services.AddLoggingConfig(builder.Configuration);
    SerilogConfigurator.Configure(builder.Configuration);   // 全局静态 Log.Logger

    // ② 各模块服务注册
    builder.Services.AddSharedServices();
    builder.Services.AddDataServices(builder.Configuration);
    builder.Services.AddSecurityServices(builder.Configuration);
    builder.Services.AddCommunicationServices(builder.Configuration);
    builder.Services.AddDeviceServices(builder.Configuration);

    // ③ 窗体注册
    builder.Services.AddTransient<LoginForm>();
    builder.Services.AddSingleton<MainForm>();               // 主窗体单例
    builder.Services.AddTransient<UserManagementForm>();
    // ...

    using var host = builder.Build();

    // ④ 启动托管服务（LogCleanupService、DevicePollingService 等 IHostedService）
    //    注意是 Start() 而非 Run()
    host.Start();

    try
    {
        // ⑤ 数据库初始化（建表 / 升级）必须在任何仓储调用之前
        //    失败应给出明确提示并退出，而不是带着坏掉的数据库继续运行
        // ⑥ 登录 → 主窗体
        Application.Run(host.Services.GetRequiredService<MainForm>());
    }
    finally
    {
        // ⑦ 优雅停止：停止轮询、断开通道、冲刷写入队列
        host.StopAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
    }
}
```

### 4.2 启动顺序的约束

各步骤存在依赖关系，顺序不可随意调整：

| 顺序 | 步骤 | 为什么必须在此位置 |
|---|---|---|
| ① | Serilog 配置 | 后续任何步骤出错都要能记录，必须最先 |
| ② | DI 注册 | 所有服务在使用前必须注册 |
| ③ | `host.Start()` | `IHostedService`（轮询、日志清理）需在 UI 前启动 |
| ④ | 数据库初始化 | 登录要查用户表，必须先建表 |
| ⑤ | 登录 → `Application.Run` | 登录未通过不应进入主界面 |
| ⑥ | `StopAsync` | 保证退出时数据不丢、句柄不泄漏 |

**启动失败的展示方式**：数据库初始化失败、配置缺失等启动期错误，
由于此时尚无主窗体，应使用 `MessageBox.Show` 明确提示，
**不要静默退出**——现场人员看到"程序闪退"无从排查。

## 5. DI 生命周期

| 服务 | 生命周期 | 理由 |
|---|---|---|
| `ICommunicationManager` | Singleton | 持有长连接 |
| `ICommunicationChannel` | Singleton | **绝对不能是 Transient**——每次解析都会建立新连接 |
| `IComponentManager` | Singleton | 全局元件注册表 |
| `DevicePollingService` | Singleton（`IHostedService`） | 后台长期运行 |
| `IAuthService` | Singleton | 持有会话状态 |
| 各 `Repository` | Transient | 无状态，每次创建开销极小 |
| `SqliteConnectionFactory` | Singleton | 持有连接串，创建便宜 |
| `MainForm` | Singleton | 唯一主窗体 |
| 其他 `Form` | Transient | 每次打开新实例，关闭即释放 |
| `IChannelBuilder` | Transient | 无状态工厂 |

> ⚠️ **WinForms 中避免使用 Scoped 生命周期**。
> Scoped 依赖请求边界，而 WinForms 没有这个概念。
> 在 Singleton 中注入 Scoped 服务会直接抛出 `InvalidOperationException`。

## 6. 跨线程 UI 更新

事件（`ElementStateChanged`、`ChannelStatusChanged`）在**后台线程**触发，
UI 更新必须切回 UI 线程。统一通过 `UiDispatcher` 封装：

```csharp
public static class UiDispatcher
{
    /// <summary>异步切回 UI 线程（不阻塞调用方）。</summary>
    public static void Post(Control control, Action action);

    /// <summary>带节流的 Post：合并高频更新，避免 UI 被刷爆。</summary>
    public static void PostThrottled(Control control, string key, Action action, int intervalMs = 200);
}
```

**必须遵守的规则**：

1. **用 `BeginInvoke` 而非 `Invoke`**。`Invoke` 是同步的，会阻塞后台线程等待 UI 处理完；
   在 UI 繁忙时（如正在渲染大量数据）会**反压到轮询线程**，拖慢采集。
2. **高频事件必须节流**。`PostThrottled` 按 key 合并，
   保证 200ms 内同一 key 只刷新一次。不做节流的实时状态界面在元件数量多时 CPU 占用极高。
3. **窗体关闭后不得再 Post**。`BeginInvoke` 在控件句柄已销毁时会抛
   `InvalidOperationException`。`UiDispatcher` 内部需检查 `IsHandleCreated && !IsDisposed`，
   或在窗体 `FormClosing` 时取消所有事件订阅。

## 7. 窗体设计

### 7.1 导航容器（MainForm）

- `MainForm` 作为容器，通过 SunnyUI 的 `UIPage`/`UITabControl` 承载功能页。
- **各功能页的窗体应延迟创建**（首次切换时才实例化），
  避免启动时一次性创建所有窗体导致启动缓慢。
- 按 `SecurityLevel` 隐藏无权限的页签——但**业务层的权限校验不可省略**
  （见 [Security §4.5](04-security.md#45-权限校验的位置关键约定)）。

### 7.2 各窗体要点

| 窗体 | 关键点 |
|---|---|
| `LoginForm` | 认证失败需区分"凭据错误"与"服务不可达"；失败次数达阈值时提示锁定；**回车即登录** |
| `UserManagementForm` | 密码字段用 `PasswordChar`；修改密码时**不得**回显原密码；删除用户需二次确认 |
| `DeviceConfigForm` | 点位映射编辑（语义 ↔ 地址）；**保存前校验地址合法性**；修改后是否需重启采集要明确提示 |
| `DeviceDebugForm` | 手动读写点位，是现场调试的核心工具；**写操作必须二次确认**（误写寄存器可能引发设备动作，有安全风险） |
| `LogViewerForm` | 订阅 `InMemoryLogSink.EntriesAppended`；支持级别/模块/关键字过滤；自动滚动仅在用户位于底部时启用 |

> ⚠️ **`DeviceDebugForm` 的安全提示**：手动写入 PLC 寄存器会直接导致现场设备动作
> （气缸伸出、伺服移动）。该功能应限制在 `Operator` 及以上等级，
> 且写操作前必须显示确认对话框，内容包含**目标地址与将要写入的值**。

### 7.3 SunnyUI 的早期验证

> **风险提示**：SunnyUI 在 .NET 8 下的 DPI 缩放与主题兼容性需在 M5 早期验证。
> 不要等所有窗体写完才发现控件库与目标框架存在兼容问题。
> 建议第一步只做"一个空白 `UIPage` + 一个 `UIDashboard`"，确认显示正常后再铺开。

## 8. 配置绑定

```csharp
public static class ConfigurationBinding
{
    public static IServiceCollection AddModuleOptions(
        this IServiceCollection services, IConfiguration config);
}
```

各模块的配置模型定义在**各自模块内**，`App` 负责绑定：

```jsonc
{
  "Serilog":      { /* 见 Logging 模块 */ },
  "Database":     { "FilePath": "data/equipment.db", "BusyTimeoutMs": 5000 },
  "Security":     { /* 见 Security 模块 */ },
  "Communication":{ "Channels": [ /* 通道列表 */ ] },
  "Devices":      { "DefaultPollingIntervalMs": 200, "LoadConfigOnStartup": true }
}
```

**约定**：配置一律通过 `IOptions<T>` 注入，**禁止在业务代码中直接读 `IConfiguration`**。

## 9. 优雅退出

上位机被直接关闭时若处理不当，会出现数据丢失与句柄泄漏。退出流程必须覆盖：

| 步骤 | 动作 | 不做的后果 |
|---|---|---|
| 1 | 停止 `DevicePollingService` | 轮询线程继续访问已释放的资源 |
| 2 | 冲刷 `IDeviceInfoRepository` 的写入队列 | 最后一批数据丢失 |
| 3 | 断开并释放所有 `ICommunicationChannel` | 串口/Socket 句柄泄漏，下次启动可能"端口被占用" |
| 4 | 关闭 Serilog（`Log.CloseAndFlush()`） | 最后几条日志（往往正是崩溃原因）丢失 |
| 5 | 释放 Host | — |

**实现方式**：在 `MainForm` 的 `FormClosing` 中触发，
或在 `finally` 块中通过 `host.StopAsync(...)` 统一处理。
**同时必须处理**：`Application.ThreadException`（UI 线程异常）与
`AppDomain.CurrentDomain.UnhandledException`（其他线程异常与未捕获异常），
前者可提示用户并继续，后者只能记录日志。

## 10. 扩展点

| 需求 | 做法 |
|---|---|
| 新增功能页 | 新建 `Form`，在 `ServiceRegistration` 注册，在 `MainForm` 的导航中挂载 |
| 新增自定义控件 | 放到 `Controls/`，尽量设计为**无状态**（数据由外部注入），便于复用与测试 |
| 更换 UI 框架 | 业务逻辑已下沉到各模块，理论上只需重写 `App` 项目。**这也是不让业务逻辑进窗体的现实价值** |

## 11. 测试与验收

`App` **不做自动化 UI 测试**（投入产出比低）。改为：

- **业务逻辑下沉**：所有可测逻辑放在 `Devices` / `Communication` / `Security` 中，由各自的测试项目覆盖
- **手工验收清单**（M5 里程碑）：

  - [ ] DI 容器正确解析所有服务，无 `InvalidOperationException`
  - [ ] 登录 → 主界面 → 各功能页切换正常
  - [ ] 按安全等级隐藏功能页，且**绕过 UI 直接调用服务时权限仍生效**
  - [ ] 后台事件更新 UI 全部走 `BeginInvoke`，无跨线程异常
  - [ ] 高负载轮询下界面不卡顿
  - [ ] 关闭主窗体后进程干净退出（任务管理器中无残留）
  - [ ] 重复开关窗体 100 次无内存增长（验证事件订阅已正确取消）

## 12. 待细化事项

- [ ] `MainForm` 的导航形态（左侧菜单 + 内容区 / 顶部页签 / 停靠面板）
- [ ] 主监控界面的呈现方式（列表 / 卡片 / 产线平面示意图）
- [ ] 是否需要"启动即自动登录"（现场单机场景常见，需权衡安全）
- [ ] 界面刷新频率的全局上限（保护 CPU）
- [ ] 多显示器与高 DPI 的适配验证
- [ ] 是否需要系统托盘最小化与后台运行

## 13. 相关文档

- [总体架构](../architecture.md)
- [编码规范](../conventions.md)
- [Logging 模块](02-logging.md)
- [Security 模块](04-security.md)
