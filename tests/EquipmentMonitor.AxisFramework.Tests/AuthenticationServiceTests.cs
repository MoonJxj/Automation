using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Security;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>
/// 登录门面：账号认证、失败不覆盖会话、审计不含凭据。
/// </summary>
public sealed class AuthenticationServiceTests
{
    [Fact]
    public void 登录方式列表_包含本地账号()
    {
        using var harness = new UserDirectoryHarness(signedInAs: null);
        var service = CreateService(harness);

        service.Providers.Should().ContainSingle();
        service.Providers[0].Source.Should().Be(AuthenticationSource.LocalAccount);
        service.Providers[0].IsAvailable.Should().BeTrue();
        service.DefaultSource.Should().Be(AuthenticationSource.LocalAccount);
    }

    [Fact]
    public async Task 本地账号登录_建立会话并写审计()
    {
        using var harness = new UserDirectoryHarness(signedInAs: null);
        var service = CreateService(harness);

        var result = await service.SignInAsync(new SignInRequest { UserName = "admin" });

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.UserName.Should().Be("admin");
        result.Value.DisplayName.Should().Be("管理员");
        result.Value.Role.Should().Be(UserRole.Admin);
        result.Value.Source.Should().Be(AuthenticationSource.LocalAccount);
        harness.Session.Current.Should().BeSameAs(result.Value);
        harness.Audit.Records.Should().Contain(record => record.EventType == AuditEventTypes.SignInSucceeded);
    }

    [Fact]
    public async Task 不存在的账号_登录失败_不建立会话_写失败审计()
    {
        using var harness = new UserDirectoryHarness(signedInAs: null);
        var service = CreateService(harness);

        var result = await service.SignInAsync(new SignInRequest { UserName = "nobody" });

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(SecurityErrorCodes.AuthenticationUnknownUser);
        harness.Session.Current.Should().BeNull();
        harness.Audit.Records.Should().Contain(record => record.EventType == AuditEventTypes.SignInFailed);
    }

    [Fact]
    public async Task 未指定账号_登录失败()
    {
        using var harness = new UserDirectoryHarness(signedInAs: null);
        var service = CreateService(harness);

        var result = await service.SignInAsync(new SignInRequest { UserName = " " });

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(SecurityErrorCodes.AuthenticationUserRequired);
    }

    [Fact]
    public async Task 停用的账号_登录失败()
    {
        using var harness = new UserDirectoryHarness(signedInAs: null);
        var service = CreateService(harness);

        var disabled = await harness.Directory.SetEnabledAsync("operator", enabled: false);
        disabled.IsSuccess.Should().BeTrue(disabled.Error);

        var result = await service.SignInAsync(new SignInRequest { UserName = "operator" });

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(SecurityErrorCodes.AuthenticationDisabledUser);
    }

    [Fact]
    public async Task 设置口令后_错误口令失败_正确口令成功()
    {
        using var harness = new UserDirectoryHarness(signedInAs: null);
        var service = CreateService(harness);

        await harness.Directory.SetPasswordAsync("operator", "Passw0rd!");

        var wrong = await service.SignInAsync(new SignInRequest
        {
            UserName = "operator",
            Credential = "bad-password"
        });

        wrong.ErrorCode.Should().Be(SecurityErrorCodes.AuthenticationBadCredential);
        harness.Session.Current.Should().BeNull();

        var correct = await service.SignInAsync(new SignInRequest
        {
            UserName = "operator",
            Credential = "Passw0rd!"
        });

        correct.IsSuccess.Should().BeTrue(correct.Error);
        correct.Value.Role.Should().Be(UserRole.Operator);
    }

    [Fact]
    public async Task 登录失败_不覆盖已登录会话()
    {
        using var harness = new UserDirectoryHarness(signedInAs: null);
        var service = CreateService(harness);

        await service.SignInAsync(new SignInRequest { UserName = "admin" });

        var failed = await service.SignInAsync(new SignInRequest { UserName = "nobody" });

        failed.IsFailure.Should().BeTrue();
        harness.Session.Current!.UserName.Should().Be("admin");
        harness.Session.Current.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task 登出_清空会话并写审计()
    {
        using var harness = new UserDirectoryHarness(signedInAs: null);
        var service = CreateService(harness);

        await service.SignInAsync(new SignInRequest { UserName = "engineer" });
        await service.SignOutAsync("切换用户");

        harness.Session.Current.Should().BeNull();
        harness.Audit.Records.Should().Contain(record =>
            record.EventType == AuditEventTypes.SignOut && record.Detail.Contains("切换用户"));
    }

    [Fact]
    public async Task 审计与日志不含凭据()
    {
        using var harness = new UserDirectoryHarness(signedInAs: null);
        var service = CreateService(harness);

        const string password = "super-secret";
        await harness.Directory.SetPasswordAsync("operator", password);

        await service.SignInAsync(new SignInRequest
        {
            UserName = "operator",
            Credential = password
        });

        harness.Audit.Records.Should().NotContain(record => record.Detail.Contains(password));
    }

    private static AuthenticationService CreateService(UserDirectoryHarness harness)
        => new([new LocalAccountProvider(harness.Directory)], harness.Session, harness.Audit);
}
