using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Forms;
using FluentAssertions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 口令对话框：长度与确认校验、清除口令。
/// </summary>
public sealed class PasswordDialogTests
{
    [Fact]
    public void 口令过短_校验失败并提示()
    {
        StaTestHost.RunSta(() =>
        {
            using var dialog = new PasswordDialog(Account(hasPassword: false));

            dialog.SetPasswordForTest("123", "123");

            dialog.ValidateForTest().Should().BeFalse();
            dialog.ErrorText.Should().Contain("至少");
        });
    }

    [Fact]
    public void 两次输入不一致_校验失败()
    {
        StaTestHost.RunSta(() =>
        {
            using var dialog = new PasswordDialog(Account(hasPassword: false));

            dialog.SetPasswordForTest("Passw0rd!", "Passw0rd?");

            dialog.ValidateForTest().Should().BeFalse();
            dialog.ErrorText.Should().Contain("不一致");
        });
    }

    [Fact]
    public void 清除口令_结果为空且标记清除()
    {
        StaTestHost.RunSta(() =>
        {
            using var dialog = new PasswordDialog(Account(hasPassword: true));

            dialog.SetClearForTest(true);

            dialog.ValidateForTest().Should().BeTrue(dialog.ErrorText);
            dialog.ClearRequested.Should().BeTrue();
            dialog.NewPassword.Should().BeNull();
        });
    }

    [Fact]
    public void 合法口令_校验通过()
    {
        StaTestHost.RunSta(() =>
        {
            using var dialog = new PasswordDialog(Account(hasPassword: false));

            dialog.SetPasswordForTest("Passw0rd!", "Passw0rd!");

            dialog.ValidateForTest().Should().BeTrue(dialog.ErrorText);
            dialog.ClearRequested.Should().BeFalse();
            dialog.NewPassword.Should().Be("Passw0rd!");
        });
    }

    private static UserAccount Account(bool hasPassword) => new()
    {
        UserName = "operator",
        DisplayName = "操作员",
        Role = UserRole.Operator,
        IsEnabled = true,
        HasPassword = hasPassword,
        CreatedAt = DateTimeOffset.Now
    };
}
