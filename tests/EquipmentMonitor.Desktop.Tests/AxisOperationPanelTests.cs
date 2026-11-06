using System.Runtime.ExceptionServices;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Controls;
using EquipmentMonitor.Infrastructure.Auditing;
using EquipmentMonitor.Infrastructure.Axis;
using EquipmentMonitor.Infrastructure.Hardware.Simulation;
using EquipmentMonitor.Infrastructure.Security;
using FluentAssertions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 轴控面板：权限、模式切换、互锁提示、绝对/相对定位请求与下发。
/// </summary>
public sealed class AxisOperationPanelTests
{
    [Fact]
    public void Bind_有权限_进入点动模式()
    {
        StaTestHost.RunSta(() =>
        {
            using var axis = CreateAxis();
            using var panel = new AxisOperationPanel(DesktopTestServices.AllowPermissions());

            panel.Bind(axis);

            panel.HasOperationControls.Should().BeTrue();
            panel.ModeName.Should().Be("点动");
            panel.IsJogGroupVisible.Should().BeTrue();
            panel.IsPositionGroupVisible.Should().BeFalse();
        });
    }

    [Fact]
    public void Bind_无操作权限_命令区禁用()
    {
        StaTestHost.RunSta(() =>
        {
            using var axis = CreateAxis();
            using var panel = new AxisOperationPanel(DesktopTestServices.DenyPermissions());

            panel.Bind(axis);

            panel.HasOperationControls.Should().BeFalse();
            panel.AvailabilityText.Should().Contain("权限");
        });
    }

    [Fact]
    public async Task RefreshView_未使能_提示先启用()
    {
        using var axis = CreateAxis();
        await axis.InitializeAsync();

        StaTestHost.RunSta(() =>
        {
            using var panel = new AxisOperationPanel(DesktopTestServices.AllowPermissions());
            panel.Bind(axis);
            panel.RefreshView();

            panel.AvailabilityText.Should().Contain("未使能");
        });
    }

    [Fact]
    public void ModeSwitch_切换后显示对应参数组()
    {
        StaTestHost.RunSta(() =>
        {
            using var axis = CreateAxis();
            using var panel = new AxisOperationPanel(DesktopTestServices.AllowPermissions());
            panel.Bind(axis);

            panel.SelectModeForTest("定位");
            panel.IsPositionGroupVisible.Should().BeTrue();
            panel.IsJogGroupVisible.Should().BeFalse();

            panel.SelectModeForTest("回原点");
            panel.IsHomeGroupVisible.Should().BeTrue();
            panel.IsPositionGroupVisible.Should().BeFalse();
        });
    }

    [Fact]
    public void RelativeMode_BuildsMoveRelativeRequest()
    {
        StaTestHost.RunSta(() =>
        {
            using var axis = CreateAxis();
            using var panel = new AxisOperationPanel(DesktopTestServices.AllowPermissions());
            panel.Bind(axis);

            panel.SelectModeForTest("定位");
            panel.SelectPositionModeForTest(relative: true);
            panel.SetTargetForTest("12.5");

            panel.IsRelativeMode.Should().BeTrue();

            var request = panel.BuildPositionRequestForTest();

            request.Should().NotBeNull();
            request!.CommandKey.Should().Be(AxisElement.CommandMoveRelative);
            request.Arguments[CommandParameterKeys.Distance].TryAsNumber(out var distance).Should().BeTrue();
            distance.Should().Be(12.5);
        });
    }

    [Fact]
    public void AbsoluteMode_BuildsMoveAbsoluteRequest()
    {
        StaTestHost.RunSta(() =>
        {
            using var axis = CreateAxis();
            using var panel = new AxisOperationPanel(DesktopTestServices.AllowPermissions());
            panel.Bind(axis);

            panel.SelectModeForTest("定位");
            panel.SetTargetForTest("25.0");

            var request = panel.BuildPositionRequestForTest();

            request.Should().NotBeNull();
            request!.CommandKey.Should().Be(AxisElement.CommandMoveAbsolute);
            request.Arguments[CommandParameterKeys.Position].TryAsNumber(out var position).Should().BeTrue();
            position.Should().Be(25.0);
        });
    }

