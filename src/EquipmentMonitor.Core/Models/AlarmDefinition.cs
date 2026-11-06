using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 报警码的定义：等级、文本、处理建议。设备通过
/// <see cref="Abstractions.IAlarmDefinitionProvider"/> 注册。
/// </summary>
public sealed record AlarmDefinition
{
    /// <summary>稳定的报警码，作为码表的键。</summary>
    public required string Code { get; init; }

    /// <summary>等级；码表定义的等级优先于设备上报值。</summary>
    public required AlarmLevel Level { get; init; }

    /// <summary>短标题，如「伺服故障」。</summary>
    public required string Title { get; init; }

    /// <summary>
    /// 消息模板，支持 <c>{ElementName}</c>、<c>{SourceId}</c>、<c>{Code}</c>、
    /// <c>{RawCode}</c>、<c>{Detail}</c>；未提供的占位符替换为空串。
    /// </summary>
    public string MessageTemplate { get; init; } = "{ElementName} 报警（{Code}，原始码 {RawCode}）";

    /// <summary>触发条件的文字说明（SRS §10.3 要求）。</summary>
    public string? Trigger { get; init; }

    /// <summary>恢复条件的文字说明。</summary>
    public string? Recovery { get; init; }

    /// <summary>处理建议。</summary>
    public string? Remedy { get; init; }

    /// <summary>是否必须人工确认后才能从活动列表消失。</summary>
    public bool RequiresAcknowledge { get; init; } = true;
}
