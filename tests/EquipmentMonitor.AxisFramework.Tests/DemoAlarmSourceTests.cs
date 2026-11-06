using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Infrastructure.Alarms.Sources;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>演示报警源：只在 --demo-alarms 时注册，返回固定三条可辨识报警。</summary>
public sealed class DemoAlarmSourceTests
{
    [Fact]
    public async Task ReadAsync_返回三条演示报警()
    {
        var reading = await new DemoAlarmSource().ReadAsync();

        reading.IsAvailable.Should().BeTrue();
        reading.Alarms.Should().HaveCount(3);
        reading.Alarms.Select(alarm => alarm.Code).Should().BeEquivalentTo(
            ["AXIS.FAULT", "AXIS.WARNING", "CYLINDER.TIMEOUT"]);
        reading.Alarms.Should().OnlyContain(alarm => alarm.SourceId.StartsWith("demo:", StringComparison.Ordinal));
        reading.Alarms.Should().Contain(alarm => alarm.Level == AlarmLevel.Warning);
        reading.Alarms.Should().Contain(alarm => alarm.RawCode == 42);
        reading.Alarms.Should().Contain(alarm => alarm.RawCode == 1001);
    }
}