    [Fact]
    public async Task RelativeMove_从当前位置移动到目标()
    {
        using var axis = CreateAxis();
        await axis.InitializeAsync();
        await axis.ServeOnAsync();

        StaTestHost.RunSta(() =>
        {
            using var panel = new AxisOperationPanel(DesktopTestServices.AllowPermissions());

            var command = axis.Commands.Single(item => item.Key == AxisElement.CommandMoveRelative);
            var request = new ElementCommandRequest
            {
                CommandKey = command.Key,
                Arguments = new Dictionary<string, CommandValue>
                {
                    [CommandParameterKeys.Distance] = CommandValue.FromNumber(12.5)
                }
            };

            RunOnMessageLoop(panel, axis, () => panel.ExecuteAsync(command, request), () =>
                panel.ResultText.Should().Contain("已下发"));
        });

        await axis.RefreshAsync();
        axis.Status.ActualPosition.Should().Be(12.5);
    }

    [Fact]
    public async Task ExecuteAsync_未使能定位_被拒绝且位置不变()
    {
        using var axis = CreateAxis();
        await axis.InitializeAsync();

        StaTestHost.RunSta(() =>
        {
            using var panel = new AxisOperationPanel(DesktopTestServices.AllowPermissions());

            var command = axis.Commands.Single(item => item.Key == AxisElement.CommandMoveAbsolute);
            var request = new ElementCommandRequest
            {
                CommandKey = command.Key,
                Arguments = new Dictionary<string, CommandValue>
                {
                    [CommandParameterKeys.Position] = CommandValue.FromNumber(10)
                }
            };

            RunOnMessageLoop(panel, axis, () => panel.ExecuteAsync(command, request), () =>
                panel.ResultText.Should().Contain("未执行"));
        });

        await axis.RefreshAsync();
        axis.Status.ActualPosition.Should().Be(0);
    }

    /// <summary>
    /// 在真实消息泵里执行面板上的异步命令（UserControl 需要宿主窗体）。
    /// </summary>
    private static void RunOnMessageLoop(
        AxisOperationPanel panel,
        AxisElement axis,
        Func<Task> action,
        Action assertions)
    {
        Exception? failure = null;

        using var host = new Form
        {
            ShowInTaskbar = false,
            Opacity = 0,
            Width = 900,
            Height = 760
        };

        panel.Dock = DockStyle.Fill;
        host.Controls.Add(panel);
        panel.Bind(axis);

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

    [Fact]
    public void 切换模式_不移动停止与错误区()
    {
        StaTestHost.RunSta(() =>
        {
            using var axis = CreateAxis();
            using var panel = new AxisOperationPanel(DesktopTestServices.AllowPermissions());

            panel.Bind(axis);
            panel.SelectModeForTest("点动");

            var stop = panel.StopTop;
            var errorLabel = panel.ErrorLabelTop;
            var errorBox = panel.ErrorBoxTop;
            var hint = panel.HintTop;

            panel.SelectModeForTest("定位");
            panel.StopTop.Should().Be(stop);
            panel.ErrorLabelTop.Should().Be(errorLabel);
            panel.ErrorBoxTop.Should().Be(errorBox);
            panel.HintTop.Should().Be(hint);

            panel.SelectModeForTest("回原点");
            panel.StopTop.Should().Be(stop);
            panel.ErrorLabelTop.Should().Be(errorLabel);
            panel.ErrorBoxTop.Should().Be(errorBox);
            panel.HintTop.Should().Be(hint);

            panel.StopTop.Should().BeGreaterThanOrEqualTo(panel.ModeGroupBottom);
        });
    }

    private static AxisElement CreateAxis()
    {
        var controller = new SimulatedAxisController();

        return new AxisElement(
            new AxisElementConfig
            {
                ElementId = "X",
                Name = "X 轴",
                ControllerNo = 0,
                AxisNo = 0,
                DefaultVelocity = 10.0
            },
            controller,
            DesktopTestServices.AllowPermissions(),
            new LogAuditService());
    }
}
