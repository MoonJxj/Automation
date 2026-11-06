using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 内存版用户目录：用于界面测试，不落盘。
/// </summary>
internal sealed class FakeUserDirectory : IUserDirectory
{
    public List<UserAccount> Accounts { get; } = [];

    public Task<Result<IReadOnlyList<UserAccount>>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Ok<IReadOnlyList<UserAccount>>(Accounts.ToArray()));

    public Task<Result<UserAccount>> FindAsync(string userName, CancellationToken cancellationToken = default)
    {
        var account = Accounts.FirstOrDefault(item =>
            string.Equals(item.UserName, userName, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(account is null
            ? Result.Fail<UserAccount>($"账号「{userName}」不存在。", SecurityErrorCodes.UserNotFound)
            : Result.Ok(account));
    }

    public Task<Result<UserAccount>> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var account = new UserAccount
        {
            UserName = request.UserName,
            DisplayName = request.DisplayName,
            Role = request.Role,
            IsEnabled = true,
            HasPassword = false,
            CreatedAt = DateTimeOffset.Now
        };

        Accounts.Add(account);
        return Task.FromResult(Result.Ok(account));
    }

    public Task<Result> UpdateAsync(UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var index = IndexOf(request.UserName);

        if (index < 0)
        {
            return Task.FromResult(Result.Fail("账号不存在。", SecurityErrorCodes.UserNotFound));
        }

        Accounts[index] = Accounts[index] with { DisplayName = request.DisplayName, Role = request.Role };
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> SetEnabledAsync(
        string userName,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var index = IndexOf(userName);

        if (index < 0)
        {
            return Task.FromResult(Result.Fail("账号不存在。", SecurityErrorCodes.UserNotFound));
        }

        Accounts[index] = Accounts[index] with { IsEnabled = enabled };
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> SetPasswordAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var index = IndexOf(userName);

        if (index < 0)
        {
            return Task.FromResult(Result.Fail("账号不存在。", SecurityErrorCodes.UserNotFound));
        }

        Accounts[index] = Accounts[index] with { HasPassword = true };
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> ClearPasswordAsync(string userName, CancellationToken cancellationToken = default)
    {
        var index = IndexOf(userName);

        if (index < 0)
        {
            return Task.FromResult(Result.Fail("账号不存在。", SecurityErrorCodes.UserNotFound));
        }

        Accounts[index] = Accounts[index] with { HasPassword = false };
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> DeleteAsync(string userName, CancellationToken cancellationToken = default)
    {
        var index = IndexOf(userName);

        if (index < 0)
        {
            return Task.FromResult(Result.Fail("账号不存在。", SecurityErrorCodes.UserNotFound));
        }

        Accounts.RemoveAt(index);
        return Task.FromResult(Result.Ok());
    }

    private int IndexOf(string userName)
        => Accounts.FindIndex(item =>
            string.Equals(item.UserName, userName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>空认证门面：主壳测试只用到“切换用户”按钮，不需要真实登录。</summary>
internal sealed class StubAuthenticationService : IAuthenticationService
{
    public IReadOnlyList<AuthenticationProviderInfo> Providers { get; } = [];

    public AuthenticationSource DefaultSource => AuthenticationSource.LocalAccount;

    public Task<Result<UserSession>> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Fail<UserSession>("测试未配置认证。", "AUTH_STUB"));

    public Task<Result<UserSession>> SignInAsync(
        AuthenticationSource source,
        SignInRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Fail<UserSession>("测试未配置认证。", "AUTH_STUB"));

    public Task SignOutAsync(string reason, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
