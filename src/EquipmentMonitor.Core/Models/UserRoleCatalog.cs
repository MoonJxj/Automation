using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 角色的显示名与一句话职责。放 Core 是为了让界面、审计与日志用同一套措辞。
/// </summary>
public static class UserRoleCatalog
{
    /// <summary>全部角色，按能力从低到高排列（登录界面的下拉顺序）。</summary>
    public static IReadOnlyList<UserRole> All { get; } =
    [
        UserRole.Viewer,
        UserRole.Operator,
        UserRole.ProcessEngineer,
        UserRole.Engineer,
        UserRole.Admin
    ];

    /// <summary>角色的中文显示名。</summary>
    public static string DisplayName(UserRole role) => role switch
    {
        UserRole.Viewer => "观察员",
        UserRole.Operator => "操作员",
        UserRole.ProcessEngineer => "工艺工程师",
        UserRole.Engineer => "设备工程师",
        UserRole.Admin => "管理员",
        _ => role.ToString()
    };

    /// <summary>一句话职责，用于登录界面解释“选这个角色意味着什么”。</summary>
    /// <remarks>刻意保持短句：登录窗与编辑对话框的说明区只有一行高度。</remarks>
    public static string Describe(UserRole role) => role switch
    {
        UserRole.Viewer => "只读：查看状态、报警与日志。",
        UserRole.Operator => "启停设备、伺服使能/定位/点动、确认报警。",
        UserRole.ProcessEngineer => "调整工艺参数与配方。",
        UserRole.Engineer => "改设备参数、手动调试与诊断。",
        UserRole.Admin => "用户管理，并拥有全部权限。",
        _ => string.Empty
    };
}
