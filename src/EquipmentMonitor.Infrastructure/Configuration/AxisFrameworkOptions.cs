using System.Text.Json;
using System.Text.Json.Serialization;
using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure;

namespace EquipmentMonitor.Infrastructure.Configuration;

/// <summary>
/// 轴控框架的启动配置：控制器连接参数 + 每根轴的寄存器/运动参数。
/// </summary>
/// <remarks>
/// <para>
/// 当前直接复用 <see cref="ControllerConfig"/> / <see cref="AxisConfig"/> 作为配置模型，
/// 避免同一组字段维护两份；它们虽然带 SqlSugar 特性，但 JSON 反序列化只读公共属性，特性会被忽略。
/// </para>
/// <para>
/// 如果以后“配置文件字段”和“数据库字段”出现分叉（例如数据库多出审计列、配置文件需要默认值），
/// 再把这里拆成独立的 DTO，并增加 DTO → 实体的映射；不要在实体上继续堆仅配置用的字段。
/// </para>
/// </remarks>
public sealed class AxisFrameworkOptions
{
    /// <summary>控制器列表。</summary>
    public List<ControllerConfig> Controllers { get; set; } = new();

    /// <summary>轴列表。</summary>
    public List<AxisConfig> Axes { get; set; } = new();

    /// <summary>气缸列表（同一份配置里的离散 IO 元件）。</summary>
    public List<CylinderConfig> Cylinders { get; set; } = new();
}

/// <summary>
/// 从 JSON 配置文件读取 <see cref="AxisFrameworkOptions"/>。
/// </summary>
/// <remarks>
/// 文件不存在、JSON 语法错误、根对象为空都返回 <see cref="Result.Fail"/>（启动配置错误属于预期内失败），
/// 由宿主决定是弹窗、记日志还是退出，而不是在读取器里直接抛异常。
/// </remarks>
public static class AxisFrameworkOptionsLoader
{
    /// <summary>默认配置文件名；放在程序运行目录（<see cref="AppContext.BaseDirectory"/>）下。</summary>
    public const string DefaultFileName = "axis-framework.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// 读取配置文件。
    /// </summary>
    /// <param name="path">
    /// JSON 文件路径。相对路径按 <see cref="AppContext.BaseDirectory"/> 解析，
    /// 避免从快捷方式/服务启动时工作目录不同导致“明明放对了文件却找不到”。
    /// </param>
    public static Result<AxisFrameworkOptions> Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result.Fail<AxisFrameworkOptions>(
                "轴控配置文件路径为空。",
                "AxisFramework.ConfigPathEmpty");
        }

        var fullPath = ResolvePath(path);

        try
        {
            if (!File.Exists(fullPath))
            {
                return Result.Fail<AxisFrameworkOptions>(
                    $"轴控配置文件不存在：{fullPath}",
                    "AxisFramework.ConfigFileNotFound");
            }

            var json = File.ReadAllText(fullPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Result.Fail<AxisFrameworkOptions>(
                    $"轴控配置文件为空：{fullPath}",
                    "AxisFramework.ConfigFileEmpty");
            }

            var options = JsonSerializer.Deserialize<AxisFrameworkOptions>(json, SerializerOptions);
            if (options is null)
            {
                return Result.Fail<AxisFrameworkOptions>(
                    $"轴控配置文件反序列化结果为空：{fullPath}",
                    "AxisFramework.ConfigInvalid");
            }

            return Result.Ok(options);
        }
        catch (JsonException ex)
        {
            return Result.Fail<AxisFrameworkOptions>(
                $"轴控配置文件 JSON 格式错误：{fullPath}，{ex.Message}",
                "AxisFramework.ConfigInvalid",
                ex);
        }
        catch (Exception ex)
        {
            return Result.Fail<AxisFrameworkOptions>(
                $"读取轴控配置文件失败：{fullPath}，{ex.Message}",
                "AxisFramework.ConfigReadFailed",
                ex);
        }
    }

    /// <summary>
    /// 把相对路径解析到程序运行目录。绝对路径原样返回。
    /// </summary>
    public static string ResolvePath(string path)
        => Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
}
