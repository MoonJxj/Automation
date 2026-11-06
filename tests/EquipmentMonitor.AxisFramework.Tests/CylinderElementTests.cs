using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Core.Cylinder;
using EquipmentMonitor.Infrastructure.Hardware.Simulation;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>
/// 气缸元件：单控/双控、限位推导、超时故障、权限与审计。
/// </summary>
public sealed class CylinderElementTests
{
    [Fact]
    public void Commands_声明四条命令()
    {
        var (cylinder, _, _) = Create();

        cylinder.Commands.Should().HaveCount(4);
        cylinder.Commands.Select(command => command.Key).Should().BeEquivalentTo(
            [CylinderElement.CommandExtend, CylinderElement.CommandRetract,
             CylinderElement.CommandHalt, CylinderElement.CommandReset]);
    }

    [Fact]
    public async Task 初始为缩回到位_伸出后到伸出位()
    {
        var (cylinder, _, _) = Create();

        await cylinder.InitializeAsync();
        cylinder.Status.Position.Should().Be(CylinderPosition.Retracted);

        var extend = await ExecuteAsync(cylinder, CylinderElement.CommandExtend);
        extend.IsFailure.Should().BeFalse();
        extend.Value.Outcome.Should().Be(CommandOutcome.Accepted);

        await cylinder.RefreshAsync();

        cylinder.Status.Position.Should().Be(CylinderPosition.Extended);
        cylinder.Status.ExtendedSensor.Should().BeTrue();
        cylinder.Status.RetractedSensor.Should().BeFalse();
        cylinder.State.Should().Be(ElementState.Ready);
    }

    [Fact]
    public async Task 伸出后再伸出_被互锁拒绝()
    {
        var (cylinder, _, _) = Create();
        await cylinder.InitializeAsync();
        await ExecuteAsync(cylinder, CylinderElement.CommandExtend);
        await cylinder.RefreshAsync();

        var again = await ExecuteAsync(cylinder, CylinderElement.CommandExtend);

        again.IsFailure.Should().BeTrue();
        again.ErrorCode.Should().Be(CommandRejectionCodes.Blocked);
        again.Error.Should().Contain("已伸出");
    }

    [Fact]
    public async Task 动作超时_置故障并阻断后续动作_复位后恢复()
    {
        var (cylinder, io, _) = Create(timeoutSeconds: 0.05);
        io.MotionDuration = TimeSpan.FromSeconds(5);

        await cylinder.InitializeAsync();

        var extend = await ExecuteAsync(cylinder, CylinderElement.CommandExtend);
        extend.IsFailure.Should().BeFalse();

        await Task.Delay(80);
        await cylinder.RefreshAsync();

        cylinder.Status.IsFault.Should().BeTrue();
        cylinder.Status.Position.Should().Be(CylinderPosition.Fault);
        cylinder.EvaluateCommand(CylinderElement.CommandRetract).IsAvailable.Should().BeFalse();

        var blocked = await ExecuteAsync(cylinder, CylinderElement.CommandRetract);
        blocked.IsFailure.Should().BeTrue();
        blocked.ErrorCode.Should().Be(CommandRejectionCodes.Blocked);

        var reset = await ExecuteAsync(cylinder, CylinderElement.CommandReset);
        reset.IsFailure.Should().BeFalse();

        cylinder.EvaluateCommand(CylinderElement.CommandExtend).IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task 无操作权限_抛UnauthorizedAccessException()
    {
        var io = new SimulatedIoController();
        using var cylinder = new CylinderElement(Config(), io, TestServices.DenyPermissions(), TestServices.RecordingAuditService());
        await cylinder.InitializeAsync();

        var act = () => ExecuteAsync(cylinder, CylinderElement.CommandExtend);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task 伸出与缩回都写审计()
    {
        var (cylinder, _, audit) = Create();
        await cylinder.InitializeAsync();

        await ExecuteAsync(cylinder, CylinderElement.CommandExtend);
        await cylinder.RefreshAsync();
        await ExecuteAsync(cylinder, CylinderElement.CommandRetract);

        audit.Records.Should().Contain(record =>
            record.EventType == AuditEventTypes.CommandExecuted && record.Detail.Contains(CylinderElement.CommandExtend));
        audit.Records.Should().Contain(record =>
            record.EventType == AuditEventTypes.CommandExecuted && record.Detail.Contains(CylinderElement.CommandRetract));
    }

    private static Task<Result<ElementCommandResult>> ExecuteAsync(CylinderElement cylinder, string key)
        => cylinder.ExecuteCommandAsync(new ElementCommandRequest { CommandKey = key });

    private static (CylinderElement Cylinder, SimulatedIoController Io, RecordingAudit Audit) Create(
        double timeoutSeconds = 2.0)
    {
        var io = new SimulatedIoController { MotionDuration = TimeSpan.Zero };
        var config = Config(timeoutSeconds);

        io.ConfigureCylinder(config.ExtendOutput, config.RetractOutput ?? -1, config.ExtendedInput, config.RetractedInput);

        var audit = TestServices.RecordingAuditService();
        var cylinder = new CylinderElement(config, io, TestServices.AllowPermissions(), audit);

        return (cylinder, io, audit);
    }

    private static CylinderConfig Config(double timeoutSeconds = 2.0) => new()
    {
        ElementId = "Cyl-1",
        Name = "夹紧气缸",
        ControllerConfigId = 1,
        ControllerNo = 0,
        ExtendOutput = 0,
        RetractOutput = 1,
        ExtendedInput = 0,
        RetractedInput = 1,
        ActionTimeoutSeconds = timeoutSeconds
    };
}
