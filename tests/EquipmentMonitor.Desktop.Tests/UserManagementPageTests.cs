using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Forms;
using EquipmentMonitor.Infrastructure.Security;
using FluentAssertions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 用户管理页：列表渲染、权限不足时动作禁用、保护当前登录账号。
/// </summary>
public sealed class UserManagementPageTests
{
    [Fact]
    public void 有权限_刷新后显示账号列表()
    {
        StaTestHost.RunSta(() =>
        {
            var directory = new FakeUserDirectory();
            directory.Accounts.Add(Account("admin", "管理员", UserRole.Admin));
            directory.Accounts.Add(Account("operator", "操作员", UserRole.Operator));

            var session = new SessionProvider();
            session.Set(SessionFor("admin", UserRole.Admin));

            using var page = new UserManagementPage(
                directory,
                DesktopTestServices.AllowPermissions(),
                session);

            page.RefreshForTestAsync().GetAwaiter().GetResult();

            page.ActionsAllowed.Should().BeTrue();
            page.RowCount.Should().Be(2);
            page.RenderedRows.Should().Contain(row =>
                row.User == "admin" && row.Role == "管理员" && row.Status == "启用");
            page.CreateButtonEnabled.Should().BeTrue();
            page.HintText.Should().Contain("共 2 个账号");
        });
    }

    [Fact]
    public void 无权限_动作按钮全部禁用()
    {
        StaTestHost.RunSta(() =>
        {
            var directory = new FakeUserDirectory();
            directory.Accounts.Add(Account("admin", "管理员", UserRole.Admin));

            var session = new SessionProvider();
            session.Set(SessionFor("viewer", UserRole.Viewer));

            using var page = new UserManagementPage(
                directory,
                DesktopTestServices.DenyPermissions(),
                session);

            page.RefreshForTestAsync().GetAwaiter().GetResult();

            page.ActionsAllowed.Should().BeFalse();
            page.CreateButtonEnabled.Should().BeFalse();
            page.CanToggleSelected.Should().BeFalse();
            page.CanDeleteSelected.Should().BeFalse();
        });
    }

    [Fact]
    public void 选中当前登录账号_不能停用或删除()
    {
        StaTestHost.RunSta(() =>
        {
            var directory = new FakeUserDirectory();
            directory.Accounts.Add(Account("admin", "管理员", UserRole.Admin));
            directory.Accounts.Add(Account("operator", "操作员", UserRole.Operator));

            var session = new SessionProvider();
            session.Set(SessionFor("admin", UserRole.Admin));

            using var page = new UserManagementPage(
                directory,
                DesktopTestServices.AllowPermissions(),
                session);

            page.RefreshForTestAsync().GetAwaiter().GetResult();

            page.SelectUserForTest("admin");
            page.CanToggleSelected.Should().BeFalse("不能停用当前登录账号");
            page.CanDeleteSelected.Should().BeFalse("不能删除当前登录账号");

            page.SelectUserForTest("operator");
            page.CanToggleSelected.Should().BeTrue();
            page.CanDeleteSelected.Should().BeTrue();
        });
    }

    private static UserAccount Account(string userName, string displayName, UserRole role) => new()
    {
        UserName = userName,
        DisplayName = displayName,
        Role = role,
        IsEnabled = true,
        HasPassword = false,
        CreatedAt = DateTimeOffset.Now
    };

    private static UserSession SessionFor(string userName, UserRole role) => new()
    {
        UserName = userName,
        DisplayName = userName,
        Role = role,
        Source = AuthenticationSource.LocalAccount
    };
}
