using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Alarms;
using EquipmentMonitor.Infrastructure.Alarms.Definitions;
using EquipmentMonitor.Infrastructure.Configuration;
using EquipmentMonitor.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>
/// 报警服务：上升沿/恢复/确认/抖动/源离线/瞬时事件。
/// </summary>
/// <remarks>
/// 这些用例钉住的是报警语义本身，不是某个设备：
/// 无论 PLC、板卡还是伺服接入，行为都必须一致。
/// </remarks>
public sealed class AlarmServiceTests
{
    [Fact]
    public async Task EvaluateAsync_新报警_产生未确认记录()
    {
        var source = new FakeAlarmSource { Reading = AlarmSourceReading.Available([Signal()]) };
        using var service = Create(source);

        await service.EvaluateAsync();

        var snapshot = service.Snapshot;
        snapshot.Count.Should().Be(1);
        snapshot.UnacknowledgedCount.Should().Be(1);

        var record = snapshot.Active[0];
        record.State.Should().Be(AlarmState.Active);
        record.Level.Should().Be(AlarmLevel.Error);
        record.Title.Should().Be("伺服故障");
        record.Message.Should().Contain("X 轴").And.Contain("42");
        record.Code.Should().Be("AXIS.FAULT");
        record.RaiseCount.Should().Be(1);
    }

    [Fact]
    public async Task EvaluateAsync_同一报警连续两轮_不重复写历史()
    {
        var history = new InMemoryAlarmHistoryStore();
        var source = new FakeAlarmSource { Reading = AlarmSourceReading.Available([Signal()]) };
        using var service = Create(source, history: history);

        await service.EvaluateAsync();
        await service.EvaluateAsync();

        service.Snapshot.Active[0].RaiseCount.Should().Be(1);
        history.Records.Should().ContainSingle();
    }

    [Fact]
    public async Task Acknowledge_活动报警_状态变为已确认并写审计()
    {
        var audit = TestServices.RecordingAuditService();
        var session = new SessionProvider();
        session.Set(new UserSession
        {
            UserName = "张三",
            DisplayName = "张三",
            Role = UserRole.Operator,
            Source = AuthenticationSource.LocalAccount
        });

        var source = new FakeAlarmSource { Reading = AlarmSourceReading.Available([Signal()]) };
        using var service = Create(source, audit: audit, session: session);

        await service.EvaluateAsync();
        var id = service.Snapshot.Active[0].Id;

        var result = service.Acknowledge(id);

        result.IsSuccess.Should().BeTrue(result.Error);
        service.Snapshot.Active[0].State.Should().Be(AlarmState.Acknowledged);
        service.Snapshot.Active[0].AcknowledgedBy.Should().Be("张三");
        service.Snapshot.UnacknowledgedCount.Should().Be(0);
        audit.Records.Should().Contain(record => record.EventType == AuditEventTypes.AlarmAcknowledged);
    }

