using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Forms;
using EquipmentMonitor.Desktop.Monitoring;
using FluentAssertions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 气缸列表：快照 → 行渲染。
/// </summary>
public sealed class CylinderOverviewPageTests
{
    [Fact]
    public void RefreshView_渲染气缸位置与限位()
    {
        var store = new CylinderSnapshotStore();
        var now = DateTimeOffset.Now;

        store.Publish(new CylinderMonitorSnapshot(
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
                        UpdatedAt: now),
                    null,
                    now)
            ],
            now,
            null));

        StaTestHost.RunSta(() =>
        {
            using var page = new CylinderOverviewPage(store, DesktopTestServices.EmptyProvider());
            page.RefreshView();

            page.RowCount.Should().Be(1);
            page.RenderedRows[0].Name.Should().Be("夹紧气缸");
            page.RenderedRows[0].Position.Should().Be("缩回到位");
            page.HintText.Should().Contain("1 个气缸");
        });
    }

    [Fact]
    public void RefreshView_采集失败_提示包含错误()
    {
        var store = new CylinderSnapshotStore();
        store.Publish(new CylinderMonitorSnapshot([], DateTimeOffset.Now, "Cyl-1：尚未初始化"));

        StaTestHost.RunSta(() =>
        {
            using var page = new CylinderOverviewPage(store, DesktopTestServices.EmptyProvider());
            page.RefreshView();

            page.RowCount.Should().Be(0);
            page.HintText.Should().Contain("采集异常");
            page.HintText.Should().Contain("尚未初始化");
        });
    }
}
