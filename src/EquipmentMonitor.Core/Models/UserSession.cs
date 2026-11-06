using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 当前登录会话的不可变快照：谁、以什么角色、通过什么方式进来、何时进来、何时过期。
/// </summary>
/// <remarks>
/// <para><b>不可变</b>：会话会被 UI 线程与后台线程同时读取；可变对象会出现
/// 「检查通过 → 降权 → 操作执行」的窗口期。</para>
/// <para><b>不含任何凭据</b>：本类型会被写进日志与审计，密码/令牌不进入这里；
/// <see cref="ToString"/> 因此可以原样记录。</para>
/// </remarks>
public sealed record UserSession
{
    /// <summary>登录名。本地简单模式下是操作者填写的姓名。</summary>
    public required string UserName { get; init; }

    /// <summary>界面显示名（可以是姓名/工号）。</summary>
    public required string DisplayName { get; init; }

    /// <summary>角色：决定权限集合。</summary>
    public required UserRole Role { get; init; }

    /// <summary>认证方式：决定这个会话的“可信度”来源，便于审计与将来按方式限制等级。</summary>
    public required AuthenticationSource Source { get; init; }

    /// <summary>登录时间。</summary>
    public DateTimeOffset SignedInAt { get; init; } = DateTimeOffset.Now;

    /// <summary>
    /// 过期时间；<c>null</c> 表示不过期。
    /// </summary>
    /// <remarks>本地简单模式不设过期（监控画面常整班挂着）；远端令牌过期后由认证实现填写。</remarks>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>会话是否已过期。</summary>
    public bool IsExpired => ExpiresAt is { } expiresAt && DateTimeOffset.Now >= expiresAt;

    /// <inheritdoc />
    public override string ToString()
        => $"UserSession(User={UserName}, Role={Role}, Source={Source}, SignedInAt={SignedInAt:O})";
}
