using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>测试替身：容器、权限。</summary>
internal static class DesktopTestServices
{
    public static IServiceProvider EmptyProvider()
        => new ServiceCollection().BuildServiceProvider();

    public static IPermissionService AllowPermissions() => new AllowAllPermissions();

    public static IPermissionService DenyPermissions() => new DenyAllPermissions();
}

internal sealed class AllowAllPermissions : IPermissionService
{
    public bool HasPermission(Permission permission) => true;

    public void RequirePermission(Permission permission)
    {
        // 放行
    }
}

internal sealed class DenyAllPermissions : IPermissionService
{
    public bool HasPermission(Permission permission) => false;

    public void RequirePermission(Permission permission)
        => throw new UnauthorizedAccessException($"缺少权限 {permission}。");
}
