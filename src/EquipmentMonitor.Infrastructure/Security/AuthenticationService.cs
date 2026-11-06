using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using Serilog;

namespace EquipmentMonitor.Infrastructure.Security;

/// <summary>
/// 登录门面：选择认证方式 → 认证 → 建立会话 → 写审计。全进程只有这一条登录路径。
/// </summary>
/// <remarks>
/// <para><b>扩展方式</b>：新增认证方式只需注册一个 <see cref="IAuthenticationProvider"/>；
/// 本类按 <see cref="IAuthenticationProvider.Source"/> 分派，不认识具体实现类型。</para>
/// <para><b>失败不写会话</b>：认证失败时当前会话保持不变（不会把已登录的人踢下线），
/// 但会写一条 Warning 与一条审计；凭据永远不进入日志。</para>
/// <para><b>默认认证方式 = 注册顺序第一项</b>：现场只注册本地实现时行为确定；
/// 将来同时注册多种方式时，由界面按 <see cref="Providers"/> 显式选择，不靠默认值猜。</para>
/// </remarks>
public sealed class AuthenticationService : IAuthenticationService
{
    private static readonly ILogger Logger = Log.ForContext<AuthenticationService>();

    private readonly IReadOnlyList<IAuthenticationProvider> _providers;
    private readonly IReadOnlyList<AuthenticationProviderInfo> _providerInfos;
    private readonly ISessionProvider _session;
    private readonly IAuditService _audit;

    /// <summary>创建登录门面。</summary>
    /// <param name="providers">已注册的认证方式。</param>
    /// <param name="session">会话持有者。</param>
    /// <param name="audit">审计服务。</param>
    /// <exception cref="InvalidOperationException">一个认证方式都没注册 —— 属于组合根配置错误。</exception>
    public AuthenticationService(
        IEnumerable<IAuthenticationProvider> providers,
        ISessionProvider session,
        IAuditService audit)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(audit);

        _providers = providers.ToArray();

        if (_providers.Count == 0)
        {
            throw new InvalidOperationException(
                "未注册任何 IAuthenticationProvider。请在组合根注册至少一种认证方式（当前为 LocalRoleSelectionProvider）。");
        }

        _session = session;
        _audit = audit;
        _providerInfos = _providers
            .Select(provider => new AuthenticationProviderInfo(provider.Source, provider.DisplayName, provider.IsAvailable))
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<AuthenticationProviderInfo> Providers => _providerInfos;

    /// <inheritdoc />
    public AuthenticationSource DefaultSource => _providers[0].Source;

    /// <inheritdoc />
    public Task<Result<UserSession>> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken = default)
        => SignInAsync(DefaultSource, request, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<UserSession>> SignInAsync(
        AuthenticationSource source,
        SignInRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var provider = _providers.FirstOrDefault(item => item.Source == source);

        if (provider is null)
        {
            Logger.Warning("登录失败：未注册认证方式。Source={Source}", source);
            return Result.Fail<UserSession>(
                $"未注册认证方式「{source}」。",
                "AUTH_PROVIDER_MISSING");
        }

        if (!provider.IsAvailable)
        {
            Logger.Warning("登录失败：认证方式不可用。Source={Source}", source);
            return Result.Fail<UserSession>(
                $"认证方式「{provider.DisplayName}」当前不可用。",
                "AUTH_PROVIDER_UNAVAILABLE");
        }

        Result<UserSession> result;

        try
        {
            result = await provider.AuthenticateAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "认证实现抛出异常。Source={Source} UserName={UserName}", source, request.UserName);

            await TryAuditAsync(
                    AuditEventTypes.SignInFailed,
                    $"Source={source} UserName={request.UserName} Reason=认证实现异常：{ex.Message}",
                    cancellationToken)
                .ConfigureAwait(false);

            return Result.Fail<UserSession>("认证过程发生异常，请查看日志。", "AUTH_FAILED", ex);
        }

        if (result.IsFailure)
        {
            Logger.Warning(
                "登录失败。Source={Source} UserName={UserName} Reason={Reason}",
                source,
                request.UserName,
                result.Error);

            await TryAuditAsync(
                    AuditEventTypes.SignInFailed,
                    $"Source={source} UserName={request.UserName} Reason={result.Error}",
                    cancellationToken)
                .ConfigureAwait(false);

            return result;
        }

        var session = result.Value;

        _session.Set(session);

        Logger.Information(
            "登录成功。User={UserName} Role={Role} Source={Source}",
            session.UserName,
            session.Role,
            session.Source);

        await TryAuditAsync(
                AuditEventTypes.SignInSucceeded,
                $"User={session.UserName} Role={session.Role} Source={session.Source}",
                cancellationToken)
            .ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc />
    public async Task SignOutAsync(string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var current = _session.Current;

        if (current is null)
        {
            return;
        }

        Logger.Information(
            "登出。User={UserName} Role={Role} Reason={Reason}",
            current.UserName,
            current.Role,
            reason);

        // 先写审计再清会话：审计实现从当前会话补“操作者”，清空后就只剩“未登录”。
        await TryAuditAsync(
                AuditEventTypes.SignOut,
                $"User={current.UserName} Role={current.Role} Reason={reason}",
                cancellationToken)
            .ConfigureAwait(false);

        _session.Clear(reason);
    }

    /// <summary>
    /// 写审计；失败只记日志。<b>审计失败不得影响登录结果</b>。
    /// </summary>
    private async Task TryAuditAsync(string eventType, string detail, CancellationToken cancellationToken)
    {
        try
        {
            await _audit.RecordAsync(eventType, detail, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "写审计失败：{AuditEventType}", eventType);
        }
    }
}
