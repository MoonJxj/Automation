using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 一种认证方式：把 <see cref="SignInRequest"/> 变成已认证的 <see cref="UserSession"/>。
/// </summary>
/// <remarks>
/// <para><b>这是“接入未来认证方式”的唯一扩展点</b>。新增本地口令 / Windows / 远端认证时，
/// 只需实现本接口并在 DI 里注册，<see cref="IAuthenticationService"/> 与所有业务调用点都不用改。</para>
/// <para><b>实现约束</b></para>
/// <list type="bullet">
///   <item>失败返回 <see cref="Result.Fail{T}"/>（凭据错误、账号禁用、服务不可达都是预期内失败），
///   不要用异常表达；异常只留给实现自身的编程错误。</item>
///   <item><b>不泄露账号是否存在</b>：凭据型实现应对“用户不存在”与“口令错误”返回同一个提示。</item>
///   <item><b>角色来源必须可信</b>：只有本地简单模式可以采信 <see cref="SignInRequest.RequestedRole"/>；
///   其余实现必须由凭据或服务端决定角色。</item>
///   <item>禁止记录 <see cref="SignInRequest.Credential"/>；<see cref="SignInRequest.ToString"/> 已脱敏，
///   但实现也不应把请求对象整体序列化进日志。</item>
/// </list>
/// </remarks>
public interface IAuthenticationProvider
{
    /// <summary>认证方式标识，与 <see cref="SignInRequest"/> 一起决定由谁处理。</summary>
    AuthenticationSource Source { get; }

    /// <summary>界面显示名，例如“本地角色登录”。</summary>
    string DisplayName { get; }

    /// <summary>
    /// 当前环境是否可用。不可用时登录界面应置灰该项，而不是点了再报错。
    /// </summary>
    /// <remarks>例：Windows 认证在非 Windows 平台或取不到身份时返回 <c>false</c>，不抛异常。</remarks>
    bool IsAvailable { get; }

    /// <summary>
    /// 执行认证。
    /// </summary>
    /// <param name="request">登录请求。</param>
    /// <param name="cancellationToken">取消令牌（远端认证必须支持）。</param>
    /// <returns>成功时返回会话快照（尚未写入会话持有者）；失败时返回可读原因。</returns>
    Task<Result<UserSession>> AuthenticateAsync(
        SignInRequest request,
        CancellationToken cancellationToken = default);
}
