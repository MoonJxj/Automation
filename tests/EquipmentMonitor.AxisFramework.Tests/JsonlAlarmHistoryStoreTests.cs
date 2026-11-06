using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Alarms.History;
using EquipmentMonitor.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>JSONL 历史：追加、按 Id 折叠、过滤与损坏行跳过。</summary>
public sealed class JsonlAlarmHistoryStoreTests
{
    [Fact]
    public async Task AppendAndQuery_同Id多行_查询返回最新状态()
    {
        var directory = NewDirectory();
        var store = CreateStore(directory);
        var record = Record("a1", "AXIS.FAULT", AlarmLevel.Error);

        await store.AppendAsync(record);
        await store.AppendAsync(record with
        {
            AcknowledgedAt = record.RaisedAt.AddMinutes(1),
            AcknowledgedBy = "张三"
        });

        var result = await store.QueryAsync(new AlarmHistoryQuery());

        result.Should().ContainSingle();
        result[0].Id.Should().Be("a1");
        result[0].AcknowledgedBy.Should().Be("张三");
    }

    [Fact]
    public async Task Query_按等级与来源过滤()
    {
        var directory = NewDirectory();
        var store = CreateStore(directory);

        await store.AppendAsync(Record("w1", "AXIS.WARNING", AlarmLevel.Warning) with { Key = new AlarmKey("axis:1:0", "AXIS.WARNING") });
        await store.AppendAsync(Record("e1", "AXIS.FAULT", AlarmLevel.Error) with { Key = new AlarmKey("axis:1:0", "AXIS.FAULT") });
        await store.AppendAsync(Record("c1", "CYLINDER.TIMEOUT", AlarmLevel.Error) with { Key = new AlarmKey("cylinder:Cyl-1", "CYLINDER.TIMEOUT") });

        var result = await store.QueryAsync(new AlarmHistoryQuery
        {
            MinimumLevel = AlarmLevel.Error,
            SourceId = "axis:1:0"
        });

        result.Should().ContainSingle();
        result[0].Id.Should().Be("e1");
    }

    [Fact]
    public async Task Query_损坏行被跳过_不影响其他记录()
    {
        var directory = NewDirectory();
        var store = CreateStore(directory);
        await store.AppendAsync(Record("ok", "AXIS.FAULT", AlarmLevel.Error));

        await File.AppendAllTextAsync(
            Path.Combine(directory, $"alarms-{DateTimeOffset.Now:yyyyMMdd}.jsonl"),
            "{ not valid json" + Environment.NewLine);

        var result = await store.QueryAsync(new AlarmHistoryQuery());

        result.Should().ContainSingle();
        result[0].Id.Should().Be("ok");
    }

    private static JsonlAlarmHistoryStore CreateStore(string directory)
        => new(Options.Create(new AlarmOptions
        {
            HistoryDirectory = directory,
            HistoryRetentionDays = 0
        }));

    private static AlarmRecord Record(string id, string code, AlarmLevel level) => new()
    {
        Id = id,
        Key = new AlarmKey("axis:1:0", code, 1),
        Level = level,
        SourceKind = AlarmSourceKind.Servo,
        Title = "标题",
        Message = "消息",
        RaisedAt = DateTimeOffset.UtcNow,
        LastSeenAt = DateTimeOffset.UtcNow
    };

    private static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"em-alarm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
