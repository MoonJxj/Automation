using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Monitoring;
using EquipmentMonitor.Infrastructure.Auditing;
using EquipmentMonitor.Infrastructure.Axis;
using EquipmentMonitor.Infrastructure.Hardware.Simulation;
using EquipmentMonitor.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 采集服务与快照仓：验证「刷新 → 快照 → 读取」这条链路，不依赖宿主时钟。
/// </summary>
public sealed class AxisMonitorServiceTests
{
    [Fact]
    public async Task RefreshOnce_未初始化的轴_返回错误但不中断整轮()
    {
        var controller = new SimulatedAxisController(axisCount: 1);
        using var element = CreateElement(controller);

        var store = new AxisSnapshotStore();
        store.Attach([element]);

        var monitor = new AxisMonitorService(store, NullLogger<AxisMonitorService>.Instance);
        var snapshot = await monitor.RefreshOnceAsync(CancellationToken.None);

        snapshot.Axes.Should().HaveCount(1);
        snapshot.LastError.Should().NotBeNullOrWhiteSpace();
        snapshot.Axes[0].State.Should().Be(ElementState.Unknown);
        store.Current.LastError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RefreshOnce_已初始化的模拟轴_发布就绪快照()
    {
        var controller = new SimulatedAxisController(axisCount: 1);
        using var element = CreateElement(controller);

        (await element.InitializeAsync()).IsSuccess.Should().BeTrue();

        var store = new AxisSnapshotStore();
        store.Attach([element]);

        var monitor = new AxisMonitorService(store, NullLogger<AxisMonitorService>.Instance);
        var snapshot = await monitor.RefreshOnceAsync(CancellationToken.None);

        snapshot.Axes.Should().HaveCount(1);
        snapshot.LastError.Should().BeNull();
        snapshot.Axes[0].State.Should().Be(ElementState.Ready);
    }

    [Fact]
    public void Store_未挂载时返回空集合与空帧()
    {
        var store = new AxisSnapshotStore();

        store.Elements.Should().BeEmpty();
        store.Current.Axes.Should().BeEmpty();
        store.Current.RefreshedAt.Should().BeNull();
    }

    private static AxisElement CreateElement(SimulatedAxisController controller)
        => new(
            new AxisElementConfig
            {
                ElementId = "Axis-0-0",
                Name = "X 轴（模拟）",
                ControllerNo = 0,
                AxisNo = 0
            },
            controller,
            DesktopTestServices.AllowPermissions(),
            new LogAuditService());
}
