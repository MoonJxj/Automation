using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Forms;
using FluentAssertions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 用户编辑对话框：默认最小权限、账号名校验、编辑模式账号名不可改。
/// </summary>
public sealed class UserEditDialogTests
{
    [Fact]
    public void 新建_默认观察员_校验通过()
    {
        StaTestHost.RunSta(() =>
        {
            using var dialog = new UserEditDialog();

            dialog.SetUserNameForTest("zhangsan");
            dialog.SetDisplayNameForTest("张三");

            dialog.ValidateForTest().Should().BeTrue(dialog.ErrorText);
            dialog.CreateRequest.Should().NotBeNull();
            dialog.CreateRequest!.UserName.Should().Be("zhangsan");
            dialog.CreateRequest.Role.Should().Be(UserRole.Viewer);
            dialog.UpdateRequest.Should().BeNull();
        });
    }

    [Fact]
    public void 新建_非法账号名_校验失败并提示()
    {
        StaTestHost.RunSta(() =>
        {
            using var dialog = new UserEditDialog();

            dialog.SetUserNameForTest("a");
            dialog.SetDisplayNameForTest("张三");

            dialog.ValidateForTest().Should().BeFalse();
            dialog.ErrorText.Should().Contain("账号名");
        });
    }

    [Fact]
    public void 编辑_账号名不可改_请求保留原账号()
    {
        StaTestHost.RunSta(() =>
        {
            var existing = new UserAccount
            {
                UserName = "operator",
                DisplayName = "操作员",
                Role = UserRole.Operator,
                IsEnabled = true,
                HasPassword = false,
                CreatedAt = DateTimeOffset.Now
            };

            using var dialog = new UserEditDialog(existing);
            dialog.SetDisplayNameForTest("一号操作工");
            dialog.SelectRoleForTest(UserRole.ProcessEngineer);

            dialog.ValidateForTest().Should().BeTrue(dialog.ErrorText);
            dialog.CreateRequest.Should().BeNull();
            dialog.UpdateRequest.Should().NotBeNull();
            dialog.UpdateRequest!.UserName.Should().Be("operator");
            dialog.UpdateRequest.DisplayName.Should().Be("一号操作工");
            dialog.UpdateRequest.Role.Should().Be(UserRole.ProcessEngineer);
        });
    }
}
