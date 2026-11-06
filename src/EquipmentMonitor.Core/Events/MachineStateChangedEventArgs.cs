using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Events;

/// <summary>
/// 机台状态变化事件。
/// </summary>
/// <remarks>
/// <b>在轮询线程上触发</b>；订阅方（UI）自行 marshal。
/// </remarks>
public sealed class MachineStateChangedEventArgs : EventArgs
{
    /// <summary>机台编码。</summary>
    public required string MachineCode { get; init; }

    /// <summary>变化前的状态。</summary>
    public required MachineState OldState { get; init; }

    /// <summary>变化后的状态。</summary>
    public required MachineState NewState { get; init; }

    /// <summary>触发原因。<b>这是现场排障最需要的一条信息</b>。</summary>
    public required string Trigger { get; init; }

    /// <summary>触发本次变化的元件（若有）。聚合触发时指出是哪个元件导致。</summary>
    public string? SourceElementId { get; init; }

    /// <summary>在旧状态停留的时长（毫秒）。由注入的时间戳计算。</summary>
    public double? DurationMs { get; init; }

    /// <summary>变化发生的时刻（UTC）。</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>是否为人为切换（暂停 / 维护）。人为切换不计入故障统计。</summary>
    public bool IsManual { get; init; }

    /// <inheritdoc />
    public override string ToString()
        => $"MachineStateChanged({MachineCode}, {OldState} -> {NewState}, " +
           $"Trigger={Trigger}, Manual={IsManual}, Duration={DurationMs:F0}ms)";
}
