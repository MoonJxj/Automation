using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Infrastructure.Alarms.Sources;

/// <summary>
/// 演示用报警源：仅在宿主带 <c>--demo-alarms</c> 启动时注册，用于界面验收/演示。
/// </summary>
/// <remarks>
/// <para>
/// 它返回固定的三条报警（伺服故障、伺服报警、气缸超时），让报警页在没有真实设备故障时
/// 也能展示“未确认/已确认/历史”的完整链路。
/// </para>
/// <para>
/// <b>不得默认注册</b>：现场启动不应凭空出现报警。注册条件集中在
/// <c>Program.ConfigureServices(..., demoAlarms)</c>，默认 <c>false</c>。
/// </para>
/// </remarks>
public sealed class DemoAlarmSource : IAlarmSource
{
    /// <inheritdoc />
    public string SourceId => "demo";

    /// <inheritdoc />
    public AlarmSourceKind Kind => AlarmSourceKind.System;

    /// <inheritdoc />
    public ValueTask<AlarmSourceReading> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(AlarmSourceReading.Available(
        [
            new AlarmSignal
            {
                SourceId = "demo:axis-1",
                SourceKind = AlarmSourceKind.Servo,
                Code = "AXIS.FAULT",
                Level = AlarmLevel.Error,
                ElementId = "Demo-Axis-1",
                ElementName = "X 轴（演示）",
                RawCode = 42
            },
            new AlarmSignal
            {
                SourceId = "demo:axis-2",
                SourceKind = AlarmSourceKind.Servo,
                Code = "AXIS.WARNING",
                Level = AlarmLevel.Warning,
                ElementId = "Demo-Axis-2",
                ElementName = "Y 轴（演示）"
            },
            new AlarmSignal
            {
                SourceId = "demo:cyl-1",
                SourceKind = AlarmSourceKind.Cylinder,
                Code = "CYLINDER.TIMEOUT",
                Level = AlarmLevel.Error,
                ElementId = "Demo-Cyl-1",
                ElementName = "夹紧气缸（演示）",
                RawCode = 1001
            }
        ]));
    }
}
