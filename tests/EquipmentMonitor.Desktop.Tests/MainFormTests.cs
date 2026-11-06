using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Forms;
using EquipmentMonitor.Desktop.Monitoring;
using EquipmentMonitor.Infrastructure.Auditing;
using EquipmentMonitor.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 主壳：状态栏显示当前用户与角色、导航按权限显隐、无权限时不显示任何页面入口。
/// </summary>
public sealed class MainFormTests
{
    [Fact]
    public void 有会话_状态栏显示用户名与角色_且导航可见()
    {
        using var host = Program.BuildHost([]);
        var session = host.Services.GetRequiredService<ISessionProvider>();

        session.Set(Session(UserRole.ProcessEngineer, "张三"));

        StaTestHost.RunSta(() =>
        {
            using var form = (MainForm)host.Services.GetRequiredService(typeof(MainForm));

            form.RefreshSessionViewForTest();

            form.UserText.Should().Contain("张三").And.Contain("工艺工程师");
            form.VisibleNavigationCount.Should().Be(4);
        });
    }

    [Fact]
    public void 无会话_显示未登录且导航全部隐藏()
    {
        using var host = Program.BuildHost([]);

        StaTestHost.RunSta(() =>
        {
            using var form = (MainForm)host.Services.GetRequiredService(typeof(MainForm));

            form.RefreshSessionViewForTest();

            form.UserText.Should().Contain("未登录");
            form.VisibleNavigationCount.Should().Be(0);
        });
    }

    [Fact]
    public void 无报警_状态栏显示报警无()
    {
        using var host = Program.BuildHost([]);

        StaTestHost.RunSta(() =>
        {
            using var form = (MainForm)host.Services.GetRequiredService(typeof(MainForm));

            form.RefreshStatusForTest();

            form.AlarmText.Should().Contain("报警：无");
        });
    }

    [Fact]
    public void 无查看权限_导航全部隐藏()
    {
        // 直接构造：BuildHost 的 configure 钩子在服务注册之前执行，
        // 这里需要“最后注册的权限服务”是拒绝版，所以不走容器覆盖。
        var session = new SessionProvider();
        var authentication = new StubAuthenticationService();

        session.Set(Session(UserRole.Admin, "管理员"));

        StaTestHost.RunSta(() =>
        {
            using var form = new MainForm(
                DesktopTestServices.EmptyProvider(),
                new AxisSnapshotStore(),
                session,
                authentication,
                new DenyAllPermissions());

            form.RefreshSessionViewForTest();

            form.VisibleNavigationCount.Should().Be(0);
        });
    }

    private static UserSession Session(UserRole role, string name) => new()
    {
        UserName = name,
        DisplayName = name,
        Role = role,
        Source = AuthenticationSource.LocalAccount
    };
}
