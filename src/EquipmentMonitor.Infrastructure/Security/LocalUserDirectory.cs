using System.Text.Json;
using System.Text.Json.Serialization;
using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using Serilog;

namespace EquipmentMonitor.Infrastructure.Security;

/// <summary>
/// 本地用户目录：账号持久化在 JSON 文件里，口令用 PBKDF2 哈希（可选）。
/// </summary>
/// <remarks>
/// <para><b>默认账号</b>：文件不存在时生成 5 个演示账号（观察员/操作员/工艺/设备/管理员），
/// <b>均未设置口令</b>（工位信任）。现场交付前应为每个账号设置口令，或改用 Windows / 远端认证。</para>
/// <para><b>口令可选</b>：未设置口令的账号直接放行；设置后登录必须校验。
/// 这样既能从“工位信任”平滑迁移到“有口令”，也不会在首次启动时强迫用户记住一个默认口令。</para>
/// <para><b>并发</b>：所有读写都在 <see cref="SemaphoreSlim"/> 内完成；落盘用“临时文件 + 替换”，
/// 避免写一半断电把用户库写坏。用户管理是低频操作，锁内做哈希/写盘的代价可以接受。</para>
/// <para><b>不可改的东西</b>：账号名一旦创建不可修改（审计与历史记录都以它为键）；
/// 最后一个启用中的管理员不能被停用/删除/降级；当前登录账号不能停用或删除。</para>
/// </remarks>
public sealed class LocalUserDirectory : IUserDirectory, IDisposable
{
    private const int FileFormatVersion = 1;

    private static readonly ILogger Logger = Log.ForContext<LocalUserDirectory>();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly UserDirectoryOptions _options;
    private readonly IPermissionService _permissions;
    private readonly IAuditService _audit;
    private readonly ISessionProvider _session;
    private readonly PasswordHasher _hasher;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Dictionary<string, UserRecord>? _users;
    private bool _disposed;

    /// <summary>创建本地用户目录。</summary>
    /// <param name="options">文件路径配置。</param>
    /// <param name="permissions">权限服务：写操作要求 <see cref="Permission.UserManage"/>。</param>
    /// <param name="audit">审计服务。</param>
    /// <param name="session">会话：用于“不能停用/删除当前登录账号”的保护。</param>
    public LocalUserDirectory(
        UserDirectoryOptions options,
        IPermissionService permissions,
        IAuditService audit,
        ISessionProvider session)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(session);

