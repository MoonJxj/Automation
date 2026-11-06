namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 一次报警变更的类型，供事件订阅方做增量刷新。
/// </summary>
public enum AlarmChangeKind
{
    /// <summary>新报警产生（或抖动窗口内重新激活）。</summary>
    Raised = 0,

    /// <summary>报警被确认。</summary>
    Acknowledged = 1,

    /// <summary>源已恢复（记录可能仍保留为“恢复未确认”）。</summary>
    Recovered = 2,

    /// <summary>已有报警的字段更新（最近发生时间、消息、等级）。</summary>
    Updated = 3
}
