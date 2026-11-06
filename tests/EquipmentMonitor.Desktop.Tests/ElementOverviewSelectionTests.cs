using System.Runtime.ExceptionServices;
using EquipmentMonitor.Core;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Controls;
using EquipmentMonitor.Desktop.Forms;
using EquipmentMonitor.Desktop.Monitoring;
using EquipmentMonitor.Infrastructure.Auditing;
using EquipmentMonitor.Infrastructure.Axis;
using EquipmentMonitor.Infrastructure.Hardware.Simulation;
using EquipmentMonitor.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 总览页：选中行 → 弹窗打开操作台并绑定对应轴；选中跨表格重绑保持。
/// </summary>
public sealed class ElementOverviewSelectionTests
{
    [Fact]
    public void RefreshView_默认选中第一行_跨刷新保持()
    {
        using var axis = CreateAxis("Axis-0-0", "X 轴", 0);
        var store = new AxisSnapshotStore();
        store.Attach([axis]);
        store.Publish(Snapshot(DateTimeOffset.Now, (axis, ElementState.Ready)));

        StaTestHost.RunSta(() =>
        {
            using var page = new ElementOverviewPage(store, DesktopTestServices.EmptyProvider());
            page.RefreshView();

            page.SelectedElement().Should().BeSameAs(axis, "未点击时也应默认选中第一行");

            // 下一帧：采集时间变化会触发表格重绑，选中不能被清掉。
            store.Publish(Snapshot(DateTimeOffset.Now.AddSeconds(1), (axis, ElementState.Ready)));
            page.RefreshView();

            page.SelectedElement().Should().BeSameAs(axis);
        });
    }

    [Fact]
    public void 选中第二行_打开操作台_绑定的是第二根轴()
    {
        using var faulted = CreateAxis("Axis-0-0", "A 轴", 0);
        using var ready = CreateAxis("Axis-0-1", "B 轴", 1);
        var store = new AxisSnapshotStore();
        store.Attach([faulted, ready]);
        store.Publish(Snapshot(
            DateTimeOffset.Now,
            (ready, ElementState.Ready),
            (faulted, ElementState.Error)));

        var services = new ServiceCollection()
            .AddSingleton<IPermissionService>(DesktopTestServices.AllowPermissions())
            .AddTransient<AxisOperationPanel>()
            .AddTransient<ElementOperationPage>()
            .BuildServiceProvider();

        StaTestHost.RunSta(() =>
        {
            using var page = new ElementOverviewPage(store, services);
            page.RefreshView();

            // 故障置顶：第一行是 A 轴；显式选第二行 B 轴。
            page.SelectRow(1);
            page.SelectedElement().Should().BeSameAs(ready);

            Exception? failure = null;
            var timer = new System.Windows.Forms.Timer { Interval = 100 };

            timer.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.OfType<ElementOperationPage>().FirstOrDefault();

                if (dialog is null)
                {
                    return;
                }

                timer.Stop();

                try
                {
                    dialog.Panel.CurrentAxis.Should().BeSameAs(ready);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    dialog.Close();
                    page.Close();
                }
            };

            page.Load += (_, _) =>
            {
                timer.Start();
                page.OpenSelected();
            };

            Application.Run(page);
            timer.Dispose();

            if (failure is not null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        });
    }

    private static AxisMonitorSnapshot Snapshot(
        DateTimeOffset capturedAt,
        params (AxisElement Axis, ElementState State)[] axes)
        => new(
            axes.Select(item => new AxisSnapshot(
                item.Axis.ElementMetadata.ElementId,
                item.Axis.ElementMetadata.Name,
                item.Axis.ControllerNo,
                item.Axis.AxisNo,
                item.State,
                new AxisStatus
                {
                    InPosition = item.State == ElementState.Ready,
                    IsFault = item.State == ElementState.Error
                },
                null,
                capturedAt)).ToArray(),
            capturedAt,
            null);

    private static AxisElement CreateAxis(string elementId, string name, int axisNo)
    {
        var controller = new SimulatedAxisController();

        return new AxisElement(
            new AxisElementConfig
            {
                ElementId = elementId,
                Name = name,
                ControllerNo = 0,
                AxisNo = axisNo,
                DefaultVelocity = 10.0
            },
            controller,
            DesktopTestServices.AllowPermissions(),
            new LogAuditService());
    }
}
