using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>新建用户的请求。</summary>
/// <remarks>新账号默认启用且不设口令（工位信任）；口令由管理员随后单独设置。</remarks>
public sealed record CreateUserRequest
{
    /// <summary>登录账号名。</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>显示名。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>角色。默认最小权限（观察员）。</summary>
    public UserRole Role { get; init; } = UserRole.Viewer;
}
