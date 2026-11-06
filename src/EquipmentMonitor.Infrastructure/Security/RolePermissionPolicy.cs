using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Infrastructure.Security;

/// <summary>
/// 本地的角色权限表：五个角色，能力单调递增。
/// </summary>
/// <remarks>
/// <para><b>为什么写死在代码里而不是配置</b>：这张表就是本阶段的安全模型本身。
/// 做成运行时配置等于“一次配置失误就能让某个角色在现场突然什么都做不了”，
/// 而那种问题只有到现场才会被发现。远端权限管理接管时，换成本接口的另一个实现即可，
/// 不改任何调用点（见 <c>docs/module-design/11-authorization.md</c>）。</para>
/// <para><b>单调性</b>：后一个角色的权限集合 = 前一个角色的权限集合 + 新增项，
/// 由构造方式保证；测试 <c>RolePermissionPolicyTests</c> 会逐级校验。</para>
/// <para><b>未知角色</b>返回空集合：枚举值来自反序列化/远端时可能不在定义内，必须按拒绝处理。</para>
/// </remarks>
public sealed class RolePermissionPolicy : IPermissionPolicy
{
    private static readonly Permission[] ViewerGrants =
    [
        Permission.MachineView,
        Permission.LogView
    ];

    private static readonly Permission[] OperatorGrants =
    [
        .. ViewerGrants,
        Permission.MachineOperate,
        Permission.AlarmAcknowledge
    ];

    private static readonly Permission[] ProcessEngineerGrants =
    [
        .. OperatorGrants,
        Permission.ProcessConfig
    ];

    private static readonly Permission[] EngineerGrants =
    [
        .. ProcessEngineerGrants,
        Permission.MachineConfig
    ];

    private static readonly Permission[] AdminGrants =
    [
        .. EngineerGrants,
        Permission.UserManage
    ];

    private static readonly IReadOnlyDictionary<UserRole, Permission[]> GrantsByRole =
        new Dictionary<UserRole, Permission[]>
        {
            [UserRole.Viewer] = ViewerGrants,
            [UserRole.Operator] = OperatorGrants,
            [UserRole.ProcessEngineer] = ProcessEngineerGrants,
            [UserRole.Engineer] = EngineerGrants,
            [UserRole.Admin] = AdminGrants
        };

    /// <inheritdoc />
    public IReadOnlyList<Permission> GetGrantedPermissions(UserRole role)
        => GrantsByRole.TryGetValue(role, out var permissions) ? permissions : [];

    /// <inheritdoc />
    public bool IsGranted(UserRole role, Permission permission)
        => GrantsByRole.TryGetValue(role, out var permissions) && Array.IndexOf(permissions, permission) >= 0;
}
