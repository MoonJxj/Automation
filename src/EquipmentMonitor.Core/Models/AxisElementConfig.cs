namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 轴元件运行所需的配置快照。
/// </summary>
/// <remarks>
/// <para>
/// 与数据库实体 <c>AxisConfig</c> 分开：实体承载持久化列与 SqlSugar 特性，
/// 本记录只承载轴元件运行期真正需要的字段。这样 <c>AxisElement</c> 不依赖 ORM，
/// 单元测试可以直接 <c>new AxisElementConfig { ... }</c>，不需要建库。
/// </para>
/// <para>
/// 距离、速度、加速度一律是<b>工程单位</b>。原始值 → 工程值的缩放由控制器实现负责，
/// 元件层不再做第二次缩放 —— 两层各缩放一次是现场“位置差 1000 倍”的经典来源。
/// </para>
/// </remarks>
public sealed record AxisElementConfig
{
    /// <summary>元件标识，机台内唯一。</summary>
    public required string ElementId { get; init; }

    /// <summary>显示名称。</summary>
    public required string Name { get; init; }

    /// <summary>控制器编号，对应 <c>IAxisController</c> 的 <c>ControllerNo</c> 参数。</summary>
    public required int ControllerNo { get; init; }

    /// <summary>轴编号，对应 <c>IAxisController</c> 的 <c>axisNo</c> 参数。</summary>
    public required int AxisNo { get; init; }

    /// <summary>所属工位编号（骨架阶段为 1/2）。</summary>
    public int StationId { get; init; } = StationCatalog.Station1;

    /// <summary>工程单位（<c>mm</c>、<c>deg</c> 等）。</summary>
    public string Unit { get; init; } = "mm";

    /// <summary>允许的最大速度（工程单位/秒）；<c>null</c> 表示不校验。</summary>
    public double? MaxVelocity { get; init; }

    /// <summary>软限位下限（工程单位）；<c>null</c> 表示不启用。</summary>
    public double? SoftLimitMin { get; init; }

    /// <summary>软限位上限（工程单位）；<c>null</c> 表示不启用。</summary>
    public double? SoftLimitMax { get; init; }

    /// <summary>默认定位速度（工程单位/秒）。</summary>
    public double DefaultVelocity { get; init; } = 10.0;

    /// <summary>默认加速度（工程单位/秒²）。</summary>
    public double Acceleration { get; init; } = 100.0;

    /// <summary>默认减速度（工程单位/秒²）。</summary>
    public double Deceleration { get; init; } = 100.0;

    /// <summary>默认加加速度（工程单位/秒³）；<c>0</c> 表示不使用 S 曲线。</summary>
    public double Jerk { get; init; }

    /// <summary>回零方式（厂商/PLCopen 取值）；<c>0</c> 表示使用实现默认值。</summary>
    public int HomeMethod { get; init; }

    /// <summary>是否启用该轴。禁用后不参与初始化与轮询。</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 校验配置合法性。配置错误属于编程错误，直接抛异常（在构造期炸掉，而不是运行期静默走错）。
    /// </summary>
    /// <exception cref="InvalidOperationException">配置不合法。</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ElementId))
        {
            throw new InvalidOperationException("轴元件缺少 ElementId。");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException($"轴元件 {ElementId} 缺少名称。");
        }

        if (ControllerNo < 0)
        {
            throw new InvalidOperationException($"轴元件 {ElementId} 的 ControllerNo 不能为负数。");
        }

        if (AxisNo < 0)
        {
            throw new InvalidOperationException($"轴元件 {ElementId} 的 AxisNo 不能为负数。");
        }

        if (DefaultVelocity <= 0 || !double.IsFinite(DefaultVelocity))
        {
            throw new InvalidOperationException($"轴元件 {ElementId} 的 DefaultVelocity 必须是有限正数。");
        }

        if (MaxVelocity is <= 0)
        {
            throw new InvalidOperationException($"轴元件 {ElementId} 的 MaxVelocity 必须大于 0。");
        }

        if (MaxVelocity is { } max && DefaultVelocity > max)
        {
            throw new InvalidOperationException(
                $"轴元件 {ElementId} 的 DefaultVelocity {DefaultVelocity} 超过 MaxVelocity {max}。");
        }
    }
}
