namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 用户角色 —— 决定“这个人能做什么”。
/// </summary>
/// <remarks>
/// <para><b>五个角色按能力单调递增</b>：高角色拥有低角色的全部权限
/// （<c>Viewer ⊂ Operator ⊂ ProcessEngineer ⊂ Engineer ⊂ Admin</c>，由 <c>RolePermissionPolicy</c> 保证）。
/// 单调性的意义是：升级角色不会让某个已经能做的工作突然做不了。</para>
/// <para><b>数值只用于排序与存储</b>，不是权限位；授权一律查 <c>IPermissionPolicy</c>，
/// 禁止写 <c>role &gt;= UserRole.Engineer</c> 这类比较。</para>
/// <para><b>与旧体系的差异</b>：旧 <c>EquipmentMonitor.Core.Enums.UserRole</c> 是四角色
/// （Viewer/Operator/Engineer/Admin），新框架增加 <see cref="ProcessEngineer"/>，
/// 用来区分「工艺参数」与「设备配置」两类写权限 —— 否则工艺与设备两个岗位无法分权。
/// 两者的统一属于需求变更，见 <c>docs/module-design/11-authorization.md</c>。</para>
/// </remarks>
public enum UserRole
{
    /// <summary>观察员：只看监控画面、报警与日志，不能操作设备。</summary>
    Viewer = 0,

    /// <summary>操作员：设备启停、伺服使能/定位/点动、确认报警；不能改参数。</summary>
    Operator = 1,

    /// <summary>工艺工程师：在操作员基础上可改工艺参数/配方（不碰设备与通道配置）。</summary>
    ProcessEngineer = 2,

    /// <summary>设备工程师：可改设备/通道/轴参数、手动调试与诊断。</summary>
    Engineer = 3,

    /// <summary>管理员：用户与权限管理，并拥有全部权限。</summary>
    Admin = 4
}
