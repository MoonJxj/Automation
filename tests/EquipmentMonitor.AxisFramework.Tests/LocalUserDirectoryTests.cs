using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Security;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>
/// 本地用户目录：种子账号、CRUD、最后一个管理员 / 当前账号保护、口令设置与清除、审计。
/// </summary>
public sealed class LocalUserDirectoryTests
{
    [Fact]
    public async Task 首次加载_生成五个演示账号_均未设置口令()
    {
        using var harness = new UserDirectoryHarness();

        var result = await harness.Directory.ListAsync();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().HaveCount(5);
        result.Value.Should().OnlyContain(account => !account.HasPassword);
        result.Value.Should().ContainSingle(account =>
            account.UserName == "admin" && account.Role == UserRole.Admin);
        File.Exists(harness.FilePath).Should().BeTrue("首次加载应把种子账号落盘");
    }

    [Fact]
    public async Task 新建账号_成功并持久化()
    {
        using var harness = new UserDirectoryHarness();

        var created = await harness.Directory.CreateAsync(new CreateUserRequest
        {
            UserName = "ZhangSan",
            DisplayName = "张三",
            Role = UserRole.Operator
        });

        created.IsSuccess.Should().BeTrue(created.Error);
        created.Value.Role.Should().Be(UserRole.Operator);
        created.Value.IsEnabled.Should().BeTrue();

        // 用同一个文件重新打开，确认不是只在内存里。
        var reopened = harness.Reopen();
        var found = await reopened.FindAsync("zhangsan");

        found.IsSuccess.Should().BeTrue(found.Error);
        found.Value.DisplayName.Should().Be("张三");
        found.Value.Role.Should().Be(UserRole.Operator);
    }

    [Fact]
    public async Task 重复账号_大小写不敏感_被拒绝()
    {
        using var harness = new UserDirectoryHarness();

        var result = await harness.Directory.CreateAsync(new CreateUserRequest
        {
            UserName = "ADMIN",
            DisplayName = "另一个管理员",
            Role = UserRole.Admin
        });

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(SecurityErrorCodes.DuplicateUserName);
    }

    [Fact]
    public async Task 非法账号名与显示名_被拒绝()
    {
        using var harness = new UserDirectoryHarness();

        var shortName = await harness.Directory.CreateAsync(new CreateUserRequest
        {
            UserName = "a",
            DisplayName = "短名",
            Role = UserRole.Viewer
        });

        shortName.ErrorCode.Should().Be(SecurityErrorCodes.InvalidUserName);

        var chineseName = await harness.Directory.CreateAsync(new CreateUserRequest
        {
            UserName = "张三",
            DisplayName = "张三",
            Role = UserRole.Viewer
        });

        chineseName.ErrorCode.Should().Be(SecurityErrorCodes.InvalidUserName);

        var emptyDisplay = await harness.Directory.CreateAsync(new CreateUserRequest
        {
            UserName = "zhangsan",
            DisplayName = " ",
            Role = UserRole.Viewer
        });

        emptyDisplay.ErrorCode.Should().Be(SecurityErrorCodes.InvalidDisplayName);
    }

    [Fact]
    public async Task 修改角色_读回生效()
    {
        using var harness = new UserDirectoryHarness();

        var updated = await harness.Directory.UpdateAsync(new UpdateUserRequest
        {
            UserName = "operator",
            DisplayName = "一号操作工",
            Role = UserRole.ProcessEngineer
        });

        updated.IsSuccess.Should().BeTrue(updated.Error);

        var found = await harness.Directory.FindAsync("operator");
        found.Value.DisplayName.Should().Be("一号操作工");
        found.Value.Role.Should().Be(UserRole.ProcessEngineer);
    }

    [Fact]
    public async Task 降级最后一个管理员_被拒绝()
    {
        using var harness = new UserDirectoryHarness();

        var result = await harness.Directory.UpdateAsync(new UpdateUserRequest
        {
            UserName = "admin",
            DisplayName = "管理员",
            Role = UserRole.Operator
        });

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(SecurityErrorCodes.LastAdminProtected);
    }

    [Fact]
    public async Task 停用最后一个管理员_被拒绝()
    {
        // 当前登录账号是 operator，避免先命中“不能停用自己”的保护。
        using var harness = new UserDirectoryHarness(signedInAs: "operator");

        var result = await harness.Directory.SetEnabledAsync("admin", enabled: false);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(SecurityErrorCodes.LastAdminProtected);
    }

    [Fact]
    public async Task 停用或删除当前登录账号_被拒绝()
    {
        using var harness = new UserDirectoryHarness(signedInAs: "operator");

        var disable = await harness.Directory.SetEnabledAsync("operator", enabled: false);
        disable.ErrorCode.Should().Be(SecurityErrorCodes.SelfProtected);

        var delete = await harness.Directory.DeleteAsync("operator");
        delete.ErrorCode.Should().Be(SecurityErrorCodes.SelfProtected);
    }

