namespace EquipmentMonitor.Infrastructure.Axis;

/// <summary>
/// 异步/特殊控制器的适配工厂：对外只返回同步 <see cref="IAxisController"/>。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IControllerFactory"/> 只能按品牌键从 DI 取同步控制器；Modbus 轴需要
/// <see cref="IRegisterController"/> 和每轴地址，无法用 keyed DI 直接构造。
/// 本接口把“按控制器配置构造并适配”的职责单独拿出来。
/// </para>
/// <para>
/// <b>返回值刻意是 <see cref="IAxisController"/></b>：异步契约 <c>IAsyncAxisController</c>
/// 是 Infrastructure 内部实现细节，不进入公共入口。
/// </para>
/// <para>
/// 接口留在 Axis（协议无关）；具体 Modbus 实现放在 <c>Hardware/PLC/Modbus</c>。
/// </para>
/// </remarks>
public interface IAxisControllerAdapterFactory
{
    /// <summary>该工厂是否能处理此控制器配置。</summary>
    bool CanCreate(ControllerConfig controllerConfig);

    /// <summary>创建并适配控制器；调用方只看到同步 <see cref="IAxisController"/>。</summary>
    /// <param name="controllerConfig">控制器配置。</param>
    /// <param name="axes">该控制器下的轴配置。</param>
    IAxisController Create(
        ControllerConfig controllerConfig,
        IReadOnlyList<AxisConfig> axes);
}
