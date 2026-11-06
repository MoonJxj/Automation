namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 运行期元件注册表：轴/气缸装配完成后登记，供报警源等后台消费者读取。
/// </summary>
/// <remarks>
/// 元件由 <c>AxisFrameworkLoader</c> 手工创建，不在 DI 里；
/// 这个注册表是「装配结果」与「DI 单例消费者」之间的最小桥。
/// 只读属性返回构造后不再变化的快照，读取方无需加锁。
/// </remarks>
public interface IElementRegistry
{
    /// <summary>已装配的轴元件。</summary>
    IReadOnlyList<IElementAxis> Axes { get; }

    /// <summary>已装配的气缸元件。</summary>
    IReadOnlyList<IElementCylinder> Cylinders { get; }
}
