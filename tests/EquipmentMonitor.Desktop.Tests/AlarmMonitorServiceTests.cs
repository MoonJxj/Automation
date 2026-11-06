using EquipmentMonitor.Desktop.Monitoring;
using EquipmentMonitor.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>报警采集宿主：周期来源与单轮调用。</summary>
public sealed class AlarmMonitorServiceTests
{
    [Fact]
    public async Task EvaluateOnce_调用报警服务一次()
    {
        var alarms = new RecordingAlarmService();
        var service = new AlarmMonitorService(
            alarms,
            Options.Create(new AlarmOptions { PollIntervalMs = 200 }),
            NullLogger<AlarmMonitorService>.Instance);

        await service.EvaluateOnceAsync();

        alarms.EvaluateCount.Should().Be(1);
        service.Interval.Should().Be(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void 配置周期过小_按最小100ms处理()
    {
        var service = new AlarmMonitorService(
            new RecordingAlarmService(),
            Options.Create(new AlarmOptions { PollIntervalMs = 1 }),
            NullLogger<AlarmMonitorService>.Instance);

        service.Interval.Should().Be(TimeSpan.FromMilliseconds(100));
    }
}
