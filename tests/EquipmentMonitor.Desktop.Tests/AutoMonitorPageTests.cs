using EquipmentMonitor.Core;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Forms;
using EquipmentMonitor.Desktop.Monitoring;
using EquipmentMonitor.Desktop.Operation;
using EquipmentMonitor.Infrastructure.Auditing;
using EquipmentMonitor.Infrastructure.Hardware.Simulation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 自动监控页顶部：设备状态、摘要与启停按钮的显隐/可用性。
/// </summary>
public sealed class AutoMonitorPageTests
{
    [Fact]
    public void RefreshDeviceStatus_轴就绪_显示待机并允许启动()
    {
        var store = new AxisSnapshotStore();
        store.Publish(new AxisMonitorSnapshot(
            [CreateSnapshot("X 轴", ElementState.Ready)],
            DateTimeOffset.Now,
            null));

        using var provider = BuildProvider(store, DesktopTestServices.AllowPermissions());

        StaTestHost.RunSta(() =>
        {
            using var page = new AutoMonitorPage(provider);
            page.RefreshDeviceStatusForTest();

            page.DeviceStateText.Should().Be("设备状态：待机");
            page.DeviceSummaryText.Should().Contain("轴 1/1 就绪");
            page.DeviceSummaryText.Should().Contain("采集正常");
            page.StartEnabled.Should().BeTrue();
            page.StopEnabled.Should().BeFalse();
        });
    }

    [Fact]
    public void RefreshDeviceStatus_无操作权限_启停按钮均不可用()
    {
        var store = new AxisSnapshotStore();
        store.Publish(new AxisMonitorSnapshot(
            [CreateSnapshot("X 轴", ElementState.Ready)],
            DateTimeOffset.Now,
            null));

        using var provider = BuildProvider(store, DesktopTestServices.DenyPermissions());

        StaTestHost.RunSta(() =>
        {
            using var page = new AutoMonitorPage(provider);
            page.RefreshDeviceStatusForTest();

            page.StartEnabled.Should().BeFalse();
            page.StopEnabled.Should().BeFalse();
            page.ResetEnabled.Should().BeFalse();
        });
    }

    [Fact]
    public async Task RefreshDeviceStatus_设备运行中_停止可用启动不可用()
    {
        var controller = new SimulatedAxisController(axisCount: 1);
        using var axis = CreateAxis(controller);
        (await axis.InitializeAsync()).IsSuccess.Should().BeTrue();

        var store = new AxisSnapshotStore();
        store.Attach([axis]);

        using var provider = BuildProvider(store, DesktopTestServices.AllowPermissions());
        var runService = provider.GetRequiredService<MachineRunService>();
        (await runService.StartAsync("测试启动")).IsSuccess.Should().BeTrue();

        StaTestHost.RunSta(() =>
        {
            using var page = new AutoMonitorPage(provider);
            page.RefreshDeviceStatusForTest();

            page.DeviceStateText.Should().Be("设备状态：运行中");
            page.StartEnabled.Should().BeFalse();
            page.StopEnabled.Should().BeTrue();
        });
    }

    private static ServiceProvider BuildProvider(
        AxisSnapshotStore axisStore,
        IPermissionService permissions)
    {
        var services = new ServiceCollection();

        services.AddSingleton(axisStore);
        services.AddSingleton(new CylinderSnapshotStore());
        services.AddSingleton(permissions);
        services.AddSingleton<IAuditService>(new LogAuditService());
        services.AddSingleton<ILogger<MachineRunService>>(NullLogger<MachineRunService>.Instance);
        services.AddSingleton<MachineRunService>();

        services.AddTransient<ElementOverviewPage>();
        services.AddTransient<CylinderOverviewPage>();

        return services.BuildServiceProvider();
    }

    private static AxisSnapshot CreateSnapshot(string name, ElementState state)
        => new(
            $"Axis-{name}",
            name,
            0,
            0,
            state,
            new AxisStatus { IsEnabled = state == ElementState.Ready },
            null,
            DateTimeOffset.Now);

    private static AxisElement CreateAxis(SimulatedAxisController controller)
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
