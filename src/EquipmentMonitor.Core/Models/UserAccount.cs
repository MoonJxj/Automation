using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 用户账号的只读快照（不含口令哈希）。
/// </summary>
/// <remarks>
/// <para><b>绝不携带口令哈希</b>：本类型会出现在界面、审计与日志里。
/// 口令是否已设置用 <see cref="HasPassword"/> 表达，校验只发生在用户目录内部。</para>
/// <para><b>角色由管理员在用户管理页分配</b>，登录时以此为准；
/// 客户端不能再“自选角色”。</para>
/// </remarks>
public sealed record UserAccount
{
    /// <summary>登录账号名（唯一，大小写不敏感）。</summary>
    public required string UserName { get; init; }

    /// <summary>显示名（姓名 / 工号）。</summary>
    public required string DisplayName { get; init; }

    /// <summary>角色：决定权限集合。</summary>
    public required UserRole Role { get; init; }

    /// <summary>账号是否启用；停用后不能登录。</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>是否已设置口令。未设置 = 工位信任模式。</summary>
    public bool HasPassword { get; init; }

    /// <summary>创建时间（本地时间）。</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    /// <inheritdoc />
    public override string ToString()
        => $"UserAccount(UserName={UserName}, DisplayName={DisplayName}, Role={Role}, Enabled={IsEnabled}, HasPassword={HasPassword})";
}
