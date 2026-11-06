using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Forms;
using FluentAssertions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>报警页：渲染、空状态、确认按钮权限、确认调用、历史切换。</summary>
public sealed class AlarmInfoPageTests
{
    [Fact]
    public void 有报警_渲染行并显示计数()
    {
        var alarms = new RecordingAlarmService { Snapshot = Snapshot(Record()) };

        StaTestHost.RunSta(() =>
        {
            using var page = new AlarmInfoPage(alarms, DesktopTestServices.AllowPermissions());

            page.RefreshView();

            page.RowCount.Should().Be(1);
            page.RenderedRows[0].State.Should().Be("未确认");
            page.RenderedRows[0].Message.Should().Contain("故障");
            page.HintText.Should().Contain("活动报警 1");
        });
    }

    [Fact]
    public void 无报警_显示当前无报警()
    {
        var alarms = new RecordingAlarmService { Snapshot = AlarmSnapshot.Empty };

        StaTestHost.RunSta(() =>
        {
            using var page = new AlarmInfoPage(alarms, DesktopTestServices.AllowPermissions());

            page.RefreshView();

            page.RowCount.Should().Be(0);
            page.HintText.Should().Contain("当前无报警");
        });
    }

    [Fact]
    public void 无确认权限_确认按钮禁用()
    {
        var alarms = new RecordingAlarmService { Snapshot = Snapshot(Record()) };

        StaTestHost.RunSta(() =>
        {
            using var page = new AlarmInfoPage(alarms, DesktopTestServices.DenyPermissions());

            page.CanAcknowledge.Should().BeFalse();
        });
    }

    [Fact]
    public void 有确认权限_确认选中行_调用服务()
    {
        var alarms = new RecordingAlarmService { Snapshot = Snapshot(Record()) };

        StaTestHost.RunSta(() =>
        {
            using var page = new AlarmInfoPage(alarms, DesktopTestServices.AllowPermissions());

            page.RefreshView();
            page.SelectedId.Should().Be("a1");

            page.AcknowledgeSelectedForTest();

            alarms.Acknowledged.Should().Contain("a1");
        });
    }

    [Fact]
    public void 历史_查询并渲染已归档记录()
    {
        var alarms = new RecordingAlarmService
        {
            Snapshot = AlarmSnapshot.Empty,
            History = [Record() with { ClearedAt = DateTimeOffset.UtcNow, AcknowledgedAt = DateTimeOffset.UtcNow }]
        };

        StaTestHost.RunSta(() =>
        {
            using var page = new AlarmInfoPage(alarms, DesktopTestServices.AllowPermissions());

            page.ToggleHistoryForTestAsync().GetAwaiter().GetResult();

            page.RowCount.Should().Be(1);
            page.RenderedRows[0].State.Should().Be("已恢复");
            page.HintText.Should().Contain("历史报警 1");
        });
    }

    private static AlarmSnapshot Snapshot(AlarmRecord record) => new(
        [record],
        UnacknowledgedCount: 1,
        HighestLevel: AlarmLevel.Error,
        CapturedAt: DateTimeOffset.UtcNow);

    private static AlarmRecord Record() => new()
    {
        Id = "a1",
        Key = new AlarmKey("axis:1:0", "AXIS.FAULT", 42),
        Level = AlarmLevel.Error,
        SourceKind = AlarmSourceKind.Servo,
        Title = "伺服故障",
        Message = "轴 X 轴 故障（码 42）",
        Remedy = "检查驱动器报警码",
        ElementId = "Axis-1-0",
        ElementName = "X 轴",
        RaisedAt = DateTimeOffset.UtcNow,
        LastSeenAt = DateTimeOffset.UtcNow
    };
}
