using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>编辑用户的请求：账号名不可改，只能改显示名与角色。</summary>
/// <remarks>启用/停用、口令是独立操作，避免“编辑资料时顺手改了别人的权限/口令”。</remarks>
public sealed record UpdateUserRequest
{
    /// <summary>要修改的账号名。</summary>
    public required string UserName { get; init; }

    /// <summary>新的显示名。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>新的角色。</summary>
    public UserRole Role { get; init; } = UserRole.Viewer;
}
