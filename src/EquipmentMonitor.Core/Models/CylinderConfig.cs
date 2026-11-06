namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 气缸配置：两个限位输入 + 一个/两个电磁阀输出。
/// </summary>
/// <remarks>
/// <para><b>单控 vs 双控</b>：<see cref="RetractOutput"/> 为空表示单控（弹簧复位）：
/// 输出置位伸出，复位缩回；双控则两个输出互锁，禁止同时置位。</para>
/// <para><b>限位接在离散输入</b>：本配置按“限位直接接 IO”建模；
/// 若限位经现场总线映射到寄存器，需要另做寄存器版气缸元件，不要在这里硬塞地址。</para>
/// </remarks>
public sealed record CylinderConfig
{
    /// <summary>元件标识（机台内唯一）。</summary>
    public required string ElementId { get; init; }

    /// <summary>显示名称。</summary>
    public required string Name { get; init; }

    /// <summary>所属控制器配置 Id。</summary>
    public int ControllerConfigId { get; init; }

    /// <summary>控制器编号（板卡/PLC 内部编号）。</summary>
    public int ControllerNo { get; init; }

    /// <summary>所属工位编号（骨架阶段为 1/2）。</summary>
    public int StationId { get; init; } = StationCatalog.Station1;

    /// <summary>是否启用；禁用时元件只读且不参与控制。</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>伸出电磁阀输出点号。</summary>
    public int ExtendOutput { get; init; }

    /// <summary>缩回电磁阀输出点号；<c>null</c> 表示单控气缸（弹簧复位）。</summary>
    public int? RetractOutput { get; init; }

    /// <summary>伸出到位限位输入点号。</summary>
    public int ExtendedInput { get; init; }

    /// <summary>缩回到位限位输入点号。</summary>
    public int RetractedInput { get; init; }

    /// <summary>动作超时（秒）；超时后置故障，必须复位。</summary>
    public double ActionTimeoutSeconds { get; init; } = 3.0;

    /// <summary>描述。</summary>
    public string? Description { get; init; }

    /// <summary>是否双控（有缩回电磁阀）。</summary>
    public bool IsDoubleActing => RetractOutput is not null;

    /// <summary>校验配置；非法配置在装配期直接失败。</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ElementId))
        {
            throw new InvalidOperationException("气缸配置缺少 ElementId。");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException($"气缸 {ElementId} 缺少 Name。");
        }

        if (ControllerConfigId <= 0)
        {
            throw new InvalidOperationException($"气缸 {ElementId} 的 ControllerConfigId 必须大于 0。");
        }

        if (ExtendOutput < 0 || RetractOutput is < 0)
        {
            throw new InvalidOperationException($"气缸 {ElementId} 的输出点号不能为负数。");
        }

        if (ExtendedInput < 0 || RetractedInput < 0)
        {
            throw new InvalidOperationException($"气缸 {ElementId} 的输入点号不能为负数。");
        }

        if (ExtendedInput == RetractedInput)
        {
            throw new InvalidOperationException($"气缸 {ElementId} 的伸出/缩回限位不能使用同一个输入点。");
        }

        if (IsDoubleActing && ExtendOutput == RetractOutput)
        {
            throw new InvalidOperationException($"气缸 {ElementId} 的伸出/缩回电磁阀不能使用同一个输出点。");
        }

        if (!double.IsFinite(ActionTimeoutSeconds) || ActionTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException($"气缸 {ElementId} 的动作超时必须大于 0 秒。");
        }
    }
}
