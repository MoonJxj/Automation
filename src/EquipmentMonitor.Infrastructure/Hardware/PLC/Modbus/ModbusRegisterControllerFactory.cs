using EquipmentMonitor.Infrastructure;

namespace EquipmentMonitor.Infrastructure.Hardware.PLC.Modbus;

/// <summary>
/// Modbus 能力工厂：按 <see cref="ControllerConfig"/> 从协议池取连接，创建寄存器/线圈读写层。
/// </summary>
/// <remarks>
/// <para>
/// 这是“Modbus 作为一个独立功能”的入口：它只关心连接参数（TCP/RTU、IP、端口、串口、从站号）
/// 和 <see cref="IRegisterController"/> 的创建，不关心轴、IO 或元件语义。
/// </para>
/// <para>
/// <b>一个 PLC 一个协议实例</b>：连接由 <see cref="ModbusProtocolPool"/> 按端点复用，
/// 这里不再自己 <c>new ModbusProtocol</c>。
/// </para>
/// </remarks>
public sealed class ModbusRegisterControllerFactory
{
    private readonly ModbusProtocolPool _protocolPool;

    /// <summary>创建工厂。</summary>
    /// <param name="protocolPool">Modbus 协议池（连接按 PLC 复用）。</param>
    public ModbusRegisterControllerFactory(ModbusProtocolPool protocolPool)
    {
        ArgumentNullException.ThrowIfNull(protocolPool);
        _protocolPool = protocolPool;
    }

    /// <summary>
    /// 按控制器配置创建寄存器/线圈读写层。只构造，不调用 <c>Connect()</c>。
    /// </summary>
    public IRegisterController Create(ControllerConfig controllerConfig)
    {
        ArgumentNullException.ThrowIfNull(controllerConfig);

        var protocol = _protocolPool.GetOrCreate(controllerConfig);
        return new ModbusRegisterController(protocol);
    }
}
