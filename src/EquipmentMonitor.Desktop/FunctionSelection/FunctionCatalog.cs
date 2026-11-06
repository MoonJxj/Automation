using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Desktop.FunctionSelection;

/// <summary>
/// 功能选择页的目录：六个规划功能只在这里列一次。
/// </summary>
/// <remarks>
/// <para><b>新增功能只改这里</b>：加一条 <see cref="FunctionEntry"/>，页面自动生成卡片；
/// 若要真正打开界面，再在 <c>FunctionSelectionPage</c> 的分发方法里按 <see cref="FunctionEntry.Key"/>
/// 加一个分支，不需要改导航与权限点。</para>
/// <para><b>权限</b>：这里写的是“打开 / 执行”所需权限；页面只用它决定卡片外观，
/// 真正的校验仍在服务入口的 <c>RequirePermission</c>。</para>
/// <para><b>待接入</b>：<see cref="FunctionAvailability.Planned"/> 的条目必须写清楚缺什么契约，
/// 避免现场把“没做”误认为“坏了”。</para>
/// </remarks>
public static class FunctionCatalog
{
    /// <summary>工位屏蔽。</summary>
    public const string StationBypassKey = "station-bypass";

    /// <summary>前后工序对接。</summary>
    public const string LineHandshakeKey = "line-handshake";

    /// <summary>换型配方。</summary>
    public const string RecipeKey = "recipe-change";

    /// <summary>辅助功能（清洗 / 吹气 / 润滑）。</summary>
    public const string AuxiliaryKey = "auxiliary";

    /// <summary>操作帮助。</summary>
    public const string HelpKey = "operation-help";

    /// <summary>设备维护。</summary>
    public const string MaintenanceKey = "maintenance";

    /// <summary>全部功能入口（顺序即卡片顺序）。</summary>
    public static IReadOnlyList<FunctionEntry> All { get; } =
    [
        new(
            StationBypassKey,
            "工位屏蔽",
            "临时让自动循环跳过某个工位；必须填写原因并写入审计。",
            Permission.MachineConfig,
            FunctionAvailability.Ready),
        new(
            LineHandshakeKey,
            "前后工序对接",
            "与上下游设备交换允许 / 完成信号；需先定义握手点位。",
            Permission.MachineOperate,
            FunctionAvailability.Planned),
        new(
            RecipeKey,
            "换型配方",
            "选择产品配方并下发到控制器；需配方存储与下载契约。",
            Permission.ProcessConfig,
            FunctionAvailability.Planned),
        new(
            AuxiliaryKey,
            "辅助功能",
            "清洗、吹气、润滑等维护动作；需现场定义动作点位。",
            Permission.MachineOperate,
            FunctionAvailability.Planned),
        new(
            HelpKey,
            "操作帮助",
            "查看各模块用途、权限说明与现场注意事项。",
            Permission.MachineView,
            FunctionAvailability.Ready),
        new(
            MaintenanceKey,
            "设备维护",
            "保养计数、点检记录与易损件寿命；需维护数据源。",
            Permission.MachineConfig,
            FunctionAvailability.Planned)
    ];

    /// <summary>按标识查找；未知标识返回 <c>null</c>。</summary>
    public static FunctionEntry? Find(string key)
    {
        foreach (var entry in All)
        {
            if (entry.Key == key)
            {
                return entry;
            }
        }

        return null;
    }
}
