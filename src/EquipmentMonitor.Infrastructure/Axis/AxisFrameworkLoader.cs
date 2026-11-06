using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Core.Cylinder;
using EquipmentMonitor.Infrastructure.Hardware.Simulation;

namespace EquipmentMonitor.Infrastructure.Axis;

/// <summary>
/// 启动装配结果：控制器按 <see cref="ControllerConfig.Id"/> 索引，轴/气缸元件按配置顺序排列。
/// </summary>
/// <param name="Controllers">已初始化的控制器；键是 <see cref="ControllerConfig.Id"/>。</param>
/// <param name="Elements">已初始化的轴元件。</param>
/// <param name="Cylinders">已初始化的气缸元件。</param>
public sealed record AxisFrameworkBuildResult(
    IReadOnlyDictionary<int, IAxisController> Controllers,
    IReadOnlyList<AxisElement> Elements,
    IReadOnlyList<CylinderElement> Cylinders);

/// <summary>
/// 把数据库中的控制器/轴配置装配成运行期对象。
/// </summary>
/// <remarks>
/// <para>
/// 装配顺序：控制器（按 <see cref="ControllerConfig.SortOrder"/>、再按 Id）→ 初始化 →
/// 该控制器下的轴（按 <see cref="AxisConfig.AxisNo"/>）→ 映射配置 → 初始化轴元件。
/// </para>
/// <para>
/// <b>失败策略</b>：任何控制器初始化失败、轴配置非法、轴初始化失败都会让整次装配失败，
/// 并返回所有错误（不是只报第一个）。轴控启动“带病运行”比启动失败更危险：
/// 操作工看到界面正常，按下命令才发现某根轴根本没上线。
/// </para>
/// <para>
/// <b>生命周期</b>：控制器由 <see cref="IControllerFactory"/> 提供（通常是 DI 单例），
/// 本装配器不释放它们；轴元件由本装配器创建，失败时会释放已创建的元件。
/// </para>
/// </remarks>
public sealed class AxisFrameworkLoader
{
    private readonly IControllerFactory _controllerFactory;
    private readonly IPermissionService _permissions;
    private readonly IAuditService _audit;
    private readonly IAxisControllerAdapterFactory? _adapterFactory;
    private readonly global::Common.ILoggerManager? _logger;

    /// <summary>创建装配器。</summary>
    /// <param name="controllerFactory">按品牌键创建同步控制器的工厂。</param>
    /// <param name="permissions">权限服务，注入每个轴元件。</param>
    /// <param name="audit">审计服务，注入每个轴元件。</param>
    /// <param name="adapterFactory">可选的适配工厂（Modbus 等异步/特殊控制器）；为 <c>null</c> 时只用同步工厂。</param>
    /// <param name="logger">可选日志；透传给轴/气缸元件。</param>
    public AxisFrameworkLoader(
        IControllerFactory controllerFactory,
        IPermissionService permissions,
        IAuditService audit,
        IAxisControllerAdapterFactory? adapterFactory = null,
        global::Common.ILoggerManager? logger = null)
    {
        ArgumentNullException.ThrowIfNull(controllerFactory);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(audit);

        _controllerFactory = controllerFactory;
        _permissions = permissions;
        _audit = audit;
        _adapterFactory = adapterFactory;
        _logger = logger;
    }

    /// <summary>
    /// 按配置装配控制器、轴元件与气缸元件。
    /// </summary>
    public Task<Result<AxisFrameworkBuildResult>> BuildAsync(
        IReadOnlyCollection<ControllerConfig> controllerConfigs,
        IReadOnlyCollection<AxisConfig> axisConfigs,
        CancellationToken cancellationToken = default)
        => BuildAsync(controllerConfigs, axisConfigs, cylinderConfigs: null, cancellationToken: cancellationToken);