        _options = options;
        _permissions = permissions;
        _audit = audit;
        _session = session;
        _hasher = new PasswordHasher();
    }

    /// <summary>用户文件的完整路径（诊断与日志用）。</summary>
    public string FilePath => ResolvePath();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<UserAccount>>> ListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var users = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);

                var accounts = users.Values
                    .OrderByDescending(record => record.Role)
                    .ThenBy(record => record.UserName, StringComparer.OrdinalIgnoreCase)
                    .Select(ToAccount)
                    .ToArray();

                return Result.Ok<IReadOnlyList<UserAccount>>(accounts);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return StoreFailure<IReadOnlyList<UserAccount>>(ex, "读取用户目录失败");
        }
    }

    /// <inheritdoc />
    public async Task<Result<UserAccount>> FindAsync(string userName, CancellationToken cancellationToken = default)
    {
        var key = UserRules.NormalizeUserName(userName);

        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var users = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);

                return users.TryGetValue(key, out var record)
                    ? Result.Ok(ToAccount(record))
                    : Result.Fail<UserAccount>($"账号「{key}」不存在。", SecurityErrorCodes.UserNotFound);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return StoreFailure<UserAccount>(ex, "读取用户目录失败");
        }
    }

    /// <inheritdoc />
    public async Task<Result<UserAccount>> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userName = UserRules.NormalizeUserName(request.UserName);
        var nameCheck = UserRules.ValidateUserName(userName);
        if (nameCheck.IsFailure)
        {
            return Result.Fail<UserAccount>(nameCheck.Error, nameCheck.ErrorCode);
        }

        var displayCheck = UserRules.ValidateDisplayName(request.DisplayName);
        if (displayCheck.IsFailure)
        {
            return Result.Fail<UserAccount>(displayCheck.Error, displayCheck.ErrorCode);
        }

        if (!Enum.IsDefined(request.Role))
        {
            return Result.Fail<UserAccount>("角色无效。", SecurityErrorCodes.InvalidRole);
        }

        _permissions.RequirePermission(Permission.UserManage);

        try
        {
            UserAccount account;

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var users = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);

                if (users.ContainsKey(userName))
                {
                    return Result.Fail<UserAccount>($"账号「{userName}」已存在。", SecurityErrorCodes.DuplicateUserName);
                }

                var record = new UserRecord
                {
                    UserName = userName,
                    DisplayName = request.DisplayName.Trim(),
                    Role = request.Role,
                    IsEnabled = true,
                    PasswordHash = null,
                    CreatedAt = DateTimeOffset.Now
                };

                users[userName] = record;
                await SaveCoreAsync(users, cancellationToken).ConfigureAwait(false);
                account = ToAccount(record);
            }
            finally
            {
                _gate.Release();
            }

            await AuditAsync(
                    AuditEventTypes.UserCreated,
                    $"UserName={account.UserName} DisplayName={account.DisplayName} Role={account.Role}",
                    cancellationToken)
                .ConfigureAwait(false);

            Logger.Information(
                "已新建用户。UserName={UserName} Role={Role}",
                account.UserName,
                account.Role);

            return Result.Ok(account);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return StoreFailure<UserAccount>(ex, "新建用户失败");
        }
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userName = UserRules.NormalizeUserName(request.UserName);
        var displayCheck = UserRules.ValidateDisplayName(request.DisplayName);
        if (displayCheck.IsFailure)
        {
            return Result.Fail(displayCheck.Error, displayCheck.ErrorCode);
        }

        if (!Enum.IsDefined(request.Role))
        {
            return Result.Fail("角色无效。", SecurityErrorCodes.InvalidRole);
        }

        _permissions.RequirePermission(Permission.UserManage);

        try
        {
            string detail;

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var users = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);

                if (!users.TryGetValue(userName, out var record))
                {
                    return Result.Fail($"账号「{userName}」不存在。", SecurityErrorCodes.UserNotFound);
                }

                var oldRole = record.Role;
                var oldDisplayName = record.DisplayName;

                if (oldRole == UserRole.Admin && request.Role != UserRole.Admin && record.IsEnabled &&
                    CountOtherEnabledAdmins(users, userName) == 0)
                {
                    return Result.Fail(
                        "不能降级最后一个启用中的管理员。请先启用或新建另一个管理员。",
                        SecurityErrorCodes.LastAdminProtected);
                }

                record.DisplayName = request.DisplayName.Trim();
                record.Role = request.Role;

                await SaveCoreAsync(users, cancellationToken).ConfigureAwait(false);

                detail =
                    $"UserName={record.UserName} DisplayName={oldDisplayName}→{record.DisplayName} " +
                    $"Role={oldRole}→{record.Role}";
            }
            finally
            {
                _gate.Release();
            }

            await AuditAsync(AuditEventTypes.UserUpdated, detail, cancellationToken).ConfigureAwait(false);
            Logger.Information("已修改用户。Detail={Detail}", detail);

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return StoreFailure(ex, "修改用户失败");
        }
    }

    /// <inheritdoc />
    public async Task<Result> SetEnabledAsync(
        string userName,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var key = UserRules.NormalizeUserName(userName);
        _permissions.RequirePermission(Permission.UserManage);

        try
        {
            string detail;

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var users = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);

                if (!users.TryGetValue(key, out var record))
                {
                    return Result.Fail($"账号「{key}」不存在。", SecurityErrorCodes.UserNotFound);
                }

                if (record.IsEnabled == enabled)
                {
                    return Result.Ok();
                }

                if (!enabled)
                {
                    if (IsCurrentUser(record.UserName))
                    {
                        return Result.Fail(
                            "不能停用当前登录账号。请先切换到其他账号再操作。",
                            SecurityErrorCodes.SelfProtected);
                    }

                    if (record.Role == UserRole.Admin && CountOtherEnabledAdmins(users, key) == 0)
                    {
                        return Result.Fail(
                            "不能停用最后一个启用中的管理员。请先启用或新建另一个管理员。",
                            SecurityErrorCodes.LastAdminProtected);
                    }
                }

                record.IsEnabled = enabled;
                await SaveCoreAsync(users, cancellationToken).ConfigureAwait(false);
                detail = $"UserName={record.UserName} Enabled={enabled}";
            }
            finally
            {
                _gate.Release();
            }

            await AuditAsync(AuditEventTypes.UserEnabledChanged, detail, cancellationToken).ConfigureAwait(false);
            Logger.Information("已变更用户启用状态。Detail={Detail}", detail);

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return StoreFailure(ex, "变更用户状态失败");
        }
    }

    /// <inheritdoc />
    public async Task<Result> SetPasswordAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var key = UserRules.NormalizeUserName(userName);
        var passwordCheck = PasswordRules.Validate(password);
        if (passwordCheck.IsFailure)
        {
            return passwordCheck;
        }

        _permissions.RequirePermission(Permission.UserManage);

        // 哈希在锁外算：PBKDF2 要几十到几百毫秒，不该占住用户文件的锁。
        var hash = _hasher.Hash(password);

        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var users = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);

                if (!users.TryGetValue(key, out var record))
                {
                    return Result.Fail($"账号「{key}」不存在。", SecurityErrorCodes.UserNotFound);
                }

                record.PasswordHash = hash;
                await SaveCoreAsync(users, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }

            await AuditAsync(
                    AuditEventTypes.UserPasswordChanged,
                    $"UserName={key} Action=Set",
                    cancellationToken)
                .ConfigureAwait(false);

            Logger.Information("已设置用户口令。UserName={UserName}", key);

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return StoreFailure(ex, "设置口令失败");
        }
    }

    /// <inheritdoc />
    public async Task<Result> ClearPasswordAsync(string userName, CancellationToken cancellationToken = default)
    {
        var key = UserRules.NormalizeUserName(userName);
        _permissions.RequirePermission(Permission.UserManage);

        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var users = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);

                if (!users.TryGetValue(key, out var record))
                {
                    return Result.Fail($"账号「{key}」不存在。", SecurityErrorCodes.UserNotFound);
                }

                if (record.PasswordHash is null)
                {
                    return Result.Ok();
                }

                record.PasswordHash = null;
                await SaveCoreAsync(users, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }

            await AuditAsync(
                    AuditEventTypes.UserPasswordChanged,
                    $"UserName={key} Action=Cleared",
                    cancellationToken)
                .ConfigureAwait(false);

            Logger.Warning("已清除用户口令，该账号回到工位信任模式。UserName={UserName}", key);

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return StoreFailure(ex, "清除口令失败");
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(string userName, CancellationToken cancellationToken = default)
    {
        var key = UserRules.NormalizeUserName(userName);
        _permissions.RequirePermission(Permission.UserManage);

        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var users = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);

                if (!users.TryGetValue(key, out var record))
                {
                    return Result.Fail($"账号「{key}」不存在。", SecurityErrorCodes.UserNotFound);
                }

                if (IsCurrentUser(record.UserName))
                {
                    return Result.Fail(
                        "不能删除当前登录账号。请先切换到其他账号再操作。",
                        SecurityErrorCodes.SelfProtected);
                }

                if (record.Role == UserRole.Admin && record.IsEnabled && CountOtherEnabledAdmins(users, key) == 0)
                {
                    return Result.Fail(
                        "不能删除最后一个启用中的管理员。请先启用或新建另一个管理员。",
                        SecurityErrorCodes.LastAdminProtected);
                }

                users.Remove(key);
                await SaveCoreAsync(users, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }

            await AuditAsync(AuditEventTypes.UserDeleted, $"UserName={key}", cancellationToken).ConfigureAwait(false);
            Logger.Information("已删除用户。UserName={UserName}", key);

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return StoreFailure(ex, "删除用户失败");
        }
    }

    /// <summary>
    /// 校验账号与口令（认证专用）。<b>不</b>校验 <see cref="Permission.UserManage"/>：
    /// 登录发生在会话建立之前。
    /// </summary>
    internal async Task<Result<UserAccount>> VerifyAsync(
        string userName,
        string? credential,
        CancellationToken cancellationToken = default)
    {
        var key = UserRules.NormalizeUserName(userName);

        if (key.Length == 0)
        {
            return Result.Fail<UserAccount>("请选择登录账号。", SecurityErrorCodes.AuthenticationUserRequired);
        }

        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var users = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);

                if (!users.TryGetValue(key, out var record))
                {
                    return Result.Fail<UserAccount>(
                        $"账号「{key}」不存在。",
                        SecurityErrorCodes.AuthenticationUnknownUser);
                }

                if (!record.IsEnabled)
                {
                    return Result.Fail<UserAccount>(
                        $"账号「{record.UserName}」已停用，请联系管理员。",
                        SecurityErrorCodes.AuthenticationDisabledUser);
                }

                if (record.PasswordHash is not null && !_hasher.Verify(record.PasswordHash, credential))
                {
                    return Result.Fail<UserAccount>(
                        "口令不正确。",
                        SecurityErrorCodes.AuthenticationBadCredential);
                }

                return Result.Ok(ToAccount(record));
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return StoreFailure<UserAccount>(ex, "读取用户目录失败");
        }
    }

    /// <summary>读取用户集合。<b>调用方必须持有 <see cref="_gate"/></b>。</summary>
    private async Task<Dictionary<string, UserRecord>> LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (_users is not null)
        {
            return _users;
        }

        var path = ResolvePath();
        Dictionary<string, UserRecord> users;

        if (File.Exists(path))
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var file = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<UserFile>(json, SerializerOptions);

            if (file?.Users is null)
            {
                throw new InvalidDataException($"用户文件格式不正确：{path}");
            }

            users = new Dictionary<string, UserRecord>(StringComparer.OrdinalIgnoreCase);

            foreach (var record in file.Users)
            {
                var key = UserRules.NormalizeUserName(record.UserName);

                if (key.Length == 0)
                {
                    continue;
                }

                users[key] = record;
            }
        }
        else
        {
            users = SeedUsers();
            await SaveCoreAsync(users, cancellationToken).ConfigureAwait(false);

            Logger.Warning(
                "用户文件不存在，已生成 {Count} 个演示账号（均未设置口令）。请在现场交付前设置口令或接入 Windows/远端认证。File={FilePath}",
                users.Count,
                path);
        }

        _users = users;
        return users;
    }

    /// <summary>写用户文件。调用方必须持有 <see cref="_gate"/>。</summary>
    private async Task SaveCoreAsync(Dictionary<string, UserRecord> users, CancellationToken cancellationToken)
    {
        var path = ResolvePath();
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var file = new UserFile
        {
            Version = FileFormatVersion,
            Users = users.Values
                .OrderBy(record => record.UserName, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        var json = JsonSerializer.Serialize(file, SerializerOptions);
        var tempPath = path + ".tmp";

        await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, path, overwrite: true);
    }

    private static Dictionary<string, UserRecord> SeedUsers()
    {
        var now = DateTimeOffset.Now;

        UserRecord Create(string userName, string displayName, UserRole role) => new()
        {
            UserName = userName,
            DisplayName = displayName,
            Role = role,
            IsEnabled = true,
            PasswordHash = null,
            CreatedAt = now
        };

        var records = new[]
        {
            Create("viewer", "观察员", UserRole.Viewer),
            Create("operator", "操作员", UserRole.Operator),
            Create("process", "工艺工程师", UserRole.ProcessEngineer),
            Create("engineer", "设备工程师", UserRole.Engineer),
            Create("admin", "管理员", UserRole.Admin)
        };

        return records.ToDictionary(record => record.UserName, StringComparer.OrdinalIgnoreCase);
    }

    private string ResolvePath()
        => Path.IsPathRooted(_options.FilePath)
            ? Path.GetFullPath(_options.FilePath)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _options.FilePath));

    private bool IsCurrentUser(string userName)
    {
        var current = _session.Current?.UserName;

        return current is not null &&
               string.Equals(current, userName, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountOtherEnabledAdmins(Dictionary<string, UserRecord> users, string exceptUserName)
        => users.Values.Count(record =>
            record.Role == UserRole.Admin &&
            record.IsEnabled &&
            !string.Equals(record.UserName, exceptUserName, StringComparison.OrdinalIgnoreCase));

    private static UserAccount ToAccount(UserRecord record) => new()
    {
        UserName = record.UserName,
        DisplayName = record.DisplayName,
        Role = record.Role,
        IsEnabled = record.IsEnabled,
        HasPassword = !string.IsNullOrEmpty(record.PasswordHash),
        CreatedAt = record.CreatedAt
    };

    private async Task AuditAsync(string eventType, string detail, CancellationToken cancellationToken)
    {
        try
        {
            await _audit.RecordAsync(eventType, detail, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "写用户管理审计失败：{AuditEventType}", eventType);
        }
    }

    private Result<T> StoreFailure<T>(Exception ex, string message)
    {
        Logger.Error(ex, "{Message}。File={FilePath}", message, FilePath);

        return Result.Fail<T>($"{message}：{ex.Message}", SecurityErrorCodes.StoreUnavailable, ex);
    }

    private Result StoreFailure(Exception ex, string message)
    {
        Logger.Error(ex, "{Message}。File={FilePath}", message, FilePath);

        return Result.Fail($"{message}：{ex.Message}", SecurityErrorCodes.StoreUnavailable, ex);
    }

}

/// <summary>用户文件的 JSON 结构。</summary>
internal sealed class UserFile
{
    public int Version { get; set; } = 1;

    public List<UserRecord> Users { get; set; } = [];
}

/// <summary>单条账号的存储结构（含口令哈希，绝不外泄）。</summary>
internal sealed class UserRecord
{
    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Viewer;

    public bool IsEnabled { get; set; } = true;

    public string? PasswordHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
