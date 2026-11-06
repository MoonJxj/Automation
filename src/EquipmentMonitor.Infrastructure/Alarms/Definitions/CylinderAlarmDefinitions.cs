using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Infrastructure.Alarms.Definitions;

/// <summary>
/// 气缸内置报警码表：与 <c>CylinderElement</c> 的 1001/1002 故障码一一对应。
/// </summary>
public sealed class CylinderAlarmDefinitions : IAlarmDefinitionProvider
{
    /// <inheritdoc />
    public IEnumerable<AlarmDefinition> GetDefinitions() =>
    [
        new AlarmDefinition
        {
            Code = "CYLINDER.TIMEOUT",
            Level = AlarmLevel.Error,
            Title = "气缸动作超时",
            MessageTemplate = "气缸 {ElementName} 动作超时仍未到位",
            Trigger = "下发方向命令后超过配置的动作超时时间",
            Recovery = "执行气缸复位",
            Remedy = "检查气路压力、电磁阀与限位开关"
        },
        new AlarmDefinition
        {
            Code = "CYLINDER.SENSOR_CONFLICT",
            Level = AlarmLevel.Error,
            Title = "气缸限位矛盾",
            MessageTemplate = "气缸 {ElementName} 伸出/缩回限位同时为 ON，位置不可信",
            Trigger = "两个限位输入同时为 ON",
            Recovery = "排除传感器/机械故障后复位",
            Remedy = "检查传感器短路、机械卡死与接线"
        },
        new AlarmDefinition
        {
            Code = "CYLINDER.FAULT",
            Level = AlarmLevel.Error,
            Title = "气缸故障",
            MessageTemplate = "气缸 {ElementName} 故障（码 {RawCode}）",
            Trigger = "元件报告未登记的故障码",
            Recovery = "排除故障后复位",
            Remedy = "检查气路、阀与限位开关"
        }
    ];
}
