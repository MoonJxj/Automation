namespace EquipmentMonitor.Infrastructure.Hardware.PLC.Modbus;

/// <summary>
/// Modbus 轴寄存器布局常量，与 <c>docs/axis-command-interface.md</c> 对齐。
/// </summary>
/// <remarks>
/// 状态区 9 个寄存器、命令区 10 个寄存器 + 独立 Execute 线圈。
/// 偏移量写死在这里，理由与旧 <c>ServoElement</c> 相同：寄存器布局由 PLC 程序决定，
/// 配置改不动它；把偏移做成配置只会把“抄错偏移”变成运行期静默错误。
/// 现场差异用每轴基址（<see cref="ModbusAxisRegisterMap"/>）表达。
/// </remarks>
public static class ModbusAxisRegisterLayout
{
    /// <summary>状态区寄存器数量。</summary>
    public const ushort StateRegisterCount = 9;

    /// <summary>命令区寄存器数量。</summary>
    public const ushort CommandRegisterCount = 10;

    /// <summary>状态区 +0/+1：当前位置 i32（高字在前）。</summary>
    public const ushort StatePosition = 0;

    /// <summary>状态区 +2：当前速度 i16。</summary>
    public const ushort StateVelocity = 2;

    /// <summary>状态区 +3：当前扭矩 i16。</summary>
    public const ushort StateTorque = 3;

    /// <summary>状态区 +4：状态字（位定义见 <see cref="AxisStatusWord"/>）。</summary>
    public const ushort StateWord = 4;

    /// <summary>状态区 +5：故障码 u16。</summary>
    public const ushort StateFaultCode = 5;

    /// <summary>状态区 +6/+7：目标位置回显 i32。</summary>
    public const ushort StateTargetPosition = 6;

    /// <summary>状态区 +8：回零方式回显 u16。</summary>
    public const ushort StateHomingMethod = 8;

    /// <summary>命令区 +0：CiA402 控制字。</summary>
    public const ushort CommandControlWord = 0;

    /// <summary>命令区 +1：操作模式（1=PP，2=PV，6=HM）。</summary>
    public const ushort CommandMode = 1;

    /// <summary>命令区 +2/+3：目标位置 i32。</summary>
    public const ushort CommandTargetPosition = 2;

    /// <summary>命令区 +4：相对/绝对（0=绝对，1=相对）。</summary>
    public const ushort CommandRelative = 4;

    /// <summary>命令区 +5：目标速度 i16。</summary>
    public const ushort CommandTargetVelocity = 5;

    /// <summary>命令区 +6：加速度 u16。</summary>
    public const ushort CommandAccel = 6;

    /// <summary>命令区 +7：减速度 u16。</summary>
    public const ushort CommandDecel = 7;

    /// <summary>命令区 +8：加加速度 u16（0=不使用）。</summary>
    public const ushort CommandJerk = 8;

    /// <summary>命令区 +9：回零方式。</summary>
    public const ushort CommandHomingMethod = 9;

    /// <summary>使能控制字（Operation Enabled）。</summary>
    public const ushort ControlEnableOperation = 0x000F;

    /// <summary>下使能控制字（Switch On Disabled）。</summary>
    public const ushort ControlSwitchOn = 0x0007;

    /// <summary>故障复位控制字。</summary>
    public const ushort ControlFaultReset = 0x0080;

    /// <summary>快速停止控制字。</summary>
    public const ushort ControlQuickStop = 0x0002;

    /// <summary>轮廓位置模式（PP）。</summary>
    public const ushort ModeProfilePosition = 1;

    /// <summary>轮廓速度模式（PV）。</summary>
    public const ushort ModeProfileVelocity = 2;

    /// <summary>回零模式（HM）。</summary>
    public const ushort ModeHoming = 6;
}

/// <summary>
/// 单轴 Modbus 寄存器映射与工程换算。
/// </summary>
/// <remarks>
/// 每根轴的基址、Execute 线圈地址和缩放独立配置；同一控制器下的轴共用寄存器层。
/// 缩放方向：<b>原始值 × Scale = 工程值</b>，读写共用同一个系数。
/// </remarks>
public sealed record ModbusAxisRegisterMap
{
    /// <summary>状态区起始地址（零基）。</summary>
    public required ushort StateStartAddress { get; init; }

    /// <summary>命令区起始地址（零基）。</summary>
    public required ushort CommandStartAddress { get; init; }

    /// <summary>Execute 线圈地址（零基）。</summary>
    public required ushort ExecuteCoilAddress { get; init; }

    /// <summary>位置缩放：工程值 = 原始值 × 本值。</summary>
    public double PositionScale { get; init; } = 1.0;

    /// <summary>速度缩放。</summary>
    public double VelocityScale { get; init; } = 1.0;

    /// <summary>扭矩缩放。</summary>
    public double TorqueScale { get; init; } = 1.0;

    /// <summary>加速度缩放。</summary>
    public double AccelerationScale { get; init; } = 1.0;

    /// <summary>减速度缩放。</summary>
    public double DecelerationScale { get; init; } = 1.0;

    /// <summary>加加速度缩放。</summary>
    public double JerkScale { get; init; } = 1.0;

    /// <summary>默认定位速度（工程单位/秒）。</summary>
    public double DefaultVelocity { get; init; } = 10.0;

    /// <summary>默认加速度（工程单位/秒²）。</summary>
    public double Acceleration { get; init; } = 100.0;

    /// <summary>默认减速度（工程单位/秒²）。</summary>
    public double Deceleration { get; init; } = 100.0;

    /// <summary>默认加加速度（工程单位/秒³）；<c>0</c> 表示不使用。</summary>
    public double Jerk { get; init; }

    /// <summary>默认回零方式。</summary>
    public int HomeMethod { get; init; }

    /// <summary>校验映射是否可用于写入（地址/缩放不能为 0 或负数）。</summary>
    public void Validate()
    {
        if (StateStartAddress + ModbusAxisRegisterLayout.StateRegisterCount > ushort.MaxValue)
        {
            throw new InvalidOperationException("状态区地址越界。");
        }

        if (CommandStartAddress + ModbusAxisRegisterLayout.CommandRegisterCount > ushort.MaxValue)
        {
            throw new InvalidOperationException("命令区地址越界。");
        }

        if (PositionScale <= 0 || VelocityScale <= 0 || TorqueScale <= 0 ||
            AccelerationScale <= 0 || DecelerationScale <= 0 || JerkScale <= 0)
        {
            throw new InvalidOperationException("缩放系数必须大于 0。");
        }

        if (DefaultVelocity <= 0 || Acceleration <= 0 || Deceleration <= 0)
        {
            throw new InvalidOperationException("默认速度/加速度/减速度必须大于 0。");
        }
    }
}
