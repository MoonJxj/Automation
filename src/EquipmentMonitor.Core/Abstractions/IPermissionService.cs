using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 权限校验。业务入口用 <see cref="RequirePermission"/>（失败抛异常），UI 只用
/// <see cref="HasPermission"/> 决定显隐 —— UI 隐藏不等于权限控制。
/// </summary>
/// <remarks>
/// <para><b>为什么 RequirePermission 抛异常而不是返回 Result</b>：越权是调用方的编程错误
/// （界面没拦住），不是设备状态；抛异常能保证“忘了判断”不会被静默吞掉。</para>
/// <para><b>判定来源</b>：当前会话（<see cref="ISessionProvider"/>）+ 角色权限表
/// （<see cref="IPermissionPolicy"/>）。两个接口都可以替换实现，本接口的语义不变。</para>
/// <para><b>安全默认值：没有会话 = 全部拒绝</b>。不要在未登录时给任何兜底权限 ——
/// 那会让“忘了登录”变成“以某个默认角色进入现场”。</para>
/// </remarks>
public interface IPermissionService
{
    /// <summary>当前会话是否具备该权限。</summary>
    bool HasPermission(Permission permission);

    /// <summary>
    /// 要求权限；不具备时抛 <see cref="UnauthorizedAccessException"/>。
    /// </summary>
    /// <param name="permission">所需权限点。</param>
    /// <exception cref="UnauthorizedAccessException">
    /// 未登录，或当前角色未被授予该权限。异常消息可直接展示给用户。
    /// </exception>
    void RequirePermission(Permission permission);
}
