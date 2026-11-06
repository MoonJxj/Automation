using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Alarms;
using EquipmentMonitor.Infrastructure.Alarms.Sources;
using EquipmentMonitor.Infrastructure.Axis;
using EquipmentMonitor.Infrastructure.Hardware.Simulation;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>
/// 设备报警源：轴/气缸的领域状态 → 统一 <see cref="AlarmSignal"/>。
/// </summary>
public sealed class AlarmSourceTests
{
    [Fact]
    public async Task AxisAlarmSource_轴故障_映射AXIS_FAULT并带原始码()
    {
        var controller = new SimulatedAxisController(axisCount: 1);
        using var axis = CreateAxis(controller);

        (await axis.InitializeAsync()).IsSuccess.Should().BeTrue();
        controller.SetFault(0, 42);
        (await axis.RefreshAsync()).IsSuccess.Should().BeTrue();

        var registry = new ElementRegistry();
        registry.Attach([axis], []);

        var reading = await new AxisAlarmSource(registry).ReadAsync();

        reading.IsAvailable.Should().BeTrue();
        reading.Alarms.Should().ContainSingle();
        reading.Alarms[0].Code.Should().Be("AXIS.FAULT");
        reading.Alarms[0].RawCode.Should().Be(42);
        reading.Alarms[0].ElementName.Should().Be("X 轴");
        reading.Alarms[0].SourceId.Should().Be("axis:0:0");
    }

    [Fact]
    public async Task AxisAlarmSource_禁用轴_不产生报警()
    {
        var controller = new SimulatedAxisController(axisCount: 1);
        using var axis = CreateAxis(controller, isEnabled: false);

        (await axis.InitializeAsync()).IsSuccess.Should().BeTrue();

        var registry = new ElementRegistry();
        registry.Attach([axis], []);

        var reading = await new AxisAlarmSource(registry).ReadAsync();

        reading.Alarms.Should().BeEmpty();
    }

    [Fact]
    public async Task CylinderAlarmSource_超时故障_映射CYLINDER_TIMEOUT()
    {
        var cylinder = new StubCylinder
        {
            Status = FaultStatus(1001, "动作超时")
        };

        var registry = new ElementRegistry();
        registry.Attach([], [cylinder]);

        var reading = await new CylinderAlarmSource(registry).ReadAsync();

        reading.Alarms.Should().ContainSingle();
        reading.Alarms[0].Code.Should().Be("CYLINDER.TIMEOUT");
        reading.Alarms[0].RawCode.Should().Be(1001);
        reading.Alarms[0].SourceId.Should().Be("cylinder:Cyl-1");
    }

    [Fact]
    public async Task CylinderAlarmSource_限位矛盾_映射CYLINDER_SENSOR_CONFLICT()
    {
        var cylinder = new StubCylinder
        {
            Status = FaultStatus(1002, "限位同时为 ON")
        };

        var registry = new ElementRegistry();
        registry.Attach([], [cylinder]);

        var reading = await new CylinderAlarmSource(registry).ReadAsync();

        reading.Alarms.Should().ContainSingle();
        reading.Alarms[0].Code.Should().Be("CYLINDER.SENSOR_CONFLICT");
    }

    [Fact]
    public async Task CylinderAlarmSource_其他故障码_映射CYLINDER_FAULT()
    {
        var cylinder = new StubCylinder
        {
            Status = FaultStatus(9999, "未知故障")
        };

        var registry = new ElementRegistry();
        registry.Attach([], [cylinder]);

        var reading = await new CylinderAlarmSource(registry).ReadAsync();

        reading.Alarms.Should().ContainSingle();
        reading.Alarms[0].Code.Should().Be("CYLINDER.FAULT");
        reading.Alarms[0].RawCode.Should().Be(9999);
    }

    [Fact]
    public async Task CylinderAlarmSource_无故障_不产生报警()
    {
        var cylinder = new StubCylinder { Status = CylinderStatus.Unknown };

        var registry = new ElementRegistry();
        registry.Attach([], [cylinder]);

        var reading = await new CylinderAlarmSource(registry).ReadAsync();

        reading.Alarms.Should().BeEmpty();
    }

    private static AxisElement CreateAxis(SimulatedAxisController controller, bool isEnabled = true)
        => new(
            new AxisElementConfig
            {
                ElementId = "Axis-0-0",
                Name = "X 轴",
                ControllerNo = 0,
                AxisNo = 0,
                IsEnabled = isEnabled
            },
            controller,
            TestServices.AllowPermissions(),
            TestServices.RecordingAuditService());

    private static CylinderStatus FaultStatus(int code, string message) => new(
        IsEnabled: true,
        Position: CylinderPosition.Fault,
        ExtendedSensor: false,
        RetractedSensor: false,
        IsMoving: false,
        IsFault: true,
        FaultCode: code,
        LastError: message,
        UpdatedAt: DateTimeOffset.UtcNow);
}
