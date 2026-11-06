namespace EquipmentMonitor.Shared.Constants;

/// <summary>
/// 模块名常量。模块名决定日志写入哪个文件，以及 <c>ModuleEnricher</c> 的推导白名单。
/// </summary>
/// <remarks>
/// 这些值与解决方案中的项目名一一对应（见 docs/architecture.md §3.1 依赖矩阵）。
/// 新增模块时必须同步在此登记，否则该模块的日志会全部归入 <see cref="Other"/>。
/// </remarks>
public static class ModuleNames
{
    /// <summary>WinForms 启动项目。</summary>
    public const string App = "App";

    /// <summary>核心抽象层（接口 / 模型 / 枚举）。</summary>
    public const string Core = "Core";

    /// <summary>公共工具与常量。</summary>
    public const string Shared = "Shared";

    /// <summary>日志模块自身。</summary>
    public const string Logging = "Logging";

    /// <summary>认证与权限。</summary>
    public const string Security = "Security";

    /// <summary>通讯协议实现。</summary>
    public const string Communication = "Communication";

    /// <summary>设备元件与状态机。</summary>
    public const string Devices = "Devices";

    /// <summary>数据访问。</summary>
    public const string Data = "Data";

    /// <summary>无法识别来源时的兜底模块名。</summary>
    public const string Other = "Other";

    /// <summary>
    /// 所有已知模块。用作 <c>ModuleEnricher</c> 的推导白名单。
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        App, Core, Shared, Logging, Security, Communication, Devices, Data
    ];
}
