using System.Runtime.ExceptionServices;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Controls;
using EquipmentMonitor.Infrastructure.Auditing;
using EquipmentMonitor.Core.Cylinder;
using EquipmentMonitor.Infrastructure.Hardware.Simulation;
using EquipmentMonitor.Infrastructure.Security;
using FluentAssertions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 气缸面板：权限、初始位置、伸出命令下发与到位刷新。
/// </summary>
public sealed class CylinderOperationPanelTests
{
    [Fact]
    public async Task Bind_有权限_初始为缩回到位()
    {
        using var cylinder = CreateCylinder();
        await cylinder.InitializeAsync();

        StaTestHost.RunSta(() =>
        {
            using var panel = new CylinderOperationPanel(DesktopTestServices.AllowPermissions());

            panel.Bind(cylinder);
            panel.RefreshView();

            panel.HasOperationControls.Should().BeTrue();
            panel.PositionText.Should().Be("缩回到位");
            panel.IsExtendEnabled.Should().BeTrue();
            panel.IsRetractEnabled.Should().BeFalse("已经缩回到位，再缩回没有意义");
        });
    }

    [Fact]
    public void Bind_无操作权限_控制区禁用()
    {
        StaTestHost.RunSta(() =>
        {
            using var cylinder = CreateCylinder();
            using var panel = new CylinderOperationPanel(DesktopTestServices.DenyPermissions());

            panel.Bind(cylinder);
            panel.RefreshView();

            panel.HasOperationControls.Should().BeFalse();
            panel.AvailabilityText.Should().Contain("权限");
            panel.IsExtendEnabled.Should().BeFalse();
        });
    }

    [Fact]
    public async Task 伸出命令_下发后刷新为伸出到位()
    {
        using var cylinder = CreateCylinder();
        await cylinder.InitializeAsync();

        StaTestHost.RunSta(() =>
        {
            using var panel = new CylinderOperationPanel(DesktopTestServices.AllowPermissions());

            RunOnMessageLoop(
                panel,
                cylinder,
                async () =>
                {
                    await ExecuteAsync(panel, CylinderElement.CommandExtend);
                    await cylinder.RefreshAsync();
                },
                () =>
                {
                    panel.RefreshView();
                    panel.ResultText.Should().Contain("已下发");
                    panel.PositionText.Should().Be("伸出到位");
                    panel.IsRetractEnabled.Should().BeTrue();
                    panel.IsExtendEnabled.Should().BeFalse();
                });
        });
    }

    private static async Task ExecuteAsync(CylinderOperationPanel panel, string commandKey)
    {
        var cylinder = panel.CurrentCylinder!;
        var command = cylinder.Commands.Single(item => item.Key == commandKey);

        await panel.ExecuteAsync(command, new ElementCommandRequest { CommandKey = commandKey });
    }

    private static void RunOnMessageLoop(
        CylinderOperationPanel panel,
        IElementCylinder cylinder,
        Func<Task> action,
        Action assertions)
    {
        Exception? failure = null;

        using var host = new Form
        {
            ShowInTaskbar = false,
            Opacity = 0,
            Width = 700,
            Height = 600
        };

        panel.Dock = DockStyle.Fill;
        host.Controls.Add(panel);
        panel.Bind(cylinder);

        host.Load += async (_, _) =>
        {
            try
            {
                await action().ConfigureAwait(true);
                assertions();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                host.Close();
            }
        };

        Application.Run(host);

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static CylinderElement CreateCylinder()
    {
        var io = new SimulatedIoController { MotionDuration = TimeSpan.Zero };

        var config = new CylinderConfig
        {
            ElementId = "Cyl-1",
            Name = "夹紧气缸",
            ControllerConfigId = 1,
            ControllerNo = 0,
            ExtendOutput = 0,
            RetractOutput = 1,
            ExtendedInput = 0,
            RetractedInput = 1,
            ActionTimeoutSeconds = 2.0
        };

        io.ConfigureCylinder(config.ExtendOutput, config.RetractOutput!.Value, config.ExtendedInput, config.RetractedInput);

        return new CylinderElement(
            config,
            io,
            DesktopTestServices.AllowPermissions(),
            new LogAuditService());
    }
}
