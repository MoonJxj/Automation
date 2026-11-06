using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Events;

/// <summary>
/// 报警变化事件参数（不可变）。
/// </summary>
/// <remarks>
/// 事件在后台线程触发；订阅方（尤其界面）必须自行 <c>BeginInvoke</c> 或改为定时拉快照。
/// 订阅者抛出的异常由报警服务隔离，不影响其他订阅者。
/// </remarks>
public sealed class AlarmChangedEventArgs : EventArgs
{
    /// <summary>变化类型。</summary>
    public required AlarmChangeKind Kind { get; init; }

    /// <summary>发生变化的记录（确认/恢复后的最新状态）。</summary>
    public required AlarmRecord Record { get; init; }

    /// <summary>变化后的活动报警快照。</summary>
    public required AlarmSnapshot Snapshot { get; init; }
}