    /// <summary>
    /// 按配置装配控制器、轴元件与气缸元件。
    /// </summary>
    /// <param name="controllerConfigs">控制器配置。</param>
    /// <param name="axisConfigs">轴配置。</param>
    /// <param name="cylinderConfigs">气缸配置；为 <c>null</c> 时不装配气缸。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<Result<AxisFrameworkBuildResult>> BuildAsync(
        IReadOnlyCollection<ControllerConfig> controllerConfigs,
        IReadOnlyCollection<AxisConfig> axisConfigs,
        IReadOnlyCollection<CylinderConfig>? cylinderConfigs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(controllerConfigs);
        ArgumentNullException.ThrowIfNull(axisConfigs);

        var errors = new List<string>();
        var controllers = new Dictionary<int, IAxisController>();
        var elements = new List<AxisElement>();
        var cylinders = new List<CylinderElement>();

        var enabledControllers = controllerConfigs
            .Where(c => c.IsEnabled)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToArray();

        var knownControllerIds = enabledControllers.Select(c => c.Id).ToHashSet();

        // 指向不存在/已禁用控制器的轴配置必须报出来，不能静默忽略 —— 现场会表现为“少了一根轴”。
        foreach (var orphan in axisConfigs.Where(a => !knownControllerIds.Contains(a.ControllerConfigId)))
        {
            errors.Add(
                $"轴配置 {DescribeAxis(orphan)} 指向不存在或已禁用的控制器配置 Id={orphan.ControllerConfigId}。");
        }

        foreach (var controllerConfig in enabledControllers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var useAdapterFactory = _adapterFactory?.CanCreate(controllerConfig) == true;

            if (!useAdapterFactory && string.IsNullOrWhiteSpace(controllerConfig.Brand))
            {
                errors.Add($"控制器配置 {controllerConfig.Name ?? controllerConfig.Id.ToString()} 缺少 Brand。");
                continue;
            }

            var axes = axisConfigs
                .Where(a => a.ControllerConfigId == controllerConfig.Id)
                .OrderBy(a => a.AxisNo)
                .ToArray();

            IAxisController controller;
            try
            {
                controller = useAdapterFactory
                    ? _adapterFactory!.Create(controllerConfig, axes)
                    : _controllerFactory.Create<IAxisController>(controllerConfig.Brand!);
            }
            catch (Exception ex)
            {
                errors.Add(
                    $"控制器 {controllerConfig.Name ?? controllerConfig.Id.ToString()} " +
                    $"（品牌 {controllerConfig.Brand}）创建失败：{ex.Message}");
                continue;
            }

            if (!controller.IsInitialize && !controller.Initialize())
            {
                errors.Add(
                    $"控制器 {controllerConfig.Name ?? controllerConfig.Id.ToString()} " +
                    $"（品牌 {controllerConfig.Brand}，编号 {controllerConfig.ControllerNo}）初始化失败。");
                continue;
            }

            controllers[controllerConfig.Id] = controller;

            foreach (var axisConfig in axes)
            {
                AxisElement element;
                try
                {
                    var elementConfig = axisConfig.ToElementConfig(controllerConfig.ControllerNo);
                    element = new AxisElement(elementConfig, controller, _permissions, _audit, _logger);
                }
                catch (Exception ex)
                {
                    errors.Add($"轴配置 {DescribeAxis(axisConfig)} 非法：{ex.Message}");
                    continue;
                }

                var initialized = await element.InitializeAsync(cancellationToken).ConfigureAwait(false);
                if (initialized.IsFailure)
                {
                    errors.Add($"轴 {element.ElementMetadata.ElementId} 初始化失败：{initialized.Error}");
                    element.Dispose();
                    continue;
                }

                elements.Add(element);
            }
        }

        if (cylinderConfigs is { Count: > 0 })
        {
            await BuildCylindersAsync(
                enabledControllers,
                knownControllerIds,
                cylinderConfigs,
                cylinders,
                errors,
                cancellationToken).ConfigureAwait(false);
        }

