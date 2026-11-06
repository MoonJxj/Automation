using EquipmentMonitor.Core;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Forms;
using EquipmentMonitor.Desktop.Monitoring;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 手动操作页：固定工位1/工位2，切换后伺服/气缸列表只显示本工位元件。
/// </summary>
public sealed class ManualOperationPageTests
{
    [Fact]
    public void 切换工位_伺服与气缸列表按工位过滤()
    {
        using var host = Program.BuildHost([]);
        var axisStore = host.Services.GetRequiredService<AxisSnapshotStore>();
        var cylinderStore = host.Services.GetRequiredService<CylinderSnapshotStore>();
        var now = DateTimeOffset.Now;

        axisStore.Publish(new AxisMonitorSnapshot(
            [
                Axis("Axis-1", "工位1 X 轴", 1, 2),
                Axis("Axis-2", "工位2 Y 轴", 2, 3)
            ],
            now,
            null));

        cylinderStore.Publish(new CylinderMonitorSnapshot(
            [
                Cylinder("Cyl-1", "工位1 夹紧气缸", 1),
                Cylinder("Cyl-2", "工位2 顶升气缸", 2)
            ],
            now,
            null));

        StaTestHost.RunSta(() =>
        {
            using var page = host.Services.GetRequiredService<ManualOperationPage>();

            page.StationId.Should().Be(StationCatalog.Station1);
            page.AxisPage.Should().NotBeNull();
            page.CylinderPage.Should().NotBeNull();
            page.AxisPage!.RenderedRows.Should().ContainSingle(row => row.Axis.Contains("工位1"));
            page.CylinderPage!.RenderedRows.Should().ContainSingle(row => row.Name.Contains("工位1"));

            page.SelectStationForTest(StationCatalog.Station2);

            page.StationId.Should().Be(StationCatalog.Station2);
            page.AxisPage.RenderedRows.Should().ContainSingle(row => row.Axis.Contains("工位2"));
            page.CylinderPage.RenderedRows.Should().ContainSingle(row => row.Name.Contains("工位2"));
        });
    }

    private static AxisSnapshot Axis(string id, string name, int stationId, int axisNo) => new(
        id,
        name,
        0,
        axisNo,
        ElementState.Ready,
        new AxisStatus { InPosition = true },
        null,
        DateTimeOffset.Now,
        stationId);

    private static CylinderSnapshot Cylinder(string id, string name, int stationId) => new(
        id,
        name,
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
        DateTimeOffset.Now,
        stationId);
}
