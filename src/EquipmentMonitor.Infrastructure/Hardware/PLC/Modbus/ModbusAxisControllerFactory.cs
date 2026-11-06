using EquipmentMonitor.Infrastructure;
using EquipmentMonitor.Infrastructure.Axis;

namespace EquipmentMonitor.Infrastructure.Hardware.PLC.Modbus;

/// <summary>
/// Modbus 轴控制器工厂：只负责“轴配置 → 每轴寄存器映射 + 轴控制器 + 同步适配”，
/// Modbus 连接和寄存器层由 <see cref="ModbusRegisterControllerFactory"/> 提供。
/// </summary>
/// <remarks>
/// <para>
/// 职责拆分：Modbus 功能负责连接与寄存器/线圈读写；本工厂负责轴寄存器布局与适配。
/// 两者通过 <see cref="IRegisterController"/> 连接，互不知道对方的实现细节。
/// </para>
/// <para>
/// 工厂只负责构造，不负责 <c>Connect()</c>；连接由 <see cref="AxisFrameworkLoader"/>
/// 调用的 <c>Initialize()</c> 触发。
/// </para>
/// </remarks>
public sealed class ModbusAxisControllerFactory : IAxisControllerAdapterFactory
{
    private readonly ModbusRegisterControllerFactory _registerFactory;

    /// <summary>创建工厂。</summary>
    /// <param name="registerFactory">Modbus 寄存器层工厂（独立功能）。</param>
    public ModbusAxisControllerFactory(ModbusRegisterControllerFactory registerFactory)
    {
        ArgumentNullException.ThrowIfNull(registerFactory);
        _registerFactory = registerFactory;
    }

    /// <inheritdoc />
    public bool CanCreate(ControllerConfig controllerConfig)
        => controllerConfig.ConnectionType is ControllerConnectionType.ModbusTcp
            or ControllerConnectionType.ModbusRtu;

    /// <inheritdoc />
    public IAxisController Create(
        ControllerConfig controllerConfig,
        IReadOnlyList<AxisConfig> axes)
    {
        ArgumentNullException.ThrowIfNull(controllerConfig);
        ArgumentNullException.ThrowIfNull(axes);

        if (axes.Count == 0)
        {
            throw new InvalidOperationException(
                $"控制器 {controllerConfig.Name ?? controllerConfig.Id.ToString()} 没有轴配置，无法创建 Modbus 轴控制器。");
        }

        var maps = axes.ToDictionary(axis => axis.AxisNo, axis => axis.ToModbusMap());
        var registers = _registerFactory.Create(controllerConfig);
        var asyncController = new ModbusAxisController(registers, maps);

        return new AsyncAxisControllerAdapter(asyncController);
    }
}
