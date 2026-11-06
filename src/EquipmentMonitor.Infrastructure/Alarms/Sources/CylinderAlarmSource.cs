using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Infrastructure.Alarms.Sources;

/// <summary>
/// 气缸报警源：把 <see cref="CylinderStatus"/> 的超时/限位矛盾翻译成统一报警。
/// </summary>
/// <remarks>
/// 故障码到报警码的映射与 <c>CylinderElement</c> 的 1001/1002 常量一致；
/// 未登记的码落到 <c>CYLINDER.FAULT</c>，保留原始码供现场排查。
/// </remarks>
public sealed class CylinderAlarmSource : IAlarmSource
{
    private const int TimeoutFaultCode = 1001;
    private const int SensorConflictFaultCode = 1002;

    private readonly IElementRegistry _registry;

    /// <summary>创建气缸报警源。</summary>
    /// <param name="registry">运行期元件注册表。</param>
    public CylinderAlarmSource(IElementRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc />
    public string SourceId => "cylinder";

    /// <inheritdoc />
    public AlarmSourceKind Kind => AlarmSourceKind.Cylinder;

    /// <inheritdoc />
    public ValueTask<AlarmSourceReading> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var alarms = new List<AlarmSignal>();

        foreach (var cylinder in _registry.Cylinders)
        {
            if (!cylinder.ElementMetadata.IsEnabled)
            {
                continue;
            }

            var status = cylinder.Status;

            if (!status.IsFault)
            {
                continue;
            }

            var code = status.FaultCode switch
            {
                TimeoutFaultCode => "CYLINDER.TIMEOUT",
                SensorConflictFaultCode => "CYLINDER.SENSOR_CONFLICT",
                _ => "CYLINDER.FAULT"
            };

            alarms.Add(new AlarmSignal
            {
                SourceId = $"cylinder:{cylinder.ElementMetadata.ElementId}",
                SourceKind = AlarmSourceKind.Cylinder,
                Code = code,
                Level = AlarmLevel.Error,
                ElementId = cylinder.ElementMetadata.ElementId,
                ElementName = cylinder.ElementMetadata.Name,
                RawCode = status.FaultCode,
                Detail = status.LastError
            });
        }

        return ValueTask.FromResult(AlarmSourceReading.Available(alarms));
    }
}
