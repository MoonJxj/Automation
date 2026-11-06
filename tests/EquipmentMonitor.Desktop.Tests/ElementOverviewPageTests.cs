using EquipmentMonitor.Core;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Desktop.Forms;
using EquipmentMonitor.Desktop.Monitoring;
using FluentAssertions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 只读元件总览页：验证「快照 → 排序 → 行」的渲染契约。
/// </summary>
public sealed class ElementOverviewPageTests
{
    [Fact]
    public void RefreshView_故障置顶_就绪在后()
    {
        var store = new AxisSnapshotStore();
        store.Publish(new AxisMonitorSnapshot(
            [
                CreateAxis("B", "B 轴", 1, ElementState.Ready, new AxisStatus { InPosition = true }),
                CreateAxis("A", "A 轴", 0, ElementState.Error, new AxisStatus { IsFault = true, FaultCode = 12 })
            ],
            DateTimeOffset.Now,
            null));

        StaTestHost.RunSta(() =>
        {
            using var page = new ElementOverviewPage(store, DesktopTestServices.EmptyProvider());
            page.RefreshView();

            page.RowCount.Should().Be(2);
            page.RenderedRows[0].State.Should().Be("故障");
            page.RenderedRows[1].State.Should().Be("就绪");
            page.HintText.Should().Contain("2 根轴");
        });
    }

    [Fact]
    public void RefreshView_无数据_显示零行且提示等待()
    {
        var store = new AxisSnapshotStore();

        StaTestHost.RunSta(() =>
        {
            using var page = new ElementOverviewPage(store, DesktopTestServices.EmptyProvider());
            page.RefreshView();

            page.RowCount.Should().Be(0);
            page.HintText.Should().Contain("0 根轴");
        });
    }

    [Fact]
    public void RefreshView_采集失败_提示包含错误()
    {
        var store = new AxisSnapshotStore();
        store.Publish(new AxisMonitorSnapshot([], DateTimeOffset.Now, "Axis-0-0：尚未初始化"));

        StaTestHost.RunSta(() =>
        {
            using var page = new ElementOverviewPage(store, DesktopTestServices.EmptyProvider());
            page.RefreshView();

            page.HintText.Should().Contain("采集异常");
            page.HintText.Should().Contain("尚未初始化");
        });
    }

    private static AxisSnapshot CreateAxis(
        string elementId,
        string name,
        int axisNo,
        ElementState state,
        AxisStatus status)
        => new(elementId, name, 0, axisNo, state, status, null, DateTimeOffset.Now);
}
