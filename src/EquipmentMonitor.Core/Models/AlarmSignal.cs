using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 设备侧上报的一条报警事实（不可变）。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Abstractions.IAlarmSource"/> 返回它表示「当前仍然存在」；
/// <see cref="Abstractions.IAlarmService.Report"/> 接收它表示「刚刚发生的一次事件」。
/// 两者都只描述事实，不做去重、确认或历史——那些是报警服务的职责。
/// </para>
/// <para>
/// <see cref="SourceId"/> 的层级约定见 <see cref="Abstractions.IAlarmSource"/>：
/// 必须等于源组标识，或以「源组标识 + <c>:</c>」开头。
/// </para>
/// </remarks>
public sealed record AlarmSignal
{
    /// <summary>具体来源标识，如 <c>axis:1:0</c>、<c>cylinder:Cyl-1</c>、<c>plc:1</c>。</summary>
    public required string SourceId { get; init; }

    /// <summary>来源类别。</summary>
    public required AlarmSourceKind SourceKind { get; init; }

    /// <summary>稳定的报警码，如 <c>AXIS.FAULT</c>、<c>CYLINDER.TIMEOUT</c>。</summary>
    public required string Code { get; init; }

    /// <summary>等级；未在码表中登记时以此为准。</summary>
    public required AlarmLevel Level { get; init; }

    /// <summary>关联元件（可选），用于界面定位到具体设备。</summary>
    public string? ElementId { get; init; }

    /// <summary>元件显示名（可选），用于消息模板的 <c>{ElementName}</c>。</summary>
    public string? ElementName { get; init; }

    /// <summary>设备原始码（伺服故障码、PLC 位号等）；没有则为 <c>null</c>。</summary>
    public int? RawCode { get; init; }

    /// <summary>补充说明（如异常消息）；只进消息文本，不进去重键。</summary>
    public string? Detail { get; init; }

    /// <summary>
    /// 瞬时报警的自动恢复时间；<c>null</c> 表示必须人工确认。
    /// 只对 <see cref="Abstractions.IAlarmService.Report"/> 有意义，持续状态由源消失来恢复。
    /// </summary>
    public TimeSpan? AutoClearAfter { get; init; }
}