    [Fact]
    public async Task Acknowledge_无权限_抛异常()
    {
        var source = new FakeAlarmSource { Reading = AlarmSourceReading.Available([Signal()]) };
        using var service = Create(source, permissions: TestServices.DenyPermissions());

        await service.EvaluateAsync();
        var id = service.Snapshot.Active[0].Id;

        var act = () => service.Acknowledge(id);

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Acknowledge_不存在的报警_返回失败()
    {
        using var service = Create(new FakeAlarmSource());

        var result = service.Acknowledge("no-such-id");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("Alarm.NotFound");
    }

    [Fact]
    public async Task EvaluateAsync_源恢复且未确认_保留为恢复未确认()
    {
        var source = new FakeAlarmSource { Reading = AlarmSourceReading.Available([Signal()]) };
        using var service = Create(source);

        await service.EvaluateAsync();
        var id = service.Snapshot.Active[0].Id;

        source.Reading = AlarmSourceReading.Available([]);
        await service.EvaluateAsync();

        service.Snapshot.Count.Should().Be(1);
        service.Snapshot.Active[0].State.Should().Be(AlarmState.RecoveredUnacknowledged);

        service.Acknowledge(id);
        service.Snapshot.Count.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAsync_源恢复且已确认_直接归档()
    {
        var source = new FakeAlarmSource { Reading = AlarmSourceReading.Available([Signal()]) };
        using var service = Create(source);

        await service.EvaluateAsync();
        service.Acknowledge(service.Snapshot.Active[0].Id);

        source.Reading = AlarmSourceReading.Available([]);
        await service.EvaluateAsync();

        service.Snapshot.Count.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAsync_源不可用_冻结已有报警并报源离线()
    {
        var source = new FakeAlarmSource { Reading = AlarmSourceReading.Available([Signal()]) };
        using var service = Create(source);

        await service.EvaluateAsync();
        var id = service.Snapshot.Active[0].Id;

        source.Reading = AlarmSourceReading.Unavailable("连接超时");
        await service.EvaluateAsync();

        service.Snapshot.Count.Should().Be(2);
        service.Snapshot.Active.Should().Contain(record => record.Id == id);
        service.Snapshot.Active.Should().Contain(
            record => record.Code == AlarmService.SourceOfflineCode && record.State == AlarmState.Active);

        // 冻结：原报警没有被当成“已恢复”。
        service.Snapshot.Active.Single(record => record.Id == id).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_源从未成功_不报离线()
    {
        var source = new FakeAlarmSource { Reading = AlarmSourceReading.Unavailable("启动期还没连上") };
        using var service = Create(source);

        await service.EvaluateAsync();

        service.Snapshot.Count.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAsync_单源异常_不影响其他源()
    {
        var broken = new FakeAlarmSource { Failure = new InvalidOperationException("源坏了") };
        var working = new FakeAlarmSource
        {
            SourceId = "cylinder",
            Kind = AlarmSourceKind.Cylinder,
            Reading = AlarmSourceReading.Available([Signal(sourceId: "cylinder:Cyl-1")])
        };

        using var service = CreateMany(broken, working);

        await service.EvaluateAsync();

        service.Snapshot.Count.Should().Be(1);
        service.Snapshot.Active[0].SourceId.Should().Be("cylinder:Cyl-1");
    }

    [Fact]
    public async Task EvaluateAsync_抖动窗口内重复_复用同一条记录()
    {
        var time = new TestTimeProvider();
        var source = new FakeAlarmSource { Reading = AlarmSourceReading.Available([Signal()]) };
        using var service = Create(source, time: time, options: new AlarmOptions
        {
            RetainUnacknowledgedOnRecovery = false,
            ChatterWindowSeconds = 5
        });

        await service.EvaluateAsync();
        var firstId = service.Snapshot.Active[0].Id;

        source.Reading = AlarmSourceReading.Available([]);
        await service.EvaluateAsync();
        service.Snapshot.Count.Should().Be(0);

        time.Advance(TimeSpan.FromSeconds(1));
        source.Reading = AlarmSourceReading.Available([Signal()]);
        await service.EvaluateAsync();

        var second = service.Snapshot.Active[0];
        second.Id.Should().Be(firstId);
        second.RaiseCount.Should().Be(2);
    }

    [Fact]
    public async Task EvaluateAsync_抖动窗口外重复_生成新记录()
    {
        var time = new TestTimeProvider();
        var source = new FakeAlarmSource { Reading = AlarmSourceReading.Available([Signal()]) };
        using var service = Create(source, time: time, options: new AlarmOptions
        {
            RetainUnacknowledgedOnRecovery = false,
            ChatterWindowSeconds = 5
        });

        await service.EvaluateAsync();
        var firstId = service.Snapshot.Active[0].Id;

        source.Reading = AlarmSourceReading.Available([]);
        await service.EvaluateAsync();

        time.Advance(TimeSpan.FromSeconds(10));
        source.Reading = AlarmSourceReading.Available([Signal()]);
        await service.EvaluateAsync();

        service.Snapshot.Active[0].Id.Should().NotBe(firstId);
    }

    [Fact]
    public void Report_瞬时报警_确认后归档()
    {
        using var service = Create(new FakeAlarmSource());

        var report = service.Report(Signal(code: "AXIS.COMMAND_FAILED", rawCode: null));

        report.IsSuccess.Should().BeTrue(report.Error);
        service.Snapshot.Count.Should().Be(1);

        var id = service.Snapshot.Active[0].Id;
        service.Acknowledge(id).IsSuccess.Should().BeTrue();

        service.Snapshot.Count.Should().Be(0);
    }

    [Fact]
    public void Report_缺少来源_返回失败()
    {
        using var service = Create(new FakeAlarmSource());

        var result = service.Report(Signal() with { SourceId = " " });

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("Alarm.InvalidSource");
    }

    [Fact]
    public async Task Report_自动恢复到期_变为恢复未确认()
    {
        var time = new TestTimeProvider();
        using var service = Create(new FakeAlarmSource(), time: time);

        service.Report(Signal(code: "AXIS.COMMAND_FAILED") with { AutoClearAfter = TimeSpan.FromSeconds(1) });
        service.Snapshot.Count.Should().Be(1);

        time.Advance(TimeSpan.FromSeconds(2));
        await service.EvaluateAsync();

        service.Snapshot.Count.Should().Be(1);
        service.Snapshot.Active[0].State.Should().Be(AlarmState.RecoveredUnacknowledged);
    }

    [Fact]
    public async Task Snapshot_按等级降序_同级按首次发生升序()
    {
        var time = new TestTimeProvider();
        var source = new FakeAlarmSource
        {
            Reading = AlarmSourceReading.Available(
            [
                Signal(code: "AXIS.WARNING", rawCode: null) with { Level = AlarmLevel.Warning },
                Signal(code: "AXIS.FAULT")
            ])
        };

        using var service = Create(source, time: time);

        await service.EvaluateAsync();

        service.Snapshot.Active[0].Level.Should().Be(AlarmLevel.Error);
        service.Snapshot.Active[1].Level.Should().Be(AlarmLevel.Warning);
        service.Snapshot.HighestLevel.Should().Be(AlarmLevel.Error);
    }

    private static AlarmSignal Signal(string code = "AXIS.FAULT", int? rawCode = 42, string sourceId = "axis:1:0")
        => new()
        {
            SourceId = sourceId,
            SourceKind = AlarmSourceKind.Servo,
            Code = code,
            Level = AlarmLevel.Error,
            RawCode = rawCode,
            ElementId = "Axis-1-0",
            ElementName = "X 轴"
        };

    private static AlarmService Create(
        FakeAlarmSource source,
        IPermissionService? permissions = null,
        RecordingAudit? audit = null,
        ISessionProvider? session = null,
        TestTimeProvider? time = null,
        InMemoryAlarmHistoryStore? history = null,
        AlarmOptions? options = null)
        => CreateMany(source, permissions, audit, session, time, history, options);

    private static AlarmService CreateMany(
        params IAlarmSource[] sources)
        => CreateMany(sources, null, null, null, null, null, null);

    private static AlarmService CreateMany(
        IAlarmSource[] sources,
        IPermissionService? permissions,
        RecordingAudit? audit,
        ISessionProvider? session,
        TestTimeProvider? time,
        InMemoryAlarmHistoryStore? history,
        AlarmOptions? options)
        => new(
            sources,
            new AlarmCatalog(
            [
                new SystemAlarmDefinitions(),
                new AxisAlarmDefinitions(),
                new CylinderAlarmDefinitions()
            ]),
            permissions ?? TestServices.AllowPermissions(),
            audit ?? TestServices.RecordingAuditService(),
            history ?? new InMemoryAlarmHistoryStore(),
            Options.Create(options ?? new AlarmOptions()),
            session,
            time ?? new TestTimeProvider());

    private static AlarmService CreateMany(
        FakeAlarmSource source,
        IPermissionService? permissions,
        RecordingAudit? audit,
        ISessionProvider? session,
        TestTimeProvider? time,
        InMemoryAlarmHistoryStore? history,
        AlarmOptions? options)
        => CreateMany([source], permissions, audit, session, time, history, options);
}
