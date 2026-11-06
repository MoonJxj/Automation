using System.Globalization;
using EquipmentMonitor.Core;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Axis;

// ⚠️ 分部类边界：控件字段与 InitializeComponent 只在 AxisOperationPanel.Designer.cs 中定义。
// 本文件不要再声明控件字段，也不要再写 InitializeComponent。

namespace EquipmentMonitor.Desktop.Controls;

/// <summary>
/// 轴控面板（UserControl，嵌入主界面）：主控/轴使能、命令选择、点动参数、定位（绝对/相对）、轴状态与当前值。
/// 界面布局在 <c>AxisOperationPanel.Designer.cs</c>，可直接在 VS 设计器里编辑。
/// </summary>
/// <remarks>
/// <para><b>为什么不弹窗</b>：总览与操作面板同屏（左列表、右面板），选行即切换轴，避免“打开/关闭对话框”打断操作。</para>
/// <para><b>命令映射</b>：启用/禁用 → MC_Power；点动 → JogBackward/JogForward，松开 → MC_Halt；
/// 定位 → MC_MoveAbsolute / MC_MoveRelative；回原点 → MC_Home；停止 → MC_Halt；确认 → MC_Reset。</para>
/// <para><b>主控</b>：仅界面使能，不向设备下发命令。</para>
/// <para><b>参数</b>：速度/加速度/加加速度来自轴配置，当前控制器契约不支持单次命令覆盖，因此只读。</para>
/// </remarks>
public sealed partial class AxisOperationPanel : UserControl
{
    private static readonly TimeSpan ResultHoldTime = TimeSpan.FromSeconds(3);

    private IPermissionService? _permissions;
    private AxisElement? _axis;
    private System.Windows.Forms.Timer? _refreshTimer;
    private ToolTip? _toolTip;
    private Form? _watchedForm;
    private bool _jogActive;
    private bool _busy;
    private bool _mainActive = true;
    private bool _allowed;
    private DateTimeOffset _resultAt = DateTimeOffset.MinValue;
    private string? _resultText;

    /// <summary>设计器专用构造：只初始化界面，不连接任何服务。</summary>
    public AxisOperationPanel()
    {
        InitializeComponent();

        // 参考图的橙色警示条带斜纹；只影响绘制，不参与布局。
        _stripePanel.Paint += (_, e) => PaintWarningStripe(e.Graphics, _stripePanel.ClientSize);
    }

    /// <summary>构造轴控面板。</summary>
    public AxisOperationPanel(IPermissionService permissions)
        : this()
    {
        AttachPermissions(permissions);
    }

