using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 气缸契约：状态快照 + 命令能力。
/// </summary>
/// <remarks>
/// <para>命令语义与轴一致：<b>下发成功不等于到位</b>。<c>Extend</c>/<c>Retract</c> 只把电磁阀置位，
/// 是否到位由 <see cref="IElement.RefreshAsync"/> 读取限位判断；超时由元件置故障。</para>
/// <para>单控气缸的“停止”实际是断阀后弹簧复位；双控气缸断阀后停在原地（由阀的中位机能决定），
/// 界面必须把这条差异写清楚，不能把“停止”说成“停在中间”。</para>
/// </remarks>
public interface IElementCylinder : IElement, IElementCommandTarget
{
    /// <summary>最近一次采集的气缸状态。</summary>
    CylinderStatus Status { get; }

    /// <summary>是否双控（两个电磁阀）。</summary>
    bool IsDoubleActing { get; }

    /// <summary>最近一次错误说明；正常为 <c>null</c>。</summary>
    string? LastError { get; }

    /// <summary>动作超时（秒），供界面显示。</summary>
    double ActionTimeoutSeconds { get; }
}
