using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Infrastructure.Alarms.Definitions;

/// <summary>
/// 报警服务自身生成的码（不依赖任何设备 provider）。
/// </summary>
public sealed class SystemAlarmDefinitions : IAlarmDefinitionProvider
{
    /// <inheritdoc />
    public IEnumerable<AlarmDefinition> GetDefinitions() =>
    [
        new AlarmDefinition
        {
            Code = "COMM.SOURCE_OFFLINE",
            Level = AlarmLevel.Error,
            Title = "报警源离线",
            MessageTemplate = "报警来源 {SourceId} 读取失败：{Detail}",
            Trigger = "报警源曾成功读取，随后连续读取失败",
            Recovery = "报警源恢复可读",
            Remedy = "检查连接、从站地址与网线；离线期间该源已有报警被冻结，不会自动清除"
        }
    ];
}
