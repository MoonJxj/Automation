using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Events;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>记录确认调用、可注入快照的报警服务替身（只测界面行为，不测报警状态机）。</summary>
internal sealed class RecordingAlarmService : IAlarmService
{
    public AlarmSnapshot Snapshot { get; set; } = AlarmSnapshot.Empty;

    public IReadOnlyList<AlarmRecord> History { get; set; } = [];

    public List<string> Acknowledged { get; } = [];

    public List<string> AcknowledgedAll { get; } = [];

    public int EvaluateCount { get; private set; }

    public event EventHandler<AlarmChangedEventArgs>? Changed
    {
        add { }
        remove { }
    }

    public Task EvaluateAsync(CancellationToken cancellationToken = default)
    {
        EvaluateCount++;
        return Task.CompletedTask;
    }

    public Result Report(AlarmSignal signal) => Result.Ok();

    public Result Acknowledge(string alarmId)
    {
        Acknowledged.Add(alarmId);
        return Result.Ok();
    }

    public Result AcknowledgeAll(string? sourceId = null)
    {
        AcknowledgedAll.Add(sourceId ?? "*");
        return Result.Ok();
    }

    public Task<IReadOnlyList<AlarmRecord>> QueryHistoryAsync(
        AlarmHistoryQuery query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(History);
}
