using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Axis;
using EquipmentMonitor.Infrastructure.Hardware.Simulation;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>
/// 命令契约：权限、参数校验、互锁、审计 —— 都在 <see cref="AxisElement"/> 的单一入口上验证。
/// </summary>
public sealed class AxisElementCommandTests
{
    [Fact]
    public void Commands_声明九条命令_且全部通过校验()
    {
        var axis = CreateAxis(out _, out _);

        axis.Commands.Should().HaveCount(9);
        axis.Commands.Select(command => command.Key).Should().OnlyHaveUniqueItems();

        foreach (var command in axis.Commands)
        {
            command.Invoking(c => c.Validate()).Should().NotThrow();
        }
    }

    [Fact]
    public async Task MoveAbs_未使能_被互锁拒绝并给出原因()
    {
        var axis = CreateAxis(out _, out var audit);
        await axis.InitializeAsync();

        var result = await axis.MoveAbsAsync(10.0);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(CommandRejectionCodes.Blocked);
        result.Error.Should().Contain("未使能");
        audit.Records.Should().Contain(record => record.EventType == AuditEventTypes.CommandRejected);
    }

    [Fact]
    public async Task MoveAbs_超出软限位_在入队前被参数校验拒绝()
    {
        var axis = CreateAxis(
            out _,
            out _,
            config => config with { SoftLimitMin = -100, SoftLimitMax = 100 });

        await axis.InitializeAsync();
        await axis.ServeOnAsync();

        var result = await axis.MoveAbsAsync(200.0);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(CommandRejectionCodes.InvalidArgument);
        result.Error.Should().Contain("超出范围");
    }

    [Fact]
    public async Task ExecuteCommand_无操作权限_抛UnauthorizedAccessException()
    {
        var simulator = new SimulatedAxisController();
        using var axis = new AxisElement(Config(), simulator, TestServices.DenyPermissions(), TestServices.RecordingAuditService());

        await axis.InitializeAsync();

        var act = () => axis.ServeOnAsync();

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ExecuteCommand_未知命令_拒绝并写审计()
    {
        var axis = CreateAxis(out _, out var audit);
        await axis.InitializeAsync();

        var result = await axis.ExecuteCommandAsync(
            new ElementCommandRequest { CommandKey = "MC_NoSuchCommand" });

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(CommandRejectionCodes.UnknownCommand);
        audit.Records.Should().Contain(record =>
            record.EventType == AuditEventTypes.CommandRejected &&
            record.Detail.Contains("MC_NoSuchCommand"));
    }

    [Fact]
    public async Task ExecuteCommand_使能_返回Accepted并写执行审计()
    {
        var axis = CreateAxis(out _, out var audit);
        await axis.InitializeAsync();

        var result = await axis.ExecuteCommandAsync(new ElementCommandRequest
        {
            CommandKey = AxisElement.CommandPower,
            Arguments = new Dictionary<string, CommandValue>
            {
                [CommandParameterKeys.Enable] = CommandValue.FromBool(true)
            }
        });

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Outcome.Should().Be(CommandOutcome.Accepted);
        audit.Records.Should().Contain(record => record.EventType == AuditEventTypes.CommandExecuted);
    }

    [Fact]
    public async Task EvaluateCommand_未使能_移动命令不可用且原因可读()
    {
        var axis = CreateAxis(out _, out _);
        await axis.InitializeAsync();

        var availability = axis.EvaluateCommand(AxisElement.CommandMoveAbsolute);

        availability.IsAvailable.Should().BeFalse();
        availability.Reason.Should().NotBeNullOrWhiteSpace().And.Contain("未使能");
    }

    [Fact]
    public async Task EvaluateCommand_未知命令_返回Blocked而不抛异常()
    {
        var axis = CreateAxis(out _, out _);
        await axis.InitializeAsync();

        var availability = axis.EvaluateCommand("MC_NoSuchCommand");

        availability.IsAvailable.Should().BeFalse();
        availability.Reason.Should().Contain("未知命令");
    }

    [Fact]
    public async Task ExecuteCommand_缺参数_在接受层拒绝且不写报文()
    {
        var axis = CreateAxis(out var simulator, out var audit);
        await axis.InitializeAsync();
        await axis.ServeOnAsync();

        simulator.SetFault(0, faultCode: 12);

        var result = await axis.ExecuteCommandAsync(
            new ElementCommandRequest { CommandKey = AxisElement.CommandMoveAbsolute });

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(CommandRejectionCodes.InvalidArgument);

        audit.Records.Should().Contain(record => record.EventType == AuditEventTypes.CommandRejected);
    }

    private static AxisElement CreateAxis(
        out SimulatedAxisController simulator,
        out RecordingAudit audit,
        Func<AxisElementConfig, AxisElementConfig>? configure = null)
    {
        simulator = new SimulatedAxisController();
        audit = TestServices.RecordingAuditService();

        var config = Config();
        if (configure is not null)
        {
            config = configure(config);
        }

        return new AxisElement(config, simulator, TestServices.AllowPermissions(), audit);
    }

    private static AxisElementConfig Config() => new()
    {
        ElementId = "X",
        Name = "X 轴",
        ControllerNo = 0,
        AxisNo = 0,
        DefaultVelocity = 10.0
    };
}
