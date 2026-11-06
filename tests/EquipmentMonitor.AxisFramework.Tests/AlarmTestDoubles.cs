using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>可控的报警源：测试通过 <see cref="Reading"/> / <see cref="Failure"/> 驱动每轮读数。</summary>
internal sealed class FakeAlarmSource : IAlarmSource
{
    public string SourceId { get; init; } = "axis";

    public AlarmSourceKind Kind { get; init; } = AlarmSourceKind.Servo;

    public AlarmSourceReading Reading { get; set; } = AlarmSourceReading.Available([]);

    public Exception? Failure { get; set; }

    public int ReadCount { get; private set; }

    public ValueTask<AlarmSourceReading> ReadAsync(CancellationToken cancellationToken = default)
    {
        ReadCount++;

        if (Failure is not null)
        {
            throw Failure;
        }

        return ValueTask.FromResult(Reading);
    }
}

/// <summary>内存历史：记录每次追加，供断言“是否重复写历史”。</summary>
internal sealed class InMemoryAlarmHistoryStore : IAlarmHistoryStore
{
    private readonly List<AlarmRecord> _records = [];

    public IReadOnlyList<AlarmRecord> Records
    {
        get
        {
            lock (_records)
            {
                return _records.ToArray();
            }
        }
    }

    public Task AppendAsync(AlarmRecord record, CancellationToken cancellationToken = default)
    {
        lock (_records)
        {
            _records.Add(record);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AlarmRecord>> QueryAsync(
        AlarmHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        lock (_records)
        {
            IReadOnlyList<AlarmRecord> result = _records
                .Where(record => query.MinimumLevel is not { } minimum || record.Level >= minimum)
                .Where(record => query.SourceId is null || record.SourceId == query.SourceId)
                .Where(record => query.State is not { } state || record.State == state)
                .OrderByDescending(record => record.RaisedAt)
                .Take(query.Limit)
                .ToArray();

            return Task.FromResult(result);
        }
    }
}

/// <summary>可控时间源：验证抖动窗口与“最近发生时间”而不真实等待。</summary>
internal sealed class TestTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan delta) => Now += delta;
}

/// <summary>
/// 气缸桩：只用于验证 <c>CylinderAlarmSource</c> 的码映射，不参与命令/IO。
/// </summary>
internal sealed class StubCylinder : IElementCylinder
{
    public ElementMetadata ElementMetadata { get; init; } = new()
    {
        ElementId = "Cyl-1",
        ElementType = "Cylinder",
        Name = "夹紧气缸"
    };

    public ElementState State { get; set; } = ElementState.Ready;

    public bool IsInitialized => true;

    public CylinderStatus Status { get; set; } = CylinderStatus.Unknown;

    public bool IsDoubleActing => false;

    public string? LastError => Status.LastError;

    public double ActionTimeoutSeconds => 3.0;

    public IReadOnlyList<ElementCommandDescriptor> Commands => [];

    public Task<Result> InitializeAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Ok());

    public Task<Result> RefreshAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Ok());

    public Task<Result> ResetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Ok());

    public CommandAvailability EvaluateCommand(string commandKey) => CommandAvailability.Available;

    public Task<Result<ElementCommandResult>> ExecuteCommandAsync(
        ElementCommandRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Fail<ElementCommandResult>("桩元件不执行命令。", "Stub.NotSupported"));
}
