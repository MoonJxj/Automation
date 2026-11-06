using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Desktop.Monitoring;
using EquipmentMonitor.Desktop.Operation;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentMonitor.Desktop.Forms;

/// <summary>
/// 自动监控页：顶部设备状态与启停操作，下面是伺服轴 / 气缸两个只读页签。
/// </summary>
/// <remarks>
/// <para><b>设备状态</b>：由 <see cref="DeviceStatusEvaluator"/> 汇总轴/气缸/报警/运行快照；
/// 页面只渲染，不自己推导状态。</para>
/// <para><b>启停</b>：<see cref="MachineRunService"/> 对伺服轴下发使能/停止命令，并维护运行状态。
/// 当前没有 PLC 自动循环点位，启动只完成伺服使能；界面必须保留这条限制说明。</para>
/// <para><b>线程</b>：500ms 定时器读快照；命令通过服务异步执行，页面不直接触碰元件。</para>
/// </remarks>
public sealed partial class AutoMonitorPage : Form
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<AutoMonitorPage>();

    private readonly ElementOverviewPage? _axisPage;
    private readonly CylinderOverviewPage? _cylinderPage;

    private MachineRunService? _runService;
    private AxisSnapshotStore? _axisStore;
    private CylinderSnapshotStore? _cylinderStore;
    private IAlarmService? _alarms;
    private IPermissionService? _permissions;
    private System.Windows.Forms.Timer? _statusTimer;

    /// <summary>防止按钮在异步序列完成前被重复点击（服务侧还有一道门）。</summary>
    private bool _operationInProgress;

    /// <summary>设计器专用构造。</summary>
    public AutoMonitorPage()
    {
        InitializeComponent();
    }

    /// <summary>构造自动监控页。</summary>
    public AutoMonitorPage(IServiceProvider services)
        : this()
    {
        ArgumentNullException.ThrowIfNull(services);

        _axisPage = services.GetRequiredService<ElementOverviewPage>();
        _axisPage.StationFilter = null;
        Embed(_axisPage, _axisTab);

        _cylinderPage = services.GetRequiredService<CylinderOverviewPage>();
        _cylinderPage.StationFilter = null;
        Embed(_cylinderPage, _cylinderTab);

        _runService = services.GetService<MachineRunService>();
        _axisStore = services.GetService<AxisSnapshotStore>();
        _cylinderStore = services.GetService<CylinderSnapshotStore>();
        _alarms = services.GetService<IAlarmService>();
        _permissions = services.GetService<IPermissionService>();

        _startButton.Click += async (_, _) => await StartDeviceAsync().ConfigureAwait(true);
        _stopButton.Click += async (_, _) => await StopDeviceAsync().ConfigureAwait(true);
        _resetButton.Click += async (_, _) => await ResetAxisFaultsAsync().ConfigureAwait(true);

        _statusTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _statusTimer.Tick += (_, _) => RefreshDeviceStatus();

        VisibleChanged += (_, _) =>
        {
            if (Visible)
            {
                RefreshDeviceStatus();
                _statusTimer.Start();
            }
            else
            {
                _statusTimer.Stop();
            }
        };

        FormClosed += (_, _) => _statusTimer?.Dispose();

        RefreshDeviceStatus();
    }

    /// <summary>伺服页（测试用）。</summary>
    internal ElementOverviewPage? AxisPage => _axisPage;

    /// <summary>气缸页（测试用）。</summary>
    internal CylinderOverviewPage? CylinderPage => _cylinderPage;

    /// <summary>设备状态文案（测试用）。</summary>
    internal string DeviceStateText => _stateLabel.Text ?? string.Empty;

    /// <summary>设备摘要文案（测试用）。</summary>
    internal string DeviceSummaryText => _summaryLabel.Text ?? string.Empty;

    /// <summary>操作提示文案（测试用）。</summary>
    internal string RunHintText => _runHintLabel.Text ?? string.Empty;

    /// <summary>启动按钮是否可用（测试用）。</summary>
    internal bool StartEnabled => _startButton.Enabled;

    /// <summary>停止按钮是否可用（测试用）。</summary>
    internal bool StopEnabled => _stopButton.Enabled;

    /// <summary>复位按钮是否可用（测试用）。</summary>
    internal bool ResetEnabled => _resetButton.Enabled;

    /// <summary>刷新设备状态（测试用；生产路径由 500ms 定时器触发）。</summary>
    internal void RefreshDeviceStatusForTest() => RefreshDeviceStatus();

    private void RefreshDeviceStatus()
    {
        var runService = _runService;

        if (runService is null)
        {
            _stateLabel.Text = "设备状态：—";
            _stateLabel.ForeColor = SystemColors.GrayText;
            _summaryLabel.Text = "当前未接入机台运行服务。";
            _startButton.Enabled = false;
            _stopButton.Enabled = false;
            _resetButton.Enabled = false;
            return;
        }

        var run = runService.Snapshot;
        var axes = _axisStore?.Current ?? AxisMonitorSnapshot.Empty;
        var cylinders = _cylinderStore?.Current ?? CylinderMonitorSnapshot.Empty;
        var status = DeviceStatusEvaluator.Evaluate(run, axes, cylinders, _alarms?.Snapshot);

        _stateLabel.Text = $"设备状态：{status.Text}";
        _stateLabel.ForeColor = status.Kind switch
        {
            DeviceStatusKind.Running => Color.ForestGreen,
            DeviceStatusKind.Idle => Color.RoyalBlue,
            DeviceStatusKind.Fault => Color.Firebrick,
            DeviceStatusKind.Busy => Color.DarkOrange,
            DeviceStatusKind.Moving => Color.DarkOrange,
            DeviceStatusKind.Warning => Color.DarkOrange,
            DeviceStatusKind.CollectionWarning => Color.DarkOrange,
            _ => SystemColors.GrayText
        };

        _summaryLabel.Text = status.Summary;

        var canOperate = _permissions?.HasPermission(Permission.MachineOperate) == true;
        var busy = _operationInProgress || run.IsBusy;

        _startButton.Enabled = canOperate
                               && !busy
                               && run.State == MachineRunState.Stopped
                               && axes.Axes.Count > 0;

        _stopButton.Enabled = canOperate
                              && !busy
                              && run.State != MachineRunState.Stopped;

        _resetButton.Enabled = canOperate
                               && !busy
                               && run.State != MachineRunState.Running
                               && axes.Axes.Any(axis => axis.Status.IsFault || axis.State == ElementState.Error);
    }

    private async Task StartDeviceAsync()
    {
        if (_operationInProgress || _runService is null)
        {
            return;
        }

        var axisCount = EnabledAxisCount();

        if (!Confirm(
                "确认启动设备",
                $"将向 {axisCount} 根伺服轴下发使能（MC_Power）。{Environment.NewLine}{Environment.NewLine}" +
                "当前未接 PLC 自动流程：本操作只完成伺服使能，不会启动物料/循环。" +
                $"{Environment.NewLine}急停、安全门与硬限位仍由 PLC/电气回路兜底。" +
                $"{Environment.NewLine}{Environment.NewLine}确认启动设备？"))
        {
            return;
        }

        await RunOperationAsync(
            "启动",
            cancellationToken => _runService.StartAsync("自动监控页启动设备", cancellationToken))
            .ConfigureAwait(true);
    }

    private async Task StopDeviceAsync()
    {
        if (_operationInProgress || _runService is null)
        {
            return;
        }

        var axisCount = EnabledAxisCount();

        if (!Confirm(
                "确认停止设备",
                $"将向 {axisCount} 根伺服轴下发停止（MC_Halt）。{Environment.NewLine}{Environment.NewLine}" +
                "停止后轴保持使能；需要断使能请到操作台执行“软件急停”或使用现场安全回路。" +
                $"{Environment.NewLine}{Environment.NewLine}确认停止设备？"))
        {
            return;
        }

        await RunOperationAsync(
            "停止",
            cancellationToken => _runService.StopAsync("自动监控页停止设备", cancellationToken))
            .ConfigureAwait(true);
    }

    private async Task ResetAxisFaultsAsync()
    {
        if (_operationInProgress || _runService is null)
        {
            return;
        }

        if (!Confirm(
                "确认复位轴故障",
                "将对全部故障轴下发复位（MC_Reset）。" +
                $"{Environment.NewLine}{Environment.NewLine}复位只清除故障标志，不排除故障原因；" +
                $"请确认设备已安全、故障原因已排除。{Environment.NewLine}{Environment.NewLine}确认复位？"))
        {
            return;
        }

        await RunOperationAsync(
            "复位",
            cancellationToken => _runService.ResetAxisFaultsAsync("自动监控页复位轴故障", cancellationToken))
            .ConfigureAwait(true);
    }

    private async Task RunOperationAsync(
        string action,
        Func<CancellationToken, Task<Result>> operation)
    {
        _operationInProgress = true;
        RefreshDeviceStatus();

        try
        {
            // 页面不提供取消：启停序列需要完整执行；中途取消由服务内部回滚。
            var result = await operation(CancellationToken.None).ConfigureAwait(true);

            if (result.IsSuccess)
            {
                var message = _runService?.Snapshot.Message;
                _runHintLabel.Text = string.IsNullOrWhiteSpace(message)
                    ? $"{action}完成。"
                    : $"{action}完成：{message}";
                _runHintLabel.ForeColor = Color.ForestGreen;
                return;
            }

            _runHintLabel.Text = $"{action}失败：{result.Error}";
            _runHintLabel.ForeColor = Color.Firebrick;

            ShowError($"{action}失败", result.Error);
        }
        catch (UnauthorizedAccessException ex)
        {
            _runHintLabel.Text = $"{action}被拒绝：{ex.Message}";
            _runHintLabel.ForeColor = Color.Firebrick;
            ShowError($"{action}被拒绝", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "设备级操作 {Action} 失败。", action);
            _runHintLabel.Text = $"{action}失败：{ex.Message}";
            _runHintLabel.ForeColor = Color.Firebrick;
            ShowError($"{action}失败", ex.Message);
        }
        finally
        {
            _operationInProgress = false;
            RefreshDeviceStatus();
        }
    }

    private int EnabledAxisCount()
        => _axisStore?.Elements.Count(axis => axis.ElementMetadata.IsEnabled) ?? 0;

    private bool Confirm(string caption, string text)
    {
        var owner = FindTopLevelOwner(this);

        return owner is null
            ? MessageBox.Show(
                text,
                caption,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes
            : MessageBox.Show(
                owner,
                text,
                caption,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private void ShowError(string caption, string text)
    {
        var owner = FindTopLevelOwner(this);

        if (owner is null)
        {
            MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else
        {
            MessageBox.Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>页面会被嵌到主窗体里，消息框要用真正的顶层窗体做 owner。</summary>
    private static Form? FindTopLevelOwner(Control control)
    {
        for (var current = control; current is not null; current = current.Parent)
        {
            if (current is Form { TopLevel: true } form)
            {
                return form;
            }
        }

        return null;
    }

    private static void Embed(Form page, TabPage tab)
    {
        page.TopLevel = false;
        page.FormBorderStyle = FormBorderStyle.None;
        page.Dock = DockStyle.Fill;
        tab.Controls.Add(page);
        page.Show();
    }
}
