using EquipmentMonitor.Core;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Monitoring;
using EquipmentMonitor.Desktop.Operation;
using FluentAssertions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 设备状态汇总：优先级与摘要文案一次锁定，页面不再各写一套。
/// </summary>
public sealed class DeviceStatusEvaluatorTests
{
    [Fact]
    public void Evaluate_运行中_优先于待机()
    {
        var run = new MachineRunSnapshot(MachineRunState.Running, MachineRunActivity.None, DateTimeOffset.Now, "已启动", null);
        var axes = Snapshot(Axis("X", ElementState.Ready));

        var status = DeviceStatusEvaluator.Evaluate(run, axes, CylinderMonitorSnapshot.Empty, null);

        status.Text.Should().Be("运行中");
        status.Kind.Should().Be(DeviceStatusKind.Running);
        status.Summary.Should().Contain("轴 1/1 就绪");
        status.Summary.Should().Contain("采集正常");
    }

    [Fact]
    public void Evaluate_轴故障_优先于运行状态()
    {
        var run = new MachineRunSnapshot(MachineRunState.Running, MachineRunActivity.None, DateTimeOffset.Now, "已启动", null);
        var axes = Snapshot(Axis("X", ElementState.Error, fault: true));

        var status = DeviceStatusEvaluator.Evaluate(run, axes, CylinderMonitorSnapshot.Empty, null);

        status.Text.Should().Be("故障");
        status.Kind.Should().Be(DeviceStatusKind.Fault);
    }

    [Fact]
    public void Evaluate_启动中_显示忙状态()
    {
        var run = new MachineRunSnapshot(MachineRunState.Stopped, MachineRunActivity.Starting, DateTimeOffset.Now, null, null);
        var axes = Snapshot(Axis("X", ElementState.Ready));

        var status = DeviceStatusEvaluator.Evaluate(run, axes, CylinderMonitorSnapshot.Empty, null);

        status.Text.Should().Be("启动中…");
        status.Kind.Should().Be(DeviceStatusKind.Busy);
    }

    [Fact]
    public void Evaluate_采集异常_无元件时也可见()
    {
        var run = MachineRunSnapshot.Empty;
        var axes = new AxisMonitorSnapshot([], DateTimeOffset.Now, "Axis-0-0：尚未初始化");

        var status = DeviceStatusEvaluator.Evaluate(run, axes, CylinderMonitorSnapshot.Empty, null);

        status.Text.Should().Be("采集异常");
        status.Kind.Should().Be(DeviceStatusKind.CollectionWarning);
        status.Summary.Should().Contain("采集异常");
    }

    [Fact]
    public void Evaluate_全部就绪_待机()
    {
        var run = MachineRunSnapshot.Empty;
        var axes = Snapshot(Axis("X", ElementState.Ready));
        var cylinders = new CylinderMonitorSnapshot(
            [
                new CylinderSnapshot(
                    "Cyl-1",
                    "夹紧气缸",
                    0,
                    ElementState.Ready,
                    new CylinderStatus(
                        IsEnabled: true,
                        Position: CylinderPosition.Retracted,
                        ExtendedSensor: false,
                        RetractedSensor: true,
                        IsMoving: false,
                        IsFault: false,
                        FaultCode: 0,
                        LastError: null,
                        UpdatedAt: DateTimeOffset.Now),
                    null,
                    DateTimeOffset.Now)
            ],
            DateTimeOffset.Now,
            null);

        var status = DeviceStatusEvaluator.Evaluate(run, axes, cylinders, null);

        status.Text.Should().Be("待机");
        status.Kind.Should().Be(DeviceStatusKind.Idle);
        status.Summary.Should().Contain("气缸 1");
    }

    private static AxisMonitorSnapshot Snapshot(params AxisSnapshot[] axes)
        => new(axes, DateTimeOffset.Now, null);

    private static AxisSnapshot Axis(string name, ElementState state, bool fault = false)
        => new(
            $"Axis-{name}",
            name,
            0,
            0,
            state,
            new AxisStatus { IsFault = fault, FaultCode = fault ? 42 : 0 },
            null,
            DateTimeOffset.Now);
}
