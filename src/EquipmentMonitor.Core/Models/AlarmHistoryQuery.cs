using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 历史查询条件。所有字段都是可选的「与」条件。
/// </summary>
public sealed record AlarmHistoryQuery
{
    /// <summary>起始时间（含）。</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>结束时间（含）。</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>最低等级（含）。</summary>
    public AlarmLevel? MinimumLevel { get; init; }

    /// <summary>生命周期状态。</summary>
    public AlarmState? State { get; init; }

    /// <summary>按来源精确过滤。</summary>
    public string? SourceId { get; init; }

    /// <summary>按元件精确过滤。</summary>
    public string? ElementId { get; init; }

    /// <summary>最多返回条数；实现必须夹在 1..5000。</summary>
    public int Limit { get; init; } = 500;
}