    /// <summary>
    /// 注入权限服务。设计器先通过无参构造创建控件，运行期再由宿主窗体注入。
    /// </summary>
    public void AttachPermissions(IPermissionService permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        _permissions = permissions;

        _toolTip = new ToolTip();
        _toolTip.SetToolTip(_mainActivateButton, "仅界面使能，不向设备下发命令。");
        _toolTip.SetToolTip(_mainDeactivateButton, "仅界面使能，不向设备下发命令。");
        _toolTip.SetToolTip(_velocityBox, "来自轴配置；单次命令覆盖暂不支持。");
        _toolTip.SetToolTip(_accelBox, "来自轴配置；单次命令覆盖暂不支持。");
        _toolTip.SetToolTip(_jerkBox, "来自轴配置；单次命令覆盖暂不支持。");
        _toolTip.SetToolTip(_ackButton, "MC_Reset：清除轴故障。");
        _toolTip.SetToolTip(_relativeRadio, "MC_MoveRelative：从当前位置移动指定距离。");

        _mainActivateButton.Click += (_, _) => SetMainActive(true);
        _mainDeactivateButton.Click += (_, _) => SetMainActive(false);

        _axisEnableButton.Click += (_, _) => _ = ExecuteKeyAsync(
            AxisElement.CommandPower, (CommandParameterKeys.Enable, CommandValue.FromBool(true)));
        _axisDisableButton.Click += (_, _) => _ = ExecuteKeyAsync(
            AxisElement.CommandPower, (CommandParameterKeys.Enable, CommandValue.FromBool(false)));

        _jogModeRadio.CheckedChanged += (_, _) => UpdateModePanels();
        _positionModeRadio.CheckedChanged += (_, _) => UpdateModePanels();
        _homeModeRadio.CheckedChanged += (_, _) => UpdateModePanels();

        _absoluteRadio.CheckedChanged += (_, _) => UpdatePositionModeLabels();
        _relativeRadio.CheckedChanged += (_, _) => UpdatePositionModeLabels();

        _jogBackwardButton.MouseDown += (_, _) => _ = StartJogAsync(forward: false);
        _jogBackwardButton.MouseUp += (_, _) => _ = StopJogAsync();
        _jogBackwardButton.MouseLeave += (_, _) => _ = StopJogAsync();

        _jogForwardButton.MouseDown += (_, _) => _ = StartJogAsync(forward: true);
        _jogForwardButton.MouseUp += (_, _) => _ = StopJogAsync();
        _jogForwardButton.MouseLeave += (_, _) => _ = StopJogAsync();

        _stopButton.Click += (_, _) => _ = ExecuteKeyAsync(AxisElement.CommandHalt);
        _homeButton.Click += (_, _) => _ = ExecuteKeyAsync(AxisElement.CommandHome);
        _moveButton.Click += (_, _) => _ = MoveToTargetAsync();
        _ackButton.Click += (_, _) => _ = ExecuteKeyAsync(AxisElement.CommandReset);

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 150 };
        _refreshTimer.Tick += (_, _) => RefreshView();
        _refreshTimer.Start();
    }

    /// <summary>绑定一根轴并刷新界面。</summary>
    public void Bind(AxisElement axis)
    {
        ArgumentNullException.ThrowIfNull(axis);

        StopJogIfActive();

        _axis = axis;

        var unit = axis.Unit;
        _velocityUnitLabel.Text = $"{unit}/s";
        _accelUnitLabel.Text = $"{unit}/s²";
        _jerkUnitLabel.Text = $"{unit}/s³";
        _targetUnitLabel.Text = unit;
        _positionUnitLabel.Text = unit;
        _actualVelocityUnitLabel.Text = $"{unit}/s";

        _velocityBox.Text = axis.DefaultVelocity.ToString("F1", CultureInfo.CurrentCulture);
        _accelBox.Text = axis.Acceleration.ToString("F1", CultureInfo.CurrentCulture);
        _jerkBox.Text = axis.Jerk.ToString("F1", CultureInfo.CurrentCulture);
        _jerkLimitCheck.Checked = axis.Jerk > 0;
        _jerkBox.Enabled = axis.Jerk > 0;

        _absoluteRadio.Checked = true;
        _targetPositionBox.Text = axis.Status.ActualPosition.ToString("F3", CultureInfo.CurrentCulture);

        _allowed = _permissions?.HasPermission(Permission.MachineOperate) ?? false;

        if (!_allowed)
        {
            SetCommandAreaEnabled(false);
            _hintLabel.Text = "当前账号无「设备操作」权限 —— 命令区已禁用。";
        }

        UpdateModePanels();
        UpdatePositionModeLabels();
        RefreshView();
    }

    /// <summary>当前绑定的轴（测试用）。</summary>
    internal AxisElement? CurrentAxis => _axis;

    /// <summary>当前是否具备操作区（有权限且已绑定轴）。</summary>
    internal bool HasOperationControls => _axis is not null && _allowed;

    /// <summary>当前命令模式（测试用）。</summary>
    internal string ModeName => _jogModeRadio.Checked ? "点动"
        : _positionModeRadio.Checked ? "定位"
        : _homeModeRadio.Checked ? "回原点" : "-";

    /// <summary>定位模式是否为相对定位（测试用）。</summary>
    internal bool IsRelativeMode => _relativeRadio.Checked;

    /// <summary>是否显示点动参数组（测试用）。</summary>
    internal bool IsJogGroupVisible => _jogModeRadio.Checked;

    /// <summary>是否显示定位参数组（测试用）。</summary>
    internal bool IsPositionGroupVisible => _positionModeRadio.Checked;

    /// <summary>是否显示回原点组（测试用）。</summary>
    internal bool IsHomeGroupVisible => _homeModeRadio.Checked;

    /// <summary>点动是否仍在走（测试用）。</summary>
    internal bool IsJogActive => _jogActive;

    /// <summary>主控（界面使能）是否激活（测试用）。</summary>
    internal bool IsMainActive => _mainActive;

    /// <summary>互锁/权限提示（测试用）。</summary>
    internal string AvailabilityText => _hintLabel.Text ?? string.Empty;

    /// <summary>命令结果文本（测试用）。</summary>
    internal string ResultText => _resultText ?? _infoBox.Text ?? string.Empty;

    /// <summary>停止按钮上沿（测试用；用于验证模式切换不写布局）。</summary>
    internal int StopTop => _stopButton.Top;

    /// <summary>错误标签上沿（测试用）。</summary>
    internal int ErrorLabelTop => _errorLabel.Top;

    /// <summary>错误框上沿（测试用）。</summary>
    internal int ErrorBoxTop => _errorBox.Top;

    /// <summary>提示标签上沿（测试用）。</summary>
    internal int HintTop => _hintLabel.Top;

    /// <summary>三个模式组的最低边缘（测试用）。</summary>
    internal int ModeGroupBottom => Math.Max(_jogGroup.Bottom, Math.Max(_positionGroup.Bottom, _homeGroup.Bottom));

    /// <summary>切换命令模式（测试用）。</summary>
    internal void SelectModeForTest(string mode)
    {
        _jogModeRadio.Checked = mode == "点动";
        _positionModeRadio.Checked = mode == "定位";
        _homeModeRadio.Checked = mode == "回原点";
        UpdateModePanels();
    }

    /// <summary>切换绝对/相对定位（测试用）。</summary>
    internal void SelectPositionModeForTest(bool relative)
    {
        _relativeRadio.Checked = relative;
        _absoluteRadio.Checked = !relative;
        UpdatePositionModeLabels();
    }

    /// <summary>设置定位输入值（测试用）。</summary>
    internal void SetTargetForTest(string text) => _targetPositionBox.Text = text;

    /// <summary>按当前界面构造定位请求（测试用）。</summary>
    internal ElementCommandRequest? BuildPositionRequestForTest()
    {
        if (!double.TryParse(_targetPositionBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
            !double.TryParse(_targetPositionBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return null;
        }

        return _relativeRadio.Checked
            ? new ElementCommandRequest
            {
                CommandKey = AxisElement.CommandMoveRelative,
                Arguments = new Dictionary<string, CommandValue>
                {
                    [CommandParameterKeys.Distance] = CommandValue.FromNumber(value)
                }
            }
            : new ElementCommandRequest
            {
                CommandKey = AxisElement.CommandMoveAbsolute,
                Arguments = new Dictionary<string, CommandValue>
                {
                    [CommandParameterKeys.Position] = CommandValue.FromNumber(value)
                }
            };
    }

    /// <summary>按当前状态刷新指示器、当前值与按钮可用性。</summary>
    internal void RefreshView()
    {
        var axis = _axis;

        if (axis is null)
        {
            return;
        }

        var status = axis.Status;

        SetIndicator(_enabledIndicator, status.IsEnabled ? Color.LimeGreen : Color.DimGray);
        SetIndicator(_homedIndicator, status.IsHome ? Color.LimeGreen : Color.DimGray);
        SetIndicator(_readyIndicator, axis.State == ElementState.Ready ? Color.LimeGreen : Color.DimGray);
        SetIndicator(_axisErrorIndicator, status.IsFault ? Color.Red : Color.DimGray);
        SetIndicator(_driveErrorIndicator, status.FaultCode != 0 ? Color.Red : Color.DimGray);

        _positionValueBox.Text = status.ActualPosition.ToString("F3", CultureInfo.CurrentCulture);
        _actualVelocityBox.Text = status.ActualVelocity.ToString("F3", CultureInfo.CurrentCulture);

        if (_resultText is null || DateTimeOffset.Now - _resultAt > ResultHoldTime)
        {
            _infoBox.Text = DescribeState(axis);
        }
        else
        {
            _infoBox.Text = _resultText;
        }

        if (axis.LastError is null)
        {
            _errorBox.Text = "正常";
            _errorBox.ForeColor = SystemColors.WindowText;
        }
        else
        {
            _errorBox.Text = axis.LastError.Message;
            _errorBox.ForeColor = Color.Firebrick;
        }

        if (_allowed)
        {
            UpdateButtons(status);
            UpdateHint(axis, status);
        }
    }

    private void UpdateButtons(AxisStatus status)
    {
        var canOperate = _allowed && _mainActive;

        _mainActivateButton.Enabled = !_mainActive;
        _mainDeactivateButton.Enabled = _mainActive;

        _axisEnableButton.Enabled = canOperate && !status.IsEnabled && !status.IsFault;
        _axisDisableButton.Enabled = canOperate && status.IsEnabled;

        var motionAllowed = canOperate && status.IsEnabled && !status.IsFault;
        _jogBackwardButton.Enabled = motionAllowed;
        _jogForwardButton.Enabled = motionAllowed;
        _moveButton.Enabled = motionAllowed;
        _homeButton.Enabled = motionAllowed;
        _stopButton.Enabled = canOperate && status.IsEnabled;
        _ackButton.Enabled = canOperate && status.IsFault;
    }

    private void UpdateHint(AxisElement axis, AxisStatus status)
    {
        if (!_mainActive)
        {
            _hintLabel.Text = "主控未激活（仅界面使能，不影响设备）。";
            return;
        }

        if (!status.IsEnabled)
        {
            _hintLabel.Text = "提示：轴未使能 —— 先点「启用」。";
            return;
        }

        if (status.IsFault)
        {
            _hintLabel.Text = $"提示：轴故障（{status.FaultCode}）—— 先点「确认」复位。";
            return;
        }

        var availability = axis.EvaluateCommand(AxisElement.CommandMoveAbsolute);

        _hintLabel.Text = availability.IsAvailable
            ? string.Empty
            : $"提示：{availability.Reason}";
    }

    private void SetMainActive(bool active)
    {
        _mainActive = active;
        RefreshView();
    }

    private void UpdateModePanels()
    {
        var jog = _jogModeRadio.Checked;
        var position = _positionModeRadio.Checked;

        _jogGroup.Visible = jog;
        _positionGroup.Visible = position;
        _homeGroup.Visible = !jog && !position;

        _jogBackwardButton.Visible = jog;
        _jogForwardButton.Visible = jog;

        // 模式组高度不同，但停止/错误/提示的坐标统一由 Designer 决定；
        // 这里只切可见性，绝不写 Top，否则设计器里怎么调都会被运行期改回去。
    }

    private void UpdatePositionModeLabels()
    {
        var relative = _relativeRadio.Checked;

        _targetLabel.Text = relative ? "相对位移：" : "目标位置：";
        _moveButton.Text = relative ? "相对" : "定位";
    }

    private void SetCommandAreaEnabled(bool enabled)
    {
        _commandGroup.Enabled = enabled;
        _jogGroup.Enabled = enabled;
        _positionGroup.Enabled = enabled;
        _homeGroup.Enabled = enabled;

        _mainActivateButton.Enabled = enabled;
        _mainDeactivateButton.Enabled = enabled;
        _axisEnableButton.Enabled = enabled;
        _axisDisableButton.Enabled = enabled;
        _jogBackwardButton.Enabled = enabled;
        _jogForwardButton.Enabled = enabled;
        _moveButton.Enabled = enabled;
        _homeButton.Enabled = enabled;
        _stopButton.Enabled = enabled;
        _ackButton.Enabled = enabled;
    }

    private async Task ExecuteKeyAsync(string commandKey, params (string Key, CommandValue Value)[] arguments)
    {
        var axis = _axis;

        if (axis is null || _busy)
        {
            return;
        }

        var command = axis.Commands.FirstOrDefault(
            item => string.Equals(item.Key, commandKey, StringComparison.Ordinal));

        if (command is null)
        {
            ShowResult($"{commandKey}：该轴不支持此命令。");
            return;
        }

        var map = new Dictionary<string, CommandValue>(arguments.Length, StringComparer.Ordinal);

        foreach (var (key, value) in arguments)
        {
            map[key] = value;
        }

        var request = new ElementCommandRequest { CommandKey = command.Key, Arguments = map };

        if (command.Risk == ElementCommandRisk.Confirm && !Confirm(command, request))
        {
            ShowResult($"{command.DisplayName}：已取消。");
            return;
        }

        await ExecuteAsync(command, request).ConfigureAwait(true);
    }

    private async Task MoveToTargetAsync()
    {
        var axis = _axis;

        if (axis is null || _busy)
        {
            return;
        }

        var request = BuildPositionRequestForTest();

        if (request is null)
        {
            ShowResult("目标位置不是有效数值。");
            return;
        }

        var command = axis.Commands.FirstOrDefault(
            item => string.Equals(item.Key, request.CommandKey, StringComparison.Ordinal));

        if (command is null)
        {
            ShowResult($"{request.CommandKey}：该轴不支持此命令。");
            return;
        }

        if (command.Risk == ElementCommandRisk.Confirm && !Confirm(command, request))
        {
            ShowResult($"{command.DisplayName}：已取消。");
            return;
        }

        await ExecuteAsync(command, request).ConfigureAwait(true);
    }

    internal async Task ExecuteAsync(ElementCommandDescriptor command, ElementCommandRequest request)
    {
        var axis = _axis;

        if (axis is null)
        {
            return;
        }

        _busy = true;

        try
        {
            var result = await axis.ExecuteCommandAsync(request).ConfigureAwait(true);

            if (result.IsFailure)
            {
                ShowResult($"{command.DisplayName}：未执行（{result.ErrorCode}）—— {result.Error}");
            }
            else if (result.Value.Outcome == CommandOutcome.Accepted)
            {
                ShowResult(string.IsNullOrWhiteSpace(result.Value.Message)
                    ? $"{command.DisplayName}：已下发。"
                    : $"{command.DisplayName}：{result.Value.Message}");
            }
            else
            {
                ShowResult($"{command.DisplayName}：{result.Value.Outcome}" +
                           (string.IsNullOrWhiteSpace(result.Value.Message) ? string.Empty : $" —— {result.Value.Message}"));
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowResult($"{command.DisplayName}：权限不足 —— {ex.Message}");
        }
        finally
        {
            _busy = false;
            RefreshView();
        }
    }

    private async Task StartJogAsync(bool forward)
    {
        var axis = _axis;

        if (axis is null || _jogActive || _busy || !_allowed || !_mainActive)
        {
            return;
        }

        var key = forward ? AxisElement.CommandJogForward : AxisElement.CommandJogBackward;
        var command = axis.Commands.FirstOrDefault(item => item.Key == key);

        if (command is null)
        {
            return;
        }

        var result = await axis.ExecuteCommandAsync(
            new ElementCommandRequest { CommandKey = key }).ConfigureAwait(true);

        _jogActive = !result.IsFailure;
        ShowResult(result.IsFailure
            ? $"{command.DisplayName}：未执行（{result.ErrorCode}）—— {result.Error}"
            : $"{command.DisplayName}：已启动，松开后停止。");
        RefreshView();
    }

    private async Task StopJogAsync()
    {
        var axis = _axis;

        if (axis is null || !_jogActive)
        {
            return;
        }

        _jogActive = false;

        var result = await axis.ExecuteCommandAsync(
            new ElementCommandRequest { CommandKey = AxisElement.CommandHalt }).ConfigureAwait(true);

        ShowResult(result.IsFailure
            ? $"停止：未执行（{result.ErrorCode}）—— {result.Error}"
            : "停止：已下发。");
        RefreshView();
    }

    private void StopJogIfActive()
    {
        if (_jogActive)
        {
            _ = StopJogAsync();
        }
    }

    private bool Confirm(ElementCommandDescriptor command, ElementCommandRequest request)
    {
        var arguments = request.Arguments.Count == 0
            ? "（无参数）"
            : string.Join("，", request.Arguments.Select(pair => $"{pair.Key}={pair.Value}"));

        return MessageBox.Show(
            FindForm(),
            $"元件：{_axis?.ElementMetadata.ElementId}{Environment.NewLine}" +
            $"命令：{command.DisplayName}（{command.Key}）{Environment.NewLine}" +
            $"参数：{arguments}{Environment.NewLine}{Environment.NewLine}" +
            "确认要执行这条命令吗？",
            "二次确认",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.OK;
    }

    private void ShowResult(string text)
    {
        _resultText = text;
        _resultAt = DateTimeOffset.Now;
        _infoBox.Text = text;
    }

    private static void SetIndicator(Control indicator, Color color) => indicator.BackColor = color;

    private static void PaintWarningStripe(Graphics graphics, Size size)
    {
        using var pen = new Pen(Color.White, 6);

        for (var x = -size.Height; x < size.Width; x += 22)
        {
            graphics.DrawLine(pen, x, size.Height, x + size.Height, 0);
        }
    }

    private static string DescribeState(AxisElement axis)
    {
        var status = axis.Status;

        if (!status.IsEnabled)
        {
            return "轴未使能。";
        }

        if (status.IsFault)
        {
            return $"轴故障，故障码 {status.FaultCode}。";
        }

        if (status.IsHoming)
        {
            return "轴正在回零。";
        }

        if (status.IsMoving)
        {
            return "轴正在运动。";
        }

        return status.InPosition ? "轴处于停止状态（已到位）。" : "轴处于停止状态。";
    }

    /// <inheritdoc />
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (!Visible)
        {
            StopJogIfActive();
        }
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);

        if (_watchedForm is not null)
        {
            _watchedForm.Deactivate -= OnWatchedFormDeactivate;
        }

        _watchedForm = FindForm();

        if (_watchedForm is not null)
        {
            _watchedForm.Deactivate += OnWatchedFormDeactivate;
        }
    }

    private void OnWatchedFormDeactivate(object? sender, EventArgs e) => StopJogIfActive();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer?.Dispose();
            _toolTip?.Dispose();

            if (_watchedForm is not null)
            {
                _watchedForm.Deactivate -= OnWatchedFormDeactivate;
                _watchedForm = null;
            }
        }

        base.Dispose(disposing);
    }
}
