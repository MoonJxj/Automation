using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Infrastructure.Axis;

namespace EquipmentMonitor.Desktop.Monitoring;

/// <summary>
/// 轴元件集合与最新快照的交换点：采集线程写，UI 线程读。
/// </summary>
/// <remarks>
/// <para>两个字段都用 <see cref="Volatile"/> 读写引用，读方无锁拿到完整对象；
/// 不提供“先读后改”的复合操作，正是为了避免调用方在 UI 线程上做同步。</para>
/// <para>元件集合在启动时挂载一次，之后只读；运行期增删元件属于 R2 之后的配置热重载。</para>
/// </remarks>
public sealed class AxisSnapshotStore
{
    private IReadOnlyList<AxisElement> _elements = [];
    private AxisMonitorSnapshot _current = AxisMonitorSnapshot.Empty;

    /// <summary>参与采集的轴元件（启动时挂载，之后只读）。</summary>
    public IReadOnlyList<AxisElement> Elements => Volatile.Read(ref _elements);

    /// <summary>最近一帧采集结果。</summary>
    public AxisMonitorSnapshot Current => Volatile.Read(ref _current);

    /// <summary>挂载轴元件集合。必须在宿主启动前调用。</summary>
    public void Attach(IReadOnlyList<AxisElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        Volatile.Write(ref _elements, elements);
    }

    /// <summary>发布一帧采集结果。</summary>
    public void Publish(AxisMonitorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref _current, snapshot);
    }
}