    [Fact]
    public async Task 删除普通账号_成功并写审计()
    {
        using var harness = new UserDirectoryHarness();

        var result = await harness.Directory.DeleteAsync("viewer");

        result.IsSuccess.Should().BeTrue(result.Error);

        var list = await harness.Directory.ListAsync();
        list.Value.Should().HaveCount(4);
        harness.Audit.Records.Should().Contain(record => record.EventType == AuditEventTypes.UserDeleted);
    }

    [Fact]
    public async Task 设置口令后_登录必须校验_且审计不含明文()
    {
        using var harness = new UserDirectoryHarness();
        var provider = new LocalAccountProvider(harness.Directory);

        var beforePassword = await provider.AuthenticateAsync(new SignInRequest { UserName = "operator" });
        beforePassword.IsSuccess.Should().BeTrue(beforePassword.Error);

        const string password = "Passw0rd!";
        var set = await harness.Directory.SetPasswordAsync("operator", password);
        set.IsSuccess.Should().BeTrue(set.Error);

        var withoutPassword = await provider.AuthenticateAsync(new SignInRequest { UserName = "operator" });
        withoutPassword.ErrorCode.Should().Be(SecurityErrorCodes.AuthenticationBadCredential);

        var wrongPassword = await provider.AuthenticateAsync(new SignInRequest
        {
            UserName = "operator",
            Credential = "wrong-password"
        });
        wrongPassword.ErrorCode.Should().Be(SecurityErrorCodes.AuthenticationBadCredential);

        var correct = await provider.AuthenticateAsync(new SignInRequest
        {
            UserName = "operator",
            Credential = password
        });
        correct.IsSuccess.Should().BeTrue(correct.Error);
        correct.Value.Role.Should().Be(UserRole.Operator);

        File.ReadAllText(harness.FilePath).Should().NotContain(password);
        harness.Audit.Records.Should().Contain(record => record.EventType == AuditEventTypes.UserPasswordChanged);
    }

    [Fact]
    public async Task 清除口令_恢复工位信任()
    {
        using var harness = new UserDirectoryHarness();
        var provider = new LocalAccountProvider(harness.Directory);

        await harness.Directory.SetPasswordAsync("operator", "Passw0rd!");
        var cleared = await harness.Directory.ClearPasswordAsync("operator");

        cleared.IsSuccess.Should().BeTrue(cleared.Error);

        var login = await provider.AuthenticateAsync(new SignInRequest { UserName = "operator" });
        login.IsSuccess.Should().BeTrue(login.Error);
    }

    [Fact]
    public async Task 无权限_写操作抛异常_但登录仍可列出账号()
    {
        using var harness = new UserDirectoryHarness(signedInAs: null, permissions: TestServices.DenyPermissions());

        var list = await harness.Directory.ListAsync();
        list.IsSuccess.Should().BeTrue(list.Error);

        var act = () => harness.Directory.CreateAsync(new CreateUserRequest
        {
            UserName = "zhangsan",
            DisplayName = "张三",
            Role = UserRole.Operator
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task 不存在的账号_返回失败码()
    {
        using var harness = new UserDirectoryHarness();
        var provider = new LocalAccountProvider(harness.Directory);

        var find = await harness.Directory.FindAsync("nobody");
        find.ErrorCode.Should().Be(SecurityErrorCodes.UserNotFound);

        var login = await provider.AuthenticateAsync(new SignInRequest { UserName = "nobody" });
        login.ErrorCode.Should().Be(SecurityErrorCodes.AuthenticationUnknownUser);
    }

    [Fact]
    public async Task 每个用户管理操作都写审计()
    {
        using var harness = new UserDirectoryHarness();

        await harness.Directory.CreateAsync(new CreateUserRequest
        {
            UserName = "zhangsan",
            DisplayName = "张三",
            Role = UserRole.Operator
        });

        await harness.Directory.UpdateAsync(new UpdateUserRequest
        {
            UserName = "zhangsan",
            DisplayName = "张三",
            Role = UserRole.ProcessEngineer
        });

        await harness.Directory.SetEnabledAsync("zhangsan", enabled: false);
        await harness.Directory.SetPasswordAsync("zhangsan", "Passw0rd!");
        await harness.Directory.DeleteAsync("zhangsan");

        var events = harness.Audit.Records.Select(record => record.EventType).ToHashSet();

        events.Should().Contain(
        [
            AuditEventTypes.UserCreated,
            AuditEventTypes.UserUpdated,
            AuditEventTypes.UserEnabledChanged,
            AuditEventTypes.UserPasswordChanged,
            AuditEventTypes.UserDeleted
        ]);
    }
}
