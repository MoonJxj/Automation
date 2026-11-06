using EquipmentMonitor.Core.Abstractions;

namespace EquipmentMonitor.Infrastructure.Alarms;

/// <summary>
/// 运行期元件注册表：装配完成后一次性发布，供报警源等 DI 单例读取。
/// </summary>
/// <remarks>
/// <para>
/// 元件由 <c>AxisFrameworkLoader</c> 手工创建，不在 DI 里；本类是「装配结果」与
/// 「DI 单例消费者」之间的最小桥。只负责发布，不持有生命周期（元件由宿主释放）。
/// </para>
/// <para>
/// 发布是整体替换：读取方拿到的数组在发布后不再变化，无需在遍历时加锁。
/// </para>
/// </remarks>
public sealed class ElementRegistry : IElementRegistry
{
    private readonly object _gate = new();
    private IReadOnlyList<IElementAxis> _axes = Array.Empty<IElementAxis>();
    private IReadOnlyList<IElementCylinder> _cylinders = Array.Empty<IElementCylinder>();

    /// <inheritdoc />
    public IReadOnlyList<IElementAxis> Axes
    {
        get
        {
            lock (_gate)
            {
                return _axes;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IElementCylinder> Cylinders
    {
        get
        {
            lock (_gate)
            {
                return _cylinders;
            }
        }
    }

    /// <summary>
    /// 发布装配结果。重复调用会整体替换（用于测试或重新装配）。
    /// </summary>
    /// <param name="axes">已初始化的轴元件。</param>
    /// <param name="cylinders">已初始化的气缸元件。</param>
    public void Attach(IEnumerable<IElementAxis> axes, IEnumerable<IElementCylinder> cylinders)
    {
        ArgumentNullException.ThrowIfNull(axes);
        ArgumentNullException.ThrowIfNull(cylinders);

        var axisSnapshot = axes.ToArray();
        var cylinderSnapshot = cylinders.ToArray();

        lock (_gate)
        {
            _axes = axisSnapshot;
            _cylinders = cylinderSnapshot;
        }
    }
}
