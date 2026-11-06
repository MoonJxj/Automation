using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 一条报警的完整记录（不可变）。界面、历史查询、审计共用同一形状。
/// </summary>
/// <remarks>
/// <para>
/// <b>生命周期状态由时间字段推导</b>（<see cref="State"/>），不单独存储：
/// 单独存一个状态字段会出现「已确认但状态还是 Active」的不一致。
/// </para>
/// <para>
/// <see cref="Id"/> 是实例标识：确认与历史按它定位；同一 <see cref="Key"/> 在抖动窗口外
/// 再次发生会生成新的 <see cref="Id"/>，历史保留两条。
/// </para>
/// </remarks>
public sealed record AlarmRecord
{
    /// <summary>实例标识；确认与历史按它定位。</summary>
    public required string Id { get; init; }

    /// <summary>去重键。</summary>
    public required AlarmKey Key { get; init; }

    /// <summary>报警等级。</summary>
    public required AlarmLevel Level { get; init; }

    /// <summary>来源类别。</summary>
    public required AlarmSourceKind SourceKind { get; init; }

    /// <summary>短标题。</summary>
    public required string Title { get; init; }

    /// <summary>面向操作工的消息（已替换模板占位符）。</summary>
    public required string Message { get; init; }

    /// <summary>处理建议。</summary>
    public string? Remedy { get; init; }

    /// <summary>关联元件标识。</summary>
    public string? ElementId { get; init; }

    /// <summary>关联元件显示名。</summary>
    public string? ElementName { get; init; }

    /// <summary>首次发生时间。</summary>
    public required DateTimeOffset RaisedAt { get; init; }

    /// <summary>最近一次被源确认仍存在的时间。</summary>
    public required DateTimeOffset LastSeenAt { get; init; }

    /// <summary>在抖动窗口内重复发生的次数（含首次）。</summary>
    public int RaiseCount { get; init; }

    /// <summary>确认时间；<c>null</c> 表示未确认。</summary>
    public DateTimeOffset? AcknowledgedAt { get; init; }

    /// <summary>确认人（会话用户名）。</summary>
    public string? AcknowledgedBy { get; init; }

    /// <summary>恢复时间；<c>null</c> 表示源仍在报告。</summary>
    public DateTimeOffset? ClearedAt { get; init; }

    /// <summary>是否必须人工确认。</summary>
    public bool RequiresAcknowledge { get; init; } = true;

    /// <summary>源当前是否仍在报告。</summary>
    public bool IsActive => ClearedAt is null;

    /// <summary>生命周期状态（由时间字段推导，避免两个真相源）。</summary>
    public AlarmState State => ClearedAt is null
        ? (AcknowledgedAt is null ? AlarmState.Active : AlarmState.Acknowledged)
        : (AcknowledgedAt is null ? AlarmState.RecoveredUnacknowledged : AlarmState.Cleared);

    /// <summary>来源标识（冗余自 <see cref="Key"/>，便于绑定）。</summary>
    public string SourceId => Key.SourceId;

    /// <summary>报警码（冗余自 <see cref="Key"/>，便于绑定）。</summary>
    public string Code => Key.Code;

    /// <summary>设备原始码（冗余自 <see cref="Key"/>）。</summary>
    public int? RawCode => Key.RawCode;
}
