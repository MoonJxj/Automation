namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 报警记录的生命周期状态，由 <c>AlarmRecord</c> 的时间字段推导，不单独存储。
/// </summary>
public enum AlarmState
{
    /// <summary>活动、未确认。</summary>
    Active = 0,

    /// <summary>活动、已确认；源仍在报告。</summary>
    Acknowledged = 1,

    /// <summary>源已恢复、但操作员尚未确认（防止“一闪而过没人看见”）。</summary>
    RecoveredUnacknowledged = 2,

    /// <summary>已恢复、已确认；从活动列表归档到历史。</summary>
    Cleared = 3
}
