using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Infrastructure.Alarms.Definitions;

/// <summary>
/// 轴/伺服内置报警码表。具体故障码的厂商含义由设备手册解释，
/// 这里只定义「一类报警」的等级、标题与处理建议。
/// </summary>
public sealed class AxisAlarmDefinitions : IAlarmDefinitionProvider
{
    /// <inheritdoc />
    public IEnumerable<AlarmDefinition> GetDefinitions() =>
    [
        new AlarmDefinition
        {
            Code = "AXIS.FAULT",
            Level = AlarmLevel.Error,
            Title = "伺服故障",
            MessageTemplate = "轴 {ElementName} 故障（码 {RawCode}）",
            Trigger = "状态字 bit5 置位，或故障码非 0",
            Recovery = "执行 MC_Reset 后故障位清零",
            Remedy = "查驱动器报警码，排除后复位"
        },
        new AlarmDefinition
        {
            Code = "AXIS.WARNING",
            Level = AlarmLevel.Warning,
            Title = "伺服报警",
            MessageTemplate = "轴 {ElementName} 报警（状态字 bit4，可继续运行）",
            Trigger = "状态字 bit4 置位",
            Recovery = "报警位清零",
            Remedy = "关注趋势；不强制停机"
        },
        new AlarmDefinition
        {
            Code = "AXIS.REFRESH_FAILED",
            Level = AlarmLevel.Error,
            Title = "轴刷新失败",
            MessageTemplate = "轴 {ElementName} 刷新失败：{Detail}",
            Trigger = "元件状态为 Error，且不是故障码导致",
            Recovery = "下一次刷新成功",
            Remedy = "检查通信链路、控制器与服务状态"
        }
    ];
}