        if (errors.Count > 0)
        {
            foreach (var element in elements)
            {
                element.Dispose();
            }

            foreach (var cylinder in cylinders)
            {
                cylinder.Dispose();
            }

            return Result.Fail<AxisFrameworkBuildResult>(
                $"轴控装配失败（{errors.Count} 项）：{string.Join("；", errors)}",
                "AxisFramework.BuildFailed");
        }

        return Result.Ok(new AxisFrameworkBuildResult(controllers, elements, cylinders));
    }

    private async Task BuildCylindersAsync(
        IReadOnlyList<ControllerConfig> enabledControllers,
        IReadOnlySet<int> knownControllerIds,
        IReadOnlyCollection<CylinderConfig> cylinderConfigs,
        List<CylinderElement> cylinders,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        foreach (var orphan in cylinderConfigs.Where(c => !knownControllerIds.Contains(c.ControllerConfigId)))
        {
            errors.Add(
                $"气缸配置 {orphan.ElementId} 指向不存在或已禁用的控制器配置 Id={orphan.ControllerConfigId}。");
        }

        var ioControllers = new Dictionary<int, IIoController>();

        foreach (var group in cylinderConfigs
                     .Where(c => knownControllerIds.Contains(c.ControllerConfigId))
                     .GroupBy(c => c.ControllerConfigId)
                     .OrderBy(g => g.Key))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var controllerConfig = enabledControllers.First(c => c.Id == group.Key);

            if (string.IsNullOrWhiteSpace(controllerConfig.Brand))
            {
                errors.Add($"气缸所在控制器 {group.Key} 缺少 Brand。");
                continue;
            }

            if (!ioControllers.TryGetValue(controllerConfig.Id, out var ioController))
            {
                try
                {
                    ioController = _controllerFactory.Create<IIoController>(controllerConfig.Brand);
                }
                catch (Exception ex)
                {
                    errors.Add(
                        $"气缸 IO 控制器 {controllerConfig.Name ?? controllerConfig.Id.ToString()} " +
                        $"（品牌 {controllerConfig.Brand}）创建失败：{ex.Message}");
                    continue;
                }

                if (!ioController.IsInitialize && !ioController.Initialize())
                {
                    errors.Add(
                        $"气缸 IO 控制器 {controllerConfig.Name ?? controllerConfig.Id.ToString()} 初始化失败。");
                    continue;
                }

                ioControllers[controllerConfig.Id] = ioController;
            }

            foreach (var config in group)
            {
                CylinderElement cylinder;

                try
                {
                    config.Validate();

                    if (ioController is SimulatedIoController simulated)
                    {
                        simulated.ConfigureCylinder(
                            config.ExtendOutput,
                            config.RetractOutput ?? -1,
                            config.ExtendedInput,
                            config.RetractedInput);
                    }

                    cylinder = new CylinderElement(config, ioController, _permissions, _audit, _logger);
                }
                catch (Exception ex)
                {
                    errors.Add($"气缸配置 {config.ElementId} 非法：{ex.Message}");
                    continue;
                }

                var initialized = await cylinder.InitializeAsync(cancellationToken).ConfigureAwait(false);
                if (initialized.IsFailure)
                {
                    errors.Add($"气缸 {cylinder.ElementMetadata.ElementId} 初始化失败：{initialized.Error}");
                    cylinder.Dispose();
                    continue;
                }

                cylinders.Add(cylinder);
            }
        }

        // IO 控制器由本装配器为本轮创建；失败时统一释放，成功时随元件生命周期留在内存。
        foreach (var controller in ioControllers.Values)
        {
            if (errors.Count == 0)
            {
                break;
            }

            controller.Dispose();
        }
    }

    private static string DescribeAxis(AxisConfig axis)
        => string.IsNullOrWhiteSpace(axis.Name)
            ? $"Id={axis.Id}/AxisNo={axis.AxisNo}"
            : $"{axis.Name}(Id={axis.Id}/AxisNo={axis.AxisNo})";
}
