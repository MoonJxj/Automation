namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 气缸位置（由两个限位开关推导）。
/// </summary>
/// <remarks>
/// 气缸只有两个可靠位置：缩回到位、伸出到位。两个传感器都亮或都不亮都不是“正常中间态”，
/// 而是需要现场确认的异常 —— 前者可能是限位开关短路/机械卡死，后者可能是断线或动作未完成。
/// </remarks>
public enum CylinderPosition
{
    /// <summary>未知（尚未采集或传感器不可信）。</summary>
    Unknown = 0,

    /// <summary>缩回到位。</summary>
    Retracted = 1,

    /// <summary>伸出到位。</summary>
    Extended = 2,

    /// <summary>动作中（两个限位都未亮，且已下发方向命令）。</summary>
    Moving = 3,

    /// <summary>中间/矛盾状态（两个限位同时亮，或停在中间）。</summary>
    Intermediate = 4,

    /// <summary>故障（超时、通讯失败、传感器矛盾）。</summary>
    Fault = 5
}
