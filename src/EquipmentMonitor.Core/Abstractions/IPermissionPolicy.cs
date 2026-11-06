using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 角色到权限点的映射（授权决策的唯一来源）。
/// </summary>
/// <remarks>
/// <para><b>为什么单独抽出策略接口</b>：当前本地实现是写死在代码里的五角色表；
/// 远端权限管理的下一步是“角色由服务端下发、权限表可在线更新”。
/// 那属于换实现，不应改 <see cref="IPermissionService"/> 或任何业务调用点。</para>
/// <para><b>实现方要求</b>：纯查询、无副作用、任意线程可调用；
/// 远端实现必须在本地缓存授权结果，不能把网络调用放进这个同步接口。</para>
/// </remarks>
public interface IPermissionPolicy
{
    /// <summary>角色拥有的权限点集合。未知角色返回空集合（拒绝）。</summary>
    IReadOnlyList<Permission> GetGrantedPermissions(UserRole role);

    /// <summary>角色是否被授予某个权限点。</summary>
    bool IsGranted(UserRole role, Permission permission);
}
