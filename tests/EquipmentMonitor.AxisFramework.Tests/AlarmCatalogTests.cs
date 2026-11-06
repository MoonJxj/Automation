using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Alarms;
using EquipmentMonitor.Infrastructure.Alarms.Definitions;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>报警码表：合并、覆盖与未定义码回退。</summary>
public sealed class AlarmCatalogTests
{
    [Fact]
    public void Resolve_已登记码_使用码表定义()
    {
        var catalog = new AlarmCatalog([new AxisAlarmDefinitions()]);

        var definition = catalog.Resolve(Signal("AXIS.FAULT"));

        definition.Title.Should().Be("伺服故障");
        definition.Level.Should().Be(AlarmLevel.Error);
        definition.Remedy.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Resolve_未登记码_回退且等级取上报值()
    {
        var catalog = new AlarmCatalog([new AxisAlarmDefinitions()]);

        var definition = catalog.Resolve(Signal("PLC.ALM.9999") with { Level = AlarmLevel.Warning });

        definition.Title.Should().Be("未定义报警");
        definition.Level.Should().Be(AlarmLevel.Warning);
        definition.MessageTemplate.Should().Contain("{Code}");
        definition.MessageTemplate.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Resolve_后注册的provider覆盖先注册()
    {
        var catalog = new AlarmCatalog(
        [
            new AxisAlarmDefinitions(),
            new OverrideAxisFault()
        ]);

        var definition = catalog.Resolve(Signal("AXIS.FAULT"));

        definition.Title.Should().Be("现场覆盖的伺服故障");
    }

    [Fact]
    public void Resolve_没有任何provider_仍然返回回退定义()
    {
        var catalog = new AlarmCatalog([]);

        var definition = catalog.Resolve(Signal("UNKNOWN"));

        definition.Should().NotBeNull();
        definition.Title.Should().Be("未定义报警");
    }

    private static AlarmSignal Signal(string code) => new()
    {
        SourceId = "axis:1:0",
        SourceKind = AlarmSourceKind.Servo,
        Code = code,
        Level = AlarmLevel.Error
    };

    private sealed class OverrideAxisFault : IAlarmDefinitionProvider
    {
        public IEnumerable<AlarmDefinition> GetDefinitions() =>
        [
            new AlarmDefinition
            {
                Code = "AXIS.FAULT",
                Level = AlarmLevel.Critical,
                Title = "现场覆盖的伺服故障"
            }
        ];
    }
}
