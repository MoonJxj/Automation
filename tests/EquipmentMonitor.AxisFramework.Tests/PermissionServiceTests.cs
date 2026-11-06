using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Security;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>
/// 权限判定的安全默认值：无会话 / 过期 / 越权都必须拒绝，且异常消息可读。
/// </summary>
public sealed class PermissionServiceTests
{
    [Fact]
    public void 无会话_全部拒绝()
    {
        var (service, _) = Create();

        service.HasPermission(Permission.MachineView).Should().BeFalse();
        service.HasPermission(Permission.MachineOperate).Should().BeFalse();
    }

    [Fact]
    public void 无会话_RequirePermission_抛出并提示未登录()
    {
        var (service, _) = Create();

        var act = () => service.RequirePermission(Permission.MachineOperate);

        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*尚未登录*操作设备*");
    }

    [Fact]
    public void 操作员_可操作设备_不可改配置()
    {
        var (service, session) = Create();
        session.Set(SessionFor(UserRole.Operator));

        service.HasPermission(Permission.MachineOperate).Should().BeTrue();
        service.HasPermission(Permission.MachineView).Should().BeTrue();
        service.HasPermission(Permission.ProcessConfig).Should().BeFalse();
        service.HasPermission(Permission.MachineConfig).Should().BeFalse();
    }

    [Fact]
    public void 工艺工程师_可改工艺参数_不可改设备配置()
    {
        var (service, session) = Create();
        session.Set(SessionFor(UserRole.ProcessEngineer));

        service.HasPermission(Permission.ProcessConfig).Should().BeTrue();
        service.HasPermission(Permission.MachineConfig).Should().BeFalse();
        service.HasPermission(Permission.UserManage).Should().BeFalse();
    }

    [Fact]
    public void 管理员_可管理用户()
    {
        var (service, session) = Create();
        session.Set(SessionFor(UserRole.Admin));

        service.HasPermission(Permission.UserManage).Should().BeTrue();
        service.HasPermission(Permission.ProcessConfig).Should().BeTrue();
    }

    [Fact]
    public void 越权_异常消息包含角色与权限中文名()
    {
        var (service, session) = Create();
        session.Set(SessionFor(UserRole.Viewer));

        var act = () => service.RequirePermission(Permission.MachineOperate);

        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*观察员*操作设备*");
    }

    [Fact]
    public void 会话过期_拒绝()
    {
        var (service, session) = Create();
        session.Set(SessionFor(UserRole.Admin) with { ExpiresAt = DateTimeOffset.Now.AddMinutes(-1) });

        service.HasPermission(Permission.UserManage).Should().BeFalse();

        var act = () => service.RequirePermission(Permission.UserManage);
        act.Should().Throw<UnauthorizedAccessException>().WithMessage("*过期*");
    }

    [Fact]
    public void 登出后_恢复拒绝()
    {
        var (service, session) = Create();
        session.Set(SessionFor(UserRole.Admin));
        service.HasPermission(Permission.UserManage).Should().BeTrue();

        session.Clear("测试登出");

        service.HasPermission(Permission.UserManage).Should().BeFalse();
    }

    private static (PermissionService Service, SessionProvider Session) Create()
    {
        var session = new SessionProvider();
        return (new PermissionService(session, new RolePermissionPolicy()), session);
    }

    private static UserSession SessionFor(UserRole role) => new()
    {
        UserName = "测试用户",
        DisplayName = "测试用户",
        Role = role,
        Source = AuthenticationSource.LocalAccount
    };
}
