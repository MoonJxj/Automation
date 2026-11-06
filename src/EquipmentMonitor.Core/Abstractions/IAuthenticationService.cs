using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 登录门面：按认证方式选择 <see cref="IAuthenticationProvider"/>，成功后建立会话并写审计。
/// </summary>
/// <remarks>
/// <para><b>为什么要有门面而不是让界面直接调 provider</b>：审计、失败日志、会话写入只能有一份实现；
/// 界面各自调用 provider 时，一定会漏掉其中某一步。</para>
/// <para><b>扩展方式</b>：新增认证方式 = 新增 <see cref="IAuthenticationProvider"/> 实现 + 注册，
/// 本接口与调用方均不改。远端权限管理需要的登录流程（取令牌 → 建会话）也只是另一个 provider。</para>
/// </remarks>
public interface IAuthenticationService
{
    /// <summary>已注册的认证方式（按注册顺序，第一项是默认方式）。</summary>
    IReadOnlyList<AuthenticationProviderInfo> Providers { get; }

    /// <summary>默认认证方式：未指定时使用注册顺序的第一项。</summary>
    AuthenticationSource DefaultSource { get; }

    /// <summary>用默认认证方式登录。</summary>
    Task<Result<UserSession>> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>用指定认证方式登录。</summary>
    /// <param name="source">认证方式；未注册时返回失败，不抛异常。</param>
    /// <param name="request">登录请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Result<UserSession>> SignInAsync(
        AuthenticationSource source,
        SignInRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>登出：清空会话并写审计。未登录时是空操作。</summary>
    /// <param name="reason">登出原因（“切换用户”“会话超时”“退出程序”）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SignOutAsync(string reason, CancellationToken cancellationToken = default);
}
