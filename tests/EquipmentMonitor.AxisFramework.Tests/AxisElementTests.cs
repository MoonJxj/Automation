using EquipmentMonitor.Core;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Axis;
using EquipmentMonitor.Infrastructure.Hardware.Simulation;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

public class AxisElementTests
{
    [Fact]
    public async Task InitializeAsync_WhenControllerInitializes_BecomesReady()
    {
        var axis = CreateAxis(out _);

        var result = await axis.InitializeAsync();

        result.IsSuccess.Should().BeTrue();
        axis.IsInitialized.Should().BeTrue();
        axis.State.Should().Be(ElementState.Ready);
        axis.Status.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task ServeOnAsync_EnablesAxis()
    {
        var axis = CreateAxis(out _);
        await axis.InitializeAsync();

        var result = await axis.ServeOnAsync();
        await axis.RefreshAsync();

        result.IsSuccess.Should().BeTrue();
        axis.Status.IsEnabled.Should().BeTrue();
        axis.State.Should().Be(ElementState.Ready);
    }

    [Fact]
    public async Task MoveAbsAsync_WhenTargetValid_MovesAndReportsInPosition()
    {
        var axis = CreateAxis(out _);
        await axis.InitializeAsync();
        await axis.ServeOnAsync();

        var result = await axis.MoveAbsAsync(120.0);
        await axis.RefreshAsync();

        result.IsSuccess.Should().BeTrue();
        axis.Status.ActualPosition.Should().Be(120.0);
        axis.Status.IsMoving.Should().BeFalse();
        axis.Status.InPosition.Should().BeTrue();
        axis.State.Should().Be(ElementState.Ready);
    }

    [Fact]
    public async Task HomeAsync_MovesToZeroAndSetsHomeFlag()
    {
        var axis = CreateAxis(out var simulator);
        simulator.SetPosition(0, 50.0);
        await axis.InitializeAsync();
        await axis.ServeOnAsync();

        var result = await axis.HomeAsync();
        await axis.RefreshAsync();

        result.IsSuccess.Should().BeTrue();
        axis.Status.ActualPosition.Should().Be(0.0);
        axis.Status.IsHome.Should().BeTrue();
        axis.Status.IsMoving.Should().BeFalse();
    }

    [Fact]
    public async Task MoveAbsAsync_WhenMotionInProgress_ReturnsAfterCommandIssued()
    {
        var axis = CreateAxis(out var simulator);
        simulator.MotionDuration = TimeSpan.FromSeconds(5);
        await axis.InitializeAsync();
        await axis.ServeOnAsync();

        var result = await axis.MoveAbsAsync(10.0);
        await axis.RefreshAsync();

        result.IsSuccess.Should().BeTrue();
        axis.Status.IsMoving.Should().BeTrue();
        axis.Status.InPosition.Should().BeFalse();
    }

    [Fact]
    public async Task ResetAsync_WhenAxisFaulted_ClearsFault()
    {
        var axis = CreateAxis(out var simulator);
        await axis.InitializeAsync();
        simulator.SetFault(0, faultCode: 42);
        await axis.RefreshAsync();

        axis.Status.IsFault.Should().BeTrue();
        axis.State.Should().Be(ElementState.Error);

        var result = await axis.ResetAsync();
        await axis.RefreshAsync();

        result.IsSuccess.Should().BeTrue();
        axis.Status.IsFault.Should().BeFalse();
        axis.Status.FaultCode.Should().Be(0);
        axis.State.Should().Be(ElementState.Ready);
    }

    [Fact]
    public async Task ServeOnAsync_WhenAxisFaulted_IsRejectedWithoutWaitingForTimeout()
    {
        var axis = CreateAxis(out var simulator);
        await axis.InitializeAsync();
        simulator.SetFault(0, faultCode: 7);
        await axis.RefreshAsync();

        var result = await axis.ServeOnAsync();

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("COMMAND_BLOCKED");
    }

    [Fact]
    public async Task ReadStateWordAsync_WhenTypeUnknown_ReturnsFailure()
    {
        var axis = CreateAxis(out _);
        await axis.InitializeAsync();

        var result = await axis.ReadStateWordAsync("NoSuchWord");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("Axis.ReadStateWordFailed");
    }

    [Fact]
    public async Task RefreshAsync_MapsStatusWordAndFaultCode()
    {
        var axis = CreateAxis(out var simulator);
        simulator.SetPosition(0, 12.5);
        await axis.InitializeAsync();
        await axis.ServeOnAsync();
        simulator.SetFault(0, faultCode: 99);

        var result = await axis.RefreshAsync();

        result.IsSuccess.Should().BeTrue();
        axis.Status.IsFault.Should().BeTrue();
        axis.Status.FaultCode.Should().Be(99);
        axis.Status.IsEnabled.Should().BeFalse();
        axis.Status.UniaxialState.Should().Be(UniaxialState.Errorstop);
        axis.Status.ActualPosition.Should().Be(12.5);
    }

    private static AxisElement CreateAxis(
        out SimulatedAxisController simulator,
        Func<AxisElementConfig, AxisElementConfig>? configure = null)
    {
        simulator = new SimulatedAxisController();

        var config = new AxisElementConfig
        {
            ElementId = "X",
            Name = "X 轴",
            ControllerNo = 0,
            AxisNo = 0,
            DefaultVelocity = 10.0
        };

        if (configure is not null)
        {
            config = configure(config);
        }

        return new AxisElement(
            config,
            simulator,
            TestServices.AllowPermissions(),
            TestServices.RecordingAuditService());
    }
}
