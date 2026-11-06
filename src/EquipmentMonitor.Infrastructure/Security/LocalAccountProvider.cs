using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Infrastructure.Security;

/// <summary>
/// 本地账号认证：从用户目录取账号、校验口令（若已设置），角色以目录为准。
/// </summary>
/// <remarks>
/// <para><b>角色不接受客户端指定</b>：<see cref="SignInRequest"/> 里没有角色字段，
/// 登录后能做什么完全由管理员在用户管理页分配的角色决定。</para>
/// <para><b>口令校验留在用户目录内部</b>：provider 拿不到哈希，只能拿到“成功/失败 + 账号快照”，
/// 避免口令材料泄漏到认证层之外。</para>
/// <para><b>口令可选</b>：未设置口令的账号按工位信任放行 —— 这是迁移期安排，
/// 不是安全边界；设置口令后同一路径会强制校验。</para>
/// </remarks>
public sealed class LocalAccountProvider : IAuthenticationProvider
{
    private readonly LocalUserDirectory _directory;

    /// <summary>创建本地账号认证实现。</summary>
    /// <param name="directory">用户目录（唯一持有口令哈希的地方）。</param>
    public LocalAccountProvider(LocalUserDirectory directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        _directory = directory;
    }

    /// <inheritdoc />
    public AuthenticationSource Source => AuthenticationSource.LocalAccount;

    /// <inheritdoc />
    public string DisplayName => "本地账号";

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public async Task<Result<UserSession>> AuthenticateAsync(
        SignInRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return Result.Fail<UserSession>(
                "请选择登录账号。",
                SecurityErrorCodes.AuthenticationUserRequired);
        }

        var verified = await _directory
            .VerifyAsync(request.UserName, request.Credential, cancellationToken)
            .ConfigureAwait(false);

        if (verified.IsFailure)
        {
            return Result.Fail<UserSession>(verified.Error, verified.ErrorCode, verified.Exception);
        }

        var account = verified.Value;

        var session = new UserSession
        {
            UserName = account.UserName,
            DisplayName = account.DisplayName,
            Role = account.Role,
            Source = Source,
            SignedInAt = DateTimeOffset.Now
        };

        return Result.Ok(session);
    }
}
