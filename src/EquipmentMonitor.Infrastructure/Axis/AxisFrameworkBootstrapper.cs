using EquipmentMonitor.Common;
using EquipmentMonitor.Infrastructure.Alarms;
using EquipmentMonitor.Infrastructure.Configuration;

namespace EquipmentMonitor.Infrastructure.Axis;

/// <summary>
/// 轴控框架启动入口：读取配置文件 → 装配控制器与轴元件。
/// </summary>
/// <remarks>
/// <para>
/// 宿主（App/后台服务/测试）只需要调用 <see cref="InitializeAsync"/>，
/// 不需要知道 JSON 结构、Modbus 连接参数或寄存器映射。
/// </para>
/// <para>
/// 配置读取失败、控制器创建/初始化失败、轴配置非法都会返回 <see cref="Result.Fail"/>；
/// 部分成功不会返回半套可用对象（避免“界面正常、某根轴没上线”）。
/// </para>
/// <para>
/// 装配成功后可选的 <see cref="ElementRegistry"/> 会发布元件集合，供报警源等
/// DI 单例读取；未注入时（测试直接构造）不发布，其余行为不变。
/// </para>
/// </remarks>
public sealed class AxisFrameworkBootstrapper
{
    private readonly AxisFrameworkLoader _loader;
    private readonly ElementRegistry? _registry;

    /// <summary>创建启动器。</summary>
    /// <param name="loader">轴装配器。</param>
    /// <param name="registry">可选的元件注册表；装配成功后发布轴/气缸。</param>
    public AxisFrameworkBootstrapper(AxisFrameworkLoader loader, ElementRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _loader = loader;
        _registry = registry;
    }

    /// <summary>
    /// 使用默认配置文件（运行目录下的 <c>axis-framework.json</c>）初始化。
    /// </summary>
    public Task<Result<AxisFrameworkBuildResult>> InitializeAsync(
        CancellationToken cancellationToken = default)
        => InitializeAsync(AxisFrameworkOptionsLoader.DefaultFileName, cancellationToken);

    /// <summary>
    /// 从配置文件初始化轴控框架。
    /// </summary>
    /// <param name="configPath">
    /// 轴控 JSON 配置文件路径；相对路径按程序运行目录解析。
    /// </param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<Result<AxisFrameworkBuildResult>> InitializeAsync(
        string configPath,
        CancellationToken cancellationToken = default)
    {
        var options = AxisFrameworkOptionsLoader.Load(configPath);
        if (options.IsFailure)
        {
            return Result.Fail<AxisFrameworkBuildResult>(
                options.Error,
                options.ErrorCode,
                options.Exception);
        }

        var result = await _loader.BuildAsync(
            options.Value.Controllers,
            options.Value.Axes,
            options.Value.Cylinders,
            cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess && _registry is not null)
        {
            // 装配成功后一次性发布；失败时不发布半套元件。
            _registry.Attach(result.Value.Elements, result.Value.Cylinders);
        }

        return result;
    }
}
