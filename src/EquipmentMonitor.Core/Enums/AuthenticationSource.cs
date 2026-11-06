namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 认证方式 —— 回答“你是通过什么方式进来的”。
/// </summary>
/// <remarks>
/// <para>认证方式与角色正交：同一种认证方式可以对应任意角色，
/// 同一角色也可能通过不同方式进入。会话里两者分开记录，审计才说得清
/// 「谁、用什么方式、以什么身份」。</para>
/// <para><b>当前只实现 <see cref="LocalAccount"/></b>：账号来自本地用户目录，
/// 口令可选（未设置口令的账号按工位信任放行）。其余两项是已定型的扩展位：
/// 新增实现只需实现 <c>IAuthenticationProvider</c> 并注册，
/// 不改 <c>IAuthenticationService</c>、不改业务调用点。</para>
/// </remarks>
public enum AuthenticationSource
{
    /// <summary>
    /// 本地账号：从本地用户目录选择账号登录；账号是否要求口令由目录中的口令哈希决定。
    /// </summary>
    /// <remarks>未设置口令的账号是“工位信任”模式，不是安全边界；
    /// 详见 <c>docs/module-design/11-authorization.md</c> 的“安全边界”一节。</remarks>
    LocalAccount = 0,

    /// <summary>Windows 登录身份（进程身份或域账号）。待实现。</summary>
    WindowsAccount = 1,

    /// <summary>远端认证/权限服务（HTTPS）。待实现。</summary>
    RemoteService = 2
}
