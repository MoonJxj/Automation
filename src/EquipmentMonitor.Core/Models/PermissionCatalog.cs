using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 权限点的显示名。放 Core 是为了让业务异常消息、界面提示与审计记录措辞一致。
/// </summary>
public static class PermissionCatalog
{
    /// <summary>权限点的中文显示名。</summary>
    public static string DisplayName(Permission permission) => permission switch
    {
        Permission.MachineView => "查看设备",
        Permission.MachineOperate => "操作设备",
        Permission.MachineConfig => "修改设备配置",
        Permission.ProcessConfig => "修改工艺参数",
        Permission.UserManage => "用户管理",
        Permission.LogView => "查看日志",
        Permission.AlarmAcknowledge => "确认报警",
        _ => permission.ToString()
    };
}
