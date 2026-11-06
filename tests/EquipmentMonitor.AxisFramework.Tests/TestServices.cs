using System.Collections.Concurrent;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>全部放行的权限服务：只用于不验证权限的用例。</summary>
internal sealed class AllowAllPermissions : IPermissionService
{
    public bool HasPermission(Permission permission) => true;

    public void RequirePermission(Permission permission)
    {
        // 放行
    }
}

/// <summary>全部拒绝的权限服务：验证业务入口真的会抛异常，而不是只在 UI 上藏按钮。</summary>
internal sealed class DenyAllPermissions : IPermissionService
{
    public bool HasPermission(Permission permission) => false;

    public void RequirePermission(Permission permission)
        => throw new UnauthorizedAccessException($"缺少权限 {permission}。");
}

/// <summary>记录型审计：断言每条命令都有记录。</summary>
internal sealed class RecordingAudit : IAuditService
{
    private readonly ConcurrentQueue<(string EventType, string Detail)> _records = new();

    public IReadOnlyList<(string EventType, string Detail)> Records => _records.ToArray();

    public Task RecordAsync(string eventType, string detail, CancellationToken cancellationToken = default)
    {
        _records.Enqueue((eventType, detail));
        return Task.CompletedTask;
    }
}

internal static class TestServices
{
    public static IPermissionService AllowPermissions() => new AllowAllPermissions();

    public static IPermissionService DenyPermissions() => new DenyAllPermissions();

    public static RecordingAudit RecordingAuditService() => new RecordingAudit();
}
