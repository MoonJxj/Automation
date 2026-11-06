using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Security;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>
/// 用户目录测试夹具：临时用户文件 + 会话 + 记录型审计。
/// </summary>
internal sealed class UserDirectoryHarness : IDisposable
{
    public UserDirectoryHarness(string? signedInAs = "admin", IPermissionService? permissions = null)
    {
        FilePath = Path.Combine(Path.GetTempPath(), $"users-{Guid.NewGuid():N}.json");

        Directory = Create(permissions);

        if (signedInAs is not null)
        {
            Session.Set(new UserSession
            {
                UserName = signedInAs,
                DisplayName = signedInAs,
                Role = UserRole.Admin,
                Source = AuthenticationSource.LocalAccount
            });
        }
    }

    public string FilePath { get; }

    public SessionProvider Session { get; } = new();

    public RecordingAudit Audit { get; } = TestServices.RecordingAuditService();

    public LocalUserDirectory Directory { get; }

    /// <summary>用同一个文件重新打开一个目录实例，验证持久化。</summary>
    public LocalUserDirectory Reopen(IPermissionService? permissions = null) => Create(permissions);

    public void Dispose()
    {
        Directory.Dispose();

        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            if (File.Exists(FilePath + ".tmp"))
            {
                File.Delete(FilePath + ".tmp");
            }
        }
        catch (IOException)
        {
            // 测试清理失败不影响结论。
        }
    }

    private LocalUserDirectory Create(IPermissionService? permissions) => new(
        new UserDirectoryOptions { FilePath = FilePath },
        permissions ?? TestServices.AllowPermissions(),
        Audit,
        Session);
}
