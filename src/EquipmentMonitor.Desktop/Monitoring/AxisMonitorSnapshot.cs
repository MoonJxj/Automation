namespace EquipmentMonitor.Desktop.Monitoring;

/// <summary>
/// 一轮采集的完整结果。UI 每次只取这一帧，不直接触碰元件。
/// </summary>
/// <param name="Axes">全部轴快照。</param>
/// <param name="RefreshedAt">这一轮的完成时间；尚未采集时为 <c>null</c>。</param>
/// <param name="LastError">本轮最后一个采集错误；全部成功时为 <c>null</c>。</param>
public sealed record AxisMonitorSnapshot(
    IReadOnlyList<AxisSnapshot> Axes,
    DateTimeOffset? RefreshedAt,
    string? LastError)
{
    /// <summary>尚未采集过任何数据时的空帧。</summary>
    public static AxisMonitorSnapshot Empty { get; } = new([], null, null);
}
