using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Monitoring;
using EquipmentMonitor.Desktop.Operation;
using EquipmentMonitor.Infrastructure.Auditing;
using EquipmentMonitor.Infrastructure.Hardware.Simulation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 机台级启停：验证“命令结果决定状态”，不把失败伪装成成功。
/// </summary>
public sealed class MachineRunServiceTests
{
    [Fact]
    public async Task StartAsync_轴就绪_进入运行并下发使能()
    {
        var controller = new SimulatedAxisController(axisCount: 2);
        using var axis0 = CreateAxis(controller, 0);
        using var axis1 = CreateAxis(controller, 1);

        (await axis0.InitializeAsync()).IsSuccess.Should().BeTrue();
        (await axis1.InitializeAsync()).IsSuccess.Should().BeTrue();

        var store = new AxisSnapshotStore();
        store.Attach([axis0, axis1]);

        var audit = new RecordingAuditService();
        var service = CreateService(store, DesktopTestServices.AllowPermissions(), audit);

        var result = await service.StartAsync("测试启动");

        result.IsSuccess.Should().BeTrue(result.Error);
        service.Snapshot.State.Should().Be(MachineRunState.Running);
        service.Snapshot.IsBusy.Should().BeFalse();

        await axis0.RefreshAsync();
        await axis1.RefreshAsync();

        axis0.Status.IsEnabled.Should().BeTrue();
        axis1.Status.IsEnabled.Should().BeTrue();
        audit.Events.Should().Contain(AuditEventTypes.MachineRunStarted);
    }

    [Fact]
    public async Task StartAsync_轴故障_拒绝启动并保持停止()
    {
        var controller = new SimulatedAxisController(axisCount: 1);
        using var axis = CreateAxis(controller, 0);

        (await axis.InitializeAsync()).IsSuccess.Should().BeTrue();
        controller.SetFault(0, faultCode: 42);
        (await axis.RefreshAsync()).IsSuccess.Should().BeTrue();
        axis.Status.IsFault.Should().BeTrue();

        var store = new AxisSnapshotStore();
        store.Attach([axis]);

        var service = CreateService(store, DesktopTestServices.AllowPermissions(), new RecordingAuditService());
        var result = await service.StartAsync("测试启动");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("故障");
        service.Snapshot.State.Should().Be(MachineRunState.Stopped);
    }

    [Fact]
    public async Task StartAsync_无操作权限_抛出越权异常()
    {
        var store = new AxisSnapshotStore();
        var service = CreateService(store, DesktopTestServices.DenyPermissions(), new RecordingAuditService());

        var act = () => service.StartAsync("测试启动");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        service.Snapshot.State.Should().Be(MachineRunState.Stopped);
    }

    [Fact]
    public async Task StartAsync_已运行_拒绝重复启动()
    {
        var controller = new SimulatedAxisController(axisCount: 1);
        using var axis = CreateAxis(controller, 0);
        (await axis.InitializeAsync()).IsSuccess.Should().BeTrue();

        var store = new AxisSnapshotStore();
        store.Attach([axis]);

        var service = CreateService(store, DesktopTestServices.AllowPermissions(), new RecordingAuditService());
        (await service.StartAsync("第一次")).IsSuccess.Should().BeTrue();

        var second = await service.StartAsync("第二次");

        second.IsFailure.Should().BeTrue();
        second.ErrorCode.Should().Be("MachineRun.AlreadyRunning");
        service.Snapshot.State.Should().Be(MachineRunState.Running);
    }

    [Fact]
    public async Task StopAsync_运行中_下发停止且保持使能()
    {
        var controller = new SimulatedAxisController(axisCount: 1);
        using var axis = CreateAxis(controller, 0);
        (await axis.InitializeAsync()).IsSuccess.Should().BeTrue();

        var store = new AxisSnapshotStore();
        store.Attach([axis]);

        var service = CreateService(store, DesktopTestServices.AllowPermissions(), new RecordingAuditService());
        (await service.StartAsync("测试启动")).IsSuccess.Should().BeTrue();

        (await axis.ForwardJogAsync()).IsSuccess.Should().BeTrue();
        (await axis.RefreshAsync()).IsSuccess.Should().BeTrue();
        axis.State.Should().Be(ElementState.Running);

        var result = await service.StopAsync("测试停止");

        result.IsSuccess.Should().BeTrue(result.Error);
        service.Snapshot.State.Should().Be(MachineRunState.Stopped);

        await axis.RefreshAsync();
        axis.Status.ActualVelocity.Should().Be(0.0);
        axis.Status.IsEnabled.Should().BeTrue("停止只下发 MC_Halt，不应擅自下使能");
    }

    [Fact]
    public async Task ResetAxisFaultsAsync_故障轴_清除故障()
    {
        var controller = new SimulatedAxisController(axisCount: 1);
        using var axis = CreateAxis(controller, 0);
        (await axis.InitializeAsync()).IsSuccess.Should().BeTrue();
        controller.SetFault(0, faultCode: 42);
        (await axis.RefreshAsync()).IsSuccess.Should().BeTrue();

        var store = new AxisSnapshotStore();
        store.Attach([axis]);

        var service = CreateService(store, DesktopTestServices.AllowPermissions(), new RecordingAuditService());
        var result = await service.ResetAxisFaultsAsync("测试复位");

        result.IsSuccess.Should().BeTrue(result.Error);

        await axis.RefreshAsync();
        axis.Status.IsFault.Should().BeFalse();
        axis.Status.FaultCode.Should().Be(0);
    }

    [Fact]
    public async Task ResetAxisFaultsAsync_运行中_拒绝复位()
    {
        var controller = new SimulatedAxisController(axisCount: 1);
        using var axis = CreateAxis(controller, 0);
        (await axis.InitializeAsync()).IsSuccess.Should().BeTrue();

        var store = new AxisSnapshotStore();
        store.Attach([axis]);

        var service = CreateService(store, DesktopTestServices.AllowPermissions(), new RecordingAuditService());
        (await service.StartAsync("测试启动")).IsSuccess.Should().BeTrue();

        var result = await service.ResetAxisFaultsAsync("测试复位");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("MachineRun.Running");
    }

    private static MachineRunService CreateService(
        AxisSnapshotStore store,
        IPermissionService permissions,
        IAuditService audit)
        => new(
            store,
            permissions,
            audit,
            NullLogger<MachineRunService>.Instance);

    private static AxisElement CreateAxis(SimulatedAxisController controller, int axisNo)
        => new(
            new AxisElementConfig
            {
                ElementId = $"Axis-0-{axisNo}",
                Name = $"轴 {axisNo}（模拟）",
                ControllerNo = 0,
                AxisNo = axisNo
            },
            controller,
            DesktopTestServices.AllowPermissions(),
            new LogAuditService());

    private sealed class RecordingAuditService : IAuditService
    {
        public List<string> Events { get; } = [];

        public Task RecordAsync(string eventType, string detail, CancellationToken cancellationToken = default)
        {
            Events.Add(eventType);
            return Task.CompletedTask;
        }
    }
}
