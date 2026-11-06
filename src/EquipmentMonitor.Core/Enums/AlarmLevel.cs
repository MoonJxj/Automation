namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 报警等级。只影响显示、排序与提示，**不驱动设备动作**。
/// </summary>
/// <remarks>
/// 等级由报警码表决定（<c>IAlarmCatalog</c>），设备上报值只在“码未登记”时兜底。
/// 报警不是安全功能：急停、安全门、超程由 PLC/硬件回路判定。
/// </remarks>
public enum AlarmLevel
{
    /// <summary>提示：无需处理，仅记录。</summary>
    Info = 0,

    /// <summary>警告：可继续运行，需要关注（状态字 bit4、参数接近边界）。</summary>
    Warning = 1,

    /// <summary>错误：设备不能继续运行，需要人工介入（伺服故障、气缸超时）。</summary>
    Error = 2,

    /// <summary>严重：安全相关（急停、安全门、超程），由 PLC/安全回路判定。</summary>
    Critical = 3
}
