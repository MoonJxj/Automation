using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Forms;
using FluentAssertions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 登录窗体：只列启用账号、角色说明来自账号、口令输入按账号是否设置口令启用。
/// </summary>
/// <remarks>登录动作本身由 <c>AuthenticationServiceTests</c> 覆盖；窗体只负责收集与展示。</remarks>
public sealed class SignInFormTests
{
    [Fact]
    public void 登录窗体_只列出启用账号_并显示角色说明()
    {
        StaTestHost.RunSta(() =>
        {
            var directory = new FakeUserDirectory();
            directory.Accounts.Add(Account("admin", "管理员", UserRole.Admin));
            directory.Accounts.Add(Account("engineer", "设备工程师", UserRole.Engineer, hasPassword: true));
            directory.Accounts.Add(Account("operator", "操作员", UserRole.Operator, isEnabled: false));

            using var form = new SignInForm(new StubAuthenticationService(), directory);
            form.LoadAccountsForTestAsync().GetAwaiter().GetResult();

            form.AccountCount.Should().Be(2, "停用的账号不应出现在登录列表里");
            form.RoleText.Should().Contain("管理员").And.Contain("用户管理");
            form.IsPasswordRequired.Should().BeFalse("管理员账号未设置口令");
        });
    }

    [Fact]
    public void 有口令账号_选中后要求输入口令()
    {
        StaTestHost.RunSta(() =>
        {
            var directory = new FakeUserDirectory();
            directory.Accounts.Add(Account("admin", "管理员", UserRole.Admin));
            directory.Accounts.Add(Account("engineer", "设备工程师", UserRole.Engineer, hasPassword: true));

            using var form = new SignInForm(new StubAuthenticationService(), directory);
            form.LoadAccountsForTestAsync().GetAwaiter().GetResult();

            form.SelectAccountForTest("engineer");

            form.IsPasswordRequired.Should().BeTrue();
            form.RoleText.Should().Contain("设备工程师");
        });
    }

    [Fact]
    public void 没有启用账号_提示联系管理员()
    {
        StaTestHost.RunSta(() =>
        {
            var directory = new FakeUserDirectory();

            using var form = new SignInForm(new StubAuthenticationService(), directory);
            form.LoadAccountsForTestAsync().GetAwaiter().GetResult();

            form.AccountCount.Should().Be(0);
            form.ErrorText.Should().Contain("没有启用中的账号");
        });
    }

    private static UserAccount Account(
        string userName,
        string displayName,
        UserRole role,
        bool isEnabled = true,
        bool hasPassword = false) => new()
    {
        UserName = userName,
        DisplayName = displayName,
        Role = role,
        IsEnabled = isEnabled,
        HasPassword = hasPassword,
        CreatedAt = DateTimeOffset.Now
    };
}
