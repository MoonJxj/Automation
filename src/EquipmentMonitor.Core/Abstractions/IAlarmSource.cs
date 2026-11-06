using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 持续型报警源：服务周期读取「当前存在的报警集合」。
/// </summary>
/// <remarks>
/// <para>
/// PLC 报警字、板卡/伺服状态字、气缸故障、通信健康度都实现它。
/// 实现方只描述事实，不负责去重、确认、历史——那些是 <see cref="IAlarmService"/> 的职责。
/// </para>
/// <para>
/// <b>SourceId 的层级约定</b>：本接口的 <see cref="SourceId"/> 是<b>源组</b>标识
/// （如 <c>axis</c>、<c>cylinder</c>、<c>plc:1</c>）；<see cref="AlarmSignal.SourceId"/> 是
/// <b>具体实例</b>（如 <c>axis:1:0</c>），必须以「组标识 + <c>:</c>」开头。
/// 报警服务按这个前缀把记录归组，源离线时只冻结本组的报警。
/// </para>
/// </remarks>
public interface IAlarmSource
{
    /// <summary>稳定且唯一的源组标识；同一设备重启后保持不变。</summary>
    string SourceId { get; }

    /// <summary>来源类别。</summary>
    AlarmSourceKind Kind { get; }

    /// <summary>
    /// 读取当前状态；失败返回 <see cref="AlarmSourceReading.Unavailable"/>，不要抛异常。
    /// </summary>
    ValueTask<AlarmSourceReading> ReadAsync(CancellationToken cancellationToken = default);
}
