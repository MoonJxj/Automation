namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 报警来源的物理类别，用于界面分组与文本。
/// </summary>
/// <remarks>
/// 这是“显示分类”，不是设备类型注册表：新增设备品牌不需要加枚举值，
/// 用 <see cref="Servo"/> / <see cref="MotionCard"/> 等现有类别即可。
/// </remarks>
public enum AlarmSourceKind
{
    /// <summary>运动控制卡（雷赛、固高等）。</summary>
    MotionCard = 0,

    /// <summary>PLC（Modbus 报警字、PLC 侧状态）。</summary>
    Plc = 1,

    /// <summary>伺服驱动器（可经板卡或 PLC 接入）。</summary>
    Servo = 2,

    /// <summary>气缸/离散 IO 元件。</summary>
    Cylinder = 3,

    /// <summary>通用 IO 点位。</summary>
    Io = 4,

    /// <summary>通信链路/协议健康度。</summary>
    Communication = 5,

    /// <summary>命令执行失败等瞬态事件。</summary>
    Command = 6,

    /// <summary>宿主/系统事件（启动失败、配置错误）。</summary>
    System = 7
}
