namespace EquipmentMonitor.Infrastructure.Security;

/// <summary>
/// 本地用户目录的配置。相对路径按 <see cref="AppContext.BaseDirectory"/> 解析，
/// 避免从快捷方式启动时工作目录不同导致“账号全都消失了”。
/// </summary>
public sealed class UserDirectoryOptions
{
    /// <summary>默认文件名；与 <c>axis-framework.json</c> 一样放在程序运行目录。</summary>
    public const string DefaultFileName = "users.json";

    /// <summary>用户文件路径（相对或绝对）。</summary>
    public string FilePath { get; set; } = DefaultFileName;
}
