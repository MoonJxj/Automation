namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 轴参数键常量。与命令参数键一样，字面量散落多处时拼错不会报错，只会表现为“参数没生效”。
/// </summary>
public static class AxisParameterKeys
{
    /// <summary>轴名称（显示用）。</summary>
    public const string Name = "Name";

    /// <summary>工程单位（mm/deg 等）。</summary>
    public const string Unit = "Unit";

    /// <summary>所属工位。</summary>
    public const string StationId = "StationId";

    /// <summary>是否启用该轴。</summary>
    public const string IsEnabled = "IsEnabled";

    /// <summary>默认定位速度（工程单位/秒）。</summary>
    public const string DefaultVelocity = "DefaultVelocity";

    /// <summary>默认加速度（工程单位/秒²）。</summary>
    public const string Acceleration = "Acceleration";

    /// <summary>默认减速度（工程单位/秒²）。</summary>
    public const string Deceleration = "Deceleration";

    /// <summary>默认加加速度（工程单位/秒³）。</summary>
    public const string Jerk = "Jerk";

    /// <summary>允许的最大速度（工程单位/秒）。</summary>
    public const string MaxVelocity = "MaxVelocity";

    /// <summary>软限位下限（工程单位）。</summary>
    public const string SoftLimitMin = "SoftLimitMin";

    /// <summary>软限位上限（工程单位）。</summary>
    public const string SoftLimitMax = "SoftLimitMax";

    /// <summary>回零方式。</summary>
    public const string HomeMethod = "HomeMethod";

    /// <summary>位置缩放：工程值 = 原始值 × 本值。</summary>
    public const string PositionScale = "PositionScale";

    /// <summary>速度缩放。</summary>
    public const string VelocityScale = "VelocityScale";

    /// <summary>扭矩缩放。</summary>
    public const string TorqueScale = "TorqueScale";

    /// <summary>加速度缩放。</summary>
    public const string AccelerationScale = "AccelerationScale";

    /// <summary>减速度缩放。</summary>
    public const string DecelerationScale = "DecelerationScale";

    /// <summary>加加速度缩放。</summary>
    public const string JerkScale = "JerkScale";

    /// <summary>Modbus 状态区起始地址（仅 Modbus 轴）。</summary>
    public const string StateStartAddress = "StateStartAddress";

    /// <summary>Modbus 命令区起始地址（仅 Modbus 轴）。</summary>
    public const string CommandStartAddress = "CommandStartAddress";

    /// <summary>Modbus Execute 线圈地址（仅 Modbus 轴）。</summary>
    public const string ExecuteCoilAddress = "ExecuteCoilAddress";
}
