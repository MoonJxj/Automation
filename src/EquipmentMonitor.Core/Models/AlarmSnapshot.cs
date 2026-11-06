using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 活动报警的一帧快照（不可变）。界面只读它，不逐条订阅。
/// </summary>
/// <param name="Active">活动报警（含“恢复未确认”），已按等级/时间排序。</param>
/// <param name="UnacknowledgedCount">未确认条数（含恢复未确认）。</param>
/// <param name="HighestLevel">最高等级；无报警时为 <see cref="AlarmLevel.Info"/>。</param>
/// <param name="CapturedAt">快照时间。</param>
public sealed record AlarmSnapshot(
    IReadOnlyList<AlarmRecord> Active,
    int UnacknowledgedCount,
    AlarmLevel HighestLevel,
    DateTimeOffset CapturedAt)
{
    /// <summary>空快照：尚未采集或没有任何报警。</summary>
    public static readonly AlarmSnapshot Empty = new([], 0, AlarmLevel.Info, DateTimeOffset.MinValue);

    /// <summary>活动报警条数。</summary>
    public int Count => Active.Count;
}
