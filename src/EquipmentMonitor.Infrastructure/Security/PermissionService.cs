using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using Serilog;

namespace EquipmentMonitor.Infrastructure.Security;

/// <summary>
/// 基于「当前会话 + 角色权限表」的权限判定。
/// </summary>
/// <remarks>
/// <para><b>安全默认值</b>：没有会话、会话已过期、角色未被授予 —— 三种情况一律拒绝。
/// 不存在“开发期放行”的分支，避免某次调试开关被带进现场。</para>
/// <para><b>为什么拒绝时记 Warning 而不是审计</b>：<see cref="RequirePermission"/> 是同步方法，
/// 审计写入是异步的；在这里 fire-and-forget 会吞掉异常或丢失记录。越权审计由调用链上游
/// （异步业务入口 / 全局异常兜底）记录，待审计落库后统一补上。</para>
/// </remarks>
public sealed class PermissionService : IPermissionService
{
    private static readonly ILogger Logger = Log.ForContext<PermissionService>();

    private readonly ISessionProvider _session;
    private readonly IPermissionPolicy _policy;

    /// <summary>创建权限服务。</summary>
    public PermissionService(ISessionProvider session, IPermissionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(policy);

        _session = session;
        _policy = policy;
    }

    /// <inheritdoc />
    public bool HasPermission(Permission permission)
    {
        var session = _session.Current;

        return session is not null
               && !session.IsExpired
               && _policy.IsGranted(session.Role, permission);
    }

    /// <inheritdoc />
    public void RequirePermission(Permission permission)
    {
        var session = _session.Current;
        var permissionName = PermissionCatalog.DisplayName(permission);

        if (session is null)
        {
            Logger.Warning(
                "权限校验未通过：尚未登录。RequiredPermission={RequiredPermission}",
                permission);

            throw new UnauthorizedAccessException($"尚未登录，无法执行「{permissionName}」。");
        }

        if (session.IsExpired)
        {
            Logger.Warning(
                "权限校验未通过：会话已过期。UserName={UserName} RequiredPermission={RequiredPermission}",
                session.UserName,
                permission);

            throw new UnauthorizedAccessException($"登录已过期，请重新登录后再执行「{permissionName}」。");
        }

        if (_policy.IsGranted(session.Role, permission))
        {
            return;
        }

        Logger.Warning(
            "权限校验未通过。UserName={UserName} Role={Role} RequiredPermission={RequiredPermission}",
            session.UserName,
            session.Role,
            permission);

        throw new UnauthorizedAccessException(
            $"当前角色「{UserRoleCatalog.DisplayName(session.Role)}」没有「{permissionName}」权限。");
    }
}
