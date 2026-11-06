using EquipmentMonitor.Infrastructure;

namespace EquipmentMonitor.Infrastructure.Hardware.PLC.Modbus;

/// <summary>
/// <see cref="AxisConfig"/> → <see cref="ModbusAxisRegisterMap"/> 的映射。
/// </summary>
/// <remarks>
/// 只处理 Modbus 轴：需要状态区/命令区/Execute 地址；非 Modbus 轴不应调用。
/// </remarks>
public static class ModbusAxisConfigExtensions
{
    /// <summary>
    /// 把轴配置转换为 Modbus 寄存器映射。
    /// </summary>
    /// <exception cref="InvalidOperationException">缺少 Modbus 地址或缩放非法。</exception>
    public static ModbusAxisRegisterMap ToModbusMap(this AxisConfig axis)
    {
        ArgumentNullException.ThrowIfNull(axis);

        if (axis.StateStartAddress is not { } stateAddress ||
            axis.CommandStartAddress is not { } commandAddress ||
            axis.ExecuteCoilAddress is not { } executeCoilAddress)
        {
            throw new InvalidOperationException(
                $"Modbus 轴 {Describe(axis)} 缺少 StateStartAddress/CommandStartAddress/ExecuteCoilAddress；" +
                "非 Modbus 轴不应走 Modbus 映射。");
        }

        var map = new ModbusAxisRegisterMap
        {
            StateStartAddress = stateAddress,
            CommandStartAddress = commandAddress,
            ExecuteCoilAddress = executeCoilAddress,
            PositionScale = axis.PositionScale,
            VelocityScale = axis.VelocityScale,
            TorqueScale = axis.TorqueScale,
            AccelerationScale = axis.AccelerationScale,
            DecelerationScale = axis.DecelerationScale,
            JerkScale = axis.JerkScale,
            DefaultVelocity = axis.DefaultVelocity ?? 10.0,
            Acceleration = axis.Acceleration ?? 100.0,
            Deceleration = axis.Deceleration ?? 100.0,
            Jerk = axis.Jerk,
            HomeMethod = axis.HomeMethod
        };

        map.Validate();
        return map;
    }

    private static string Describe(AxisConfig axis)
        => string.IsNullOrWhiteSpace(axis.Name)
            ? $"Id={axis.Id}/AxisNo={axis.AxisNo}"
            : $"{axis.Name}(Id={axis.Id}/AxisNo={axis.AxisNo})";
}
