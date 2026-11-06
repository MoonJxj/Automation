using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Infrastructure.Alarms.Sources;

/// <summary>
/// 轴/伺服报警源：把每根轴的 <see cref="AxisStatus"/> 翻译成统一报警。
/// </summary>
/// <remarks>
/// <para>
/// 板卡与 PLC 的差异在更下层（状态字如何读）已经消化；本类只认领域状态，
/// 因此同一份代码同时适用于雷赛/固高/Modbus 轴。
/// </para>
/// <para>
/// 每组报警的 <c>SourceId</c> 用 <c>axis:{控制器号}:{轴号}</c>，源组标识是 <c>axis</c>。
/// 禁用元件不产生报警。
/// </para>
/// </remarks>
public sealed class AxisAlarmSource : IAlarmSource
{
    private readonly IElementRegistry _registry;

    /// <summary>创建轴报警源。</summary>
    /// <param name="registry">运行期元件注册表。</param>
    public AxisAlarmSource(IElementRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc />
    public string SourceId => "axis";

    /// <inheritdoc />
    public AlarmSourceKind Kind => AlarmSourceKind.Servo;

    /// <inheritdoc />
    public ValueTask<AlarmSourceReading> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var alarms = new List<AlarmSignal>();

        foreach (var axis in _registry.Axes)
        {
            if (!axis.ElementMetadata.IsEnabled)
            {
                continue;
            }

            var status = axis.Status;
            var sourceId = $"axis:{axis.ControllerNo}:{axis.AxisNo}";

            if (status.IsFault)
            {
                alarms.Add(new AlarmSignal
                {
                    SourceId = sourceId,
                    SourceKind = AlarmSourceKind.Servo,
                    Code = "AXIS.FAULT",
                    Level = AlarmLevel.Error,
                    ElementId = axis.ElementMetadata.ElementId,
                    ElementName = axis.ElementMetadata.Name,
                    RawCode = status.FaultCode
                });
            }
            else if (status.IsWarning)
            {
                alarms.Add(new AlarmSignal
                {
                    SourceId = sourceId,
                    SourceKind = AlarmSourceKind.Servo,
                    Code = "AXIS.WARNING",
                    Level = AlarmLevel.Warning,
                    ElementId = axis.ElementMetadata.ElementId,
                    ElementName = axis.ElementMetadata.Name
                });
            }
            else if (axis.State == ElementState.Error && axis.LastError is { } error)
            {
                // 不是故障码导致的 Error：通信/刷新失败。由轴源报，避免与通信源重复。
                alarms.Add(new AlarmSignal
                {
                    SourceId = sourceId,
                    SourceKind = AlarmSourceKind.Servo,
                    Code = "AXIS.REFRESH_FAILED",
                    Level = AlarmLevel.Error,
                    ElementId = axis.ElementMetadata.ElementId,
                    ElementName = axis.ElementMetadata.Name,
                    Detail = error.Message
                });
            }
        }

        return ValueTask.FromResult(AlarmSourceReading.Available(alarms));
    }
}
