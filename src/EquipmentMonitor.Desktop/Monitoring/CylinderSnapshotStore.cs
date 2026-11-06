using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Cylinder;

namespace EquipmentMonitor.Desktop.Monitoring;

/// <summary>
/// 一帧气缸采集结果（不可变）。
/// </summary>
public sealed record CylinderMonitorSnapshot(
    IReadOnlyList<CylinderSnapshot> Cylinders,
    DateTimeOffset RefreshedAt,
    string? LastError)
{
    /// <summary>空快照。</summary>
    public static CylinderMonitorSnapshot Empty { get; } = new([], default, null);
}

/// <summary>
/// 气缸元件集合与最新快照的交换点：采集线程写，UI 线程读。
/// </summary>
public sealed class CylinderSnapshotStore
{
    private IReadOnlyList<CylinderElement> _elements = [];
    private CylinderMonitorSnapshot _current = CylinderMonitorSnapshot.Empty;

    /// <summary>参与采集的气缸元件（启动时挂载，之后只读）。</summary>
    public IReadOnlyList<CylinderElement> Elements => Volatile.Read(ref _elements);

    /// <summary>最近一帧采集结果。</summary>
    public CylinderMonitorSnapshot Current => Volatile.Read(ref _current);

    /// <summary>挂载气缸元件集合。必须在宿主启动前调用。</summary>
    public void Attach(IReadOnlyList<CylinderElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        Volatile.Write(ref _elements, elements);
    }

    /// <summary>发布一帧采集结果。</summary>
    public void Publish(CylinderMonitorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref _current, snapshot);
    }
}
