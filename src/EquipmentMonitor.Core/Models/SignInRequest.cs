namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 登录请求：一次认证需要的全部输入。
/// </summary>
/// <remarks>
/// <para><b>角色不由请求决定</b>。早期版本允许客户端传 <c>RequestedRole</c>，
/// 那等于“客户端说自己是管理员”；现在角色一律由认证实现（本地目录 / Windows 映射 / 远端服务）
/// 决定，请求里不再有这个字段。</para>
/// <para><b>字段按认证方式取舍</b>：本地账号用 <see cref="UserName"/>（账号名）+
/// <see cref="Credential"/>（口令；账号未设置口令时为空）；Windows 认证只需要用户名（可为空）；
/// 远端认证把令牌放进 <see cref="Credential"/>。</para>
/// <para><see cref="ToString"/> 对凭据做脱敏，避免被顺手打进日志。</para>
/// </remarks>
public sealed record SignInRequest
{
    /// <summary>登录账号名。本地账号方式必填。</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>口令 / 令牌等凭据。未设置口令的账号为空；禁止记录到日志。</summary>
    public string? Credential { get; init; }

    /// <inheritdoc />
    public override string ToString()
        => $"SignInRequest(UserName={UserName}, Credential={(Credential is null ? "无" : "***")})";
}
